﻿using System;
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

        public ToolChangerPanel(ref KM_Controller X, ref KM_Axis SP)
        {
            InitializeComponent();
            KMx = X;    // point to the KM controller - this exposes all the KFLOP .net library functions
            SPx = SP;   // point to the Spindle Axis for fine control
            B = new BitOps();
            ledClamp.Set_State(LED_State.Off);
            ledClamp.Set_Label("Tool Clamp");

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

            tbSPCPU.Text = String.Format("{0:F}", SPx.CPU);



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

            tbX1.Text = string.Format("{0:F4}", cX);
            tbY1.Text = string.Format("{0:F4}", cY);
            tbZ1.Text = string.Format("{0:F4}", cZ);
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

            tbX1.Text = string.Format("{0:F4}", cX);
            tbY1.Text = string.Format("{0:F4}", cY);
            tbZ1.Text = string.Format("{0:F4}", cZ);
            double tempZ;
            if (double.TryParse(tbZ2.Text, out tempZ) == false)
            {
                tempZ = cZ;
            }
            KMx.CoordMotion.StraightFeed(2.0, cX, cY, tempZ, cA, cB, cC, 0, 0);
            KMx.CoordMotion.FlushSegments();
        }

        private void btnTC_H2_Click(object sender, RoutedEventArgs e)
        {
            double cX, cY, cZ, cA, cB, cC;
            cX = cY = cZ = cA = cB = cC = 0;
            KMx.CoordMotion.ReadAndSyncCurPositions(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC);
            // KMx.CoordMotion.UpdateCurrentPositionsABS(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC, true);
            // get the first TC_Z1 Z position.

            tbX1.Text = string.Format("{0:F4}", cX);
            tbY1.Text = string.Format("{0:F4}", cY);
            tbZ1.Text = string.Format("{0:F4}", cZ);
            double tempZ;
            if (double.TryParse(tbZ3.Text, out tempZ) == false)
            {
                tempZ = cZ;
            }
            KMx.CoordMotion.StraightFeed(2.0, cX, cY, tempZ, cA, cB, cC, 0, 0);
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
                if (double.TryParse(tbSPtoPos.Text, out SpPosition) == false)
                {
                    SpPosition = 0;
                    tbSPtoPos.Text = "0.0";
                }
                double JogRate;
                if (double.TryParse(tbSPJogRate.Text, out JogRate) == false)
                {
                    JogRate = AXConst.SPINDLE_HOME_RATE;
                    tbSPJogRate.Text = JogRate.ToString();
                }
                MessageBox.Show(String.Format("SP Move at {0} to {1}", JogRate, SpPosition));
                SPx.StartMoveTo(SpPosition);
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
    }
}