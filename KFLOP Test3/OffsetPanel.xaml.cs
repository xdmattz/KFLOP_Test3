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
    /// Interaction logic for OffsetPanel.xaml
    /// </summary>
    public partial class OffsetPanel : UserControl
    {
        // a copy  of the KM Controler
        private KM_Controller KMx { get; set; }
        public ButtonGrid OffsetButtons;

        public OffsetPanel(ref KM_Controller X)
        {
            KMx = X;
            InitializeComponent();
            OffsetButtons = new ButtonGrid(ref X);
            Grid1.Children.Add(OffsetButtons);
            Grid.SetColumn(OffsetButtons, 0);
            Grid.SetRow(OffsetButtons, 0);
        }

        public void AxisEnable()
        {
            int OX, OY, OZ, OA, OB, OC;
            OX = OY = OZ = OA = OB = OC = 0;
            // get the assigned axis definitions -1 indicates that an axis is not enabled.
            KMx.CoordMotion.GetAxisDefinitions(ref OX, ref OY, ref OZ, ref OA, ref OB, ref OC);
            // only show the axis that are enabled.
            if (OX == -1)
            {
                XAxisOffset.Visibility = Visibility.Collapsed;
            }
            if (OY == -1)
            {
                YAxisOffset.Visibility = Visibility.Collapsed;
            }
            if (OZ == -1)
            {
                ZAxisOffset.Visibility = Visibility.Collapsed;
            }
            if (OA == -1)
            {
                AAxisOffset.Visibility = Visibility.Collapsed;
            }
            if (OB == -1)
            {
                BAxisOffset.Visibility = Visibility.Collapsed;
            }
            if (OC == -1)
            {
                CAxisOffset.Visibility = Visibility.Collapsed;
            }
        }

        public void XAxis(double x)
        {
            XAxisOffset.Text = String.Format("X: {0:F4}", x);
        }
        public void YAxis(double y)
        {
            YAxisOffset.Text = String.Format("Y: {0:F4}", y);
        }
        public void ZAxis(double z)
        {
            ZAxisOffset.Text = String.Format("Z: {0:F4}", z);
        }
        public void AAxis(double a)
        {
            AAxisOffset.Text = String.Format("A: {0:F4}", a);
        }
        public void BAxis(double b)
        {
            BAxisOffset.Text = String.Format("B: {0:F4}", b);
        }
        public void CAxis(double c)
        {
            CAxisOffset.Text = String.Format("C: {0:F4}", c);
        }

        public void SetOffsetDisplay()  // do I need the fixure number here?
        {
            int currfix;
            currfix = KMx.CoordMotion.Interpreter.SetupParams.OriginIndex;
            double fx, fy, fz, fa, fb, fc;
            fx = fy = fz = fa = fb = fc = 0;
            KMx.CoordMotion.Interpreter.GetOrigin(currfix, ref fx, ref fy, ref fz, ref fa, ref fb, ref fc);
            XAxis(fx);
            YAxis(fy);
            ZAxis(fz);
            AAxis(fa);
            BAxis(fb);
            CAxis(fc);
        }
    }
}
