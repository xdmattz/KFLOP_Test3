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

using KMotion_dotNet;

namespace KFLOP_Test3
{
    /// <summary>
    /// Interaction logic for StatusPanel.xaml
    /// </summary>
    public partial class StatusPanel : UserControl
    {

        // a copy of the KM controller 
        private KM_Controller KMx { get; set; }

        BitOps B;

        // Status flags used in check machine status
        static int Prev_KanalogInputs;    // Kanalog Inputs
        static int Prev_KanalogOutputs;   // Kanalog Outputs
        static int Prev_KonnectIO;    // Connect Inputs

        static int LED_Count;


        public StatusPanel(ref KM_Controller X)
        {
            InitializeComponent();
            KMx = X;
            B = new BitOps();
            //  LED1.LED_Label = "test lable1";
            //  LED1.LED_Image.Source = new BitmapImage(new Uri("Small LED Off.png"));
            LED_Count = 0;

            // initialize the status LEDs
            ESRelay_LED.Set_Label("EStop EN");
            EStop_LED.Set_Label("ESTOP");
            SEnable_LED.Set_Label("Spindle EN");
            SFault_LED.Set_Label("Fault");
            PwrMod_LED.Set_Label("Pwr Module EN");
            AxisFault_LED.Set_Label("Fault");
            ZBrake_LED.Set_Label("Z-Brake");

            ToolRel_LED.Set_Label("Tool Release");
            xToolRel_LED.Set_Label("");

            Oiler_LED.Set_Label("Oiler");
            OilLevel_LED.Set_Label("Level");
            AirPres_LED.Set_Label("Air Pressure");
            DoorFan_LED.Set_Label("Door Fan");
            FloodMotor_LED.Set_Label("Flood Motor");


            ESRelay_LED.Set_State(LED_State.Off);
            EStop_LED.Set_State(LED_State.Off);
            SEnable_LED.Set_State(LED_State.Off);
            SFault_LED.Set_State(LED_State.Off);
            PwrMod_LED.Set_State(LED_State.Off);
            AxisFault_LED.Set_State(LED_State.Off);
            ZBrake_LED.Set_State(LED_State.Off);

            ToolRel_LED.Set_State(LED_State.Off);
            xToolRel_LED.Set_State(LED_State.Off);

            Oiler_LED.Set_State(LED_State.Off);
            OilLevel_LED.Set_State(LED_State.Off);
            AirPres_LED.Set_State(LED_State.Off);
            DoorFan_LED.Set_State(LED_State.Off);
            FloodMotor_LED.Set_State(LED_State.Off);


        }

        public void CheckHome(ref KM_MainStatus MStat)
        {
            int status = MStat.PC_comm[CSConst.P_STATUS];

            tbStatus1.Text = status.ToString("X8");
            // check the status bits for the Home Buttons
            // set the color of the home buttons - red = not yet homed, green = homed
            if(B.BitIsSet(status, PVConst.SB_X_HOME))
            {
                btnHomeX.Background = new SolidColorBrush(Colors.Red);
            }
            else
            {
                btnHomeX.Background = new SolidColorBrush(Colors.LightGreen);
            }
            if (B.BitIsSet(status, PVConst.SB_Y_HOME))
            {
                btnHomeY.Background = new SolidColorBrush(Colors.Red);
            }
            else
            {
                btnHomeY.Background = new SolidColorBrush(Colors.LightGreen);
            }
            if (B.BitIsSet(status, PVConst.SB_Z_HOME))
            {
                btnHomeZ.Background = new SolidColorBrush(Colors.Red);
            }
            else
            {
                btnHomeZ.Background = new SolidColorBrush(Colors.LightGreen);
            }
            if (B.BitIsSet(status, PVConst.SB_TLAUX_HOME))
            {
                btnHomeTC.Background = new SolidColorBrush(Colors.Red);
            }
            else
            {
                btnHomeTC.Background = new SolidColorBrush(Colors.LightGreen);
            }
            if (B.BitIsSet(status, PVConst.SB_SPIN_HOME))
            {
                btnHomeS.Background = new SolidColorBrush(Colors.Red);
            }
            else
            {
                btnHomeS.Background = new SolidColorBrush(Colors.LightGreen);
            }
        }

        public void CheckLimit(ref KM_MainStatus MStat)
        {
            int status = MStat.PC_comm[CSConst.P_STATUS];
            // check the status bits for the Limit switches
   //         if (B.AllInMask(status, PVConst.SB_LIMIT_MASK) == false) // if anyone is not set then on a limit switch
   //         {
                if (B.BitIsSet(status, PVConst.SB_X_LIMIT))
                { cbLimX.IsChecked = false; }
                else { cbLimX.IsChecked = true; }

                if (B.BitIsSet(status, PVConst.SB_Y_LIMIT))
                { cbLimY.IsChecked = false; }
                else { cbLimY.IsChecked = true; }

                if (B.BitIsSet(status, PVConst.SB_Z_LIMIT))
                { cbLimZ.IsChecked = false; }
                else { cbLimZ.IsChecked = true; }
   //         }

        }

