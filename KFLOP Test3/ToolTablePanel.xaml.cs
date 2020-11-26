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
    /// Interaction logic for ToolTablePanel.xaml
    /// </summary>
    public partial class ToolTablePanel : UserControl
    {
        // a copy of the KM controller 
        private KM_Controller KMx { get; set; }

        public ToolTablePanel(ref KM_Controller X)
        {
            InitializeComponent();
            KMx = X;    // point to the KM controller - this exposes all the KFLOP .net library functions

        }
    }
}
