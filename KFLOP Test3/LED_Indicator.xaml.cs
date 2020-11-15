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
    /// Interaction logic for LED_Indicator.xaml
    /// </summary>
    /// 
        // also trying to figure out the enum --- This defines the different states for an indicator LED
    public enum LED_State
    {
        Off,
        On_Red,
        On_Green,
        On_Yellow,
        On_Blue
    }

    public partial class LED_Indicator : UserControl
    {


        static Uri RedUri;
        static Uri GreenUri;
        static Uri BlueUri;
        static Uri YellowUri;
        static Uri OffUri;

        public string LED_Label { get; set; }

        public LED_Indicator()
        {
            InitializeComponent();
            // LED_Image.Source = new BitmapImage(new Uri(@"/Small LED OFF.png"));
            RedUri = new Uri("Small LED Red.png", UriKind.Relative);
            GreenUri = new Uri("Small LED Green.png", UriKind.Relative);
            BlueUri = new Uri("Small LED Blue.png", UriKind.Relative);
            YellowUri = new Uri("Small LED Yellow.png", UriKind.Relative);
            OffUri = new Uri("Small LED Off.png", UriKind.Relative);
        }

        public void Set_State(LED_State State)
        {
            switch (State)
            {
                case LED_State.Off: LED_Image.Source = new BitmapImage(OffUri);  break;
                case LED_State.On_Blue: LED_Image.Source = new BitmapImage(BlueUri);  break;
                case LED_State.On_Green: LED_Image.Source = new BitmapImage(GreenUri); break;
                case LED_State.On_Red: LED_Image.Source = new BitmapImage(RedUri); break;
                case LED_State.On_Yellow: LED_Image.Source = new BitmapImage(YellowUri); break;
                default: LED_Image.Source = new BitmapImage(OffUri); break;
            }
        }

        public void Set_Label(string lbl)
        {
            lbl_LED_Text.Content = lbl;
        }
    }
}
