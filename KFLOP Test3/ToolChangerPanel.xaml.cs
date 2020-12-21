// #define TESTBENCH  // defining this will allow operation on the testbench

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
        static bool SpindleRPM;
        static bool SpindleHomed;
        static double Spindle_Position;
        static int iPVStatus;
        static bool bTC_Clamped;
        static bool bTC_UnClamped;
        static bool bTLAUX_ARM_IN;
        static bool bTLAUX_ARM_OUT;
        static int Carousel_Position;
        static bool bTLAUX_FAULT;
        static int iTLAUX_STATUS;

        // these static variable flags indicate tool change progress
        static bool ToolChangeStatus; // true indicates everything OK false indcates an fault occured
        static bool TCProgress; // true indicaes a process step is in progress, false indicates the process has finished. 

        const bool bARM_IN = true;
        const bool bARM_OUT = false;
        const bool bCLAMP = true;
        const bool bRELEASE = false;

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
        static BWResults BWRes;
        

        public ToolChangerPanel(ref KM_Controller X, ref KM_Axis SP, string cfgFileName)
        {
            InitializeComponent();
            CfgFName = cfgFileName;

            KMx = X;    // point to the KM controller - this exposes all the KFLOP .net library functions
            SPx = SP;   // point to the Spindle Axis for fine control

            _bw = new BackgroundWorker();
            _bw2 = new BackgroundWorker();

            BWRes = new BWResults();
                        
            TCP = new ToolChangeParams();   // get the tool changer parameters
            LoadCfg(cfgFileName);

            B = new BitOps();
            ledClamp.Set_State(LED_State.Off);
            ledClamp.Set_Label("Tool Clamp");

            // disable the abort button
            btnAbort.IsEnabled = false;

            UpdateCfgUI();

            LED_SPEN.Set_Label("Spindle EN");
            LED_SPEN.Set_State(LED_State.Off);

        }

        public void TLAUX_Status(ref KM_MainStatus KStat)
        {
            getTLAUX_Status();
            if (bTC_Clamped)
            { ledClamp.Set_State(LED_State.Off); }
            else { ledClamp.Set_State(LED_State.On_Blue); }

            tbTLAUXStatus.Text = string.Format("{0:X4}", iTLAUX_STATUS);

            // get the spindle status from KSTAT
            iPVStatus = KStat.PC_comm[CSConst.P_STATUS];

            SpindleEnabled = B.BitIsSet(iPVStatus, PVConst.SB_SPINDLE_ON);
            SpindlePID = B.BitIsSet(iPVStatus, PVConst.SB_SPINDLE_PID);
            if(SpindleEnabled) { LED_SPEN.Set_State(LED_State.On_Green); }
            else { LED_SPEN.Set_State(LED_State.Off); }
            SpindleRPM = B.BitIsSet(iPVStatus, PVConst.SB_SPINDLE_RPM);
            SpindleHomed = !(B.BitIsSet(iPVStatus, PVConst.SB_SPIN_HOME));
        }

        private void btnGetTool_Click(object sender, RoutedEventArgs e)
        {
            // get a tool from the carousel
            // 
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
            // get the tool number
            int ToolNumber;
            if (int.TryParse(tbSlotNumber.Text, out ToolNumber) == false)
            {
                tbSlotNumber.Text = "1";
                MessageBox.Show("Invalid tool number - Reset");
                return;
            }

            // update the Tool change parameters
            UpdateCfg();    // this should load all the variables into the TCP class
            // start the process background worker for Get Tool
            // move Z to TC_Z1
            Start_GetTool(ToolNumber);
        }

        private void btnPutTool_Click(object sender, RoutedEventArgs e)
        {
            // put a tool from the spindle into the tool carousel
            // insure that the ARM is in, the tool is in the spindle and the slot where it goes is empty
            // first get the slot number from the UI
            int ToolNumber;
            if (int.TryParse(tbSlotNumber.Text, out ToolNumber) == false)
            {
                tbSlotNumber.Text = "1";
                MessageBox.Show("Invalid tool number - Reset");
                return;
            }

            MessageBoxResult result = MessageBox.Show("Is carousel slot #" + ToolNumber + " Empty?" , "Spindle Check", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                return;
            }
            UpdateCfg();
            Start_PutTool(ToolNumber);
        }

        private void btnExchangeTool_Click(object sender, RoutedEventArgs e)
        {

        }


