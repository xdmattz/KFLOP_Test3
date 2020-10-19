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
    /// Interaction logic for OffsetInputWindow.xaml
    /// </summary>
    public partial class OffsetInputWindow : Window
    {
        public double value { get; set; }

        public OffsetInputWindow()
        {
            InitializeComponent();
            value = 0;
            tbValue.Text = String.Format("{0:F4}", value);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                value = double.Parse(tbValue.Text);
            }catch
            {
                this.DialogResult = false;
            }
            this.DialogResult = true;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            tbValue.SelectAll();
            tbValue.Focus();
        }
    }
}
