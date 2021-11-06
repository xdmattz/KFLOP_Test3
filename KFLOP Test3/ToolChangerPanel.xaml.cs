// Must put this in every file that will look for it!
#define TESTBENCH  // defining this will allow operation on the testbench

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
        #region Global Variables
        // global variables for the user control
        // since there is only one Machine and tool changer these are all static.
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

        const bool bARM_IN = true;
        const bool bARM_OUT = false;
        const bool bCLAMP = true;
        const bool bRELEASE = false;

        // used to point to the configuration file list. 
        static ConfigFiles CFx { get; set; }
        // Tool Carousel configuration
        static ToolCarousel CarouselList;
        // tool table 
        static ToolTable TTable;

        static ToolChanger xToolChanger;
        

        #endregion

        // tool changer panel 
        // arguments ref of the KM_Controller in use
        // KM_Axis for the spindle control
        // configuration files
        public ToolChangerPanel(ref ToolChanger X, ref ConfigFiles CfgFiles)
        {
            InitializeComponent();

            CFx = CfgFiles;

            xToolChanger = X;    // point to the KM controller - this exposes all the KFLOP .net library functions

//            TCP = new ToolChangeParams();   // get the tool changer parameters
            xToolChanger.LoadTCCfg();
//            LoadCfg();
//            xToolChanger.SetParams(ref TCP);

            // Events 
            xToolChanger.ProcessUpdate += TC_ProcessChanged;
            xToolChanger.ProcessError += TC_ProcessError;
            xToolChanger.StepUpdate += TC_StepChanged;
            xToolChanger.StepError += TC_StepError;

            xToolChanger.UpdateCarousel += TC_CarouselUpdate;

            //

            ledClamp.Set_State(LED_State.Off);
            ledClamp.Set_Label("Tool Clamp");

            // disable the abort button
            btnAbort.IsEnabled = false;

            UpdateCfgUI();

            LED_SPEN.Set_Label("Spindle EN");
            LED_SPEN.Set_State(LED_State.Off);

            ToolChange_LED.Set_Label("Tool Changing");
            ToolChange_LED.Set_State(LED_State.Off);

            // initialize and load the tool carousel data
            // CarouselList = new ToolCarousel();
            // TTable = new ToolTable();
            CarouselList = xToolChanger.GetCarousel();
            TTable = xToolChanger.GetToolTable();

        }

        #region Tool Changer UI
        public void SetTC_Led()
        {
            ToolChange_LED.Set_State(LED_State.On_Blue);
        }
        public void ClearTC_Led()
        {
            ToolChange_LED.Set_State(LED_State.Off);
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

            SpindleEnabled = BitOps.BitIsSet(iPVStatus, PVConst.SB_SPINDLE_ON);
            SpindlePID = BitOps.BitIsSet(iPVStatus, PVConst.SB_SPINDLE_PID);
            if (SpindleEnabled) { LED_SPEN.Set_State(LED_State.On_Green); }
            else { LED_SPEN.Set_State(LED_State.Off); }
            SpindleRPM = BitOps.BitIsSet(iPVStatus, PVConst.SB_SPINDLE_RPM);
            SpindleHomed = !(BitOps.BitIsSet(iPVStatus, PVConst.SB_SPIN_HOME));
            // update the tool in spindle
// this needs to change to reflect the new tool managment

            lblCurrentTool.Foreground = Brushes.Black; 
            lblCurrentTool.Content = string.Format("Tool in the Spindle");

        }
        public void SetToolSlot(int ToolSlot)
        { }
        public int GetToolSlot()
        {
            return 0;
        }


        #endregion

        #region Tool Changer Action Test Buttons
        private void btnGetTool_Click(object sender, RoutedEventArgs e)
        {
            // get a tool from the carousel
            // 
            // ensure that the TLAUX ARM is IN

            // get the tool number
            int ToolNumber;
            if (int.TryParse(tbPocketNumber.Text, out ToolNumber) == false)
            {
                tbPocketNumber.Text = "0";
                MessageBox.Show("Invalid Tool Number - Reset");
                xToolChanger.SetCurrentTool(0);
                return;
            }

            // is the spindle currently empty?
            MessageBoxResult result = MessageBox.Show("Is the Spindle Empty?", "Spindle Check", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                string toolmsg = string.Format("Exchange tools {0} and {1}", ToolChanger.ToolInSpindle, ToolNumber);
                MessageBoxResult rslt = MessageBox.Show(toolmsg, "Verify", MessageBoxButton.YesNo);
                if (rslt == MessageBoxResult.Yes)
                {
                    // xToolChanger.ToolChangerDeluxe(ToolInSpindle, ToolNumber);
                    xToolChanger.ToolChangerSimple(ToolChanger.ToolInSpindle, ToolNumber);
                }
                xToolChanger.SetCurrentTool(ToolNumber); // update the interpreter slot number
                return;
            }

            ToolChanger.ToolInSpindle = 0;

            // check the tool number.
            if(ToolNumber == 0)
            {
                MessageBox.Show("Spindle is empty!");
                return;
            }
            else if (ToolNumber > MachineMotion.xTCP.CarouselSize)
            {
                string toolmsg = string.Format("Manualy insert Tool number {0} now", ToolNumber);
                MessageBox.Show(toolmsg);
               // ToolInSpindle = ToolNumber;
                xToolChanger.SetCurrentTool(ToolNumber); // update the interpreter slot number
                return;
            }

            // is the TLAUX ARM in?
            getTLAUX_Status();
            if (bTLAUX_ARM_IN == false)
            {
                MessageBox.Show("Tool Changer Error!\nArm not retracted!\n(Try Re-Homing TLAUX)");
                return;
            }

            // update the Tool change parameters

            xToolChanger.ToolChangerSimple(0, ToolNumber); // this should get "ToolNumber" from the carousel from an empty spindle
        }

        private void btnPutTool_Click(object sender, RoutedEventArgs e)
        {
            // put a tool from the spindle into the tool carousel
            // insure that the ARM is in, the tool is in the spindle and the slot where it goes is empty
            // first get the slot number from the UI
            int ToolNumber;
            if (int.TryParse(tbPocketNumber.Text, out ToolNumber) == false)
            {
                tbPocketNumber.Text = "0";
                MessageBox.Show("Invalid tool number - Reset");
                return;
            }

            MessageBoxResult result = MessageBox.Show("Is carousel slot #" + ToolNumber + " Empty?", "Spindle Check", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                return;
            }
            // xToolChanger.ToolChangerDeluxe(ToolNumber, 0);    // this should put the tool into the carousel, 
            xToolChanger.ToolChangerSimple(ToolNumber, 0);    // this should put the tool into the carousel, 
                                                 // or prompt for a manual tool removal if the 
                                                 // tool number is too big for the carousel

            xToolChanger.SetCurrentTool(0); // update the interpreter slot number so it knows it is empty

            // UpdateCfg();
            // Start_PutTool(ToolNumber);
        }

        // similar to get tool
        private void btnUnloadTool_Click(object sender, RoutedEventArgs e)
        {
            int PocketNumber;
            // check for valid carousel number
            if (int.TryParse(tbPocketNumber.Text, out PocketNumber) == false)
            {
                tbPocketNumber.Text = "0";
                MessageBox.Show("Invalid Carousel Number - Reset");
                return;
            }
            // unload the tool
            xToolChanger.UnloadTool(PocketNumber);

        }
        private void btnLoadTool_Click(object sender, RoutedEventArgs e)
        {
            int PocketNumber;
            int ToolNumber;
            // check for valid carousel and tool numbers
            if (int.TryParse(tbPocketNumber.Text, out PocketNumber) == false)
            {
                tbPocketNumber.Text = "0";
                MessageBox.Show("Invalid Carousel Number - Reset");
                return;
            }
            if (int.TryParse(tbToolNumber.Text, out ToolNumber) == false)
            {
                tbToolNumber.Text = "0";
                MessageBox.Show("Invalid Tool Number - Reset");
                xToolChanger.SetCurrentTool(0);
                return;
            }
            // check if tool is in the tool list
            if(xToolChanger.ToolInTable(ToolNumber))
            {
                xToolChanger.LoadTool(ToolNumber, PocketNumber);
            }
            else
            {
                MessageBox.Show($"Tool {ToolNumber} is not in the Tool Table");
            }
        }



        #endregion

        #region Tool Changer Test buttons
        // tests all possible basic actions of the tool changer.

        private void btnSP_PID_Click(object sender, RoutedEventArgs e)
        {
            // set the spindle to PID mode. And leave it enabled!
            if (SpindleEnabled == true)
            {
                // MessageBox.Show("Turning Off Spindle");
                btnSP_PID.Content = "Enable Spindle";
                // Disable the spindle
                xToolChanger.SpindleDisable();
            }
            else
            {
                // MessageBox.Show("Turning On Spindle");
                btnSP_PID.Content = "Disable Spindle";
                // enable PID spindle mode and home

 //               SingleAxis Sx = new SingleAxis();
 //               Sx.Pos = 0;
 //               Sx.Rate = 1500;

                if (xToolChanger.AlignSpindle(0, 1500) == false)
                {
                   MessageBox.Show("Spindle Alignment Problem!");
                }

            }
        }

        private void btnTC_H1_Click(object sender, RoutedEventArgs e)
        {
            if (xToolChanger.bwBusy())
            { return; } // don't run if the _bw worker is busy



            SingleAxis Zx = new SingleAxis();
            double tempZ;
            if (double.TryParse(tbTCH1.Text, out tempZ) == false)
            {
                MessageBox.Show("TC_H1 not valid!");
                return;
            }
            Zx.Pos = tempZ;
            if (double.TryParse(tbTCH1FR.Text, out tempZ) == false)
            {
                MessageBox.Show("TC_H1 Rate not valid");
                return;
            }
            Zx.Rate = tempZ;

            // enable the abort button
            btnAbort.IsEnabled = true;
            xToolChanger.Start_MoveZ_Process(Zx);
        }

        private void btnTC_H2_Click(object sender, RoutedEventArgs e)
        {
            // this is the same as btnTC_H1 except for the coordinates used
            if (xToolChanger.bwBusy())
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
            xToolChanger.Start_MoveZ_Process(Zx);
        }

        private void btnToolRel_Click(object sender, RoutedEventArgs e)
        {
            if (xToolChanger.bwBusy())
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
            xToolChanger.Start_TClamp_Process(xSA);
        }

        private void btnArm_In_Click(object sender, RoutedEventArgs e)
        {
            if (xToolChanger.bwBusy())
            { return; } // don't run if the _bw worker is busy

            getTLAUX_Status();
            if (bTLAUX_FAULT)
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
            xToolChanger.Start_ARM_Process(xSA);
        }

        private void btnSpindle_Click(object sender, RoutedEventArgs e)
        {
            if (xToolChanger.bwBusy())
            {
                MessageBox.Show("_bw.IsBusy!");
                return;
            } // don't run if the _bw worker is busy

            // send the spindle to XXXX
            // check that the axis is enabled and in PID mode
            double Spos, Srate;
            if (double.TryParse(tbSPIndex.Text, out Spos) == false)
            {
                MessageBox.Show("Bad Spindle Index");
                return;
            }
            if (double.TryParse(tbSPRate.Text, out Srate) == false)
            {
                MessageBox.Show("Bad Spindle Rate");
                return;
            }
            SingleAxis SX = new SingleAxis();

            SX.Pos = Spos;
            SX.Rate = Srate;

            

#if TESTBENCH
            MessageBox.Show("TB StartSpindle");
            xToolChanger.Start_Spindle_Process(SX);
#else
            if (SpindleEnabled && SpindlePID)
            {
                MessageBox.Show("StartSpindle");
                xToolChanger.Start_Spindle_Process(SX);
            }
            else
            {
                MessageBox.Show("Spindle not Enabled!");
            }
#endif
        }

        private void btnToolSel_Click(object sender, RoutedEventArgs e)
        {
            if (xToolChanger.bwBusy())
            { return; } // don't run if the _bw worker is busy

            // get the tool number from the text box
            int ToolNumber;
            if (int.TryParse(tbPocketNumber.Text, out ToolNumber) == false)
            {
                tbPocketNumber.Text = "1";
                MessageBox.Show("Invalid tool number - Reset");
                return;
            }
            // send the command to KFLOP
            if ((ToolNumber > 0) && (ToolNumber <= ToolChanger.xTCP.CarouselSize))
            {
                SingleAxis xSA = new SingleAxis();
                xSA.ToolNumber = ToolNumber;
                xToolChanger.Start_Carousel_Process(xSA);
            }
        }
        #endregion

        #region Configuration File methods
        // Configuration file - get the tool changer variables saved in the JSON file
        // these are all the tool change coordinates and speeds.

        private void UpdateCfgUI() // update the UI
        {
            //// populate the table with the parameter values
            // had to check for null because it crashes if the file isn't present.
            
            if (ToolChanger.xTCP != null)
            {
                tbTCH1.Text = ToolChanger.xTCP.TC_H1_Z.ToString();
                tbTCH1FR.Text = ToolChanger.xTCP.TC_H1_FR.ToString();
                tbTCH2.Text = ToolChanger.xTCP.TC_H2_Z.ToString();
                tbTCH2FR.Text = ToolChanger.xTCP.TC_H2_FR.ToString();
                tbSPIndex.Text = ToolChanger.xTCP.TC_Index.ToString();
                tbSPRate.Text = ToolChanger.xTCP.TC_S_FR.ToString();

                tbTSX.Text = ToolChanger.xTCP.TS_X.ToString();
                tbTSY.Text = ToolChanger.xTCP.TS_Y.ToString();
                tbTSZ.Text = ToolChanger.xTCP.TS_Z.ToString();
                tbTSZSafe.Text = ToolChanger.xTCP.TS_SAFE_Z.ToString();
                tbTSIndex.Text = ToolChanger.xTCP.TS_S.ToString();

                tbTSRate1.Text = ToolChanger.xTCP.TS_FR1.ToString();
                tbTSRate2.Text = ToolChanger.xTCP.TS_FR2.ToString();


                tbCarouselSize.Text = ToolChanger.xTCP.CarouselSize.ToString();
            }
        }

        private void UpdateCfg() // get UI values
        {
            // get the variables into TCP

            double temp;
            if (double.TryParse(tbTCH1.Text, out temp))
            { ToolChanger.xTCP.TC_H1_Z = temp; }
            if (double.TryParse(tbTCH1FR.Text, out temp))
            { ToolChanger.xTCP.TC_H1_FR = temp; }
            if (double.TryParse(tbTCH2.Text, out temp))
            { ToolChanger.xTCP.TC_H2_Z = temp; }
            if (double.TryParse(tbTCH2FR.Text, out temp))
            { ToolChanger.xTCP.TC_H2_FR = temp; }
            if (double.TryParse(tbSPIndex.Text, out temp))
            { ToolChanger.xTCP.TC_Index = temp; }
            if (double.TryParse(tbSPRate.Text, out temp))
            { ToolChanger.xTCP.TC_S_FR = temp; }

            if (double.TryParse(tbTSX.Text, out temp))
            { ToolChanger.xTCP.TS_X = temp; }
            if (double.TryParse(tbTSY.Text, out temp))
            { ToolChanger.xTCP.TS_Y = temp; }
            if (double.TryParse(tbTSZ.Text, out temp))
            { ToolChanger.xTCP.TS_Z = temp; }
            if(double.TryParse(tbTSZSafe.Text, out temp))
            { ToolChanger.xTCP.TS_SAFE_Z = temp; }
            if (double.TryParse(tbTSIndex.Text, out temp))
            { ToolChanger.xTCP.TS_S = temp; }
            if (double.TryParse(tbTSRate1.Text, out temp))
            { ToolChanger.xTCP.TS_FR1 = temp; }
            if (double.TryParse(tbTSRate2.Text, out temp))
            { ToolChanger.xTCP.TS_FR2 = temp; }
            int itemp;
            if (int.TryParse(tbCarouselSize.Text, out itemp))
            { ToolChanger.xTCP.CarouselSize = itemp; }

        }

        #region Tool Carousel Configuration File

        private void btnInitCarousel_Click(object sender, RoutedEventArgs e)
        {
            // LoadCarouselCfg();
            xToolChanger.LoadCarouselCfg();
            // populate the ToolCarouselDataGrid
            // LoadToolTable();
            xToolChanger.SetCurrentTool(0);   // initialize the current tool to tool 0
            MessageBox.Show("Tool Changer Initizlized\nThis assumes no tool in the spindle\nRemove it now if present");
            
        }

        //// load the carousel file
        //public void LoadCarouselCfg()
        //{
        //    string CFileName = System.IO.Path.Combine(CFx.ConfigPath, CFx.ToolCarouselCfg);
        //    // load the tool changer files
        //   // xToolChanger.LoadCarCfg(CFileName);
        //    if (System.IO.File.Exists(CFileName) == true)
        //    {
        //        try
        //        {
        //            JsonSerializer Jser = new JsonSerializer();
        //            StreamReader sr = new StreamReader(CFileName);
        //            JsonReader Jreader = new JsonTextReader(sr);
        //            CarouselList = Jser.Deserialize<ToolCarousel>(Jreader);
        //            sr.Close();
        //        }
        //        catch (JsonSerializationException ex)
        //        {
        //            MessageBox.Show(String.Format("{0} Exception! Carousel Not Loaded!", ex.Message));
        //            return;
        //        }
        //    }
        //    else
        //    {
        //        MessageBox.Show("File not found - Reinialize the Carousel");
        //        // ResetCarousel();
        //        // saveCarouselCfg(FileName);
        //        return;
        //    }
        //}

        // save the carousel file
        public void saveCarouselCfg(string CFileName)
        {

            var SaveFile = new SaveFileDialog();
            SaveFile.FileName = CFileName;
            if (SaveFile.ShowDialog() == true)
            {
                try
                {
                    JsonSerializer Jser = new JsonSerializer();
                    StreamWriter sw = new StreamWriter(SaveFile.FileName);
                    JsonTextWriter Jwrite = new JsonTextWriter(sw);
                    Jser.NullValueHandling = NullValueHandling.Ignore;
                    Jser.Formatting = Newtonsoft.Json.Formatting.Indented;
                    Jser.Serialize(Jwrite, CarouselList);
                    sw.Close();
                }
                catch (JsonSerializationException ex)
                {
                    MessageBox.Show(String.Format("{0} Exception", ex.Message));
                }
            }
  
         }
        // clear the carousel list and reset every thing
        private void ResetCarousel()
        {
            // check for null carousel list
            if(CarouselList.Items == null) { CarouselList.Items = new List<CarouselItem>(); }

            CarouselList.Items.Clear(); // clear the list

            // fill the list with nothing
            for (int i = 0; i < ToolChanger.xTCP.CarouselSize; i++)  
            {
                CarouselItem CI = new CarouselItem();
                CI.Pocket = i + 1;
                CI.ToolIndex = 0;
                CI.ToolInUse = false;
                CI.Description = "Empty";
                CarouselList.Items.Add(CI);
                // CarouselList.Count = i + 1;
            } 
        }
        
        public ToolCarousel GetCarousel()
        {
            return CarouselList;
        }

        #endregion

        #endregion

        #region Status methods 
        // get the status Tool Carousel and of the Spindle position
        private void getTLAUX_Status()
        {

            // get the TLAUX status and set the  apropriate flags
            // this is called every 100ms by the UI update timer
            int TStatus = xToolChanger.TLAUX_Status();

            iTLAUX_STATUS = TStatus;
            bTLAUX_ARM_IN = BitOps.BitIsSet(TStatus, PVConst.TLAUX_ARM_IN);
            bTLAUX_ARM_OUT = BitOps.BitIsSet(TStatus, PVConst.TLAUX_ARM_OUT);
            bTC_Clamped = BitOps.BitIsSet(TStatus, PVConst.TLAUX_CLAMP);
            bTC_UnClamped = BitOps.BitIsSet(TStatus, PVConst.TLAUX_UNCLAMP);
            Carousel_Position = TStatus & PVConst.TLAUX_TOOL_MASK;
            bTLAUX_FAULT = BitOps.AnyInMask(TStatus, PVConst.TLAUX_ERROR_MASK);

        }

        // Status update from the tool changer
        private void TC_ProcessChanged(string s)
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => TC_ProcessChanged1(s)));
        }
        private void TC_ProcessChanged1(string s)
        {
            lblTCProgress.Foreground = Brushes.ForestGreen;
            lblTCProgress.Content = "Process Status";
            lblTCP2.Foreground = Brushes.ForestGreen;
            lblTCP2.Content = s;
        }
        private void TC_ProcessError(string s)
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => TC_ProcessError1(s)));
        }
        private void TC_ProcessError1(string s)
        {
            lblTCProgress.Foreground = Brushes.Red;
            lblTCProgress.Content = "Process Error";
            lblTCP2.Foreground = Brushes.Red;
            lblTCP2.Content = s;
        }

        // Status update from the tool changer steps -- double dispatched because it is another thread down.
        private void TC_StepChanged(string s)
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => TC_StepChanged1(s)));
        }
        private void TC_StepChanged1(string s)
        {
            lblStepProgress.Foreground = Brushes.ForestGreen;
            lblStepProgress.Content = "Step Status";
            lblStepP2.Foreground = Brushes.ForestGreen;
            lblStepP2.Content = s;
        }

        // Status update for the tool Carousel - updating CarouselList based on what action the carousel took
        private void TC_CarouselUpdate(int pocket, int state)
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => TC_CarouselUpdate1(pocket, state)));
        }
        private void TC_CarouselUpdate1(int pocket, int state)
        {
//            MessageBox.Show($"Pocket = {pocket} state = {state}");
        }


        private void TC_StepError(string s)
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => TC_StepError1(s)));
        }
        private void TC_StepError1(string s)
        {
            lblStepProgress.Foreground = Brushes.Red;
            lblStepProgress.Content = "Step Error";
            lblStepP2.Foreground = Brushes.Red;
            lblStepP2.Content = s;
        }

        #endregion

        #region Abort button
        // the abort button.
        private void btnAbort_Click(object sender, RoutedEventArgs e)
        {
            // if the background worker is running then abort movement and stop it
            if (xToolChanger.bwBusy())
            {
                // KMx.CoordMotion.Abort(); // stop the motion! - maybe try a halt here instead?
                // call the abort function in the machine motion
                // MessageBox.Show("Abort Pressed");
                lblTCP2.Content = "Z Motion Aborted";
            }
        }

        #endregion


        #region Tool Setter
        private void btnToolSetter_Click(object sender, RoutedEventArgs e)
        {
            // make sure that the tool setter is present
            // get the tool number to measure
            // get the tool from the carousel or manual insert

            // Actual tool measurment
            // get the arguments
            ToolSetterArguments TSx = new ToolSetterArguments();
            // load the arguments from the Tool change parameters
            TSx.X = ToolChanger.xTCP.TS_X;
            TSx.Y = ToolChanger.xTCP.TS_Y;
            TSx.SafeZ = ToolChanger.xTCP.TS_SAFE_Z;
            TSx.ToolZ = ToolChanger.xTCP.TS_Z;
            TSx.Rate = ToolChanger.xTCP.TS_FR1;

//            Start_ToolSetter(TSx);

            // record the detect position or if timed out report a no detect
            // 
        }

        #endregion

        private void TC_Test_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnToolInSpindleUpdate_Click(object sender, RoutedEventArgs e)
        {
            // get the text from tbToolInSpindle and update the spindle state 
            // do this when manually inserting and removing tools
            SpindleUpdate SPUpdate = new SpindleUpdate(0);  // really put the current tool number in here!
            SPUpdate.ShowDialog();
            if (SPUpdate.DialogResult == true)
            {
                if (SPUpdate.value == 0)
                {
                    // Remove the current tool from the spindle and clear its carousel pocket
                }
                else
                {
                    // check that the number is in the tool table
                    if (xToolChanger.ToolInTable(SPUpdate.value))
                    {
                        xToolChanger.SetCurrentTool(SPUpdate.value);
                        
                    }
                    else
                    {
                        MessageBox.Show($"Tool Number {SPUpdate.value} is not in the tool table\rEnter new tools in the tool table panel");
                    }
                }
            } else
            {
                // dialog was canceled do nothing.
            }
        }


    }

    public class SingleAxis
    {
        public double Pos { get; set; }
        public double Rate { get; set; }
        public bool Move { get; set; }  // in / out or clamp / release 
        public int ToolNumber { get; set; }
        public int ToolPocket { get; set; }
    }

    public class PlaneAxis
    {
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double Rate { get; set; }
        public bool Move { get; set; }
        public int ToolNumber { get; set; }
    }

    // Background Worker results 
    public class BWResults
    {
        public bool Result { get; set; }
        public string Comment { get; set; }
    }

    public class TExchangePosition
    {
        public int PutTool { get; set; } 
        public int GetTool { get; set; }
    }

    public class ToolSetterArguments
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double SafeZ { get; set; }
        public double ToolZ { get; set; }
        public double ToolLength { get; set; }
        public double Rate { get; set; }
    }

}
