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
        

        public StatusPanel(ref KM_Controller X)
        {
            InitializeComponent();
            KMx = X;
            B = new BitOps();
          //  LED1.LED_Label = "test lable1";
          //  LED1.LED_Image.Source = new BitmapImage(new Uri("Small LED Off.png"));

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

        private void btnHomeAll_Click(object sender, RoutedEventArgs e)
        {
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
    }
}
