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
    /// Interaction logic for ButtonGrid.xaml
    /// </summary>
    public partial class ButtonGrid : UserControl
    {
        // a copy  of the KM Controler
        private KM_Controller sp { get; set; }

        static bool xEnabled = true;
        static int fixture;

        public ButtonGrid(ref KM_Controller X)
        {
            sp = X;
            InitializeComponent();
        }

        private void ButtonGridClick(object sender, RoutedEventArgs e)
        {
            // can I tell which button was clicked if there is an array of buttons?
            //
            if (xEnabled)   // this should block the buttons while executing gcode.
            {
                var tag = ((Button)sender).Tag;
                // MessageBox.Show(tag.ToString());

                foreach (Button b in this.OffsetGrid.Children)
                {
                    if (b.Tag == tag)
                    {
                        b.Background = Brushes.LightGreen;
                    }
                    else
                    {
                        b.Background = Brushes.LightGray;
                    }
                }

                // get the fixture number
                fixture = int.Parse(tag.ToString());
                // set the fixture
                //sp.CoordMotion.Interpreter.SetupParams.OriginIndex = fixture;
                sp.CoordMotion.Interpreter.ChangeFixtureNumber(fixture);
                // how do I update the fixtue coordinates from here?
            }
        }

        public void SetButton(int CurrentOffset)
        {
            foreach(Button b in this.OffsetGrid.Children)
            {
                if(b.Tag.ToString() == CurrentOffset.ToString())
                {
                    b.Background = Brushes.LightGreen;
                }
                else
                {
                    b.Background = Brushes.LightGray;
                }
            }
        }

        public void EnableButtons()
        {
            xEnabled = true;
        }

        public void DisableButtons()
        {
            xEnabled = false;
        }

    }
}
