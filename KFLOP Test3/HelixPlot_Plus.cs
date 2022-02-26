using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

using System.Windows.Media;
using System.Windows.Media.Media3D;

using HelixToolkit.Wpf;

namespace KFLOP_Test3
{
    class HelixPlot_Plus : HelixViewport3D // inherits the HelixViewport3D base class
    {
        // private variables 

        // marker is the tool indicator 
        private Transform3DGroup mtgroup;   // group for the various marker elements
        private TranslateTransform3D marker_xyz;    // a tranform for the marker so all the elemnents move together.

        // the marker elements are 
        // a truncated cone with the point at the pont of control
        private TruncatedConeVisual3D marker; // an cone used as a marker

        private TruncatedConeVisual3D tool; // part of the marker group
        private BillboardTextVisual3D toolLen;  // tool lenght display
        

        // private SphereVisual3D marker;   // stuff I was playing with...
        // private CubeVisual3D marker;
        // private ModelVisual3D marker;

        // A billboard text that shows the current marker coordinates  
        private BillboardTextVisual3D coords; // the coordinates by the marker
        private double labelOffset, minDistanceSquared; // two variables
        private string coordinateFormat; // the coordinate string format

        // the tool path trace
        private List<LinesVisual3D> trace; // the actual lines
        private LinesVisual3D path; // individual path segments
        private Point3D point0; // the last point
        private Vector3D delta0; // (dx, dy, dz)

        // the home and coordinate reference gnomons
        private CoordinateSystemVisual3D Home_gnomon;
        private CoordinateSystemVisual3D GRef_gnomon;
        private BillboardTextVisual3D GRef_coords; // name and coordinates of the GRef
        private string GRef_Format; // GRef coordinate format string
        private Transform3DGroup GRef_group; // 
        private TranslateTransform3D GRef_xyz; // transform for the GRef gnomon

        public HelixPlot_Plus() : base() // the constructor for the instance of HelixPlot_x
        {
            // this is how to initialize this instance of the HelixViewport3D 
            ZoomExtentsWhenLoaded = true;
            // ShowCoordinateSystem = false; // might have to play with this...
            ShowCoordinateSystem = true; // might have to play with this...

            ShowViewCube = false;   // apparently setting this true in the XAML supercedes this here.
            ShowFrameRate = true;
            ShowTriangleCountInfo = true;   // these seem to be built in to HelixToolKit

            ShowCameraInfo = true;

            // since this is for the BP308 display I am making the bounding box equal to the machining envelope
            // in inches 

            // default configuration
            AxisLabels = "X,Y,Z";
            BoundingBox = new Rect3D(-25, -15, -15, 26, 16, 16);    // hard coded machine size here!
            TickSize = 5;
            MinDistance = 0.005;
            DecimalPlaces = 2;

            Background = Brushes.LightSlateGray; // make this any color...
            AxisBrush = Brushes.Black;

            // MarkerBrush.Opacity = 0.4;
            SolidColorBrush mBrush = new SolidColorBrush(Brushes.HotPink.Color);
            mBrush.Opacity = 0.2;
            // MarkerBrush = Brushes.HotPink;
            MarkerBrush = mBrush;
            
            

            //           position                    look direction             up direction       animation time
            SetView(new Point3D(-12, -43, 5), new Vector3D(0, 43, -10), new Vector3D(0, 0, 1), 0);

            Elements = EElements.All;
            Elements = Elements & (~(EElements.Axes | EElements.Grid));
            CreateElements();

        }

        // a bunch of elements
        public string AxisLabels { get; set; }
        public Rect3D BoundingBox { get; set; }
        public double TickSize { get; set; }
        public double MinDistance { get; set; }
        public int DecimalPlaces { get; set; }
        public SolidColorBrush AxisBrush { get; set; }
        public SolidColorBrush MarkerBrush { get; set; }

        // define the Enum with the Flags attribute - https://docs.microsoft.com/en-us/dotnet/api/system.flagsattribute?view=net-5.0
        [Flags]
        public enum EElements   // these flags are bit mapped
        {
            None = 0x00,
            Axes = 0x01,
            Grid = 0x02,
            BoundingBox = 0x04,
            Marker = 0x08,
            Home = 0x10,
            GRef = 0x20,
            Tool = 0x40,
            All = 0x7f
        };

        public EElements Elements { get; set; }

        // gets the current trace color
        public Color TraceColor { get { return (path != null) ? path.Color : Colors.Black; } }
        // gets the current trace width
        public double TraceThickness { get { return (path != null) ? path.Thickness : 1; } }

