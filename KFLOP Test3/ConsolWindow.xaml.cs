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
    /// Interaction logic for ConsolWindow.xaml
    /// </summary>
    public partial class ConsolWindow : Window
    {
        const int MaxLines = 1000;
        public ConsolWindow()
        {
            InitializeComponent();
        }

        public void AddSomeText(string InStr)
        {
            if(ConsoleText.LineCount > MaxLines)
            {
                ConsoleText.Text = InStr; // clear the lines 
            }
            else
            {
                ConsoleText.AppendText(InStr);
            }
            ConsoleLines.Text = ConsoleText.LineCount.ToString();
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            ConsoleText.Text = "";  // clear the text from the text box.
            ConsoleLines.Text = ConsoleText.LineCount.ToString();
        }

        private void btnHide_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
    }
}
