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
        public double TS_Z { get; set; }    // tool height setter Z coordinate - can rapid to this height without breaking a tool?
            // this should probably be about 8 - 10 inches above the tool setter. long enough for the 
        public double TS_SAFE_Z { get; set; } // tool height setter safe Z - move to this height before any X,Y motion
        public double TS_S { get; set; }    // tool height setter Spindle Index coordinate
        public double TS_FR1 { get; set; }    // tool height setter FeedRate 1 - hunt
        public double TS_FR2 { get; set; }    // tool height setter FeedRate 2 - touch

        public int CarouselSize { get; set; } // the number of slots in the tool carousel
        public string CarouselToolsFileName { get; set; } // name of the carousel's loaded tool state 

    }

    public class ToolParams
    {
        // tool parameters
        public ToolParams()    // constructor
        { }

        public int index { get; set; }
        public int slot { get; set; }
        public int id { get; set; }
        public double length_offset { get; set; }
        public double diameter_offset { get; set; }
        public double x_offset { get; set; }
        public double y_offset { get; set; }
        public string description { get; set; }
        public string image_fname { get; set; }

    }
}
