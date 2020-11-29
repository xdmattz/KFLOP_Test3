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
        public double TC_H1_Z { get; set; }
        public double TC_H1_FR { get; set; } // feed rate to travel to TC_H1
        public double TC_H2_Z { get; set; }
        public double TC_H2_FR { get; set; } // feed rate to travel to TC_H2 -
        public double TC_Index { get; set; }
        public double TC_S_FR { get; set; } // spindle homeing feedrate;

        public double TS_X { get; set; }    // tool height setter X coordinate
        public double TS_Y { get; set; }    // tool height setter Y coordinate
        public double TS_Z { get; set; }    // tool height setter Z coordinate
        public double TS_S { get; set; }    // tool height setter Spindle Index coordinate
        public double TS_FR1 { get; set; }    // tool height setter FeedRate 1 - hunt
        public double TS_FR2 { get; set; }    // tool height setter FeedRate 2 - touch

    }
}
