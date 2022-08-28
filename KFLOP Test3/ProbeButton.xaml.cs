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
    /// Interaction logic for ProbeButton.xaml
    /// </summary>
    /// 
    // https://stackoverflow.com/questions/5971300/programmatically-changing-button-icon-in-wpf
    //
    public partial class ProbeButton : UserControl
    {
        private BitmapImage _buttonImage;

        public BitmapImage ButtonImage
        {
            get { return _buttonImage; }
            set {
                _buttonImage = value;
               //  OnPropertyChanged("ButtonImage");
            }
        }

        private string _buttonText;

        public string ButtonText
        {
            get { return _buttonText; }
            set
            {
                _buttonText = value;
                // OnPropertyChanged("ButtonText");

            }
        }
        public ProbeButton(BitmapImage Img, string Lbl)
        {
            ButtonImage = Img;
            ButtonText = Lbl;
            InitializeComponent();

        }
    }
}