#region Get a Tool from the Tool Changer
        private void Start_GetTool(int ToolNumber)
        {
            _bw2.WorkerReportsProgress = true;
            _bw2.DoWork += GetToolWorker;
            _bw2.ProgressChanged += GetToolProgressedChanged;
            _bw2.RunWorkerCompleted += GetToolCompleted;
            _bw2.RunWorkerAsync(ToolNumber);

        }

        private void GetToolWorker(object sender, DoWorkEventArgs e)
        {
            // Assume the following state:
            // 1. Spindle is empty
            // 2. The tool arm is retracted.
            // tool number to get is passed in the argument
            SingleAxis tSAx = new SingleAxis();
            // start a new background worker
            if (_bw.IsBusy) // if the BW worker is busy
            { return; }
            TCProgress = true;
            ToolChangeStatus = false;
            int progress_cnt = 0;

            _bw2.ReportProgress(progress_cnt++);
            // Move to H1 
            tSAx.Pos = TCP.TC_H1_Z; // Z Height position and feedrate
            tSAx.Rate = TCP.TC_H1_FR;
            Start_MoveZ_Process(tSAx);
            if(WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Index the spindle
            tSAx.Pos = TCP.TC_Index;    // Spindle position and feedrate 
            tSAx.Rate = TCP.TC_S_FR;
            Start_Spindle_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // rotate carousel to tool number
            tSAx.ToolNumber = (int)e.Argument;  // carousel position number
            Start_Carousel_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Arm Out
            tSAx.Move = bARM_OUT;
            Start_ARM_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Clamp Release
            tSAx.Move = bRELEASE;
            Start_TClamp_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // move to H2
            tSAx.Pos = TCP.TC_H2_Z;
            tSAx.Rate = TCP.TC_H2_FR;
            Start_MoveZ_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Clamp Engage
            tSAx.Move = bCLAMP;
            Start_TClamp_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Arm In
            tSAx.Move = bARM_IN;
            Start_ARM_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Spindle back to RPM Mode
            Start_SpindleRPM_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

        }
        
        private void GetToolProgressedChanged(object sender, ProgressChangedEventArgs e)
        {
            lblTCProgress.Content = "Progress :" + e.ProgressPercentage.ToString();
        }

        private void GetToolCompleted(object sender, AsyncCompletedEventArgs e)
        {
            _bw2.DoWork -= GetToolWorker;
            _bw2.ProgressChanged -= GetToolProgressedChanged;
            _bw2.RunWorkerCompleted -= GetToolCompleted;
            // report that the tool change was a success!
            // MessageBox.Show("In Get Completed");
        }
#endregion
        
        private bool WaitForProgress()
        {
            // sleep while waiting for the global variable TCProgress to be set true by the 
            // background worker thread.
            do
            {
                Thread.Sleep(100);
            } while (TCProgress);
            TCProgress = true;
            // check the results for faults and errors
            return ToolChangeStatus;
            
        }

#region Put a tool in the Tool Changer
        private void Start_PutTool(int ToolNumber)
        {
            _bw2.WorkerReportsProgress = true;
            _bw2.WorkerSupportsCancellation = true;

            _bw2.DoWork += PutToolWorker;
            _bw2.ProgressChanged += PutToolProgressedChanged;
            _bw2.RunWorkerCompleted += PutToolCompleted;
            _bw2.RunWorkerAsync(ToolNumber);
        }

        private void PutToolWorker(object sender, DoWorkEventArgs e)
        {
            // Assume the following state:
            // 1. Spindle has a tool in it
            // 2. The tool arm is retracted.
            // tool number to get is passed in the argument
            SingleAxis tSAx = new SingleAxis();
            // start a new background worker
            if (_bw.IsBusy) // if the BW worker is busy then don't do this. - should probably set some kind of flag here...
            { return; }
            TCProgress = true;
            ToolChangeStatus = false;
            int progress_cnt = 0;

            _bw2.ReportProgress(progress_cnt++);
            // Move to H2 
            tSAx.Pos = TCP.TC_H2_Z; // Z Height position and feedrate
            tSAx.Rate = TCP.TC_H2_FR;
            Start_MoveZ_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Index the spindle
            tSAx.Pos = TCP.TC_Index;    // Spindle position and feedrate 
            tSAx.Rate = TCP.TC_S_FR;
            Start_Spindle_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // rotate carousel to tool number
            tSAx.ToolNumber = (int)e.Argument;  // carousel position number
            Start_Carousel_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Arm Out
            tSAx.Move = bARM_OUT;
            Start_ARM_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Clamp Release
            tSAx.Move = bRELEASE;
            Start_TClamp_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // move to H1
            tSAx.Pos = TCP.TC_H1_Z;
            tSAx.Rate = TCP.TC_H1_FR;
            Start_MoveZ_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Clamp Engage
            tSAx.Move = bCLAMP;
            Start_TClamp_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Arm In
            tSAx.Move = bARM_IN;
            Start_ARM_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

            // Spindle back to RPM Mode
            Start_SpindleRPM_Process(tSAx);
            if (WaitForProgress() == false)
            { return; }
            _bw2.ReportProgress(progress_cnt++);

        }

        private void PutToolProgressedChanged(object sender, ProgressChangedEventArgs e)
        {
            lblTCProgress.Content = "Progress :" + e.ProgressPercentage.ToString();
        }

        private void PutToolCompleted(object sender, AsyncCompletedEventArgs e)
        {
            _bw2.DoWork -= PutToolWorker;
            _bw2.ProgressChanged -= PutToolProgressedChanged;
            _bw2.RunWorkerCompleted -= PutToolCompleted;
            // MessageBox.Show("In Put Completed");
            // report that the tool change was a success!
        }
#endregion

#region Tool Changer Test buttons
        private void btnSP_PID_Click(object sender, RoutedEventArgs e)
        {
            // set the spindle to PID mode. And leave it enabled!
            if(SpindleEnabled == true)
            {
                // MessageBox.Show("Turning Off Spindle");
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
                // MessageBox.Show("Turning On Spindle");
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
              //  var SP = KMx.GetAxis(AXConst.SPINDLE_AXIS, "Spindle");
              //  SP.StartMoveTo(0);
                SingleAxis Sx = new SingleAxis();
                Sx.Pos = 0;
                Sx.Rate = 1500;
                // MessageBox.Show("pid1 StartSpindle");
                Start_Spindle_Process(Sx);
  
            }
        }

        private void btnTC_H1_Click(object sender, RoutedEventArgs e)
        {
            if(_bw.IsBusy)
            { return; } // don't run if the _bw worker is busy



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
            Start_MoveZ_Process(Zx);
        }

        private void btnTC_H2_Click(object sender, RoutedEventArgs e)
        {
            // this is the same as btnTC_H1 except for the coordinates used
            if (_bw.IsBusy)
            { return; } // don't run if the _bw worker is busy

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
            Start_MoveZ_Process(Zx);
        }

        private void btnToolRel_Click(object sender, RoutedEventArgs e)
        {
            if (_bw.IsBusy)
            { return; } // don't run if the _bw worker is busy

            SingleAxis xSA = new SingleAxis();
            if (bTC_Clamped == true)
            {
                btnToolRel.Content = "Tool Clamp";
                xSA.Move = bRELEASE;
            }
            else
            {
                btnToolRel.Content = "Tool Release";
                xSA.Move = bCLAMP;
            }
            Start_TClamp_Process(xSA);
        }

        private void btnArm_In_Click(object sender, RoutedEventArgs e)
        {
            if (_bw.IsBusy)
            { return; } // don't run if the _bw worker is busy

            getTLAUX_Status();
            if(bTLAUX_FAULT)
            {
                MessageBox.Show("Tool Changer Fault!\nRe-Home to clear");
                return;
            }

            SingleAxis xSA = new SingleAxis();

            if (bTLAUX_ARM_IN == true)  // the current status in so move the arm out.
            {
                btnArm_In.Content = "TC ARM IN";
                xSA.Move = bARM_OUT;
            }
            else
            {
                btnArm_In.Content = "TC ARM OUT";   // the current status is out so move the arm in
                xSA.Move = bARM_IN;
            }
            Start_ARM_Process(xSA);
        }

        private void btnSpindle_Click(object sender, RoutedEventArgs e)
        {
            if (_bw.IsBusy)
            {
                MessageBox.Show("_bw.IsBusy!");
                return;
            } // don't run if the _bw worker is busy

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

#if TESTBENCH
            MessageBox.Show("TB StartSpindle");
            Start_Spindle_Process(SX);
#else
            if (SpindleEnabled && SpindlePID)
            {
                MessageBox.Show("StartSpindle");
                Start_Spindle_Process(SX);
            }
            else
            {
                MessageBox.Show("Spindle not Enabled!");
            }
#endif
        }

        private void btnToolSel_Click(object sender, RoutedEventArgs e)
        {
            if (_bw.IsBusy)
            { return; } // don't run if the _bw worker is busy

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
                SingleAxis xSA = new SingleAxis();
                xSA.ToolNumber = ToolNumber;
                Start_Carousel_Process(xSA);
            }
        }
