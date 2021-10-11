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

        private void btnToolList_Click(object sender, RoutedEventArgs e)
        {
            // list out all the tools 
            int toolIndex = 0;
            int tSlot = 0;
            int tID = 0;
            double tLen = 0;
            double tDia = 0;
            double tXoff = 0;
            double tYoff = 0;

            lbToolList.Items.Clear();   // start with a clear list

            do
            {
                KMx.CoordMotion.Interpreter.SetupParams.GetTool(toolIndex, ref tSlot, ref tID, ref tLen, ref tDia, ref tXoff, ref tYoff);
                lbToolList.Items.Add(String.Format("index:{0} Slot:{1} ID:{2} Len:{3} Dia:{4} X:{5} Y:{6}", toolIndex, tSlot, tID, tLen, tDia, tXoff, tYoff));
                toolIndex++; 
            } while ((tID != 0) && (toolIndex < 20));

        }
    }
}
