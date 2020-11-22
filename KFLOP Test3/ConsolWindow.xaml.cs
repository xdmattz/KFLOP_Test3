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

        public delegate void SendCallback(string msg);

        public event SendCallback SendButtonClickedCallback;

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

        private void btnCCmdSend_Click(object sender, RoutedEventArgs e)
        {
            // call the delegated event handler - only if it has been properly initalized.
            SendButtonClickedCallback?.Invoke(tbConsoleCommand.Text);
        }
    }
}