#endregion

#region Configuration File methods
        // Configuration file - get the tool changer variables saved in the JSON file
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
#endregion

#region Status methods 
        private void getTLAUX_Status()
        {
            lock (_Tlocker)  // lock this so only one thread can access it at a time.
            {
                // do I need to lock this? 
                // get the TLAUX status and set the  apropriate flags
                // this is called every 100ms by the UI update timer
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
//                iSpindle_Status = KMx.GetUserData(PVConst.P_STATUS_REPORT);
//                SpindleHomed = !B.BitIsSet(iSpindle_Status, PVConst.SB_SPIN_HOME);  // this inverts the logic of the bit in the status word. so true = homed
//                SpindleEnabled = B.BitIsSet(iSpindle_Status, PVConst.SB_SPINDLE_ON);
//                SpindlePID = B.BitIsSet(iSpindle_Status, PVConst.SB_SPINDLE_PID);
//                SpindleRPM = B.BitIsSet(iSpindle_Status, PVConst.SB_SPINDLE_RPM);
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
                    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_ZERO);
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

        private void CompleteStatus(BWResults res)
        {
            if (res.Result == true)  // process ended successfully - 
            {
                // maybe put something on a label - like the 
                Dispatcher.Invoke(() =>
                {
                    lblTCP2.Content = res.Comment;
                    ToolChangeStatus = true; // everything is OK
                });
            }
            else
            {
                // process ended with a fault condition - set a flag???
                // update the UI label
                Dispatcher.Invoke(() =>
                {
                    lblTCP2.Content = res.Comment;
                    ToolChangeStatus = false; // there was some kind of fault or error.
                });
            }
            TCProgress = false; // the phase is done
        }


#region MoveZ background process
        // the move Z background process
        private void Start_MoveZ_Process(SingleAxis SA)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += MoveZ_Worker; // Add the main thread method to call
            _bw.ProgressChanged += MoveZ_ProgressChanged; // add the progress changed method
            _bw.RunWorkerCompleted += MoveZ_Completed; // the the method to call when the function is done
            _bw.RunWorkerAsync(SA);
        }

        static void MoveZ_Worker(object sender, DoWorkEventArgs e)
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
                BWRes.Result = false;
                BWRes.Comment = "Axis not enabled";
                // e.Result = "Axis not enabled";
                e.Result = BWRes;
                return;
            }

            double cX, cY, cZ, cA, cB, cC;
            cX = cY = cZ = cA = cB = cC = 0;

            KMx.CoordMotion.ReadAndSyncCurPositions(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC);
            // may want to change the last two variables to pass information to the 3D path generation

            KMx.CoordMotion.StraightFeed(SAx.Rate, cX, cY, SAx.Pos, cA, cB, cC, 0, 0);
            KMx.CoordMotion.FlushSegments();    // push the segments out the buffer and into the KFLOP
            KMx.CoordMotion.WaitForSegmentsFinished(false); // - this works, but blocks the calling thread - so how to check if it is done?
            BWRes.Result = true;
            BWRes.Comment = "Z Motion done";
            // e.Result += "Motion done";
            e.Result = BWRes;

        }

        private void MoveZ_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void MoveZ_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
           // MessageBox.Show(xBWR.Comment + xBWR.Result.ToString());
            _bw.DoWork -= MoveZ_Worker;
            _bw.ProgressChanged -= MoveZ_ProgressChanged;
            _bw.RunWorkerCompleted -= MoveZ_Completed;
            //           btnAbort.IsEnabled = false; // disable the abort button -- this doesn't work... need a completely thread safe way to do this...
            Dispatcher.Invoke(() =>    // Dispatcher to the rescue!
                btnAbort.IsEnabled = false
             );
            CompleteStatus((BWResults)e.Result);
        }

       // the abort button.
        private void btnAbort_Click(object sender, RoutedEventArgs e)
        {
            // if the background worker is running then abort movement and stop it
            if (_bw.IsBusy)
            {
                // KMx.CoordMotion.Abort(); // stop the motion! - maybe try a halt here instead?
                KMx.CoordMotion.Halt(); //  Halt seems to stop quicker than Abort does.
                //  _bw.CancelAsync();  // stop the background worker.
                // note here on canceling. This doesn't do anything because the async worker is not checking for the 
                // .CancellationPending flag. - 
                // The reason the Abort button works is because of the WaitForSegmentsFinished call ends the thread
                // if there was a way to timeout, I wouldn't need the Abort button...
                MessageBox.Show("Halted");
                KMx.CoordMotion.ClearHalt();
                KMx.CoordMotion.ClearAbort(); // but Halt requies ClearAbort inorder to resume.
                // MessageBox.Show("Abort Pressed");
                lblTCP2.Content = "Z Motion Aborted";
            }
        }
