using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KMotion_dotNet;

namespace KFLOP_Test3
{
    public class Fixtures
    {
        // a copy  of the KM Controler
        private KM_Controller xKM { get; set; }

        public Fixtures(ref KM_Controller X)  // class constructor
        {
            xKM = X;    // initialize the local KM_Controller to point to the KM_Controller
        }

        public void SaveFixtures(string fileName, double convertConstant, double[] G28, double[] G30)
        {
            System.IO.StreamWriter VarFile = new System.IO.StreamWriter(fileName);
            
            double[] x = new double[6];
            // get the G28 offset
            // write to file
            for(int i = 0; i < 6; i++)
            {
                x[i] = G28[i] * convertConstant;
            }
            FixtureWriteLine(VarFile, 5161, x);
            // get the G30 offset
            for(int i = 0; i < 6; i++)
            {
                x[i] = G30[i] * convertConstant;
            }
            FixtureWriteLine(VarFile, 5181, x);
            // write to file
            // get the G92
            xKM.CoordMotion.Interpreter.GetOrigin(0, ref x[0], ref x[1], ref x[2], ref x[3], ref x[4], ref x[5]);
            for (int j = 0; j < 6; j++)
            {
                x[j] = x[j] * convertConstant;
            }
            FixtureWriteLine(VarFile, 5211, x);
            // write to file
            // write the coord system number into 5220
            VarFile.WriteLine("5220\t1.0");
            // write G54 - G59.2
            for(int i = 1; i <10; i++)
            {
                // get the existing fixture offsets
                xKM.CoordMotion.Interpreter.GetOrigin(i, ref x[0], ref x[1], ref x[2], ref x[3], ref x[4], ref x[5]);
                for(int j=0;j<6;j++)
                {
                    x[j] = x[j] * convertConstant;
                }
                FixtureWriteLine(VarFile, (5201 + i * 20), x);
            }
            VarFile.Close();
        }

        private void FixtureWriteLine(System.IO.StreamWriter Writer, int offset, double[] x)
        {
            string line;
            for(int i = 0; i < 6; i++)
            {
                line = String.Format("{0}\t{1:F5}", offset++, x[i]);
                Writer.WriteLine(line);
            }
        }

        // get the fixture values and save
    }
}
