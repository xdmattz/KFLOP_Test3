// Must put this in every file that will look for it!
#define TESTBENCH  // defining this will allow operation on the testbench
// don't forget the TESTBENCH in ToolChanger.cs

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
    /// 
    public enum TS_Actions : int
    {
        NoAction,
        ToolMeasurment,
        Calibration
    }

    public partial class ToolChangerPanel : UserControl
    {
        #region Global Variables

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
        static ToolSetter xToolSetter;

        
        private double GaugeLength;
        private int CalCycles;
        private int CalCount;

        private List<double> CalList;
        

        #endregion

        // tool changer panel 
        // arguments ref of the KM_Controller in use
        // KM_Axis for the spindle control
        // configuration files
        public ToolChangerPanel(ref ToolChanger X, ref ToolSetter TS, ref ConfigFiles CfgFiles)
        {
            InitializeComponent();

            CFx = CfgFiles;

            xToolChanger = X;    // point to the KM controller - this exposes all the KFLOP .net library functions
            xToolSetter = TS;

            xToolChanger.LoadTCCfg();

            // Events 
            xToolChanger.ProcessUpdate += TC_ProcessChanged;
            xToolChanger.ProcessError += TC_ProcessError;
            xToolChanger.StepUpdate += TC_StepChanged;
            xToolChanger.StepError += TC_StepError;
            xToolChanger.ProcessCompleted += TC_ProcessFinished; // need something to call here!

            xToolChanger.UpdateCarousel += TC_CarouselUpdate;

            xToolSetter.ProcessUpdate += TC_ProcessChanged;
            xToolSetter.ProcessError += TC_ProcessError;
            // xToolSetter.ProcessCompleted += TS_ProcessFinished; // this is a bit different from the TC Process Finished callback.

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

            ToolSetter.SetterAction = (int)TS_Actions.NoAction;

            CalList = new List<double>();
            CalList.Clear();    // start with the CalList cleared.

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

            xToolChanger.getSpindle_Status();
            // get the spindle status from KSTAT
            //iPVStatus = KStat.PC_comm[CSConst.P_STATUS];

           // SpindleEnabled = BitOps.BitIsSet(iPVStatus, PVConst.SB_SPINDLE_ON);
           // SpindlePID = BitOps.BitIsSet(iPVStatus, PVConst.SB_SPINDLE_PID);
            if (MachineMotion.SpindleEnabled) { LED_SPEN.Set_State(LED_State.On_Green); }
            else { LED_SPEN.Set_State(LED_State.Off); }
            //SpindleRPM = BitOps.BitIsSet(iPVStatus, PVConst.SB_SPINDLE_RPM);
            //SpindleHomed = !(BitOps.BitIsSet(iPVStatus, PVConst.SB_SPIN_HOME));
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
            // get the tool number
            int PocketNumber;
            if (int.TryParse(tbPocketNumber.Text, out PocketNumber) == false)
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
                string toolmsg = string.Format("Exchange tools {0} and {1}", ToolChanger.ToolInSpindle, PocketNumber);
                MessageBoxResult rslt = MessageBox.Show(toolmsg, "Verify", MessageBoxButton.YesNo);
                if (rslt == MessageBoxResult.Yes)
                {
                    // xToolChanger.ToolChangerDeluxe(ToolInSpindle, ToolNumber);
                    TC_DisableButtons();
                    xToolChanger.ToolChangerSimple(ToolChanger.ToolInSpindle, PocketNumber);
                }
                xToolChanger.SetCurrentTool(PocketNumber); // update the interpreter slot number
                return;
            }

            ToolChanger.ToolInSpindle = 0;
            ToolChanger.ToolInSpinLen = 0.0;

            // check the tool number.
            if(PocketNumber == 0)
            {
                MessageBox.Show("Spindle is empty!");
                return;
            }
            else if (PocketNumber > MachineMotion.xTCP.CarouselSize)
            {
                string toolmsg = string.Format("Manualy insert Tool number {0} now", PocketNumber);
                MessageBox.Show(toolmsg);
                // ToolInSpindle = ToolNumber;
                xToolChanger.SetCurrentTool(PocketNumber); // update the interpreter slot number
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
            TC_DisableButtons();
            xToolChanger.ToolChangerSimple(0, PocketNumber); // this should get "PocketNumber" from the carousel from an empty spindle
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

            TC_DisableButtons();
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
            TC_DisableButtons();
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
                // Assuming that the calling function already checked that the ToolNumber and Pocket are valid
                MessageBoxResult MRB = MessageBox.Show($"Put Tool Number {ToolNumber} into the spindle", "Load Carousel", MessageBoxButton.OKCancel);
                if (MRB == MessageBoxResult.OK)
                {
                    if (xToolChanger.CheckPocketEmpty(PocketNumber) == false) // check for empty pocket number
                    {
                        MRB = MessageBox.Show($"Is Carousel Pocket {PocketNumber} Empty?", "*WARNING* Pocket Not Empty", MessageBoxButton.YesNoCancel);
                        if ((MRB == MessageBoxResult.Cancel) || (MRB == MessageBoxResult.No))
                        {
                            MessageBox.Show("You Must Remove the tool from Carousel Pocket {PocketNumber} before loading", "*WARNING* Pocket Not Empty");
                            return;
                        }
                    }

                    TC_DisableButtons();
                    xToolChanger.LoadTool(ToolNumber, PocketNumber);
                }
            }
            else
            {
                MessageBox.Show($"Tool {ToolNumber} is not in the Tool Table");
            }
        }

        private void TC_DisableButtons()
        {
            // Disable the Tool Changer buttons because a tool change action was started
            btnGetTool.IsEnabled = false;
            btnPutTool.IsEnabled = false;
            btnLoadTool.IsEnabled = false;
            btnUnloadTool.IsEnabled = false;

            btnTC_H1.IsEnabled = false;
            btnTC_H2.IsEnabled = false;
            btnToolRel.IsEnabled = false;
            btnSpindle.IsEnabled = false;
            btnSP_PID.IsEnabled = false;
            btnArm_In.IsEnabled = false;
            btnToolSel.IsEnabled = false;
        }

        private void TC_EnableButtons()
        {
            // re-enable the Tool Changer buttons now that the action is over.
            btnGetTool.IsEnabled = true;
            btnPutTool.IsEnabled = true;
            btnLoadTool.IsEnabled = true;
            btnUnloadTool.IsEnabled = true;

            btnTC_H1.IsEnabled = true; 
            btnTC_H2.IsEnabled = true;
            btnToolRel.IsEnabled = true;
            btnSpindle.IsEnabled = true;
            btnSP_PID.IsEnabled = true;
            btnArm_In.IsEnabled = true;
            btnToolSel.IsEnabled = true;
        }

        #endregion

        #region Tool Changer Test buttons
        // tests all possible basic actions of the tool changer.

        private void btnSP_PID_Click(object sender, RoutedEventArgs e)
        {
            // set the spindle to PID mode. And leave it enabled!
            if (MachineMotion.SpindleEnabled == true)
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
            // get the spindle status?
            xToolChanger.getSpindle_Status();
            if (MachineMotion.SpindleEnabled && MachineMotion.SpindlePID)
            {
//                MessageBox.Show("StartSpindle");
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
            if ((ToolNumber > 0) && (ToolNumber <= MachineMotion.xTCP.CarouselSize))
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
        #region UI Config
        private void UpdateCfgUI() // update the UI
        {
            //// populate the table with the parameter values
            // had to check for null because it crashes if the file isn't present.
            
            if (MachineMotion.xTCP != null)
            {
                tbTCH1.Text = MachineMotion.xTCP.TC_H1_Z.ToString();
                tbTCH1FR.Text = MachineMotion.xTCP.TC_H1_FR.ToString();
                tbTCH2.Text = MachineMotion.xTCP.TC_H2_Z.ToString();
                tbTCH2FR.Text = MachineMotion.xTCP.TC_H2_FR.ToString();
                tbSPIndex.Text = MachineMotion.xTCP.TC_Index.ToString();
                tbSPRate.Text = MachineMotion.xTCP.TC_S_FR.ToString();

                tbTSX.Text = MachineMotion.xTCP.TS_X.ToString();
                tbTSY.Text = MachineMotion.xTCP.TS_Y.ToString();
                tbTSZ.Text = MachineMotion.xTCP.TS_Z.ToString();
                tbTSSafeZ.Text = MachineMotion.xTCP.TS_SAFE_Z.ToString();
                tbTSIndex.Text = MachineMotion.xTCP.TS_S.ToString();
                tbRefZ.Text = MachineMotion.xTCP.TS_RefZ.ToString();

                tbTSRate1.Text = MachineMotion.xTCP.TS_FR1.ToString();
                tbTSRate2.Text = MachineMotion.xTCP.TS_FR2.ToString();
                tbZBack.Text = MachineMotion.xTCP.TS_AveBackoff.ToString();

                tbCarouselSize.Text = MachineMotion.xTCP.CarouselSize.ToString();
            }
        }

        private void UpdateCfg() // get UI values
        {
            // get the variables into TCP

            double temp;
            if (double.TryParse(tbTCH1.Text, out temp))
            { MachineMotion.xTCP.TC_H1_Z = temp; }
            if (double.TryParse(tbTCH1FR.Text, out temp))
            { MachineMotion.xTCP.TC_H1_FR = temp; }
            if (double.TryParse(tbTCH2.Text, out temp))
            { MachineMotion.xTCP.TC_H2_Z = temp; }
            if (double.TryParse(tbTCH2FR.Text, out temp))
            { MachineMotion.xTCP.TC_H2_FR = temp; }
            if (double.TryParse(tbSPIndex.Text, out temp))
            { MachineMotion.xTCP.TC_Index = temp; }
            if (double.TryParse(tbSPRate.Text, out temp))
            { MachineMotion.xTCP.TC_S_FR = temp; }

            if (double.TryParse(tbTSX.Text, out temp))
            { MachineMotion.xTCP.TS_X = temp; }
            if (double.TryParse(tbTSY.Text, out temp))
            { MachineMotion.xTCP.TS_Y = temp; }
            if (double.TryParse(tbTSZ.Text, out temp))
            { MachineMotion.xTCP.TS_Z = temp; }
            if(double.TryParse(tbTSSafeZ.Text, out temp))
            { MachineMotion.xTCP.TS_SAFE_Z = temp; }
            if (double.TryParse(tbTSIndex.Text, out temp))
            { MachineMotion.xTCP.TS_S = temp; }
            if (double.TryParse(tbRefZ.Text, out temp))
            { MachineMotion.xTCP.TS_RefZ = temp; }
            if (double.TryParse(tbTSRate1.Text, out temp))
            { MachineMotion.xTCP.TS_FR1 = temp; }
            if (double.TryParse(tbTSRate2.Text, out temp))
            { MachineMotion.xTCP.TS_FR2 = temp; }
            int itemp;
            if (int.TryParse(tbCarouselSize.Text, out itemp))
            { MachineMotion.xTCP.CarouselSize = itemp; }
            if(double.TryParse(tbZBack.Text, out temp))
            { MachineMotion.xTCP.TS_AveBackoff = temp; }

        }

        private void btnCfgLoad_Click(object sender, RoutedEventArgs e)
        {
            xToolChanger.LoadTCCfg();   // get the configuration from the file
            UpdateCfgUI();
        }

        private void btnCfgSave_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult MBR = MessageBox.Show("Save the Tool Changer Configuration?","File Save", MessageBoxButton.OKCancel);
            if (MBR == MessageBoxResult.OK)
            {
                UpdateCfg();
                xToolChanger.SaveTCCfg();
            }
        }
        #endregion

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
            for (int i = 0; i < MachineMotion.xTCP.CarouselSize; i++)  
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

        private void TC_ProcessFinished()
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => TC_ProcessFinished1()));
        }
        private void TC_ProcessFinished1()
        {
            // this is what gets called whenever any of the tool Changer processes finish
            // What to do here?
            // check for an error
            // maybe enable some buttons that were disabled?
            // note that this will also get called when a tool change is caused by G-Code execution so make sure it
            // handles all the cases.
//            MessageBox.Show("TC Process Finished");
            TC_EnableButtons();
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
            // need to know the tool number and estimated length.

            // Actual tool measurment
            // get the arguments
            if (ToolSetter.TSProbeState != ProbeResult.Idle)
            {
                MessageBox.Show("Tool Setter is not ready!");
                return;
            }
            ToolSetterArguments TSx = new ToolSetterArguments();
            // load the arguments from the Tool change parameters
            TSx.X_Offset = 0;
            TSx.Y_Offset = 0;
            TSx.Z_Offset = MachineMotion.xTCP.TS_Z - MachineMotion.xTCP.TS_RefZ;
            TSx.AverageCount = 1; // for now... maybe do something else here?
            // TSx.Z_Offset = 2.0; // calculate the proper length here!

            TSx.UseExpectedZ = false;   // use the full length of the probe routine
            
            ToolSetter.SetterAction = TS_Actions.ToolMeasurment;
            xToolSetter.ProcessCompleted += TS_ProcessFinished;
            ToolSetter_Action(TSx);
        }

        //public void ToolSetter_Tool(int ToolNumber, int AveN)
        //{
        //    // get the tool length from the table
        //    if (ToolSetter.TSProbeState != ProbeResult.Idle)
        //    {
        //        MessageBox.Show("Tool Setter is not ready!");
        //        return;
        //    }
        //    // Tool tool = GetToolInfo()
        //    ToolSetterArguments TSx = new ToolSetterArguments();
        //    //TSx.X_Offset = ToolOffset
        //    // TSx.Y_Offset = ToolOffset
        //    //TSx.Z_Offset = MachineMotion.xTCP.TS_Z - MachineMotion.xTCP.TS_RefZ - Toollength plus a little bit.
        //    TSx.AverageCount = 1; // for now
        //    TSx.UseExpectedZ = false;
        //    ToolSetter.SetterAction = TS_Actions.ToolMeasurment;
        //    // set a callback to record the proper length for the tool.
        //    ToolSetter_Action(TSx);
        //}

        private void ToolSetter_Action(ToolSetterArguments xTSArg)
        {
            // make sure that the tool setter is present
            // get the tool number to measure
            // get the tool from the carousel or manual insert

            Disable_TS_Buttons();
            xToolSetter.Start_ToolSetter(xTSArg);

            // call back delegate is TS_ProcessFinished()
            tbTS_Status.Text = "In Tool Setter";

        }

        private void btnTS_Cal_Click(object sender, RoutedEventArgs e)
        {
            // get the gauge block length.
            TS_Calibrate ts_Cal = new TS_Calibrate();
            if (ts_Cal.ShowDialog() == true)
            {
                // do a tool setter operation
                // TS Z coordinate = Detect position - gauge block length
                

                if (ToolSetter.TSProbeState != ProbeResult.Idle)
                {
                    MessageBox.Show("Tool Setter is not ready!");
                    return;
                }

                GaugeLength = ts_Cal.Value;
                CalCycles = ts_Cal.Cycles;
                CalCount = 0;
                CalList.Clear();    // clear the cal list
                ToolSetterArguments TSx = new ToolSetterArguments();
                // load the arguments from the Tool change parameters
                TSx.X_Offset = 0;
                TSx.Y_Offset = 0;
                TSx.Z_Offset = MachineMotion.xTCP.TS_Z - MachineMotion.xTCP.TS_RefZ - GaugeLength + 0.125; // full length - gauge length + a little bit
                TSx.AverageCount = ts_Cal.Cycles;
                ToolSetter.SetterAction = TS_Actions.Calibration;
                // add a callback for the iterations
                xToolSetter.ProbingCompleted += TS_ProbeCallback;   // DO NOT FORGET to remove this callback!
                xToolSetter.ProcessCompleted += TS_CalProcessFinished;  // callback for calibration
                ToolSetter_Action(TSx);
            }
        }

        private void btnTS_Reset_Click(object sender, RoutedEventArgs e)
        {
            ToolSetter.TSProbeState = ProbeResult.Idle;
        }

        private void Enable_TS_Buttons()
        {
            btnToolSetter.IsEnabled = true;
            btnTS_Cal.IsEnabled = true;
            btnTS_Reset.IsEnabled = true;
        }

        private void Disable_TS_Buttons()
        {
            btnToolSetter.IsEnabled = false;
            btnTS_Cal.IsEnabled = false;
            btnTS_Reset.IsEnabled = false;
        }

        #region Tool Setter Callback functions
        // event that gets called when the Tool Setter is done.
        // call back functions for the Tool Setter
        private void TS_ProcessFinished()
        {
        MessageBox.Show("TS Process Finished!");

            // remove the callback 
            xToolSetter.ProcessCompleted -= TS_ProcessFinished;

            Enable_TS_Buttons();
            // was the TS action a calibration, or a tool measurment
            if (ToolSetter.SetterAction == TS_Actions.ToolMeasurment)
            {
                if (ToolSetter.TSProbeState == ProbeResult.Detected)
                {
                    // valid Tool Setter detection
                    MessageBox.Show($"Detected!\nX = {MachineMotion.TSCoord.X}\nY = {MachineMotion.TSCoord.Y}\nZ = {MachineMotion.TSCoord.Z}");
                    // what are the coordinates?

                }
                else
                {
                    // tool setter timout or other error
                    MessageBox.Show("Setter Timeout");
                }

            }
            else if (ToolSetter.SetterAction == TS_Actions.Calibration)
            {

            }
            
            // set the process back to idle
            ToolSetter.TSProbeState = ProbeResult.Idle;

        }

        private void TS_CalProcessFinished()
        {
            xToolSetter.ProbingCompleted -= TS_ProbeCallback;   // Very important! probing is done here!
            xToolSetter.ProcessCompleted -= TS_CalProcessFinished;  // also remove this instance...

            Enable_TS_Buttons();

            if (ToolSetter.TSProbeState == ProbeResult.Detected)
            {
                string pmsg = string.Format("Probe Measurements at\nX = {0:F6}\nY = {1:F6}\nZ:\n", MachineMotion.TSCoord.X, MachineMotion.TSCoord.Y);
                double average = 0;
                int count = 0;
                foreach (double zc in CalList)
                {
                    pmsg += string.Format("{0:F6}\n", zc);
                    count++;
                    average += zc;
                }
                average = average / (double)(count);
                pmsg += string.Format("Count = {0}\nAverage = {1:F6}\n", count, average);
                pmsg += $"Last measurement = {MachineMotion.TSCoord.Z}";
                pmsg += "\n\nSave to a file?";

                MessageBoxResult MBR = MessageBox.Show(pmsg, "Tool Setter Calibration", MessageBoxButton.YesNo);
                if (MBR == MessageBoxResult.Yes)
                {
                    // open a file and save 
                    SaveFileDialog saveFile = new SaveFileDialog();
                    if (saveFile.ShowDialog() == true)
                    {
                        File.WriteAllText(saveFile.FileName, pmsg);
                    }
                }
                // MessageBox.Show($"Calibration!\nX = {MachineMotion.TSCoord.X}\nY = {MachineMotion.TSCoord.Y}\nZ = {MachineMotion.TSCoord.Z}");
                // record the Z coordinate
                MachineMotion.xTCP.TS_RefZ = average - GaugeLength;
                UpdateCfgUI();
            }
            else
            {
                MessageBox.Show("Calibration Timeout");
            }
            ToolSetter.TSProbeState = ProbeResult.Idle;
        }

        // Probing call back
        // not sure I need the double dispatch. may only need that for accessing a UI element.
        private void TS_ProbeCallback()
//        {
//            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => TS_ProbeCallback1()));
//        }
//
//        private void TS_ProbeCallback1()
        {
            if(ToolSetter.SetterAction == TS_Actions.Calibration)
            {
                // add the TS_Coord.Z to the list of coordinates.
                CalCount++;
                CalList.Add(MachineMotion.TSCoord.Z);
               // string pmsg = $"TS Probe Cnt = {CalCount} Z = {MachineMotion.TSCoord.Z}";
               // MessageBox.Show(pmsg);
              
            }
        }
        #endregion

        #endregion

        private void TC_Test_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnToolInSpindleUpdate_Click(object sender, RoutedEventArgs e)
        {
            // get the text from tbToolInSpindle and update the spindle state 
            // do this when manually inserting and removing tools
             
            SpindleUpdate SPUpdate = new SpindleUpdate(ToolChanger.ToolInSpindle);  // really put the current tool number in here!
            SPUpdate.ShowDialog();
            if (SPUpdate.DialogResult == true)
            {
                if (SPUpdate.value == 0)
                {
                    // Remove the current tool from the spindle and clear its carousel pocket
                    xToolChanger.SetCurrentTool(0);
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

        private void btnCfgUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdateCfg();
        }


    }

 

}