        public void CheckMachineStatus(ref KM_MainStatus MStat)
        {
            tbKanIn.Text = string.Format("{0:X8}", MStat.KanalogBitsStateInputs);
            tbKanOut.Text = string.Format("{0:X8}", MStat.KanalogBitsStateOutputs);
            tbKonnIO.Text = string.Format("{0:X8}", MStat.VirtualBitsEx0);

            if ((MStat.KanalogBitsStateInputs != Prev_KanalogInputs) || (MStat.KanalogBitsStateOutputs != Prev_KanalogOutputs))
            {
                Prev_KanalogInputs = MStat.KanalogBitsStateInputs;
                Prev_KanalogOutputs = MStat.KanalogBitsStateOutputs;

                // process all the LEDs on the Kanalog inputs/outputs
                // EStop Relay
                if ((Prev_KanalogOutputs & IOConst.ESTOP_RELAY_MASK) == IOConst.ESTOP_RELAY_MASK)
                { ESRelay_LED.Set_State(LED_State.On_Green); }
                else { ESRelay_LED.Set_State(LED_State.Off); }
                // EStop State
                if ((Prev_KanalogInputs & IOConst.ESTOP_MASK) == IOConst.ESTOP_MASK)
                { EStop_LED.Set_State(LED_State.On_Red); }
                else { EStop_LED.Set_State(LED_State.Off); }
                // Spindle Enable
                if ((Prev_KanalogOutputs & IOConst.SPINDLE_ENABLE_MASK) == IOConst.SPINDLE_ENABLE_MASK)
                { SEnable_LED.Set_State(LED_State.On_Green); }
                else { SEnable_LED.Set_State(LED_State.Off); }
                // Spindle Fault
                if ((Prev_KanalogInputs & IOConst.SPINDLEF_FAULT_MASK) == 0)    // spindle fault: 1 = OK, 0 = Fault.
                { SFault_LED.Set_State(LED_State.On_Red); }
                else { SFault_LED.Set_State(LED_State.Off); }
                // Power Module Enable
                if ((Prev_KanalogInputs & IOConst.POWER_MODULE_READY_MASK) == IOConst.POWER_MODULE_READY_MASK)
                { PwrMod_LED.Set_State(LED_State.On_Green); }
                else { PwrMod_LED.Set_State(LED_State.Off); }
                // Power Module Fault
                if ((Prev_KanalogInputs & IOConst.AXIS_FAULT_MASK) == 0)    // Power Module fault: 1 = OK, 0 = Fault.
                { AxisFault_LED.Set_State(LED_State.On_Red); }
                else { AxisFault_LED.Set_State(LED_State.Off); }
                // Z Brake
                if ((Prev_KanalogOutputs & IOConst.Z_BRAKE_MASK) == IOConst.Z_BRAKE_MASK)  // Brake is on when not energized 
                { ZBrake_LED.Set_State(LED_State.On_Green); }
                else { ZBrake_LED.Set_State(LED_State.On_Red); }
                // Tool Release
                if ((Prev_KanalogInputs & IOConst.TOOL_RELEASE_MASK) == IOConst.TOOL_RELEASE_MASK)   
                { ToolRel_LED.Set_State(LED_State.On_Blue); }
                else { ToolRel_LED.Set_State(LED_State.Off); }
                // Oiler
                if ((Prev_KanalogOutputs & IOConst.OIL_LUB_MASK) == IOConst.OIL_LUB_MASK)
                { Oiler_LED.Set_State(LED_State.On_Green); }
                else { Oiler_LED.Set_State(LED_State.Off); }
                // Electrical Cabinet Door Fan
                if ((Prev_KanalogOutputs & IOConst.DOOR_FAN_MASK) == IOConst.DOOR_FAN_MASK)
                { DoorFan_LED.Set_State(LED_State.On_Green); }
                else { DoorFan_LED.Set_State(LED_State.Off); }
                // Flood Coolant Motor
                if ((Prev_KanalogOutputs & IOConst.FLOOD_MOTOR_MASK) == IOConst.FLOOD_MOTOR_MASK)
                { FloodMotor_LED.Set_State(LED_State.On_Green); }
                else { FloodMotor_LED.Set_State(LED_State.Off); }


            }

            if((MStat.VirtualBitsEx0 & IOConst.KON_STATUS_MASK) != Prev_KonnectIO)
            {
                Prev_KonnectIO = (MStat.VirtualBitsEx0 & IOConst.KON_STATUS_MASK);
                // process all the LEDs on the Konnect IO
                // Oil Level - Yellow is OK, Red is Low
                if ((Prev_KonnectIO & IOConst.LUBE_MON_MASK) == IOConst.LUBE_MON_MASK)
                { OilLevel_LED.Set_State(LED_State.On_Yellow); }
                else { OilLevel_LED.Set_State(LED_State.On_Red); }
                // Air Pressure Monitor
                if ((Prev_KonnectIO & IOConst.AIR_MON_MASK) == IOConst.AIR_MON_MASK)
                { AirPres_LED.Set_State(LED_State.On_Yellow); }
                else { AirPres_LED.Set_State(LED_State.On_Red); }

            }
            // check the machine status for things like the 
            // Konnect
            // Oiler monitor
            // Air monitor
            // 
        }