        public void CreateElements()    // it looks like this only gets called once.
        {
            Children.Clear();   // clear everything! starting from ground zero

            //Children.Add(new DefaultLights());   // first the Lights
            Children.Add(new SunLight());

            string[] labels = AxisLabels.Split(',');    // split the axis labels at the commas
            if (labels.Length < 3)
                labels = new string[] { "X", "Y", "Z" };

            double bbSize = Math.Max(Math.Max(BoundingBox.SizeX, BoundingBox.SizeY), BoundingBox.SizeZ);
            double lineThickness = bbSize / 1000;
            double arrowOffset = lineThickness * 30;
            labelOffset = lineThickness * 40;
            minDistanceSquared = MinDistance * MinDistance;

            // Add the grid
            if (Elements.HasFlag(EElements.Grid)) // if the grid bit is set in the flags... then draw the grid
            {
                var grid = new GridLinesVisual3D();
                //               grid.Center = new Point3D(BoundingBox.X + 0.5 * BoundingBox.SizeX, BoundingBox.Y + 0.5 * BoundingBox.SizeY, BoundingBox.Z);
                grid.Center = new Point3D(0.0, 0.0, BoundingBox.Z);

                grid.Length = BoundingBox.SizeX;
                grid.Width = BoundingBox.SizeY;
                grid.MinorDistance = TickSize;
                grid.MajorDistance = bbSize;
                grid.Thickness = lineThickness;
                grid.Fill = AxisBrush;
                Children.Add(grid);
            }

            // Add the Axis
            if (Elements.HasFlag(EElements.Axes))
            {
                var arrow = new ArrowVisual3D();
                arrow.Point2 = new Point3D((BoundingBox.X + BoundingBox.SizeX) + arrowOffset, 0.0, 0.0);
                arrow.Diameter = lineThickness * 5;
                arrow.Fill = AxisBrush;
                Children.Add(arrow);

                var label = new BillboardTextVisual3D();
                label.Text = labels[0];
                label.FontWeight = FontWeights.Bold;
                label.Foreground = AxisBrush;
                label.Position = new Point3D((BoundingBox.X + BoundingBox.SizeX) + labelOffset, 0.0, 0.0);
                Children.Add(label);

                arrow = new ArrowVisual3D();
                arrow.Point2 = new Point3D(0.0, (BoundingBox.Y + BoundingBox.SizeY) + arrowOffset, 0.0);
                arrow.Diameter = lineThickness * 5;
                arrow.Fill = AxisBrush;
                Children.Add(arrow);

                label = new BillboardTextVisual3D();
                label.Text = labels[1];
                label.FontWeight = FontWeights.Bold;
                label.Foreground = AxisBrush;
                label.Position = new Point3D(0.0, (BoundingBox.Y + BoundingBox.SizeY) + labelOffset, 0.0);
                Children.Add(label);

                if (BoundingBox.SizeZ > 0)
                {
                    arrow = new ArrowVisual3D();
                    arrow.Point2 = new Point3D(0.0, 0.0, (BoundingBox.Z + BoundingBox.SizeZ) + arrowOffset);
                    arrow.Diameter = lineThickness * 5;
                    arrow.Fill = AxisBrush;
                    Children.Add(arrow);

                    label = new BillboardTextVisual3D();
                    label.Text = labels[2];
                    label.FontWeight = FontWeights.Bold;
                    label.Foreground = AxisBrush;
                    label.Position = new Point3D(0.0, 0.0, (BoundingBox.Z + BoundingBox.SizeZ) + labelOffset);
                    Children.Add(label);
                }
            }

            // Add the Bounding Box
            if (Elements.HasFlag(EElements.BoundingBox) && BoundingBox.SizeZ > 0)
            {
                var box = new BoundingBoxWireFrameVisual3D();
                box.BoundingBox = BoundingBox;
                box.Thickness = 1;
                box.Color = AxisBrush.Color;
                Children.Add(box);
            }

            // Add the Marker
            if (Elements.HasFlag(EElements.Marker))
            {
                // billboard text for moving marker coordinates
                coords = new BillboardTextVisual3D();
                coordinateFormat = string.Format("{{0:F{0}}}, {{1:F{0}}}, {{2:F{0}}}", DecimalPlaces, DecimalPlaces, DecimalPlaces);  // "{0:F2}, {1:F2}, {2:F2}"
                coords.Text = string.Format(coordinateFormat, 0.0, 0.0, 0.0);
                // coords.Foreground = MarkerBrush;
                coords.Foreground = AxisBrush;
                coords.Position = new Point3D(-labelOffset, -labelOffset, labelOffset);
                Children.Add(coords);

                // what can I do with the marker?
                marker = new TruncatedConeVisual3D();
                marker.Height = 0.8;
                marker.BaseRadius = 0.0;
                marker.TopRadius = 0.4;
                marker.TopCap = true;
                marker.Origin = new Point3D(0.0, 0.0, 0.0);
                marker.Normal = new Vector3D(0.0, 0.0, 1.0);    // this should point upwards...
                marker.Fill = MarkerBrush;

                mtgroup = new Transform3DGroup();
                marker_xyz = new TranslateTransform3D();
                marker_xyz.OffsetX = 0.0;
                marker_xyz.OffsetY = 0.0;
                marker_xyz.OffsetZ = 0.0;
                mtgroup.Children.Add(marker_xyz);
                marker.Transform = mtgroup;

                Children.Add(marker);

                if (Elements.HasFlag(EElements.Tool))
                {
                    tool = new TruncatedConeVisual3D();
                    tool.Height = 0.5;
                    tool.BaseRadius = 0.25;
                    tool.TopRadius = 0.25;
                    tool.TopCap = true;
                    tool.Origin = new Point3D(0, 0, 0.8); // top of the cone
                    tool.Normal = new Vector3D(0, 0, 1);
                    tool.Fill = MarkerBrush;
                    tool.Transform = mtgroup; // same transform as the marker - should ride on top?
                    Children.Add(tool);
                }
                else
                {
                    tool = null;

                }
                
            }
            else // or no marker
            {
                marker = null;
                coords = null;
            }

            // add a coordinate gnomon
            if (Elements.HasFlag(EElements.Home))
            {
                Home_gnomon = new CoordinateSystemVisual3D();
                Home_gnomon.ArrowLengths = 1.0;
                Children.Add(Home_gnomon);

                var label = new BillboardTextVisual3D();
                label.Text = "HOME";
                label.FontWeight = FontWeights.Bold;
                label.Foreground = AxisBrush;
                label.Position = new Point3D(1, 0.5, 0.5);
                Children.Add(label);
            }
            else
            {
                Home_gnomon = null;
            }

            // add the GRef gnomon
            if(Elements.HasFlag(EElements.GRef))
            {
                GRef_gnomon = new CoordinateSystemVisual3D();
                GRef_gnomon.ArrowLengths = 0.6;
                GRef_gnomon.XAxisColor = Colors.DeepPink;
                GRef_gnomon.YAxisColor = Colors.LightGreen;
                GRef_gnomon.ZAxisColor = Colors.LightBlue;

                GRef_group = new Transform3DGroup();
                GRef_xyz = new TranslateTransform3D();  // this adds the ability to move the gnomon around.
                GRef_xyz.OffsetX = 0.0;
                GRef_xyz.OffsetY = 0.0;
                GRef_xyz.OffsetZ = 0.0;
                GRef_group.Children.Add(GRef_xyz);
                GRef_gnomon.Transform = GRef_group;

                Children.Add(GRef_gnomon);

                // the billboard coordinates of the reference gnomon
                GRef_coords = new BillboardTextVisual3D();
                GRef_Format = string.Format("G{{0}}:{{1:F{0}}}, {{2:F{0}}}, {{3:F{0}}}", DecimalPlaces, DecimalPlaces, DecimalPlaces);
                GRef_coords.Text = string.Format(GRef_Format, 54, 0.0, 0.0, 0.0);
                GRef_coords.Foreground = AxisBrush;
                GRef_coords.Position = new Point3D(-labelOffset, -labelOffset, labelOffset);
                Children.Add(GRef_coords);
            }
            else  // no GRef Coordinates
            {
                GRef_gnomon = null;
                GRef_coords = null;
                
            }

            // Add the trace 

            if (trace != null)
            {
                foreach (LinesVisual3D p in trace)
                    Children.Add(p);
                path = trace[trace.Count - 1];
            }


        }

