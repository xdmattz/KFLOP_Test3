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
// for the JSON stuff
using Newtonsoft.Json;
// for file dialog libraries
using Microsoft.Win32;
using System.IO;
//
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;
// for KMotion
using KMotion_dotNet;

namespace KFLOP_Test3
{
    /// <summary>
    /// Interaction logic for ToolChangerPanel.xaml
    /// </summary>
    public partial class ToolChangerPanel : UserControl
    {

        // global variables for the user control
        static bool SpindleEnabled;
        static bool SpindlePID;
        static bool SpindleHomed;
        static double Spindle_Position;
        static int iSpindle_Status;
        static bool bTC_Clamped;
        static bool bTC_UnClamped;
        static bool bTLAUX_ARM_IN;
        static bool bTLAUX_ARM_OUT;
        static int Carousel_Position;
        static bool bTLAUX_FAULT;
        static int iTLAUX_STATUS;

        // these are used for locking the various stages of the tool changer 
        static readonly object _Tlocker = new object();
        static readonly object _Slocker = new object();
        static readonly object _SPlocker = new object();

        static BackgroundWorker _bw;    // motion background worker
        static BackgroundWorker _bw2;   // process background worker.
        
         // a copy of the KM controller 
        static KM_Controller KMx { get; set; }
        static KM_Axis SPx { get; set; }
        private BitOps B;
        private ToolChangeParams TCP;
        private string CfgFName;
        

        public ToolChangerPanel(ref KM_Controller X, ref KM_Axis SP, string cfgFileName)
        {
            InitializeComponent();
            CfgFName = cfgFileName;

            KMx = X;    // point to the KM controller - this exposes all the KFLOP .net library functions
            SPx = SP;   // point to the Spindle Axis for fine control

            _bw = new BackgroundWorker();
            _bw2 = new BackgroundWorker();
                        
            TCP = new ToolChangeParams();   // get the tool changer parameters
            LoadCfg(cfgFileName);

            B = new BitOps();
            ledClamp.Set_State(LED_State.Off);
            ledClamp.Set_Label("Tool Clamp");

            // disable the abort button
            btnAbort.IsEnabled = false;

            UpdateCfgUI();

        }

        public void TLAUX_Status(ref KM_MainStatus KStat)
        {
            getTLAUX_Status();
            if (bTC_Clamped)
            { ledClamp.Set_State(LED_State.Off); }
            else { ledClamp.Set_State(LED_State.On_Blue); }

            tbTLAUXStatus.Text = string.Format("{0:X4}", iTLAUX_STATUS);

            // get the spindle status from KSTAT
            int PVStatus = KStat.PC_comm[CSConst.P_STATUS];

            SpindleEnabled = B.BitIsSet(PVStatus, PVConst.SB_SPINDLE_ON);
            SpindlePID = B.BitIsSet(PVStatus, PVConst.SB_SPINDLE_PID);
        }

        private void btnGetTool_Click(object sender, RoutedEventArgs e)
        {
            // ensure that the TLAUX ARM is IN
            // is the spindle currently empty?
            MessageBoxResult result = MessageBox.Show("Is the Spindle Empty?", "Spindle Check", MessageBoxButton.YesNo);
            if(result == MessageBoxResult.No)
            {
                return;
            }

            // is the TLAUX ARM in?
            getTLAUX_Status();
            if(bTLAUX_ARM_IN == false)
            { 
                MessageBox.Show("Tool Changer Error!\nArm not retracted!\n(Try Re-Homing TLAUX)");
                return;
            }

            // update the Tool change parameters
            UpdateCfg();    // this should load all the variables into the TCP class
            // start the process background worker for Get Tool
            // move Z to TC_Z1
            _bw2.DoWork += GetToolWorker;
        }

        private void GetToolWorker(object sender, DoWorkEventArgs e)
        {

        }


