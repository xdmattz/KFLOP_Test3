using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
// for the JSON stuff
using Newtonsoft.Json;
// for the message box
using System.Windows;

namespace KFLOP_Test3
{
    // list of the files for configuration
    // links to a json file 
    class ConfigFiles
    {
        public ConfigFiles()
        { }

        public string fPath { get; set; }
        public string EMCVarsFile { get; set; }
        public string KThread1 { get; set; }
        public string KThread2 { get; set; }
        public string KThread3 { get; set; }
        public string KThread4 { get; set; }
        public string KThread5 { get; set; }
        public string KThread6 { get; set; }
        public string KThread7 { get; set; }
        public string LastGCode { get; set; }
        public string MotionParams { get; set; }
        public string ToolTable { get; set; }
        public string Highlight { get; set; }
        public string ToolFile { get; set; }
        public string GCodePath { get; set; }
        public string KFlopCCodePath { get; set; }
        public string ConfigPath { get; set; }
        public string ThisFile { get; set; }


        public void SaveConfig()
        {
            fPath = System.AppDomain.CurrentDomain.BaseDirectory;
            ConfigPath = System.IO.Path.Combine(fPath, "Config");
            ThisFile = "KTestConfig.json";
            string combinedConfigFile = System.IO.Path.Combine(ConfigPath, ThisFile);

            JsonSerializer Jser = new JsonSerializer();
            StreamWriter sw = new StreamWriter(combinedConfigFile);
            JsonTextWriter Jwrite = new JsonTextWriter(sw);
            Jser.NullValueHandling = NullValueHandling.Ignore;
            Jser.Formatting = Newtonsoft.Json.Formatting.Indented;
            Jser.Serialize(Jwrite, this);
            sw.Close();
        }

        public bool FindConfig()
        {
            // check if the the config file exists
            // get the current path
            // add the config subdirectory to it.
            // check for the config file
            fPath = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            ConfigPath = System.IO.Path.Combine(fPath, "Config");
            ThisFile = "KTestConfig.json";
            // MessageBox.Show(fullPath);
            string ConfigFileName = System.IO.Path.Combine(fPath, ThisFile);
            if (System.IO.File.Exists(ConfigFileName) == true)
            {
                return true;
            }
            else
            {
                MessageBox.Show("No configureation file found");
                // what to do here? 
                // Initialize the strings to null and save the file for next time
                SaveConfig();
                return false;
            }

        }


    }

}
