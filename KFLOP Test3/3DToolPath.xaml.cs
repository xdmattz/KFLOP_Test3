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
        // global class variables
        private KM_Controller KMx;


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

        public _3DToolPath(ref KM_Controller X)
        {
            InitializeComponent();
            KMx = X;

            // after the component is initialized, then what?
            // in the HelixPlot example, a background worker is used here to gather the data(see GatherData in the example) 
            // but I think that what I need is for the callbacks from motion to add the data 
        }

        #region Motion Callback Functions
        // the motion callbacks
        // arc callback
        public void ArcCallback(bool ZeroAsFull, double FR, int plane, double x2, double y2, double ax, double ay, int rotation, double z2, double x1, double y1, double z1, int sequence, int ID)
        {
            double toolLen = KMx.CoordMotion.Interpreter.SetupParams.ToolLengthOffset;
            // make an arc from point x1,y1 to point x2, y2 on the ax, ay axis. heigth z1 to z2. 
            // if rotation is 0 then CW, 1 is CCW


            Point3D nextPoint;                  // The point to calculate
            int steps;                          // number of steps in the arc
            double Scale;                       // scale is equal to the vector magnitude
            double angleStepSize, zStepSize;    // step sizes for the angle and height
            double Xc, Yc, Zc, AngC;            // coordinates of the new point   
            double AngStart, AngEnd;            

            double vx1, vy1, vx2, vy2;  // vectors from the axis of rotation

            // subtract the axis from the start and stop positions
            vx1 = x1 - ax;
            vy1 = y1 - ay;
            vx2 = x2 - ax;
            vy2 = y2 - ay;
            // and calculate the relative angles of the two vectors
            AngStart = VectAngle_rad(vx1, vy1);
            AngEnd = VectAngle_rad(vx2, vy2);
            // extend the angles depending on their rotation and position

            if(rotation == 0) // Clockwise rotation
            {
                if (Math.Abs(AngStart - AngEnd) < 0.001) // Zero angle = full rotation
                { AngEnd -= Math.PI * 2; } // clockwise rotation
                else if (AngStart < 0.0) // start angle is negative 
                {
                    if(AngEnd > 0)
                    { AngEnd -= Math.PI * 2; }  // subtract 2pi to make it more negative than the start 
                    else if (AngStart < AngEnd)
                    { AngStart += Math.PI * 2; }
                } else // AngStart > 0
                {
                    if(AngStart < AngEnd)
                    { AngEnd -= Math.PI * 2; }
                }
            }
            else     // Counter Clockwise rotation
            {
                if(Math.Abs(AngStart - AngEnd) < 0.001) // Zero angle = full rotation
                { AngEnd += Math.PI * 2;  } // CCW rotation
                else if (AngStart < 0)
                {
                    if(AngEnd < AngStart)
                    { AngEnd += Math.PI * 2; }
                } 
                else  // AngleStart > 0
                {
                    if(AngEnd < 0)
                    { AngEnd += Math.PI * 2; }
                    else if(AngEnd < AngStart)
                    { AngEnd += Math.PI * 2; }
                }
            }

            // Scale of the arc
            Scale = Mag2D(vx1, vy1);
            // determine the number of steps
            // this is a function of the swept angle (AngEnd - AngStart) and the Scale
            steps = (int)(Math.Abs(AngEnd - AngStart) * 15);    //  15 is an arbitrary value that I picked to make things look about right...
            if(Scale > 1.0)
            {
                steps = steps * (int)(Scale * 0.75);
            }
            // steps = 20;
            zStepSize = (z2 - z1) / (double)(steps);
            angleStepSize = (AngEnd - AngStart) / (double)(steps);
            
                
            Zc = z1;
            AngC = AngStart;
            for(int i = 0;i < steps; i++)   // sweep through the angles
            {
                Zc += zStepSize;    // any Z changes - for a helix.
                AngC += angleStepSize;  
                Xc = (Scale * Math.Cos(AngC)) + ax; // scale by the magnitude and add offset
                Yc = (Scale * Math.Sin(AngC)) + ay;

                switch((CANON_PLANE)plane)
                {
                    case CANON_PLANE.CANON_PLANE_XY: nextPoint = new Point3D(Xc, Yc, Zc - toolLen); break;
                    case CANON_PLANE.CANON_PLANE_YZ: nextPoint = new Point3D(Zc, Xc, Yc - toolLen); break;
                    case CANON_PLANE.CANON_PLANE_XZ: nextPoint = new Point3D(Yc, Zc, Xc - toolLen); break;
                    default: nextPoint = new Point3D(0, 0, 0); break;
                }
                lock (points)
                {
                    points.Add(new Point3DPlus(nextPoint, Colors.Red, 1.5));
                }
            }
            Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)PlotData);
  
        }

        // straight callback
        public void StraightCallback(double FR, double x1, double y1, double z1, int sequence, int ID)
        {
            double toolLen = KMx.CoordMotion.Interpreter.SetupParams.ToolLengthOffset;
            var point = new Point3DPlus(new Point3D(x1, y1, z1 - toolLen), Colors.Green, 1.5);  // new line with color Green
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
            double toolLen = KMx.CoordMotion.Interpreter.SetupParams.ToolLengthOffset;
            var point = new Point3DPlus(new Point3D(x1, y1, z1 - toolLen), Colors.Blue, 1.5);  // new line with color Blue
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

        public void TestCallback(double DFR, double x, double y, double z, int seq, int ID)
        {
            double mag;
            mag = Math.Sqrt(x * x + y * y + z * z);

        }

        #region Math For Arc plotting
        // dot product of two vectors
        public double DotProduct2D(double x1, double y1, double x2, double y2)
        {
            return (x1 * x2 + y1 * y2);
        }

        // magnitude of a 2D vector
        public double Mag2D(double x, double y)
        {
            return (Math.Sqrt(x * x + y * y));
        }

        // angle between two vectors
        public double Vect2Angle_rad(double x1, double y1, double x2, double y2)
        {
            return (Math.Acos(DotProduct2D(x1, y1, x2, y2) / (Mag2D(x1, y1) * Mag2D(x2, y2))));
        }

        // angle of a vector relative to the X axis
        public double VectAngle_rad(double x, double y) 
        {
            if (y < 0.0) // 3rd or 4th quadrant - angle is negative
            {
                return (-Math.Acos(x / (Math.Sqrt(x * x + y * y))));
            }
            else // 1st or 2nd quadrant - angle is positive
            {
                return (Math.Acos(x / (Math.Sqrt(x * x + y * y))));
            }
        }

        #endregion

        #endregion

        public void SetGRef(int xref, double x, double y, double z)
        {
            Plot_D.SetGref(xref, x, y, z);
        }

        public void SetMarker(double x, double y, double z, double toolLen)
        {
            Plot_D.MoveMarker(x, y, z, toolLen); // does this need an invoke???
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
            // get the reference coordinates
            double gx, gy, gz;
            int xref;
            gx = gy = gz = 0;
            xref = 0;

            Plot_D.GetGref(ref xref, ref gx, ref gy, ref gz);
            Plot_D.Clear();
            // Plot_D.ClearTrace();
            Plot_D.SetGref(xref, gx, gy, gz);
        }
    }


}
