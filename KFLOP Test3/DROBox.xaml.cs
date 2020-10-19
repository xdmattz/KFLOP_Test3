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

namespace KFLOP_Test3
{
    /// <summary>
    /// Interaction logic for DROBox.xaml
    /// </summary>
    public partial class DROBox : UserControl
    {

        public int axis { set; get; }

        public delegate void ZeroCallback(int axis, double val);

        public event ZeroCallback ZeroButtonClickedCallback;


        public DROBox()
        {
            InitializeComponent();
       }

        public void SetBigColor(SolidColorBrush b)
        {
            AxisName.Foreground = b;
            BigAxisValue.Foreground = b;
        }

        public void SetBigValue(double value)
        {
            BigAxisValue.Text = String.Format("{0:F4}", value);
        }

        public void SetMachValue(double value)
        {
            tbMachValue.Text = String.Format("Mach: {0:F4}", value);
        }

        public void SetOffsetValue(double value)
        {
            tbOffsetValue.Text = String.Format("Off: {0:F4}", value);
        }

        public void SetUnits(int units)
        {
            if (units == 1)
            {
                tbUnits.Text = "inch";
            }
            else if(units == 2)
            {
                tbUnits.Text = "mm";
            }
            else
            {
                tbUnits.Text = "";
            }
        }

        private void Zero_Click(object sender, RoutedEventArgs e)
        {
            // call the delegated event handler
            ZeroButtonClickedCallback?.Invoke(axis, 0.0);
        }

        private void Set_Click(object sender, RoutedEventArgs e)
        {
            // open the offset dialog box
            OffsetInputWindow OffsetInput = new OffsetInputWindow();
            if(OffsetInput.ShowDialog() == true)
            {
                // call the delegated event handler - only if it has been properly initalized.
                ZeroButtonClickedCallback?.Invoke(axis, OffsetInput.value);
            }
        }
    }
}