        #region Homing Buttons
        private void btnHomeX_Click(object sender, RoutedEventArgs e)
        {
            Homing(T2Const.T2_HOME_X);
        }

        private void btnHomeY_Click(object sender, RoutedEventArgs e)
        {
            Homing(T2Const.T2_HOME_Y);
        }

        private void btnHomeZ_Click(object sender, RoutedEventArgs e)
        {
            Homing(T2Const.T2_HOME_Z);
        }

        private void btnHomeS_Click(object sender, RoutedEventArgs e)
        {
            Homing(T2Const.T2_HOME_SPINDLE);
        }

        private void btnHomeTC_Click(object sender, RoutedEventArgs e)
        {
            Homing(T2Const.T2_TOOL_HOME);
        }

        private void btnHomeAll_Click(object sender, RoutedEventArgs e)
        {
            Homing(T2Const.T2_TOOL_HOME);
            System.Threading.Thread.Sleep(50);
            Homing(T2Const.T2_HOME_ALL);
            
        }



        private void Homing(int AxisCmd)
        {
            int count = 0;
            // put the correct argument into the persist variable and run thread 2
            while (KMx.ThreadExecuting(2))
            {
                // DANGEROUS CODE!!!!! 
                System.Threading.Thread.Sleep(50);
                if (count++ > 10)
                {
                    MessageBox.Show($"Cannot Home Axis {AxisCmd}!");
                    return;
                }
            }
            KMx.SetUserData(PVConst.P_NOTIFY, AxisCmd);
            KMx.ExecuteProgram(2);
        }

        #endregion

        #region Limit Swith Buttons
        private void UnLimit(int AxisCmd)
        {
            int count = 0;
            // put the correct argument into the persist variable and run thread 2
            while (KMx.ThreadExecuting(2))
            {
                // DANGEROUS CODE!!!!! 
                System.Threading.Thread.Sleep(50);
                if (count++ > 10)
                {
                    MessageBox.Show($"Cannot Un-Limit Axis {AxisCmd}!");
                    return;
                }
            }
            KMx.SetUserData(PVConst.P_NOTIFY, AxisCmd);
            KMx.ExecuteProgram(2);
        }

        private void btnLimZp_Click(object sender, RoutedEventArgs e)
        {
            UnLimit(T2Const.T2_LIM_ZP);
        }

        private void btnLimZn_Click(object sender, RoutedEventArgs e)
        {
            UnLimit(T2Const.T2_LIM_ZN);
        }

        private void btnLimYp_Click(object sender, RoutedEventArgs e)
        {
            UnLimit(T2Const.T2_LIM_YP);
        }

        private void btnLimYn_Click(object sender, RoutedEventArgs e)
        {
            UnLimit(T2Const.T2_LIM_YN);
        }

        private void btnLimXp_Click(object sender, RoutedEventArgs e)
        {
            UnLimit(T2Const.T2_LIM_XP);
        }

        private void btnLimXn_Click(object sender, RoutedEventArgs e)
        {
            UnLimit(T2Const.T2_LIM_XN);
        }
        #endregion

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            switch (LED_Count)
            {
                case 0:
                    EStop_LED.Set_State(LED_State.On_Red);
                    LED_Count++;  break;
                case 1:
                    EStop_LED.Set_State(LED_State.On_Blue);
                    LED_Count++; break;
                case 2:
                    EStop_LED.Set_State(LED_State.On_Green);
                    LED_Count++; break;
                case 3:
                    EStop_LED.Set_State(LED_State.On_Yellow);
                    LED_Count++; break;
                case 4:
                    EStop_LED.Set_State(LED_State.Off);
                    LED_Count = 0; break;
                default: LED_Count = 0; break;
            }

        }


    }
}
