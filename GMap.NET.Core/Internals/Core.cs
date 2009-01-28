﻿using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Runtime.CompilerServices;

namespace GMapNET.Internals
{
   /// <summary>
   /// for internal control purposes only
   /// </summary>
   public class Core
   {
      public PointLatLng currentPosition;
      public Point currentPositionTile;
      public Point currentPositionPixel;

      public Point renderOffset;
      public Point centerTileXYLocation;
      public Point centerTileXYLocationLast;
      public Point centerTileXYOffset;
      public Point dragPoint;

      public Point mouseDown;
      public Point mouseCurrent;

      public Size sizeOfMapArea;
      public Size sizeOfTiles;

      public Rectangle tileRect = new Rectangle(new Point(0, 0), GMaps.Instance.TileSize);
      public Point tilePoint;

      public readonly TileMatrix Matrix = new TileMatrix();
      public readonly BackgroundWorker boundsChecker = new BackgroundWorker();
      public readonly BackgroundWorker loader = new BackgroundWorker();
      public readonly BackgroundWorker loader2 = new BackgroundWorker();
      public readonly BackgroundWorker loader3 = new BackgroundWorker();
      EventWaitHandle waitOnEmptyTasks = new AutoResetEvent(false);
      EventWaitHandle waitForBoundsChanged = new AutoResetEvent(true);
      public List<Point> tileDrawingList = new List<Point>();
      public readonly Queue<Point> tileLoadQueue = new Queue<Point>();
      public readonly List<Route> routes = new List<Route>();
      public readonly List<Marker> markers = new List<Marker>();

      public readonly string copyright = string.Format("©{0} Google - Map data ©{0} Tele Atlas", DateTime.Today.Year);
      bool currentMarkerEnabled = true;
      bool mouseVisible = true;
      public bool IsMouseOverMarker;

      public bool MouseVisible
      {
         get
         {
            return mouseVisible;
         }
         set
         {
            mouseVisible = value;
         }
      }
      int zoom = 12;
      int Width;
      int Height;

      /// <summary>
      /// is current marker enabled and visible
      /// </summary>
      public bool CurrentMarkerEnabled
      {
         get
         {
            return currentMarkerEnabled;
         }
         set
         {
            currentMarkerEnabled = value;
            if(OnNeedInvalidation != null)
            {
               OnNeedInvalidation();
            }
         }
      }

      /// <summary>
      /// total count of google tiles at current zoom
      /// </summary>
      public long TotalTiles
      {
         get
         {
            return GMaps.Instance.GetTileMatrixItemCount(Zoom);
         }
      }

      /// <summary>
      /// is user dragging map
      /// </summary>
      public bool IsDragging = false;

      /// <summary>
      /// map zoom
      /// </summary>
      public int Zoom
      {
         get
         {
            return zoom;
         }
         set
         {
            zoom = value;
            sizeOfTiles = GMaps.Instance.GetTileMatrixSize(value);
            currentPositionPixel = GMaps.Instance.FromLatLngToPixel(currentPosition, value);
            currentPositionTile = GMaps.Instance.FromPixelToTileXY(currentPositionPixel);
         }
      }

      /// <summary>
      /// current marker position in pixel coordinates
      /// </summary>
      public Point CurrentPositionGPixel
      {
         get
         {
            return currentPositionPixel;
         }
         internal set
         {
            currentPositionPixel = value;
         }
      }

      /// <summary>
      /// google tile in which current marker is
      /// </summary>
      public Point CurrentPositionGTile
      {
         get
         {
            return currentPositionTile;
         }
         internal set
         {
            currentPositionTile = value;
         }
      }

      /// <summary>
      /// current marker position
      /// </summary>
      public PointLatLng CurrentPosition
      {
         get
         {

            return currentPosition;
         }
         set
         {
            currentPosition = value;
            currentPositionPixel = GMaps.Instance.FromLatLngToPixel(value, Zoom);
            currentPositionTile = GMaps.Instance.FromPixelToTileXY(currentPositionPixel);

            if(OnCurrentPositionChanged != null)
               OnCurrentPositionChanged(currentPosition);
         }
      }

