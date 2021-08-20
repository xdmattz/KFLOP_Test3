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
    /// <summary>
    /// Interaction logic for ProbePanel.xaml
    /// </summary>
    public partial class ProbePanel : UserControl
    {
        #region Global Variables

        // a copy of the KM controller 
        private KM_Controller KMx { get; set; }

        static BackgroundWorker _pbw;    // Probing background worker

        #endregion

        public ProbePanel(ref KM_Controller X)
        {
            InitializeComponent();

            KMx = X;

            _pbw = new BackgroundWorker();

        }

        private void Probe_Xm_Yp_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Probe_Yp_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Probe_Xp_Yp_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Probe_Xm_Click(object sender, RoutedEventArgs e)
        {

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

        }

        private void Probe_Xm_Ym_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Probe_Ym_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Probe_Xp_Ym_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Probe_Inside_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Probe_Outside_Click(object sender, RoutedEventArgs e)
        {

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
            _pbw.WorkerReportsProgress = true;
            _pbw.WorkerSupportsCancellation = true;

            _pbw.DoWork += Probe_Worker; // Add the main thread method to call
            _pbw.ProgressChanged += Probe_ProgressChanged; // add the progress changed method
            _pbw.RunWorkerCompleted += Probe_Completed; // the the method to call when the function is done
            _pbw.RunWorkerAsync();
        }

        private void Probe_Worker(object sender, DoWorkEventArgs e)
        {
            double MotionRate = 2.0;    // inch per min
            double mrZ;
            double ProbeDistance = 1.5; // probe distance  in inches
            mrZ = (MotionRate * KMx.CoordMotion.MotionParams.CountsPerInchZ) / 60.0; // motion rate(in/min) * (counts/inch) / (60sec/ min)

            double PTimeoutMs;
            // timeout is (distance/rate) * (ms/min)
            PTimeoutMs = (ProbeDistance / MotionRate) * 60000.0;    // convert to ms for timeout  -
            // this is a simplification because it doesn't account for acceleration and deceleration times, but it is a start.

            // set the Persist Variables
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT1, (float)mrZ);
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT2, (float)0.0);
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT3, (float)0.0);
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT4, (float)PTimeoutMs);

            // execute program 2  to probe
            KMx.SetUserData(PVConst.P_NOTIFY, (T2Const.T2_PROBE_Z));    // probe Z
            KMx.ExecuteProgram(2);
            int timeoutCnt = 0;

            Int32 pTimeout2;
            pTimeout2 = (Int32)((1.2 * PTimeoutMs) / 100); 

            do
            {
                Thread.Sleep(100);
                // get the completion status

                if (timeoutCnt++ > pTimeout2)
                {
                    // Get to here if it takes too long to get a probe result
                  //  BWRes.Result = false;
                  //  BWRes.Comment = "Carousel Timeout";
                  //  e.Result = BWRes;
                    return;
                }
            } while (CheckForProbeComplete() != true);
            // look at the probe results to determine if what to do next

        }

        private void Probe_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

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
            // get the persist variables the indicate the probing has finished.
            int ProbeStatus = KMx.GetUserData(PVConst.P_STATUS);
            if ((ProbeStatus & PVConst.SB_PROBE_STATUS_MASK) != 0)  // only check the probe status bits
            {
                return true;
            }
            return false;
        }


    }
}
