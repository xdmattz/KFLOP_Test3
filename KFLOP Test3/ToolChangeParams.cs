using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFLOP_Test3
{
    public class ToolChangeParams
    {
        // note that not all elements of the machine position are used for every action.
        public M_Position TC_H1 { get; set; }   
        public M_Position TC_H2 { get; set; }
        public M_Position ToolSetter_Pos { get; set; }
        public double H1_FeedRate { get; set; } // feed rate to travel to TC_H1
        public double H2_FeedRate { get; set; } // feed rate to travel to TC_H2 - 



    }

    // machine position 
    // describes a complete machine position
    // six axis and spindle position 
    // probably don't need that much info, but I'm trying to make this "generic"
    public class M_Position
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
        public double S { get; set; }
    }

}
