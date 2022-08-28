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
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Threading;
using System.ComponentModel;

using KMotion_dotNet;
namespace KFLOP_Test3
{
    // possible probing results
    public enum ProbeResult : int
    {
        Idle,
        Probing,
        SoftTimeOut,
        MachineTimeOut,
        Detected,
        T2_ProbeError
    }




    /// <summary>
    /// Interaction logic for ProbePanel.xaml
    /// </summary>
    public partial class ProbePanel : UserControl
    {
        #region Global Variables

        // a copy of the KM controller 
        private KM_Controller KMx { get; set; }

        static BackgroundWorker _pbw;    // Probing background worker
        ProbeResult ProbeState;

        #endregion

        public ProbePanel(ref KM_Controller X)
        {
            InitializeComponent();

            KMx = X;

            _pbw = new BackgroundWorker();

            ProbeState = new ProbeResult();
            ProbeState = ProbeResult.Idle;

           // make a new button
            ProbeButton singlesurfaceBtn = new ProbeButton(new BitmapImage(new Uri("SingleSurface.png", UriKind.Relative)), "Single Surface");
            // Add an image to the button
            // singlesurfaceBtn.ButtonImage = new BitmapImage(new Uri("SingleSurface.png"));
            // add text to the button
            // singlesurfaceBtn.ButtonText = "Single Surface";
            // add the Button to the Probe Panel
            ProbeButtonPanel.Children.Add(singlesurfaceBtn);

            ProbeButton Btn2 = new ProbeButton(null, "Second Button");
            //Btn2.ButtonText = "Second Button";
            ProbeButtonPanel.Children.Add(Btn2);


        }

        public MachineCoordinates GetCoordinates()
        {
            MachineCoordinates MC = new MachineCoordinates();
            double x, y, z, a, b, c;
            x = y = z = a = b = c = 0.0;
            KMx.CoordMotion.UpdateCurrentPositionsABS(ref x, ref y, ref z, ref a, ref b, ref c, true);

            MC.X = x;
            MC.Y = y;
            MC.Z = z;
            MC.A = a;
            MC.B = b;
            MC.C = c;

            return MC;
        }

        private void Probe_Xm_Yp_Click(object sender, RoutedEventArgs e)
        {
            if (LocationCheck())
            {

            }
        }

        private void Probe_Yp_Click(object sender, RoutedEventArgs e)
        {
            if (LocationCheck())
            {

            }

        }

        private void Probe_Xp_Yp_Click(object sender, RoutedEventArgs e)
        {
            if(LocationCheck())
            {
                
            }

        }

        private void Probe_Xm_Click(object sender, RoutedEventArgs e)
        {
            if (LocationCheck())
            {

            }
        }

        private void Probe_Z_Click(object sender, RoutedEventArgs e)
        {
            if (LocationCheck())
            {
                // start probe in the Z direction.
                // get the direction speed and timeout value
                // send the command
                // start a new background worker that looks for the results or the timeout
                StartProbeProcess();
            }
            else
            {
                // probe operation is canceled
            }

        }

        private void Probe_Xp_Click(object sender, RoutedEventArgs e)
        {
            if (LocationCheck())
            {

            }
        }

        private void Probe_Xm_Ym_Click(object sender, RoutedEventArgs e)
        {
            if (LocationCheck())
            {

            }
        }

        private void Probe_Ym_Click(object sender, RoutedEventArgs e)
        {
            if (LocationCheck())
            {

            }
        }

        private void Probe_Xp_Ym_Click(object sender, RoutedEventArgs e)
        {
            if (LocationCheck())
            {

            }
        }

        private void Probe_Inside_Click(object sender, RoutedEventArgs e)
        {
            if (LocationCheck())
            {

            }
        }

        private void Probe_Outside_Click(object sender, RoutedEventArgs e)
        {
            if (LocationCheck())
            {

            }
        }

        private bool LocationCheck()
        {
            MessageBoxResult result = MessageBox.Show("Is the probe in position?\nJog to the start position and hit OK", "", MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                return true;
            }
            return false;
        }

