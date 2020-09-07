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
using System.Windows.Shapes;
using KMotion_dotNet;
using System.IO;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace KFLOP_Test3
{
    /// <summary>
    /// Interaction logic for SetupWindow.xaml
    /// </summary>
    public partial class SetupWindow : Window
    {

        KM_MotionParams XParams;

        public SetupWindow(KM_MotionParams XKM)
        {

            XParams = XKM;
            InitializeComponent();
            GetParameters(XParams);
        }

        #region Check Boxes
        private void cbDegA_Checked(object sender, RoutedEventArgs e)
        {
            // change the headings to steps/degree. etc
            tblkCperIA.Text = "cnts/deg";
            tblkVelA.Text = "deg/sec";
            tblkAccA.Text = "deg/sec2";
        }

        private void cbDegA_Unchecked(object sender, RoutedEventArgs e)
        {
            // Change the headings to steps/inch
            tblkCperIA.Text = "cnts/in";
            tblkVelA.Text = "in/sec";
            tblkAccA.Text = "in/sec2";
        }
        private void cbDegB_Checked(object sender, RoutedEventArgs e)
        {
            // change the headings to steps/degree. etc
            tblkCperIB.Text = "cnts/deg";
            tblkVelB.Text = "deg/sec";
            tblkAccB.Text = "deg/sec2";
        }

        private void cbDegB_Unchecked(object sender, RoutedEventArgs e)
        {
            // Change the headings to steps/inch
            tblkCperIB.Text = "cnts/in";
            tblkVelB.Text = "in/sec";
            tblkAccB.Text = "in/sec2";
        }
        private void cbDegC_Checked(object sender, RoutedEventArgs e)
        {
            // change the headings to steps/degree. etc
            tblkCperIC.Text = "cnts/deg";
            tblkVelC.Text = "deg/sec";
            tblkAccC.Text = "deg/sec2";
        }

        private void cbDegC_Unchecked(object sender, RoutedEventArgs e)
        {
            // Change the headings to steps/inch
            tblkCperIC.Text = "cnts/in";
            tblkVelC.Text = "in/sec";
            tblkAccC.Text = "in/sec2";
        }
        #endregion

        #region Set and Get Parameters
        private void GetParameters(KM_MotionParams X)
        {
            // Get the parameters from the motion planner and populate the GUI
            tbBreakAngle.Text = X.BreakAngle.ToString();
            tbCollTol.Text = X.CollinearTolerance.ToString();
            tbCornerTol.Text = X.CornerTolerance.ToString();
            tbFaucetAngle.Text = X.FacetAngle.ToString();
            tbLookAhead.Text = X.TPLookahead.ToString();
            tbRadA.Text = X.RadiusA.ToString();
            tbRadB.Text = X.RadiusB.ToString();
            tbRadC.Text = X.RadiusC.ToString();
            tbAccelX.Text = X.MaxAccelX.ToString();
            tbAccelY.Text = X.MaxAccelY.ToString();
            tbAccelZ.Text = X.MaxAccelZ.ToString();
            tbAccelA.Text = X.MaxAccelA.ToString();
            tbAccelB.Text = X.MaxAccelB.ToString();
            tbAccelC.Text = X.MaxAccelC.ToString();
            tbVelX.Text = X.MaxVelX.ToString();
            tbVelY.Text = X.MaxVelY.ToString();
            tbVelZ.Text = X.MaxVelZ.ToString();
            tbCntsPerInA.Text = X.CountsPerInchA.ToString();
            tbCntsPerInB.Text = X.CountsPerInchB.ToString();
            tbCntsPerInC.Text = X.CountsPerInchC.ToString();
            tbCntsPerInX.Text = X.CountsPerInchX.ToString();
            tbCntsPerInY.Text = X.CountsPerInchY.ToString();
            tbCntsPerInZ.Text = X.CountsPerInchZ.ToString();
            cbArcSeg.IsChecked = X.ArcsToSegs;
            cbDegA.IsChecked = X.DegreesA;
            cbDegB.IsChecked = X.DegreesB;
            cbDegC.IsChecked = X.DegreesC;

        }

        private void SetParameters(KM_MotionParams X)
        {
            // Get the parameters from the GUI and put them in the motion planner
            X.BreakAngle = double.Parse(tbBreakAngle.Text);
            X.CollinearTolerance = double.Parse(tbCollTol.Text);
            X.CornerTolerance = double.Parse(tbCornerTol.Text);
            X.FacetAngle = double.Parse(tbFaucetAngle.Text);
            X.TPLookahead = double.Parse(tbLookAhead.Text);
            X.RadiusA = double.Parse(tbRadA.Text);
            X.RadiusB = double.Parse(tbRadB.Text);
            X.RadiusC = double.Parse(tbRadC.Text);
            X.MaxAccelX = double.Parse(tbAccelX.Text);
            X.MaxAccelY = double.Parse(tbAccelY.Text);
            X.MaxAccelZ = double.Parse(tbAccelZ.Text);
            X.MaxAccelA = double.Parse(tbAccelA.Text);
            X.MaxAccelB = double.Parse(tbAccelB.Text);
            X.MaxAccelC = double.Parse(tbAccelC.Text);
            X.MaxVelX = double.Parse(tbVelX.Text);
            X.MaxVelY = double.Parse(tbVelY.Text);
            X.MaxVelZ = double.Parse(tbVelZ.Text);
            X.CountsPerInchA = double.Parse(tbCntsPerInA.Text);
            X.CountsPerInchB = double.Parse(tbCntsPerInB.Text);
            X.CountsPerInchC = double.Parse(tbCntsPerInC.Text);
            X.CountsPerInchX = double.Parse(tbCntsPerInX.Text);
            X.CountsPerInchY = double.Parse(tbCntsPerInY.Text);
            X.CountsPerInchZ = double.Parse(tbCntsPerInZ.Text);
            X.ArcsToSegs = (bool)cbArcSeg.IsChecked;
            X.DegreesA = (bool)cbDegA.IsChecked;
            X.DegreesB = (bool)cbDegB.IsChecked;
            X.DegreesC = (bool)cbDegC.IsChecked;
        }
        #endregion

        private void btnSaveJ_Click(object sender, RoutedEventArgs e)
        {
            SetParameters(XParams);
            var saveFile = new SaveFileDialog();
            if(saveFile.ShowDialog() == true)
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamWriter sw = new StreamWriter(saveFile.FileName);
                JsonTextWriter Jwrite = new JsonTextWriter(sw);
                Jser.NullValueHandling = NullValueHandling.Ignore;
                Jser.Formatting = Newtonsoft.Json.Formatting.Indented;
                Jser.Serialize(Jwrite, XParams);
                sw.Close();
            }

        }
        private void btnSetupCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            SetParameters(XParams);
            this.Close();
        }
    }
}