#endregion

#region Carousel background process
        // Rotate Carousel background process
        private void Start_Carousel_Process(SingleAxis SA)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += Carousel_Worker;
            _bw.ProgressChanged += Carousel_ProgressChanged;
            _bw.RunWorkerCompleted += Carousel_Completed;

            _bw.RunWorkerAsync(SA.ToolNumber);
        }

        private void Carousel_Worker(object sender, DoWorkEventArgs e)
        {
            int car_num = (int)e.Argument;
            getTLAUX_Status();
            if(Carousel_Position == car_num)
            {
                BWRes.Result = true;
                BWRes.Comment = "Carousel at " + car_num;
                e.Result = BWRes;
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
                    BWRes.Result = false;
                    BWRes.Comment = "Carousel Timeout";
                    e.Result = BWRes;
                    return;
                }
            } while (Carousel_Position != car_num);
            BWRes.Result = true;
            BWRes.Comment = "Carousel moved to " + car_num;
            e.Result = BWRes;
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
            CompleteStatus((BWResults)e.Result);
        }
#endregion

#region Spindle Indexing process
        // Index Spindle background process
        private void Start_Spindle_Process(SingleAxis SA)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += Spindle_Worker;
            _bw.ProgressChanged += Spindle_ProgressChanged;
            _bw.RunWorkerCompleted += Spindle_Completed;

            _bw.RunWorkerAsync(SA);
        }

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
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_ZERO);
                KMx.ExecuteProgram(2);
                // wait until it is done.
                do
                {
                    Thread.Sleep(100);
                    if (timeoutCnt++ > 50)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle Timeout- DANG!";
                        e.Result = BWRes;
                        return;
                    }
                    getSpindle_Status();
                } while (Math.Abs(Spindle_Position) > 2); // I think this will indicate a completed Home routine
            }
            // getSpindle_Status();
            // if the spindle is not enabled then enable it
            if (SpindlePID == false)
            {
                timeoutCnt = 0;
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_PID);  // send the command
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(50);
                    if (timeoutCnt++ > 20)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle PID Timeout";
                        e.Result = BWRes;
                        return;
                    }
                    // getSpindle_Status();
                } while (SpindlePID == false);
            }

            if (SpindleEnabled == false)
            {
                timeoutCnt = 0;
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_EN);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(50);
                    if (timeoutCnt++ > 20)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle Enable Timeout";
                        e.Result = BWRes;
                        return;
                    }
                    getSpindle_Status();
                } while (SpindleEnabled == false);
            }

            SPx.Velocity = SAs.Rate;
            SPx.StartMoveTo(SAs.Pos);
            timeoutCnt = 0;

            do {    // Wait until done or timeout
                Thread.Sleep(100);
                if (timeoutCnt++ > 50)
                {
                    BWRes.Result = false;
                    BWRes.Comment = "Spindle Index Timeout";
                    e.Result = BWRes;
                return;
                }
            } while (SPx.MotionComplete() != true);

            // note - this leaves the spindle enabled!
            // All Done!

            BWRes.Result = true;
            BWRes.Comment = "Spindle Index Done";
            e.Result = BWRes;

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
            CompleteStatus((BWResults)e.Result);
        }
        #endregion