      /// <summary>
      /// font for tooltips
      /// </summary>
      public Font TooltipFont = new Font(FontFamily.GenericSansSerif, 11, FontStyle.Regular);

      /// <summary>
      /// for tooltip text padding
      /// </summary>
      public Size TooltipTextPadding = new Size(10, 10);

      /// <summary>
      /// type of map
      /// </summary>
      public GMapType MapType = GMapType.Map;

      /// <summary>
      /// is routes enabled
      /// </summary>
      public bool RoutesEnabled = true;

      /// <summary>
      /// is markers enabled
      /// </summary>
      public bool MarkersEnabled = true;

      /// <summary>
      /// can user drag map
      /// </summary>
      public bool CanDragMap = true;

      /// <summary>
      /// style of the current marker
      /// </summary>
      public CurrentMarkerType CurrentMarkerStyle = CurrentMarkerType.GMap;

      /// <summary>
      /// map render mode
      /// </summary>
      public RenderMode RenderMode = RenderMode.GDI_PLUS;

      /// <summary>
      /// occurs when current position is changed
      /// </summary>
      public event CurrentPositionChanged OnCurrentPositionChanged;

      /// <summary>
      /// occurs when tile set load is complete
      /// </summary>
      public event TileLoadComplete OnTileLoadComplete;

      /// <summary>
      /// occurs when tile set is starting to load
      /// </summary>
      public event TileLoadStart OnTileLoadStart;

      /// <summary>
      /// occurs when clicked on marker
      /// </summary>
      public event MarkerClick OnMarkerClick;

      /// <summary>
      /// occurs on mouse enters marker area
      /// </summary>
      public event MarkerEnter OnMarkerEnter;

      /// <summary>
      /// occurs on mouse leaves marker area
      /// </summary>
      public event MarkerLeave OnMarkerLeave;

      /// <summary>
      /// occurs on tile loaded
      /// </summary>
      public event NeedInvalidation OnNeedInvalidation;

      public Core()
      {
         loader.WorkerReportsProgress = true;
         loader.WorkerSupportsCancellation = true;
         loader.ProgressChanged += new ProgressChangedEventHandler(loader_ProgressChanged);
         loader.DoWork += new DoWorkEventHandler(loader_DoWork);

         loader2.WorkerReportsProgress = true;
         loader2.WorkerSupportsCancellation = true;
         loader2.ProgressChanged += new ProgressChangedEventHandler(loader_ProgressChanged);
         loader2.DoWork += new DoWorkEventHandler(loader2_DoWork);

         loader3.WorkerReportsProgress = true;
         loader3.WorkerSupportsCancellation = true;
         loader3.ProgressChanged += new ProgressChangedEventHandler(loader_ProgressChanged);
         loader3.DoWork += new DoWorkEventHandler(loader3_DoWork);

         boundsChecker.WorkerReportsProgress = true;
         boundsChecker.WorkerSupportsCancellation = true;
         boundsChecker.ProgressChanged += new ProgressChangedEventHandler(loader_ProgressChanged);
         boundsChecker.DoWork += new DoWorkEventHandler(boundsChecker_DoWork);
      }

      public void OnMapSizeChanged(int width, int height)
      {
         this.Width = width;
         this.Height = height;
      }

      public void OnMapClose()
      {
         if(waitForBoundsChanged != null)
         {
            waitForBoundsChanged.Set();
            waitForBoundsChanged.Close();
            waitForBoundsChanged = null;
         }

         if(waitOnEmptyTasks != null)
         {
            waitForBoundsChanged.Set();
         }

         CancelAsyncTasks();
      }

