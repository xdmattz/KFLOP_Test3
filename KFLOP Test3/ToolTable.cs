using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace KFLOP_Test3
{
    // a class that contains an instance of both tool table and carousel.
    // hopfully I only have to pass a single reference to this class to access everything.

    public class ToolInfo
    {
        public ToolTable toolTable { get; set; }
        public ToolCarousel toolCarousel { get; set; }
    }

    // tool table class
    public class ToolTable
    {
        public List<Tool> Tools { get; set; }
    }
    //public class ToolTable : ObservableCollection<Tool>
    //{

    //}

    public class Tool
    {
        public Tool()
        {
            slot = 0;
            ID = 0;
            Length = 0.00;
            Diameter = 0.00;
            XOffset = 0.00;
            YOffset = 0.00;
            Comment = "";
            Image = "";
        }

        public int slot { get; set; }
        public int ID { get; set; }
        public double Length { get; set; }  // Z Offset
        public double Diameter { get; set; }
        public double XOffset { get; set; }
        public double YOffset { get; set; }
        public string Comment { get; set; }
        public string Image { get; set; }
    }

    // the tool carousel / tool changer

    // tool carousel class 
    public class ToolCarousel
    {
        public List<CarouselItem> Items { get; set; }
        // public int Count;
    }
    //public class ToolCarousel : ObservableCollection<CarouselItem>
    //{

    //}

    public class CarouselItem
    {
        public int Pocket { get; set; }     // the pocket numbers of the tool carousel
        public int ToolIndex { get; set; }  // the tool index number assigned to the pocket
                                            // this is the Tn number listed in the gcode
                                            // a 0 means that the pocket is empty
        public bool ToolInUse { get; set; } // true means that the pocket is assigned a tool number but
                                            // the tool is in the spindle
                                            // false means that the tool is in the carousel holder (pocket)
        public string Description { get; set; } // description or note about the tool
    }


}
