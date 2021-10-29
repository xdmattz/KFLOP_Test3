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
    /// Interaction logic for CurrentTool.xaml
    /// </summary>
    public partial class CurrentTool : UserControl
    {
        public CurrentTool()
        {
            InitializeComponent();
        }

        public void SetTool(int Tool)
        {
            tbCurrentTool.Text = string.Format("T{0}", Tool);
        }

        public void SetLen(double Length)
        {
            tbLength.Text = string.Format("{0:F4}", Length);
        }
    }
}