#region Spindle Return to RPM mode.
        // return the Spindle back to RPM mode.
        private void Start_SpindleRPM_Process(SingleAxis SA)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += SpindleRPM_Worker;
            _bw.ProgressChanged += SpindleRPM_ProgressChanged;
            _bw.RunWorkerCompleted += SpindleRPM_Completed;

            _bw.RunWorkerAsync(SA);
        }

        private void SpindleRPM_Worker(object sender, DoWorkEventArgs e)
        {
            int timeoutCnt;
            // Disable the spindle and set the spindle back to RPM Mode 
            getSpindle_Status();
            if(SpindleEnabled == true)
            {
                timeoutCnt = 0;
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_DIS);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(50);
                    if (timeoutCnt++ > 20)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle Disable Timeout";
                        e.Result = BWRes;
                        return;
                    }
                    getSpindle_Status();
                } while (SpindleEnabled == false);
            }
            if(SpindleRPM == false)
            {
                timeoutCnt = 0;
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_RPM);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(50);
                    if (timeoutCnt++ > 20)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle RPM Timeout";
                        e.Result = BWRes;
                        return;
                    }
                    getSpindle_Status();
                } while (SpindleRPM == false);
            }
            BWRes.Result = true;
            BWRes.Comment = "Spindle Disabled (RPM)";
            e.Result = BWRes;
        }

        private void SpindleRPM_ProgressChanged(object sender, ProgressChangedEventArgs e)
        { }

        private void SpindleRPM_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw.DoWork -= SpindleRPM_Worker;
            _bw.ProgressChanged -= SpindleRPM_ProgressChanged;
            _bw.RunWorkerCompleted -= SpindleRPM_Completed;
            CompleteStatus((BWResults)e.Result);
        }
        #endregion

