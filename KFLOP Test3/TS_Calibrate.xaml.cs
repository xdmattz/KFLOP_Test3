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
    /// Interaction logic for TS_Calibrate.xaml
    /// </summary>
    public partial class TS_Calibrate : Window
    {
        public double Value { get; set; }
        public int Cycles { get; set; }

        public TS_Calibrate()
        {
            InitializeComponent();
            Value = 0;
            Cycles = 1;
            tbGaugeLen.Text = String.Format("{0:F4}", Value);
            tbIterations.Text = $"{Cycles}";
        }

        public TS_Calibrate(double gauge, int tries )
        {
            InitializeComponent();
            Value = gauge;
            Cycles = tries;
            tbGaugeLen.Text = String.Format("{0:F4}", Value);
            tbIterations.Text = $"{Cycles}";
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            // check value. Must be a positive number less than 10

            double x;
            int i;
            if (double.TryParse(tbGaugeLen.Text, out x))
            {
                if((x > 10.0) || (x < 0.1))
                {
                    MessageBox.Show("Gauge Height out of range!\nRange is 0.1 - 10.0\nCorrect it or Cancel");
                    return;
                }
                Value = x;
                if(int.TryParse(tbIterations.Text, out i))
                {
                    if ((i > 0) && (i < 101)) // 1 to 100 cycles 
                    {
                        Cycles = i;
                        this.DialogResult = true;
                    }
                    else
                    {
                        MessageBox.Show("Number of cycles is out of range 1 - 100");
                    }
                }
                
            } else
            {
                MessageBox.Show("Gauge Height Not a Number!\nRange is 0.1 - 10.0\nCorrect it or Cancel");
                return;
            }
        }

        private void bntCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // this will select and highlight the textbox when the window is opened
            tbGaugeLen.SelectAll();
            tbGaugeLen.Focus();
        }
    }
}
