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
    /// Interaction logic for SpindleUpdate.xaml
    /// </summary>
    public partial class SpindleUpdate : Window
    {
        public int value { get; set; }

        public SpindleUpdate(int toolNum)
        {
            InitializeComponent();
            value = toolNum;
            tbNewToolNumber.Text = String.Format("{0}", value);
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            value = 0;
            this.DialogResult = true;
        }

        private void btnInsert_Click(object sender, RoutedEventArgs e)
        {
            int tValue = 0;
            if(int.TryParse(tbNewToolNumber.Text, out tValue))
            {
                // true
                value = tValue;
                this.DialogResult = true;
            }
            else
            {
                // false - bad number
                MessageBox.Show("Invalid Number\rEnter a valid Tool Number\ror Cancel");
            }
        }

        private void bntCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            tbNewToolNumber.SelectAll();
            tbNewToolNumber.Focus();
        }
    }
}