#region Tool Changer Arm process
        // TC Arm In/Out background process
        private void Start_ARM_Process(SingleAxis SA)
        {
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += TLAUX_ARM_Worker; // Add the main thread method to call
            _bw.ProgressChanged += TLAUX_ARM_ProgressChanged; // add the progress changed method
            _bw.RunWorkerCompleted += TLAUX_ARM_Completed; // the the method to call when the function is done
            _bw.RunWorkerAsync(SA.Move);
        }

        private void TLAUX_ARM_Worker(object sender, DoWorkEventArgs e)
        {
            // get the TLAUX Status
            getTLAUX_Status();
            if(bTLAUX_FAULT)
            {
                BWRes.Result = false;
                BWRes.Comment = "TLAUX Fault";
                e.Result = BWRes;
                return;
            }
            int TimeoutCnt = 0;
            if ((bool)e.Argument)    // true is arm in, false is arm out
            {
                BWRes.Comment = "TC Arm In";
                if (bTLAUX_ARM_IN)  // if the arm in then already done
                {
                    BWRes.Result = true;
                    e.Result = BWRes;
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
                        BWRes.Result = false;
                        BWRes.Comment = "TC Arm Timeout";
                        e.Result = BWRes;
                        return;
                    }
                } while (bTLAUX_ARM_IN == false);
            } else
            {
                BWRes.Comment = "TC Arm Out";
                if (bTLAUX_ARM_OUT)  // if the arm in then already done
                {
                    BWRes.Result = true;
                    e.Result = BWRes;
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
                        BWRes.Result = false;
                        BWRes.Comment = "TC Arm Timeout";
                        e.Result = BWRes;
                        return;
                    }
                } while (bTLAUX_ARM_OUT == false);
            }
            BWRes.Result = true;
            e.Result = BWRes;
        }

        private void TLAUX_ARM_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void TLAUX_ARM_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
           //  MessageBox.Show(e.Result.ToString());
            _bw.DoWork -= TLAUX_ARM_Worker;
            _bw.ProgressChanged -= TLAUX_ARM_ProgressChanged;
            _bw.RunWorkerCompleted -= TLAUX_ARM_Completed;
            CompleteStatus((BWResults)e.Result);
        }
