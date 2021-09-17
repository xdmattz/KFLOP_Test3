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
    public enum ProbeResult
    {
        Idle,
        Probing,
        SoftTimeOut,
        MachineTimeOut,
        Detected,
        T2_ProbeError
    }

    public struct MachineCoordinates
    {
        
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
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
        BitOps BTest;

        #endregion

        public ProbePanel(ref KM_Controller X)
        {
            InitializeComponent();

            KMx = X;

            _pbw = new BackgroundWorker();

            ProbeState = new ProbeResult();
            ProbeState = ProbeResult.Idle;
            BTest = new BitOps();

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
                if (BTest.AnyInMask(ProbeStatus, unchecked((int)PVConst.SB_PROBE_STATUS_MASK)))  // only check the probe status bits
                {
                    if(BTest.BitIsSet(ProbeStatus, PVConst.SB_PROBE_DETECT))
                    { ProbeState = ProbeResult.Detected; }
                    else if (BTest.BitIsSet(ProbeStatus, PVConst.SB_PROBE_TIMEOUT))
                    { ProbeState = ProbeResult.MachineTimeOut; }
                }
                
                return true;
            }
            return false;
        }


    }
}
