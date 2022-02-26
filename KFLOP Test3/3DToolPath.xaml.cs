using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.Windows.Media.Media3D; // for the Point3D
using System.Threading;
using System.Windows.Threading;
using System.Diagnostics;   // for the stopwatch
using System.ComponentModel; // for the background worker

// for KMotion libraries
using KMotion_dotNet;

namespace KFLOP_Test3
{
    /// <summary>
    /// Interaction logic for _3DToolPath.xaml
    /// </summary>
    public partial class _3DToolPath : Window
    {

        private struct Point3DPlus // Point3dPlus is a structure that contains the point, Color and Thickness
        {
            public Point3DPlus(Point3D point, Color color, double thickness)
            {
                this.point = point;     // assign the value 'point' passed to the structure to the structure's 'point' of the same name (this.point) - yes this can be very confusing!
                this.color = color;
                this.thickness = thickness;
            }
            public Point3D point;
            public Color color;
            public double thickness;

        }

        private List<Point3DPlus> points = new List<Point3DPlus>();

        public _3DToolPath()
        {
            InitializeComponent();

            // after the component is initialized, then what?
            // in the HelixPlot example, a background worker is used here to gather the data(see GatherData in the example) 
            // but I think that what I need is for the callbacks from motion to add the data 
        }

        #region Motion Callback Functions
        // the motion callbacks
        // arc callback
        public void ArcCallback(bool ZeroAsFull, double FR, int plane, double x2, double y2, double ax, double ay, int rotation, double z2, double x1, double y1, double z1, int sequence)
        {
            double x, y, z;
            x = y = z = 0.0;
            // check the plane
            if ((CANON_PLANE)plane == CANON_PLANE.CANON_PLANE_XY)
            {
                x = x2; // regular Y Y plane
                y = y2;
                z = z2;
            }
            else if((CANON_PLANE)plane == CANON_PLANE.CANON_PLANE_YZ)
            {
                x = z2;
                y = x2;
                z = y2;
            }
            else if((CANON_PLANE)plane == CANON_PLANE.CANON_PLANE_XZ)   // have not checked this one yet!
            {
                x = x2;
                y = z2;
                z = y2;
            }
            else // no valid plane for the operation!
            {
                return;
            }

            var point = new Point3DPlus(new Point3D(x, y, z), Colors.Red, 1.5);  // new arc with color red

            bool allow_invoke = false; // just copying this straight from the example - don't know what it really does...
            lock (points)
            {
                points.Add(point);
                allow_invoke = (points.Count == 1);
            }
            if (allow_invoke)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)PlotData);
            }
        }
        // straight callback
        public void StraightCallback(double FR, double x1, double y1, double z1, int sequence)
        {
            var point = new Point3DPlus(new Point3D(x1, y1, z1), Colors.Green, 1.5);  // new line with color Green
            bool allow_invoke = false; // just copying this straight from the example - don't know what it really does...
            lock (points)
            {
                points.Add(point);
                allow_invoke = (points.Count == 1);
            }
            if (allow_invoke)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)PlotData);
            }
        }
        // straight traverse callback
        public void StraightTraverseCallback(double x1, double y1, double z1, int sequence)
        {
            var point = new Point3DPlus(new Point3D(x1, y1, z1), Colors.Blue, 1.5);  // new line with color Blue
            bool allow_invoke = false; // just copying this straight from the example - don't know what it really does...
            lock (points)
            {
                points.Add(point);
                allow_invoke = (points.Count == 1);
            }
            if (allow_invoke)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)PlotData);

            }
        }
        #endregion

        public void SetGRef(int xref, double x, double y, double z)
        {
            Plot_D.SetGref(xref, x, y, z);
        }

        public void SetMarker(double x, double y, double z)
        {
            Plot_D.MoveMarker(x, y, z); // does this need an invoke???
        }

        #region Plot Data Method
        // my guess is that this will be called (invoked?) from the motion callbacks
        private void PlotData()
        {
            if (points.Count == 1)   // add a single point
            {
                Point3DPlus point;
                lock (points)
                {
                    point = points[0];
                    points.Clear();
                }
                Plot_D.AddPoint(point.point, point.color, point.thickness);
            }
            else
            {
                Point3DPlus[] pointsArray;
                lock (points)
                {
                    pointsArray = points.ToArray();
                    points.Clear();
                }

                foreach (Point3DPlus point in pointsArray)
                    Plot_D.AddPoint(point.point, point.color, point.thickness);
            }
        }
        #endregion

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            Plot_D.Clear();
        }
    }
}
