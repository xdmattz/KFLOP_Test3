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

namespace KFLOP_Test3
{
    /// <summary>
    /// Interaction logic for ResumeDialog.xaml
    /// </summary>
    public partial class ResumeDialog : Window
    {
        public double SafeZ { set; get; }
        public bool DoSafeZ { set; get; }
        public bool SafeRelAbs { set; get; }
        public bool TraverseXY { set; get; }
        public double SafeX { set; get; }
        public double SafeY { set; get; }
        public bool SafeSpindleStart { set; get; }
        public bool SafeSpindleCWCCW { set; get; }
        public double SafeSpindleSpeed { set; get; }
        public bool DoSafeFeedZ { set; get; }
        public double SafeFeedZ { set; get; }
        public double SafeFeedZRate { set; get; }
        public bool RestoreFeedRate { set; get; }
        public double ResumeFeedRate { set; get; }
        public bool Metric { set; get; }



        public ResumeDialog()
        {
            InitializeComponent();
            
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // initialize all the values when the window is loaded

            cbSafeZHeight.IsChecked = DoSafeZ;
            tbSafeZHeight.Text = string.Format("{0:F4}", SafeZ);
            rbRelMove.IsChecked = !SafeRelAbs;
            rbAbsMove.IsChecked = SafeRelAbs;
            cbXYTraverse.IsChecked = TraverseXY;
            tbXHalted.Text = string.Format("{0:F4}", SafeX);
            tbYHalted.Text = string.Format("{0:F4}", SafeY);
            cbStartSpinle.IsChecked = SafeSpindleStart;
            rbCW.IsChecked = SafeSpindleCWCCW;
            rbCCW.IsChecked = !SafeSpindleCWCCW;
            tbSpindleSpeed.Text = string.Format("{0:F4}", SafeSpindleSpeed);
            cbZFeedTo.IsChecked = DoSafeFeedZ;
            tbZFeed.Text = string.Format("{0:F4}", SafeFeedZ);
            tbZFeedRate.Text = string.Format("{0:F4}", SafeFeedZRate);
            cbRestoreFR.IsChecked = RestoreFeedRate;
            tbRestoreFR.Text = string.Format("{0:F4}", ResumeFeedRate);
            if(Metric)
            {
                lbTraverseUnits.Content = "mm";
                lbZHUnits.Content = "Z Height mm";
                lbZFR.Content = "Feed Rate mm/min";
                lbFR.Content = "Feed Rate mm/min";
            } else
            {
                lbTraverseUnits.Content = "inches";
                lbZHUnits.Content = "Z Height inches";
                lbZFR.Content = "Feed Rate in/min";
                lbFR.Content = "Feed Rate in/min";
            }
        }

        private void btnExecute_Click(object sender, RoutedEventArgs e)
        {
            // fill all the variables from the fields
            DoSafeZ = (bool) cbSafeZHeight.IsChecked;
            SafeZ = double.Parse(tbSafeZHeight.Text);
            SafeRelAbs = (bool) rbAbsMove.IsChecked;
            TraverseXY = (bool) cbXYTraverse.IsChecked;
            SafeX = double.Parse(tbXHalted.Text);
            SafeY = double.Parse(tbYHalted.Text);
            SafeSpindleStart = (bool) cbStartSpinle.IsChecked;
            SafeSpindleCWCCW = (bool) rbCW.IsChecked;
            SafeSpindleSpeed = double.Parse(tbSpindleSpeed.Text);
            DoSafeFeedZ = (bool) cbZFeedTo.IsChecked;
            SafeFeedZ = double.Parse(tbZFeed.Text);
            SafeFeedZRate = double.Parse(tbZFeedRate.Text);
            RestoreFeedRate = (bool) cbRestoreFR.IsChecked;
            ResumeFeedRate = double.Parse(tbRestoreFR.Text);

            this.DialogResult = true;
            Close();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }
    }
}