        // clear all the traces - this doesn't get called from anywhere yet... 
        public void Clear()
        {
            trace = null;
            path = null;
            CreateElements();

        }


        public void SetGref(int Gref, double x, double y, double z)
        {

            if(GRef_gnomon != null)
            {
                GRef_xyz.OffsetX = x;
                GRef_xyz.OffsetY = y;
                GRef_xyz.OffsetZ = z;

                GRef_coords.Position = new Point3D(x - labelOffset, y - labelOffset, z + labelOffset);

                // formatting for the billboard text
                int xRef;
                switch(Gref)
                {
                    case 0 : xRef = 92; break;
                    case 1: xRef = 54; break;
                    case 2: xRef = 55; break;
                    case 3: xRef = 56; break;
                    case 4: xRef = 57; break;
                    case 5: xRef = 58; break;
                    default: xRef = 59; break;
                }
                GRef_coords.Text = string.Format(GRef_Format, xRef, x, y, z);
                GRef_coords.Position = new Point3D(x - labelOffset, y - labelOffset, z + labelOffset);
            }
        }

        // two ways to add a trace 

        // create a new trace with a Point3D
        public void NewTrace(Point3D point, Color color, double thickness = 1)  // default thickness is 1
        {
            path = new LinesVisual3D();
            path.Color = color;
            path.Thickness = thickness;
            trace = new List<LinesVisual3D>();
            trace.Add(path);
            Children.Add(path);
            point0 = point;
            delta0 = new Vector3D();

//            if (marker != null)
//            {
//                MarkerPosition(point);
//            }
        }
        // create  a new trace from discrete points
        public void NewTrace(double x, double y, double z, Color color, double thickness = 1)
        {
            NewTrace(new Point3D(x, y, z), color, thickness);
        }


