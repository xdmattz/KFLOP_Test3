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
        static bool TC_Clamped;
        static bool TLAUX_ARM_IN;
        static int Carousel_Position;
        
        

        // a copy of the KM controller 
        private KM_Controller KMx { get; set; }
        private KM_Axis SPx { get; set; }
        private BitOps B;
        private ToolChangeParams TCP;
        private string CfgFName;
        

        public ToolChangerPanel(ref KM_Controller X, ref KM_Axis SP, string cfgFileName)
        {
            InitializeComponent();
            CfgFName = cfgFileName;

            KMx = X;    // point to the KM controller - this exposes all the KFLOP .net library functions
            SPx = SP;   // point to the Spindle Axis for fine control
                        
            TCP = new ToolChangeParams();   // get the tool changer parameters
            LoadCfg(cfgFileName);

            B = new BitOps();
            ledClamp.Set_State(LED_State.Off);
            ledClamp.Set_Label("Tool Clamp");

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

        public void TLAUX_Status(ref KM_MainStatus KStat)
        {
            int TStatus = KMx.GetUserData(PVConst.P_TLAUX_STATUS);
            TLAUX_ARM_IN = B.BitIsSet(TStatus, PVConst.TLAUX_ARM_IN);
            TC_Clamped = B.BitIsSet(TStatus, PVConst.TLAUX_CLAMP);
            Carousel_Position = TStatus & PVConst.TLAUX_TOOL_MASK;

            if (TC_Clamped)
            { ledClamp.Set_State(LED_State.Off); }
            else { ledClamp.Set_State(LED_State.On_Blue); }

            tbTLAUXStatus.Text = string.Format("{0:X4}", TStatus);

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
            //int TLAUX_Status = KMx.GetUserData(PVConst.P_TLAUX_STATUS);
            //if(B.AnyInMask(TLAUX_Status, PVConst.TLAUX_ERROR_MASK))
            //{
            //    MessageBox.Show("Tool Changer Error! Arm not retracted!");
            //    return;
            //}
            // retract the tool arm if it is not in.
            //if(B.BitIsSet(TLAUX_Status, PVConst.TLAUX_ARM_OUT))
            //{
            //    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_ARM_IN);
            //    KMx.ExecuteProgram(2);
            //    // how to wait until it is done here?
            //    while(B.BitIsSet(TLAUX_Status, PVConst.TLAUX_ARM_OUT) == false)
            //    {
            //        TLAUX_Status = KMx.GetUserData(PVConst.P_TLAUX_STATUS);
            //    }
            //}
            // move Z to TC_Z1
            // first get the current coordinates - Absolute machine coordinate positions
            double cX, cY, cZ, cA, cB, cC;
            cX = cY = cZ = cA = cB = cC = 0;
            KMx.CoordMotion.ReadAndSyncCurPositions(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC);
            // KMx.CoordMotion.UpdateCurrentPositionsABS(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC, true);
            // get the first TC_Z1 Z position.

            double tempZ;
            if(double.TryParse(tbZ2.Text, out tempZ) == false)
            {
                tempZ = cZ;
            }
            KMx.CoordMotion.StraightFeed(2.0 ,cX, cY, tempZ, cA, cB, cC, 0, 0);
            KMx.CoordMotion.FlushSegments();
  //          KMx.CoordMotion.WaitForMoveXYZABCFinished();
            // rotate the carousel to tool number XX
            // enable and zero Spindle
            // move Spindle to SPXX
            // release the Tool Clamp - with Air
            // TLAUX Arm OUT
            // move to TC_Z2
            // Engage the Tool Clamp
            // TLAUX Arm IN
            // 

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
            double cX, cY, cZ, cA, cB, cC;
            cX = cY = cZ = cA = cB = cC = 0;
            KMx.CoordMotion.ReadAndSyncCurPositions(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC);
            // KMx.CoordMotion.UpdateCurrentPositionsABS(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC, true);
            // get the first TC_Z1 Z position.
            double tempZ;
            if (double.TryParse(tbTCH1.Text, out tempZ) == false)
            { tempZ = cZ; }
            double tempFR;
            if (double.TryParse(tbTCH1FR.Text, out tempFR) == false)
            { tempFR = 1.0; }
            KMx.CoordMotion.StraightFeed(tempFR, cX, cY, tempZ, cA, cB, cC, 0, 0);
            KMx.CoordMotion.FlushSegments();
        }

        private void btnTC_H2_Click(object sender, RoutedEventArgs e)
        {
            double cX, cY, cZ, cA, cB, cC;
            cX = cY = cZ = cA = cB = cC = 0;
            KMx.CoordMotion.ReadAndSyncCurPositions(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC);
            // KMx.CoordMotion.UpdateCurrentPositionsABS(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC, true);
            // get the first TC_Z1 Z position.
            double tempZ;
            if (double.TryParse(tbTCH2.Text, out tempZ) == false)
            { tempZ = cZ; }
            double tempFR;
            if (double.TryParse(tbTCH2FR.Text, out tempFR) == false)
            { tempFR = 1.0; }
            KMx.CoordMotion.StraightFeed(tempFR, cX, cY, tempZ, cA, cB, cC, 0, 0);
            KMx.CoordMotion.FlushSegments();
        }

        private void btnToolRel_Click(object sender, RoutedEventArgs e)
        {
            if (TC_Clamped == true)
            {
                btnToolRel.Content = "Tool Clamp";
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_RELA);
                KMx.ExecuteProgram(2);
            }
            else
            {
                btnToolRel.Content = "Tool Release";
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_GRAB);
                KMx.ExecuteProgram(2);
            }
        }

        private void btnArm_In_Click(object sender, RoutedEventArgs e)
        {
            if(TLAUX_ARM_IN == true)
            {
                btnArm_In.Content = "TC ARM IN";
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_ARM_OUT);
                KMx.ExecuteProgram(2);
            }
            else
            {
                btnArm_In.Content = "TC ARM OUT";
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_ARM_IN);
                KMx.ExecuteProgram(2);
            }
        }

        private void btnSpindle_Click(object sender, RoutedEventArgs e)
        {
            // send the spindle to XXXX
            // check that the axis is enabled and in PID mode
            if (SpindleEnabled && SpindlePID)
            {
                double SpPosition;
                if (double.TryParse(tbSPIndex.Text, out SpPosition) == false)
                {
                    SpPosition = 0;
                    tbSPIndex.Text = "0.0";
                }
                double SPRate;
                if (double.TryParse(tbSPRate.Text, out SPRate) == false)
                {
                    SPRate = AXConst.SPINDLE_HOME_RATE;
                    tbSPRate.Text = SPRate.ToString();
                }
                SPx.Velocity = SPRate;
                SPx.StartMoveTo(SpPosition);


                // wait for move to finish
                // System.Threading.Thread.Sleep(50);
                //while(true)
                //{
                //    if (SPx.MotionComplete())
                //        break;
                //    System.Threading.Thread.Sleep(20);
                //}
            }
            else
            {
                MessageBox.Show("Spindle not Enabled!");
            }

            // Move to the spindle position
            // SP.StartMoveTo(SpPosition);
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
                KMx.SetUserData(PVConst.P_NOTIFY, (T2Const.T2_SEL_TOOL | ToolNumber));
                KMx.ExecuteProgram(2);
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

            SaveCfg(CfgFName);
        }
    }
}
