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
    /// Interaction logic for LogSliderV.xaml
    /// </summary>
    public partial class LogSliderV : UserControl
    {
        public delegate void SliderCallback(double x);

        public event SliderCallback SliderClickedCallback;


        private double _slvalue;
        public double slvalue
        {
            get
            {
                return _slvalue;
            }
            set
            {
                _slvalue = value;
                sl1.Value = log2lin(_slvalue);
            }
        }

        public LogSliderV()
        {
            InitializeComponent();
        }

        private void sl1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            tbValue.Text = String.Format("{0:F2}", lin2log(sl1.Value));
        }

        private double log2lin(double x)
        {
            return (Math.Log((((x - 0.1) * 8) + 1), 2.0) * 25.0);
        }
        private double lin2log(double x)
        {
            return ((Math.Pow(2.0, (x * 0.01) * 4) - 1) * 0.125) + 0.1;
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            tbValue.Text = "1.0";
            _slvalue = 1.0;
            sl1.Value = log2lin(_slvalue);
            // call the callback delegate with the value
            SliderClickedCallback?.Invoke(_slvalue);
        }

        private void sl1_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _slvalue = lin2log(sl1.Value);  // get the value of the slider - converted to log scale
            // call the callback delegate with the value
            SliderClickedCallback?.Invoke(_slvalue);
        }
    }
}