        private void btnPutTool_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnExchangeTool_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnSP_PID_Click(object sender, RoutedEventArgs e)
        {
            // set the spindle to PID mode. And leave it enabled!
            if(SpindleEnabled == true)
            {
                btnSP_PID.Content = "Enable Spindle";
                // Disable the spindle
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_DIS);
                KMx.ExecuteProgram(2);
                if(KMx.WaitForThreadComplete(2, 2000) == false)
                {
                    MessageBox.Show("Thread 2 stuck!");
                }
            }
            else
            {
                btnSP_PID.Content = "Disable Spindle";
                // enable PID spindle mode and home
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_PID);
                KMx.ExecuteProgram(2);
                if (KMx.WaitForThreadComplete(2, 2000) == false)
                {
                    MessageBox.Show("Thread 2 stuck!");
                }
                // Enable the spindle
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_EN);
                KMx.ExecuteProgram(2);
                if (KMx.WaitForThreadComplete(2, 2000) == false)
                {
                    MessageBox.Show("Thread 2 stuck!");
                }
                // move to zero position
                var SP = KMx.GetAxis(AXConst.SPINDLE_AXIS, "Spindle");
                SP.StartMoveTo(0);
            }
        }

        private void btnTC_H1_Click(object sender, RoutedEventArgs e)
        {

            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += MoveZWorker; // Add the main thread method to call
            _bw.ProgressChanged += MoveZProgressChanged; // add the progress changed method
            _bw.RunWorkerCompleted += MoveZCompleted; // the the method to call when the function is done

            SingleAxis Zx = new SingleAxis();
            double tempZ;
            if (double.TryParse(tbTCH1.Text, out tempZ) == false)
            {
                MessageBox.Show("TC_H1 not valid!");
                return;
            }
            Zx.Pos = tempZ;
            if(double.TryParse(tbTCH1FR.Text, out tempZ) == false)
            {
                MessageBox.Show("TC_H1 Rate not valid");
                return;
            }
            Zx.Rate = tempZ;

            // enable the abort button
            btnAbort.IsEnabled = true;
            _bw.RunWorkerAsync(Zx);
        }

        private void btnTC_H2_Click(object sender, RoutedEventArgs e)
        {
            // this is the same as btnTC_H1 except for the coordinates used
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += MoveZWorker; // Add the main thread method to call
            _bw.ProgressChanged += MoveZProgressChanged; // add the progress changed method
            _bw.RunWorkerCompleted += MoveZCompleted; // the the method to call when the function is done

            SingleAxis Zx = new SingleAxis();
            double tempZ;
            if (double.TryParse(tbTCH2.Text, out tempZ) == false)
            {
                MessageBox.Show("TC_H2 not valid!");
                return;
            }
            Zx.Pos = tempZ;
            if (double.TryParse(tbTCH2FR.Text, out tempZ) == false)
            {
                MessageBox.Show("TC_H2 Rate not valid");
                return;
            }
            Zx.Rate = tempZ;

            // enable the abort button
            btnAbort.IsEnabled = true;
            _bw.RunWorkerAsync(Zx);
        }

        private void btnToolRel_Click(object sender, RoutedEventArgs e)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += TClamp_Worker;
            _bw.ProgressChanged += TClamp_ProgressChanged;
            _bw.RunWorkerCompleted += TClamp_Completed;

            if (bTC_Clamped == true)
            {
                btnToolRel.Content = "Tool Clamp";
                _bw.RunWorkerAsync(false);  // Release the clamp
            }
            else
            {
                btnToolRel.Content = "Tool Release";
                _bw.RunWorkerAsync(true);   // engage the clamp
            }
        }

        private void btnArm_In_Click(object sender, RoutedEventArgs e)
        {
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += TLAUX_ARM_Worker; // Add the main thread method to call
            _bw.ProgressChanged += TLAUX_ARM_ProgressChanged; // add the progress changed method
            _bw.RunWorkerCompleted += TLAUX_ARM_Completed; // the the method to call when the function is done

            if (bTLAUX_ARM_IN == true)
            {
                btnArm_In.Content = "TC ARM IN";
            }
            else
            {
                btnArm_In.Content = "TC ARM OUT";
            }


            _bw.RunWorkerAsync(!bTLAUX_ARM_IN);
        }

        private void btnSpindle_Click(object sender, RoutedEventArgs e)
        {
            // send the spindle to XXXX
            // check that the axis is enabled and in PID mode
            double Spos, Srate;
            if(double.TryParse(tbSPIndex.Text, out Spos) == false)
            {
                MessageBox.Show("Bad Spindle Index");
                return;
            }
            if(double.TryParse(tbSPRate.Text, out Srate) == false)
            {
                MessageBox.Show("Bad Spindle Rate");
                return;
            }
            SingleAxis SX = new SingleAxis();

            SX.Pos = Spos;
            SX.Rate = Srate;

            getSpindle_Status();
            if (SpindleEnabled && SpindlePID)
            { 
                if (_bw.IsBusy) { _bw.CancelAsync(); }
                // start a new background worker with move to H1
                _bw.WorkerReportsProgress = true;
                _bw.WorkerSupportsCancellation = true;

                _bw.DoWork += Spindle_Worker;
                _bw.ProgressChanged += Spindle_ProgressChanged;
                _bw.RunWorkerCompleted += Spindle_Completed;

                _bw.RunWorkerAsync(SX);
            }
            else
            {
                MessageBox.Show("Spindle not Enabled!");
            }
        }

        private void btnToolSel_Click(object sender, RoutedEventArgs e)
        {
            // get the tool number from the text box
            int ToolNumber;
            if(int.TryParse(tbSlotNumber.Text, out ToolNumber) == false)
            {
                tbSlotNumber.Text = "1";
                MessageBox.Show("Invalid tool number - Reset");
                return;
            }
            // send the command to KFLOP
            if ((ToolNumber > 0) && (ToolNumber <= 8))
            {

                // start a new background worker with move to H1
                _bw.WorkerReportsProgress = true;
                _bw.WorkerSupportsCancellation = true;

                _bw.DoWork += Carousel_Worker;
                _bw.ProgressChanged += Carousel_ProgressChanged;
                _bw.RunWorkerCompleted += Carousel_Completed;

                _bw.RunWorkerAsync(ToolNumber);
            }
        }

        public void LoadCfg(string LFileName)
        {
            // load the tool changer files
            try
            {
                if (System.IO.File.Exists(LFileName) == true)
                {
                    JsonSerializer Jser = new JsonSerializer();
                    StreamReader sr = new StreamReader(LFileName);
                    JsonReader Jreader = new JsonTextReader(sr);
                    TCP = Jser.Deserialize<ToolChangeParams>(Jreader);
                    sr.Close();
                }
            }
            catch
            {
                MessageBox.Show(LFileName, "TLAUX Parameters Load Error!");
            }
        }

        private void UpdateCfgUI()
        {
            //// populate the table with the parameter values
            // had to check for null because it crashes if the file isn't present.
            if (TCP != null)
            {
                tbTCH1.Text = TCP.TC_H1_Z.ToString();
                tbTCH1FR.Text = TCP.TC_H1_FR.ToString();
                tbTCH2.Text = TCP.TC_H2_Z.ToString();
                tbTCH2FR.Text = TCP.TC_H2_FR.ToString();
                tbSPIndex.Text = TCP.TC_Index.ToString();
                tbSPRate.Text = TCP.TC_S_FR.ToString();

                tbTSX.Text = TCP.TS_X.ToString();
                tbTSY.Text = TCP.TS_Y.ToString();
                tbTSZ.Text = TCP.TS_Z.ToString();
                tbTSIndex.Text = TCP.TS_S.ToString();

                tbTSRate1.Text = TCP.TS_FR1.ToString();
                tbTSRate2.Text = TCP.TS_FR2.ToString();
            }
        }

        private void UpdateCfg()
        {
            // get the variables into TCP

            double temp;
            if (double.TryParse(tbTCH1.Text, out temp))
            { TCP.TC_H1_Z = temp; }
            if (double.TryParse(tbTCH1FR.Text, out temp))
            { TCP.TC_H1_FR = temp; }
            if (double.TryParse(tbTCH2.Text, out temp))
            { TCP.TC_H2_Z = temp; }
            if (double.TryParse(tbTCH2FR.Text, out temp))
            { TCP.TC_H2_FR = temp; }
            if (double.TryParse(tbSPIndex.Text, out temp))
            { TCP.TC_Index = temp; }
            if (double.TryParse(tbSPRate.Text, out temp))
            { TCP.TC_S_FR = temp; }

            if (double.TryParse(tbTSX.Text, out temp))
            { TCP.TS_X = temp; }
            if (double.TryParse(tbTSY.Text, out temp))
            { TCP.TS_Y = temp; }
            if (double.TryParse(tbTSZ.Text, out temp))
            { TCP.TS_Z = temp; }
            if (double.TryParse(tbTSIndex.Text, out temp))
            { TCP.TS_S = temp; }
            if (double.TryParse(tbTSRate1.Text, out temp))
            { TCP.TS_FR1 = temp; }
            if (double.TryParse(tbTSRate2.Text, out temp))
            { TCP.TS_FR2 = temp; }

        }

        private void SaveCfg(string FName)
        {


            try
            {
                var SaveFile = new SaveFileDialog();
                SaveFile.FileName = FName;
                if (SaveFile.ShowDialog() == true)
                {
                    JsonSerializer Jser = new JsonSerializer();
                    StreamWriter sw = new StreamWriter(SaveFile.FileName);
                    JsonTextWriter Jwrite = new JsonTextWriter(sw);
                    Jser.NullValueHandling = NullValueHandling.Ignore;
                    Jser.Formatting = Newtonsoft.Json.Formatting.Indented;
                    Jser.Serialize(Jwrite, TCP);
                    sw.Close();
                }
            }
            catch
            {
                MessageBox.Show(FName, "TLAUX Parameters Save Error!");
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {

            UpdateCfg();
            SaveCfg(CfgFName);
        }

        #region Status methods 
        private void getTLAUX_Status()
        {
            lock (_Tlocker)  // lock this so only one thread can access it at a time.
            {
                // do I need to lock this? 
                // get the TLAUX status and set the  apropriate flags
                int TStatus = KMx.GetUserData(PVConst.P_TLAUX_STATUS);
                iTLAUX_STATUS = TStatus;
                bTLAUX_ARM_IN = B.BitIsSet(TStatus, PVConst.TLAUX_ARM_IN);
                bTLAUX_ARM_OUT = B.BitIsSet(TStatus, PVConst.TLAUX_ARM_OUT);
                bTC_Clamped = B.BitIsSet(TStatus, PVConst.TLAUX_CLAMP);
                bTC_UnClamped = B.BitIsSet(TStatus, PVConst.TLAUX_UNCLAMP);
                Carousel_Position = TStatus & PVConst.TLAUX_TOOL_MASK;
                bTLAUX_FAULT = B.AnyInMask(TStatus, PVConst.TLAUX_ERROR_MASK);
            }
        }

        private void getSpindle_Status()
        {
            lock(_Slocker)
            {
                Spindle_Position = SPx.GetActualPositionCounts();
                iSpindle_Status = KMx.GetUserData(PVConst.P_STATUS_REPORT);
                SpindleHomed = !B.BitIsSet(iSpindle_Status, PVConst.SB_SPIN_HOME);  // this inverts the logic of the bit in the status word. so true = homed
                SpindleEnabled = B.BitIsSet(iSpindle_Status, PVConst.SB_SPINDLE_ON);
                SpindlePID = B.BitIsSet(iSpindle_Status, PVConst.SB_SPINDLE_PID);
            }
        }

        #endregion

        #region Individual tool changer motions

        // enable and align the spindle to the tool change position. 
        private bool AlignSpindle(double pos, double rate)
        {
            lock (_SPlocker)
            {
                int timeoutCnt = 0;
                // if the spindle has not been homed then home it.
                // if the spindle index > 10000 or < -10000 (+/- 5 turns) then re-home it
                getSpindle_Status();

                if ((SpindleHomed == false)   // the spindle has not been homed.
                    || (Spindle_Position > 10000)                       // or position is far high
                    || (Spindle_Position < -10000))                     // or position is far low
                {
                    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_HOME_SPINDLE);
                    KMx.ExecuteProgram(2);
                    // wait until it is done.
                    do
                    {
                        Thread.Sleep(100);
                        if (timeoutCnt++ > 50) { return false; }
                    } while (SPx.MotionComplete() != true); // I think this will indicate a completed motion.
                }
                getSpindle_Status();
                // if the spindle is not enabled then enable it
                if (SpindlePID == false)
                {
                    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_PID);
                    KMx.ExecuteProgram(2);
                    Thread.Sleep(50);
                }
                getSpindle_Status();
                if (SpindleEnabled == false)
                {
                    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_EN);
                    KMx.ExecuteProgram(2);
                    Thread.Sleep(20);
                }
                SPx.Velocity = rate;
                SPx.StartMoveTo(pos);
                // done
                // wait until it is done.
                timeoutCnt = 0;
                do
                {
                    Thread.Sleep(100);
                    if (timeoutCnt++ > 30) { return false; }
                } while (SPx.MotionComplete() != true);

                // note - this leaves the spindle enabled!
                // All Done!
                return true;
            }
        }

        private void SpindleDisable()
        {
            KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_DIS);
            KMx.ExecuteProgram(2);
        }


        // the move Z background process
        #region MoveZ background process
        static void MoveZWorker(object sender, DoWorkEventArgs e)
        {
            SingleAxis SAx = (SingleAxis)e.Argument;    // this gets the 

            //  // lets try this with StartMoveTo...
            //  var Zxs = KMx.GetAxis(AXConst.Z_AXIS, "Z-Axis");
            //  // get the CPU - counts per unit from the interpreter.
            //  Zxs.CPU = KMx.CoordMotion.MotionParams.CountsPerInchZ;
            ////  Zxs.Acceleration = KMx.CoordMotion.MotionParams.MaxAccelZ;
            //  double CRate = SAx.Rate * Zxs.CPU;
            //  Zxs.Velocity = CRate;
            //  Zxs.StartMoveTo(SAx.Pos);

            // check that the axis is enabled
            int ax, ay, az, aa, ab, ac;
            ax = ay = az = aa = ab = ac = 0;
            KMx.CoordMotion.GetAxisDefinitions(ref ax, ref ay, ref az, ref aa, ref ab, ref ac);
            if(az == -1)
            {
                e.Result = "Axis not enabled";
                return;
            }

            double cX, cY, cZ, cA, cB, cC;
            cX = cY = cZ = cA = cB = cC = 0;


            KMx.CoordMotion.ReadAndSyncCurPositions(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC);
            // may want to change the last two variables to pass information to the 3D path generation

            KMx.CoordMotion.StraightFeed(SAx.Rate, cX, cY, SAx.Pos, cA, cB, cC, 0, 0);
            KMx.CoordMotion.FlushSegments();    // push the segments out the buffer and into the KFLOP
            KMx.CoordMotion.WaitForSegmentsFinished(false); // - this works, but blocks the calling thread - so how to check if it is done?
            e.Result += "Motion done";

        }

        private void MoveZProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            
        }

        private void MoveZCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show(e.Result.ToString());
            _bw.DoWork -= MoveZWorker;
            _bw.ProgressChanged -= MoveZProgressChanged;
            _bw.RunWorkerCompleted -= MoveZCompleted;
            btnAbort.IsEnabled = false; // disable the abort button
        }

       // the abort button.
        private void btnAbort_Click(object sender, RoutedEventArgs e)
        {
            // if the background worker is running then abort movement and stop it
            if (_bw.IsBusy)
            {
                // KMx.CoordMotion.Abort(); // stop the motion! - maybe try a halt here instead?
                KMx.CoordMotion.Halt(); //  Halt seems to stop quicker than Abort does.
                MessageBox.Show("Halted");
                KMx.CoordMotion.ClearHalt();
                _bw.CancelAsync();  // stop the background worker.
                KMx.CoordMotion.ClearAbort(); // but Halt requies ClearAbort inorder to resume.
                // MessageBox.Show("Abort Pressed");
            }
        }

        #endregion

        // Rotate Carousel background process
        private void Carousel_Worker(object sender, DoWorkEventArgs e)
        {
            int car_num = (int)e.Argument;
            getTLAUX_Status();
            if(Carousel_Position == car_num)
            {
                e.Result = "Done";
                return;
            }
            KMx.SetUserData(PVConst.P_NOTIFY, (T2Const.T2_SEL_TOOL | car_num));
            KMx.ExecuteProgram(2);
            int timeoutCnt = 0;
            do
            {
                Thread.Sleep(100);
                getTLAUX_Status();
                if (timeoutCnt++ > 50)
                {
                    e.Result = "Timeout";
                    return;
                }
            } while (Carousel_Position != car_num);
            e.Result = "Done";
            return;
        }
        private void Carousel_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void Carousel_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw.DoWork -= Carousel_Worker;
            _bw.ProgressChanged -= Carousel_ProgressChanged;
            _bw.RunWorkerCompleted -= Carousel_Completed;
            MessageBox.Show(e.Result.ToString());
        }

        // Index Spindle background process
        private void Spindle_Worker(object sender, DoWorkEventArgs e)
        {
            SingleAxis SAs = (SingleAxis)e.Argument;

//            lock (_SPlocker)
//            {
                int timeoutCnt = 0;
                // if the spindle has not been homed then home it.
                // if the spindle index > 10000 or < -10000 (+/- 5 turns) then re-home it
                getSpindle_Status();

                if ((SpindleHomed == false)   // the spindle has not been homed.
                    || (Spindle_Position > 10000)                       // or position is far high
                    || (Spindle_Position < -10000))                     // or position is far low
                {
                    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_HOME_SPINDLE);
                    KMx.ExecuteProgram(2);
                    // wait until it is done.
                    do
                    {
                        Thread.Sleep(100);
                        if (timeoutCnt++ > 50)
                    {
                        e.Result = "Timeout";
                        return;
                    }
                    } while (SPx.MotionComplete() != true); // I think this will indicate a completed motion.
                }
                getSpindle_Status();
                // if the spindle is not enabled then enable it
                if (SpindlePID == false)
                {
                    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_PID);
                    KMx.ExecuteProgram(2);
                    Thread.Sleep(50);
                }
                getSpindle_Status();
                if (SpindleEnabled == false)
                {
                    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_EN);
                    KMx.ExecuteProgram(2);
                    Thread.Sleep(20);
                }
                SPx.Velocity = SAs.Rate;
                SPx.StartMoveTo(SAs.Rate);
                timeoutCnt = 0;
                do {    // Wait until done or timeout
                    Thread.Sleep(100);
                    if (timeoutCnt++ > 50)
                    {
                        e.Result = "Timeout";
                        return;
                    }
                } while (SPx.MotionComplete() != true);

            // note - this leaves the spindle enabled!
            // All Done!
            e.Result = "Done";
                return;