      /// <summary>
      /// sets to max zoom to fit all markers and centers them in map
      /// </summary>
      public bool ZoomAndCenterMarkers()
      {
         RectLatLng rect = GetRectOfAllMarkers();
         if(rect != RectLatLng.Empty)
         {
            int maxZoom = GetMaxZoomToFitRect(rect);
            if(maxZoom > 0)
            {
               PointLatLng center = new PointLatLng(rect.Lat-(rect.HeightLat/2), rect.Lng+(rect.WidthLng/2));
               CurrentPosition = center;

               if(Zoom != maxZoom)
               {
                  Zoom = maxZoom;
                  ReloadMap();
               }
               else
               {
                  GoToCurrentPosition();
               }
               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// sets current position by geocoding
      /// </summary>
      /// <param name="keys"></param>
      /// <returns></returns>
      public bool SetCurrentPositionByKeywords(string keys)
      {
         PointLatLng? pos = GMaps.Instance.GetLatLngFromGeocoder(keys);
         if(pos.HasValue)
         {
            CurrentPosition = pos.Value;
         }

         return pos.HasValue;
      }

      /// <summary>
      /// gets current map view top/left coordinate, width in Lng, height in Lat
      /// </summary>
      /// <returns></returns>
      public RectLatLng CurrentViewArea
      {
         get
         {
            RectLatLng ret = new RectLatLng();

            ret.Location = GMaps.Instance.FromPixelToLatLng(-renderOffset.X, -renderOffset.Y, Zoom);

            double lng = GMaps.Instance.FromPixelToLatLng(-renderOffset.X + Width, -renderOffset.Y, Zoom).Lng - ret.Location.Lng;
            double lat = GMaps.Instance.FromPixelToLatLng(-renderOffset.X, -renderOffset.Y - Height, Zoom).Lat - ret.Location.Lat;
            ret.Size = new SizeLatLng(lng, lat);

            return ret;
         }
      }

      /// <summary>
      /// gets lat/lng from local control coordinates
      /// </summary>
      /// <param name="local"></param>
      /// <returns></returns>
      public PointLatLng FromLocalToLatLng(Point local)
      {
         return GMaps.Instance.FromPixelToLatLng(new Point(local.X - renderOffset.X, local.Y - renderOffset.Y), Zoom);
      }

      /// <summary>
      /// gets rectangle with all markers inside
      /// </summary>
      /// <returns></returns>
      public RectLatLng GetRectOfAllMarkers()
      {
         RectLatLng ret = RectLatLng.Empty;

         lock(markers)
         {
            if(markers.Count > 0)
            {
               double left = double.MaxValue;
               double top = double.MinValue;
               double right = double.MinValue;
               double bottom = double.MaxValue;

               foreach(Marker m in markers)
               {
                  // left
                  if(m.Position.Lng < left)
                  {
                     left = m.Position.Lng;
                  }

                  // top
                  if(m.Position.Lat > top)
                  {
                     top = m.Position.Lat;
                  }

                  // right
                  if(m.Position.Lng > right)
                  {
                     right = m.Position.Lng;
                  }

                  // bottom
                  if(m.Position.Lat < bottom)
                  {
                     bottom = m.Position.Lat;
                  }
               }

               ret = RectLatLng.FromLTRB(left, top, right, bottom);
            }
         }

         return ret;
      }

      /// <summary>
      /// adds route
      /// </summary>
      /// <param name="item"></param>
      public void AddRoute(Route item)
      {
         lock(routes)
         {
            routes.Add(item);
         }

         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// removes route
      /// </summary>
      /// <param name="item"></param>
      public void RemoveRoute(Route item)
      {
         lock(routes)
         {
            routes.Remove(item);
         }

         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// clears all routes
      /// </summary>
      /// <param name="item"></param>
      public void ClearAllRoutes()
      {
         lock(routes)
         {
            routes.Clear();
         }

         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// adds marker
      /// </summary>
      /// <param name="item"></param>
      public void AddMarker(Marker item)
      {
         lock(markers)
         {
            markers.Add(item);
         }

         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// removes marker
      /// </summary>
      /// <param name="item"></param>
      public void RemoveMarker(Marker item)
      {
         lock(markers)
         {
            markers.Remove(item);
         }

         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// clears all markers
      /// </summary>
      /// <param name="item"></param>
      public void ClearAllMarkers()
      {
         lock(markers)
         {
            markers.Clear();
         }

         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// manages currently markers on map visibility
      /// </summary>
      /// <param name="visible"></param>
      public void SetCurrentMarkersVisibility(bool visible)
      {
         lock(markers)
         {
            foreach(Marker m in markers)
            {
               m.Visible = visible;
            }
         }

         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// manages currently markers on map tooltip mode
      /// </summary>
      /// <param name="mode"></param>
      public void SetCurrentMarkersTooltipMode(MarkerTooltipMode mode)
      {
         lock(markers)
         {
            foreach(Marker m in markers)
            {
               m.TooltipMode = mode;
            }
         }

         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// changes current position without changing current gtile
      /// </summary>
      /// <param name="localPoint"></param>
      public void ChangeCurrentPositionOnly(Point localPoint)
      {
         currentPosition = GMaps.Instance.FromPixelToLatLng(localPoint, Zoom);
         currentPositionPixel = GMaps.Instance.FromLatLngToPixel(currentPosition, Zoom);

         if(OnCurrentPositionChanged != null)
            OnCurrentPositionChanged(currentPosition);
      }



      /// <summary>
      /// gets rectangle around marker
      /// </summary>
      /// <param name="e"></param>
      /// <param name="m"></param>
      /// <returns></returns>
      public Rectangle GetRectForMarker(Point e, Marker m)
      {
         Rectangle rc1 = new Rectangle();
         rc1.Location = new Point(e.X - renderOffset.X, e.Y - renderOffset.Y);
         if(m.Type == MarkerType.Small)
         {
            rc1.Size = new Size(8, 10);
            rc1.Offset(-rc1.Width/2, 10);
         }
         else if(m.Type == MarkerType.Medium)
         {
            rc1.Size = new Size(18, 22);
            rc1.Offset(-rc1.Width/2, 10);
         }
         else // custom
         {
            rc1.Size = m.CustomMarker.Size;

            switch(m.CustomMarkerAlign)
            {
               case CustomMarkerAlign.MiddleMiddle:
               {
                  rc1.Offset(-rc1.Width/2, -rc1.Height/2);
               }
               break;

               case CustomMarkerAlign.Manual:
               {
                  rc1.Offset(-rc1.Width+m.CustomMarkerCenter.X, -rc1.Height+m.CustomMarkerCenter.Y);
               }
               break;
            }
         }
         return rc1;
      }

      /// <summary>
      /// checks if some marker was clicked
      /// </summary>
      /// <param name="e"></param>
      public void CheckIfClickOnMarker(Point e)
      {
         lock(markers)
         {
            for(int i = 0; i < markers.Count; i++)
            {
               Marker m = markers[i];
               if(m.Visible)
               {
                  Rectangle rc1 = GetRectForMarker(e, m);

                  using(Region rg = new Region(rc1))
                  {
                     if(rg.IsVisible(GMaps.Instance.FromLatLngToPixel(m.Position, Zoom)))
                     {
                        if(OnNeedInvalidation != null)
                        {
                           OnNeedInvalidation();
                        }

                        if(OnMarkerClick != null)
                        {
                           if(!mouseVisible)
                           {
                              //this.Cursor = System.Windows.Forms.Cursors.Default;
                              //Cursor.Show();
                              mouseVisible = true;
                           }

                           OnMarkerClick(m);
                           break;
                        }
                     }
                  }
               }
            }
         }
      }

      /// <summary>
      /// gets max zoom level to fit rectangle
      /// </summary>
      /// <param name="rect"></param>
      /// <returns></returns>
      public int GetMaxZoomToFitRect(RectLatLng rect)
      {
         int zoom = 0;

         for(int i = 1; i <= GMaps.Instance.MaxZoom; i++)
         {
            Point p1 = GMaps.Instance.FromLatLngToPixel(rect.Location, i);
            Point p2 = GMaps.Instance.FromLatLngToPixel(rect.Bottom, rect.Right, i);

            if(((p2.X - p1.X) <= Width+10) && (p2.Y - p1.Y) <= Height+10)
            {
               zoom = i;
            }
            else
            {
               break;
            }
         }

         return zoom;
      }

      /// <summary>
      /// initiates map dragging
      /// </summary>
      /// <param name="pt"></param>
      public void BeginDrag(Point pt)
      {
         dragPoint.X = pt.X - renderOffset.X;
         dragPoint.Y = pt.Y - renderOffset.Y;
         IsDragging = true;
      }

      /// <summary>
      /// ends map dragging
      /// </summary>
      public void EndDrag()
      {
         IsDragging = false;
         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// reloads map
      /// </summary>
      public void ReloadMap()
      {
         lock(tileLoadQueue)
         {
            tileLoadQueue.Clear();
         }

         {
            Matrix.Clear();

            OnNeedInvalidation();

            GC.Collect(GC.MaxGeneration);
            GC.WaitForPendingFinalizers();
         }

         GoToCurrentPosition();

         // start loading
         RunAsyncTasks();
      }

      /// <summary>
      /// moves current position into map center
      /// </summary>
      public void GoToCurrentPosition()
      {
         // reset stuff
         renderOffset = Point.Empty;
         centerTileXYOffset = Point.Empty;
         centerTileXYLocationLast = Point.Empty;
         dragPoint = Point.Empty;

         // goto location
         this.Drag(new Point(-(CurrentPositionGPixel.X - Width/2), -(CurrentPositionGPixel.Y - Height/2)));
      }

      /// <summary>
      /// drag map
      /// </summary>
      /// <param name="pt"></param>
      public void Drag(Point pt)
      {
         renderOffset.X = pt.X - dragPoint.X;
         renderOffset.Y = pt.Y - dragPoint.Y;

         centerTileXYLocation.X = ((renderOffset.X)/-GMaps.Instance.TileSize.Width) + 2;
         centerTileXYLocation.Y = ((renderOffset.Y)/-GMaps.Instance.TileSize.Height) + 2;

         if(centerTileXYLocation != centerTileXYLocationLast)
         {
            centerTileXYOffset.X = CurrentPositionGTile.X - centerTileXYLocation.X;
            centerTileXYOffset.Y = CurrentPositionGTile.Y - centerTileXYLocation.Y;
            centerTileXYLocationLast = centerTileXYLocation;

            waitForBoundsChanged.Set();
         }

         if(OnNeedInvalidation != null)
         {
            OnNeedInvalidation();
         }
      }

      /// <summary>
      /// cancels tile loaders and bounds checker
      /// </summary>
      public void CancelAsyncTasks()
      {
         if(boundsChecker.IsBusy)
         {
            boundsChecker.CancelAsync();
         }

         if(loader.IsBusy)
         {
            loader.CancelAsync();
         }

         if(loader2.IsBusy)
         {
            loader2.CancelAsync();
         }

         if(loader3.IsBusy)
         {
            loader3.CancelAsync();
         }
      }

      /// <summary>
      /// runs tile loaders and bounds checker
      /// </summary>
      void RunAsyncTasks()
      {
         if(!boundsChecker.IsBusy)
         {
            boundsChecker.RunWorkerAsync();
         }

         if(!loader.IsBusy)
         {
            loader.RunWorkerAsync();
         }

         if(!loader2.IsBusy)
         {
            loader2.RunWorkerAsync();
         }

         if(!loader3.IsBusy)
         {
            loader3.RunWorkerAsync();
         }

         waitForBoundsChanged.Set();
      }

      // loader1
      void loader_DoWork(object sender, DoWorkEventArgs e)
      {
         while(!loader.CancellationPending)
         {
            LoaderWork(1);
         }
      }

      // loader2
      void loader2_DoWork(object sender, DoWorkEventArgs e)
      {
         while(!loader2.CancellationPending)
         {
            LoaderWork(2);
         }
      }

      // loader3
      void loader3_DoWork(object sender, DoWorkEventArgs e)
      {
         while(!loader3.CancellationPending)
         {
            LoaderWork(3);
         }
      }

      /// <summary>
      /// tile loader worker
      /// </summary>
      /// <param name="id"></param>
      void LoaderWork(int id)
      {
         bool process = true;
         Point task = new Point();

         lock(tileLoadQueue)
         {
            if(tileLoadQueue.Count > 0)
            {
               task = tileLoadQueue.Dequeue();
            }
            else
            {
               process = false;
            }
         }

         if(process)
         {
            Debug.WriteLine("loader[" + id + "]: download => " + task);

            // report load start
            loader.ReportProgress(id, false);

            PureImage img = GMaps.Instance.GetImageFrom(MapType, task, Zoom, GMaps.Instance.Language, GMaps.Instance.UseTileCache);

            Tile x = new Tile(img, Zoom, task, RenderMode);
            Matrix[task] = x;

            loader.ReportProgress(id, x);
         }
         else
         {
            Debug.WriteLine("loader[" + id + "]: wait");

            // report load complete
            loader.ReportProgress(id, true);

            waitOnEmptyTasks.WaitOne();  // No more tasks - wait for a signal
         }
      }

      /// <summary>
      /// invalidates map on tile loaded
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      void loader_ProgressChanged(object sender, ProgressChangedEventArgs e)
      {
         {
            if(OnNeedInvalidation != null)
            {
               OnNeedInvalidation();
            }

            // some tile loader complete
            if(e.UserState != null && e.UserState.GetType() == typeof(bool))
            {
               bool complete = (bool) e.UserState;

               if(complete)
               {
                  if(OnTileLoadComplete != null)
                  {
                     OnTileLoadComplete(e.ProgressPercentage);
                  }
               }
               else
               {
                  if(OnTileLoadStart != null)
                  {
                     OnTileLoadStart(e.ProgressPercentage);
                  }
               }
            }
         }
      }

      /// <summary>
      /// bunds checker worker
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      void boundsChecker_DoWork(object sender, DoWorkEventArgs e)
      {
         while(!boundsChecker.CancellationPending)
         {
            waitForBoundsChanged.WaitOne();

            lock(tileDrawingList)
            {
               FindTilesAround(ref tileDrawingList);

               Matrix.ClearPointsNotIn(ref tileDrawingList);

               Debug.WriteLine("boundsChecker_DoWork: total tiles => " + tileDrawingList.Count.ToString());

               foreach(Point p in tileDrawingList)
               {
                  if(Matrix[p] == null)
                  {
                     Debug.WriteLine("EnqueueLoadTask: " + p.ToString());

                     EnqueueLoadTask(p);
                  }
               }
            }

            GC.Collect(GC.MaxGeneration);
            GC.WaitForPendingFinalizers();

            boundsChecker.ReportProgress(100);
         }
      }

      /// <summary>
      /// enqueueens tile to loaad
      /// </summary>
      /// <param name="task"></param>
      void EnqueueLoadTask(Point task)
      {
         lock(tileLoadQueue)
         {
            tileLoadQueue.Enqueue(task);
         }
         waitOnEmptyTasks.Set();
      }

      /// <summary>
      /// find tiles around to fill screen
      /// </summary>
      /// <param name="list"></param>
      void FindTilesAround(ref List<Point> list)
      {
         list.Clear();
         for(int i = -sizeOfMapArea.Width; i < sizeOfMapArea.Width; i++)
         {
            for(int j = -sizeOfMapArea.Height; j < sizeOfMapArea.Height; j++)
            {
               Point p = centerTileXYLocation;
               p.X += i;
               p.Y += j;

               if(p.X < sizeOfTiles.Width && p.Y < sizeOfTiles.Height)
               {
                  if(p.X >= 0 && p.Y >= 0)
                  {
                     list.Add(p);
                  }
               }
            }
         }
         Stuff.Shuffle<Point>(list);
      }
   }
}