#endregion

#region Tool Clamp Process
        // Tool Clamp background process
        private void Start_TClamp_Process(SingleAxis SA)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += TClamp_Worker;
            _bw.ProgressChanged += TClamp_ProgressChanged;
            _bw.RunWorkerCompleted += TClamp_Completed;
            _bw.RunWorkerAsync(SA.Move);
        }

        private void TClamp_Worker(object sender, DoWorkEventArgs e)
        {
            int timeoutCnt = 0;

            getTLAUX_Status();  // start by getting the latest TLAUX Status
            if((bool)e.Argument) // true = engage clamp, false = release clamp
            {
                BWRes.Comment = "Tool Clamp Done";
                if (bTC_Clamped)
                {
                    BWRes.Result = true;
                    e.Result = BWRes;
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
                        BWRes.Result = false;
                        BWRes.Comment = "Tool Clamp Timeout";
                        e.Result = BWRes;
                        return;
                    }
                } while (bTC_Clamped == false);
            }
            else
            {
                BWRes.Comment = "Tool Un-Clamp done";
                if (bTC_UnClamped)
                {
                    BWRes.Result = true;
                    e.Result = BWRes;
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
                        BWRes.Result = false;
                        BWRes.Comment = "Tool Clamp Timeout";
                        e.Result = BWRes;
                        return;
                    }
                } while (bTC_UnClamped == false);
            }
            BWRes.Result = true;
            e.Result = BWRes;
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
            CompleteStatus((BWResults)e.Result);
        }
#endregion

#endregion


    }

    class SingleAxis
    {
        public double Pos { get; set; }
        public double Rate { get; set; }
        public bool Move { get; set; }  // in / out or clamp / release 
        public int ToolNumber { get; set; }
    }

    class BWResults
    {
        public bool Result { get; set; }
        public string Comment { get; set; }
    }

}
