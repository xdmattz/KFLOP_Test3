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
    /// Interaction logic for ToolChangerPanel.xaml
    /// </summary>
    public partial class ToolChangerPanel : UserControl
    {

        // a copy of the KM controller 
        private KM_Controller KMx { get; set; }
        private BitOps B;

        public ToolChangerPanel(ref KM_Controller X)
        {
            InitializeComponent();
            KMx = X;    // point to the KM controller - this exposes all the KFLOP .net library functions
            B = new BitOps();

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
            //    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TO0L_ARM_IN);
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
    }
}