        // four ways to add a point to the current trace
        // add a point with a specific color and thickness
        public void AddPoint(Point3D point, Color color, double thickness = -1)
        {
            if (trace == null) // then start a new trace
            {
                NewTrace(point, color, (thickness > 0) ? thickness : 1);    // note that this checks for a valid thickness or defaults to 1
                return;
            }

            if ((point - point0).LengthSquared < minDistanceSquared) return;    // if less than the min distance then return - no new trace...

            if (path.Color != color || (thickness > 0 && path.Thickness != thickness))    // if the path color or thickness has changed...
            {
                if (thickness <= 0)
                    thickness = path.Thickness; // if thickness is not valid then use the last thickness

                path = new LinesVisual3D();
                path.Color = color;
                path.Thickness = thickness;
                trace.Add(path);
                Children.Add(path);
            }

            // If line segments AB and BC have the same direction (small cross product) then remove point B.
            bool sameDir = false;
            var delta = new Vector3D(point.X - point0.X, point.Y - point0.Y, point.Z - point0.Z);
            delta.Normalize();  // use unit vectors (magnitude 1) for the cross product calculations
            if (path.Points.Count > 0)
            {
                double xp2 = Vector3D.CrossProduct(delta, delta0).LengthSquared;
                sameDir = (xp2 < 0.0005);  // approx 0.001 seems to be a reasonable threshold from logging xp2 values
                //if (!sameDir) Title = string.Format("xp2={0:F6}", xp2);
            }

            if (sameDir)  // extend the current line segment
            {
                path.Points[path.Points.Count - 1] = point;
                point0 = point;
                delta0 += delta;
            }
            else  // add a new line segment
            {
                path.Points.Add(point0);
                path.Points.Add(point);
                point0 = point;
                delta0 = delta;
            }

            // if the marker is on then move the marker
 //           if (marker != null)
 //           {
 //               MarkerPosition(point);
 //           }
        }

        // add a point with the current color and thickness
        public void AddPoint(Point3D point)
        {
            if (path == null)
            {
                NewTrace(point, Colors.Black, 1);   // not quite sure what this does...
                return;
            }

            AddPoint(point, path.Color, path.Thickness);
        }

        // add a point at discrete values and specific color
        public void AddPoint(double x, double y, double z, Color color, double thickness = -1)
        {
            AddPoint(new Point3D(x, y, z), color, thickness);
        }

        // add a point at discrete values
        public void AddPoint(double x, double y, double z)
        {
            if (path == null)
                return;

            AddPoint(new Point3D(x, y, z), path.Color, path.Thickness);
        }


        public void MarkerPosition(Point3D point)
        {
            // move the marker (tool indicator) and the marker coordinate billboard to the position indicated by 'point'
            // marker.Origin = point;
            // marker.Center = new Point3D(point.X, point.Y, point.Z + labelOffset / 2);
            marker_xyz.OffsetX = point.X;
            marker_xyz.OffsetY = point.Y;
            marker_xyz.OffsetZ = point.Z;

            coords.Position = new Point3D(point.X - labelOffset, point.Y - labelOffset, point.Z + labelOffset);
            coords.Text = string.Format(coordinateFormat, point.X, point.Y, point.Z);
        }

        public void MoveMarker(double x, double y, double z)
        {
            if(marker != null)
            {
                MarkerPosition(new Point3D(x, y, z));

                MarkerTransparent();

            }
        }

        public void MarkerTransparent()
        {
            Children.Remove(marker);
            Children.Remove(tool);
            Children.Add(marker);
            Children.Add(tool);
            Children.Remove(coords);
            Children.Add(coords);
        }
    }
}