        private void StartProbeProcess()
        {
            double MotionRate = -15.0;    // inch per min
            double mrZ;
            double ProbeDistance = 1.5; // probe distance  in inches - this should take about 
            mrZ = (MotionRate * KMx.CoordMotion.MotionParams.CountsPerInchZ) / 60.0; // motion rate(in/min) * (counts/inch) / (60sec/ min)

            double PTimeout;
            // timeout is (distance/rate) * (ms/min)
            PTimeout = (ProbeDistance / Math.Abs(MotionRate)) * 60.0;    // convert to seconds for timeout  -
            // this is a simplification because it doesn't account for acceleration and deceleration times, but it is a start.

            // set the Persist Variables
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT1, (float)mrZ);
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT2, (float)0.0);
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT3, (float)0.0);
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT4, (float)PTimeout);

            // execute program 2  to probe
            KMx.SetUserData(PVConst.P_NOTIFY, (T2Const.T2_PROBE_Z));    // probe Z
            KMx.ExecuteProgram(2);

            _pbw.WorkerReportsProgress = true;
            _pbw.WorkerSupportsCancellation = true;

            _pbw.DoWork += Probe_Worker; // Add the main thread method to call
            _pbw.ProgressChanged += Probe_ProgressChanged; // add the progress changed method
            _pbw.RunWorkerCompleted += Probe_Completed; // the the method to call when the function is done
            _pbw.RunWorkerAsync(PTimeout);
        }

        private void Probe_Worker(object sender, DoWorkEventArgs e)
        {
            double TimeOut = (double)e.Argument; // the delay time for the probing operation
            int Tdone = (int)(TimeOut * 10.0 * 1.1);  // convert the time to sleep counts with 10% extra time over machine delay
            int SleepCnt = 0;

            ProbeState = ProbeResult.Probing;
            do
            {
                Thread.Sleep(100);
                // get the completion status
                if (SleepCnt++ > Tdone)
                {
                    ProbeState = ProbeResult.SoftTimeOut;
                    MessageBox.Show("Probe Timeout");
                    return;
                }
            } while (CheckForProbeComplete() != true);
            // look at the probe results to determine what to do next
            string Pmsg = "Done";
            MachineCoordinates MCx = GetCoordinates();
            switch(ProbeState)
            {
                case ProbeResult.Detected:
                    {
                        // get the machine coordinates.
                        Pmsg = String.Format("Probe Detect at\n X:{0}\nY:{1}\nZ{2}", MCx.X, MCx.Y, MCx.Z);
                        break;
                    }
                case ProbeResult.MachineTimeOut:
                    {
                        Pmsg = String.Format("Machine timeout. Currently at\n X:{0}\nY:{1}\nZ{2}", MCx.X, MCx.Y, MCx.Z);
                        break;
                    }
                case ProbeResult.T2_ProbeError:
                    {
                        Pmsg = String.Format("Probe Error\nMachine at\n X:{0}\nY:{1}\nZ{2}", MCx.X, MCx.Y, MCx.Z);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
            Pmsg += String.Format("\nProbe State = {0}", ProbeState);
            MessageBox.Show(Pmsg);

        }

        private void Probe_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // nothing in here right now...
        }

        private void Probe_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            // remove the methods from background worker
            _pbw.DoWork -= Probe_Worker;
            _pbw.ProgressChanged -= Probe_ProgressChanged;
            _pbw.RunWorkerCompleted -= Probe_Completed;
        }


        private bool CheckForProbeComplete()
        {
            // is thread 2 still running?
            if(KMx.ThreadExecuting(2) == false) // thread 2 has finished running
            {
                ProbeState = ProbeResult.T2_ProbeError;
                // get the persist variables the indicate the probing has finished.
                int ProbeStatus = KMx.GetUserData(PVConst.P_STATUS);
                if (BitOps.AnyInMask(ProbeStatus, unchecked((int)PVConst.SB_PROBE_STATUS_MASK)))  // only check the probe status bits
                {
                    if(BitOps.BitIsSet(ProbeStatus, PVConst.SB_PROBE_DETECT))
                    { ProbeState = ProbeResult.Detected; }
                    else if (BitOps.BitIsSet(ProbeStatus, PVConst.SB_PROBE_TIMEOUT))
                    { ProbeState = ProbeResult.MachineTimeOut; }
                }
                
                return true;
            }
            return false;
        }


    }

    // all the possible probing M Codes
    public enum ProbeCycleNumber : int
    {
        // from Renishaw chapter 2 - cycle summary
        M1 = 1, // Single Surface
        M2 = 2, // Bore
        M3 = 3, // Boss
        M4 = 4, // Pocket
        M5 = 5, // Web
        M6 = 6, // Internal Corner
        M7 = 7, // External Corner
        M8 = 8, // Line
        M9 = 9, // 3-Point Plane
        M10 = 10, // 5-Point Rectangle - Internal
        M11 = 11, // 5-Point Rectangle - External
        M12 = 12, // 3-Point Bore
        M13 = 13, // 3-Point Boss
        M14 = 14, // 3D Corner
        M15 = 15, // Rotary Axis Update
        // from Renishaw manual chapter 1 - cycle summary
        M100 = 100, // Spindle Probe Check
        M101 = 101, // Spindle Probe Calibration
        M102 = 102, // Spindle Probe Calibration - ring gauge
        M103 = 103, // Spindle Probe Length Calibration
        M104 = 104, // Spindle Probe Standard Length Calibration
        M105 = 105, // Spindle Probe Calibration - Sphere
        M110 = 110 // SupaTouch Optimisation

    }

    // work coordinate system to update
    // this is the same as the KFLOP Origin Index in the interpreter setup parameters
    public enum WCS : int
    {
        S54,
        S55,
        S56,
        S57,
        S58,
        S59,
        S101,
        S148        
    }

    // single line command components
    public class RenProbeCommands
    {
        public int MCode { get; set; }  // Cycle number ie. M101 etc. - may have to change this to an enum...
        public int C { get; set; } // End of Cycle Option C0 or C1
        public double X { get; set; }   // position relative to the active WCS
        public double Y { get; set; }
        public double Z { get; set; }
        public int A { get; set; }   // probe direction
        public double D { get; set; }   // Primary Feature Size
        public double E { get; set; }   // Secondary Feature Size
        public double I { get; set; }   // feature location relative the active WCS
        public double J { get; set; }
        public double K { get; set; }
        
        public double W { get; set; }   // Depth - always negative 
        public double S { get; set; }   // work offset
    }
}