//            }
        }
        
        private void Spindle_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void Spindle_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw.DoWork -= Spindle_Worker;
            _bw.ProgressChanged -= Spindle_ProgressChanged;
            _bw.RunWorkerCompleted -= Spindle_Completed;
        }

        // TC Arm In/Out background process
        private void TLAUX_ARM_Worker(object sender, DoWorkEventArgs e)
        {
            // get the TLAUX Status
            getTLAUX_Status();
            if(bTLAUX_FAULT)
            {
                e.Result = "TLAUX FAULT!";
                return;
            }
            int TimeoutCnt = 0;
            if ((bool)e.Argument)    // true is arm in, false is arm out
            {
                if (bTLAUX_ARM_IN)  // if the arm in then already done
                {
                    e.Result = "Done";
                    return;
                }
                // send the command to move the arm in
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_ARM_IN);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(100);
                    getTLAUX_Status();
                    if (TimeoutCnt++ > 30) // about 3 seconds. 
                    {
                        e.Result = "Timeout";
                        return;
                    }
                } while (bTLAUX_ARM_IN == false);
            } else
            {
                if (bTLAUX_ARM_OUT)  // if the arm in then already done
                {
                    e.Result = "Done";
                    return;
                }

                // send the command to move the arm in
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_ARM_OUT);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(100);
                    getTLAUX_Status();
                    if (TimeoutCnt++ > 30) // about 3 seconds. 
                    {
                        e.Result = "Timeout";
                        return;
                    }
                } while (bTLAUX_ARM_OUT == false);
            }
            e.Result = "Done";
        }

        private void TLAUX_ARM_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void TLAUX_ARM_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show(e.Result.ToString());
            _bw.DoWork -= TLAUX_ARM_Worker;
            _bw.ProgressChanged -= TLAUX_ARM_ProgressChanged;
            _bw.RunWorkerCompleted -= TLAUX_ARM_Completed;
        }

        // Tool Clamp background process
        private void TClamp_Worker(object sender, DoWorkEventArgs e)
        {
            int timeoutCnt = 0;

            getTLAUX_Status();  // start by getting the latest TLAUX Status
            if((bool)e.Argument) // true = engage clamp, false = release clamp
            {
                if(bTC_Clamped)
                {
                    e.Result = "Done";
                    return;
                }
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_GRAB);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(100);
                    getTLAUX_Status();
                    if(timeoutCnt++ > 30)
                    {
                        e.Result = "Timeout";
                        return;
                    }
                } while (bTC_Clamped == false);
            }
            else
            {
                if(bTC_UnClamped)
                {
                    e.Result = "Done";
                    return;
                }
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_RELA);    // release with a burst of air
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(100);
                    getTLAUX_Status();
                    if (timeoutCnt++ > 30)
                    {
                        e.Result = "Timeout";
                        return;
                    }
                } while (bTC_UnClamped == false);
            }
            e.Result = "Done";
            return;
        }
        private void TClamp_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }
        private void TClamp_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw.DoWork -= TClamp_Worker;
            _bw.ProgressChanged -= TClamp_ProgressChanged;
            _bw.RunWorkerCompleted -= TClamp_Completed;

            MessageBox.Show(e.Result.ToString());
        }

        // these methods need to be run in a different thread if the UI thread is going to be updated while they are running
        // need to figure out how to synch the thread so a tool change happens efficiently 
        private bool TLAUX_Arm_In()
        {
            KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_ARM_IN);
            KMx.ExecuteProgram(2);
            // wait till arm is in!
            Stopwatch WaitTimer = new Stopwatch();
            WaitTimer.Restart();
            while(true)
            {
                if(WaitTimer.ElapsedMilliseconds > Timeout.T3Sec)
                {
                    return false;
                }
                if (bTLAUX_ARM_IN) break; // TLAUX_ARM_IN is updated in TLAUX_Status - which is called by the UI updater
                Thread.Sleep(20);
            }
            WaitTimer.Stop();
            return true;
        }

        private bool TLAUX_Arm_Out()
        {
            KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_ARM_OUT);
            KMx.ExecuteProgram(2);
            // wait till arm is in!
            Stopwatch WaitTimer = new Stopwatch();
            WaitTimer.Restart();
            while (true)
            {
                if (WaitTimer.ElapsedMilliseconds > Timeout.T3Sec)
                {
                    return false;
                }
                if (bTLAUX_ARM_OUT) break; // TLAUX_ARM_IN is updated in TLAUX_Status - which is called by the UI updater
                Thread.Sleep(20);
            }
            WaitTimer.Stop();
            return true;
        }

        #endregion

        
    }

    class SingleAxis
    {
        public double Pos { get; set; }
        public double Rate { get; set; }
    }

}
