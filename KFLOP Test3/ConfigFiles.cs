using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFLOP_Test3
{
    // list of the files for configuration
    // links to a json file 
    class ConfigFiles
    {
        public ConfigFiles()
        {}

        public string fPath { get; set; }
        public string EMCVarsFile { get; set; }
        public string KThread1 { get; set; }
        public string KThread2 { get; set; }
        public string KThread3 { get; set; }
        public string KThread4 { get; set; }
        public string KThread5 { get; set; }
        public string KThread6 { get; set; }
        public string KThread7 { get; set; }
        public string MotionParams { get; set; }
        public string ToolTable { get; set; }
        public string Highlight { get; set; }

    }
}
