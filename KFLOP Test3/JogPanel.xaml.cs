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
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class JogPanel : UserControl
    {
        private bool Jogging = false;
        private bool JogEnabled = false;
        private bool KbdJog = false;

        private int JAxis_X;
        private int JAxis_Y;
        private int JAxis_Z;
        private int JAxis_A;
        private int JAxis_B;
        private int JAxis_C;

        // a copy of the KM controller 
        private KM_Controller KMx { get; set; }

        public JogPanel(ref KM_Controller X)
        {
            InitializeComponent();
            KMx = X;
        }



        #region Individudal Jog Buttons 

        #region X Axis
        private void btnXp_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_X, true);
        }

        private void btnXp_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_X, true);
        }

        private void btnXn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_X, false);
        }

        private void btnXn_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_X, false);
        }
        #endregion

        #region Y Axis
        private void btnYp_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_Y, true);
        }

        private void btnYp_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_Y, true);
        }

        private void btnYn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_Y, false);
        }

        private void btnYn_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_Y, false);
        }
        #endregion

        #region Z Axis
        private void btnZp_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_Z, true);
        }

        private void btnZp_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_Z, true);
        }

        private void btnZn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_Z, false);
        }

        private void btnZn_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_Z, false);
        }
        #endregion

        #region A Axis
        private void btnAp_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_A, true);
        }

        private void btnAp_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_A, true);
        }

        private void btnAn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_A, false);
        }

        private void btnAn_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_A, false);
        }
        #endregion

        #region B Axis
        private void btnBp_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_B, true);
        }

        private void btnBp_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_B, true);
        }

        private void btnBn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_B, false);
        }

        private void btnBn_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_B, false);
        }
        #endregion

        #region C Axis
        private void btnCp_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_C, true);
        }

        private void btnCp_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_C, true);
        }

        private void btnCn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            JogActionStart(JAxis_C, false);
        }

        private void btnCn_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            JogActionStop(JAxis_C, false);
        }
        #endregion

        #endregion

        // Determine whether it is a step or Jog action.
        // 
        private void JogActionStart(int Axis, bool Direction)
        {
            String Dir;
            if(Direction)
            { Dir = "+"; }
            else
            { Dir = "-"; }
            TestLabel.Content = String.Format("Axis {0} {1} Mouse Down", Dir, Axis);
            JogWriteLineException(String.Format("Jog{0}={1}{2}",Axis,Dir,20000));

            Jogging = true;
        }
        
        private void JogActionStop(int Axis, bool Direction)
        {
            String Dir;
            if (Direction)
            { Dir = "+"; }
            else
            { Dir = "-"; }
            TestLabel.Content = String.Format("Axis {0} {1} Mouse Up", Dir, Axis);
            // Jog at 0
            JogWriteLineException(String.Format("Jog{0}=0", Axis));
            Jogging = false;
        }

        private void JogWriteLineException(String s)
        {
            try
            {
                KMx.WriteLine(s);
            }
            catch (DMException ex) // In case disconnect in the middle of reading status
            {
                MessageBox.Show(ex.InnerException.Message);
            }
        }

        #region Jog Action Radio Buttons
        private void JogActionRB_Checked(object sender, RoutedEventArgs e)
        {
            var rbutton = sender as RadioButton;
            String btnText;

            btnText = rbutton.Content.ToString();
            
            if (btnText == "Continuous")
            {
                MessageBox.Show("Continuous Jog");
            }
            else
            {
                double stepsize;
                if (double.TryParse(btnText, out stepsize))
                {
                    MessageBox.Show(String.Format("Stepping at {0}", stepsize));
                }

            }
        }
        #endregion

        #region Enable and Disable the jog buttons
        // this will make the jog buttons for the enabled axis visible.
        public void JogAxisVisible()
        {
            // get the assigned axis definitions -1 indicates that an axis is not enabled.
            KMx.CoordMotion.GetAxisDefinitions(ref JAxis_X, ref JAxis_Y, ref JAxis_Z, ref JAxis_A, ref JAxis_B, ref JAxis_C);
            if(JAxis_X == -1)
            {
                btnXp.Visibility = Visibility.Collapsed;
                btnXn.Visibility = Visibility.Collapsed;
            }
            if (JAxis_Y == -1)
            {
                btnYp.Visibility = Visibility.Collapsed;
                btnYn.Visibility = Visibility.Collapsed;
            }
            if (JAxis_Z == -1)
            {
                btnZp.Visibility = Visibility.Collapsed;
                btnZn.Visibility = Visibility.Collapsed;
            }
            if (JAxis_A == -1)
            {
                btnAp.Visibility = Visibility.Collapsed;
                btnAn.Visibility = Visibility.Collapsed;
            }
            if (JAxis_B == -1)
            {
                btnBp.Visibility = Visibility.Collapsed;
                btnBn.Visibility = Visibility.Collapsed;
            }
            if (JAxis_C == -1)
            {
                btnCp.Visibility = Visibility.Collapsed;
                btnCn.Visibility = Visibility.Collapsed;
            }
        }

        public void EnJog(bool En)
        {
            if(JAxis_X != -1)
            {
                btnXp.IsEnabled = En;
                btnXn.IsEnabled = En;
            }
            if (JAxis_Y != -1)
            {
                btnYp.IsEnabled = En;
                btnYn.IsEnabled = En;
            }
            if (JAxis_Z != -1)
            {
                btnZp.IsEnabled = En;
                btnZn.IsEnabled = En;
            }
            if (JAxis_A != -1)
            {
                btnAp.IsEnabled = En;
                btnAn.IsEnabled = En;
            }
            if (JAxis_B != -1)
            {
                btnBp.IsEnabled = En;
                btnBn.IsEnabled = En;
            }
            if (JAxis_C != -1)
            {
                btnCp.IsEnabled = En;
                btnCn.IsEnabled = En;
            }
        }

        public void EnableJog()
        {
            JogAxisVisible();
            EnJog(true);
        }
        public void DisableJog()
        {
            EnJog(false);
        }
        #endregion

        private void btnJogInit_Click(object sender, RoutedEventArgs e)
        {
            // do everything here to initialize the Jog panel
            EnableJog();
        }

        // enable / disable keyboard jogging
        private void btnKbdJog_Click(object sender, RoutedEventArgs e)
        {
            if (KbdJog) //  If keyboard jog is true then disable the keyboard jogging
            {
                KbdJog = false;
                // change the button background color back to normal
                btnKbdJog.Background = new SolidColorBrush(Colors.LightGray);
                
            }
            else
            {
                KbdJog = true;
                btnKbdJog.Background = new SolidColorBrush(Colors.Red);
            }
        }

        private void RadBtnStackPanel_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(KbdJog)  // only do this if keyboard jogging is enabled
            {
                switch(e.Key)
                {
                    case Key.Left:
                        JogActionStart(JAxis_X, false);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        JogActionStart(JAxis_X, true);
                        e.Handled = true;
                        break;
                    case Key.Up:
                        JogActionStart(JAxis_Y, true);
                        e.Handled = true;
                        break;
                    case Key.Down:
                        JogActionStart(JAxis_Y, false);
                        e.Handled = true;
                        break;
                    case Key.PageUp:
                        JogActionStart(JAxis_Z, true);
                        e.Handled = true;
                        break;
                    case Key.PageDown:
                        JogActionStart(JAxis_Z, false);
                        e.Handled = true;
                        break;
                    default: break;
                }
            }
        }

        private void RadBtnStackPanel_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if(KbdJog)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        JogActionStop(JAxis_X, true);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        JogActionStop(JAxis_X, true);
                        e.Handled = true;
                        break;
                    case Key.Up:
                        JogActionStop(JAxis_Y, true);
                        e.Handled = true;
                        break;
                    case Key.Down:
                        JogActionStop(JAxis_Y, true);
                        e.Handled = true;
                        break;
                    case Key.PageUp:
                        JogActionStop(JAxis_Z, true);
                        e.Handled = true;
                        break;
                    case Key.PageDown:
                        JogActionStop(JAxis_Z, true);
                        e.Handled = true;
                        break;
                    default: break;
                }
            }
        }
    }
}
