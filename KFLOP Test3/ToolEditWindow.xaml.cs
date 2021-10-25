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
    /// Interaction logic for ToolEditWindow.xaml
    /// </summary>
    public partial class ToolEditWindow : Window
    {

        static public Tool ETool;

        public int tSlot;

        public ToolEditWindow(Tool EditTool)
        {
            InitializeComponent();
            
            if(EditTool != null)
            {
                // fill the window with the current EditTool
                FillWindow(EditTool);
                ETool = EditTool;
            }
            else
            {
                
            }
        }

        public void FillWindow(Tool eTool)
        {
            tSlot = eTool.slot;
            tbSlot.Text = $"{eTool.slot}";
            tbIndex.Text = $"{eTool.ID}";
            tbLength.Text = string.Format("{0:F4}", eTool.Length);
            tbDiameter.Text = string.Format("{0:F4}", eTool.Diameter);
            tbXOffset.Text = string.Format("{0:F4}", eTool.XOffset);
            tbYOffset.Text = string.Format("{0:F4}", eTool.YOffset);
            tbComment.Text = eTool.Comment;
            tbImage.Text = eTool.Image;
        }

        public void GetWindow(Tool eTool)
        {
            int tempInt = 0;
            double tempDouble = 0;
            if (int.TryParse(tbSlot.Text, out tempInt) == true)
            { eTool.slot = tempInt; }
            else { eTool.slot = 0; }    // empty slot?
            if (int.TryParse(tbIndex.Text, out tempInt) == true)
            { eTool.ID = tempInt; }
            else { eTool.ID = 0; }
            if(double.TryParse(tbLength.Text, out tempDouble) == true)
            { eTool.Length = tempDouble; }
            else { eTool.Length = 0.0; }
            if (double.TryParse(tbDiameter.Text, out tempDouble) == true)
            { eTool.Diameter = tempDouble; }
            else { eTool.Diameter = 0.0; }
            if(double.TryParse(tbXOffset.Text, out tempDouble) == true)
            { eTool.XOffset = tempDouble; }
            else { eTool.XOffset = 0.0; }
            if(double.TryParse(tbYOffset.Text, out tempDouble) == true)
            { eTool.YOffset = tempDouble; }
            else { eTool.YOffset = 0.0;  }
            eTool.Comment = tbComment.Text;
            eTool.Image = tbImage.Text;
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            // is there some way to check for a unique tool here?
            // GetWindow(ETool); only do this after it has been checked for duplicates
            int tempInt;
            if (int.TryParse(tbSlot.Text, out tempInt) == true)
            {
                tSlot = tempInt;
            } else { tSlot = -1; }
            this.DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
