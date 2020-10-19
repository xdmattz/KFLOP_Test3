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

        public string fPath { get; set; }   // the default directory path name for the program
        // Files for setting up the RS274 Interpreter (EMC)
        // the offset parameter file - see the NIST RS274/NGC Interpreter section 3.2.1
        // https://tsapps.nist.gov/publication/get_pdf.cfm?pub_id=823374
        public string EMCVarsFile { get; set; }     // which one of the two following files is active? 
        public string EMCVarsFile_mm { get; set; }  // the EMCVars file all in mm
        public string EMCVarsFile_inch { get; set; } // the EMDVars file all in inches
        public string EMCSetupFile { get; set; }    // the in KMCNC this is the default.set file  
        // 
        public string ToolTable { get; set; }
        public string ToolFile { get; set; }        
        public string ToolFile_mm { get; set; } // tool file 
        public string ToolFile_inch { get; set; }
        public string MotionParams { get; set; }
        // KFLOP C Programs - C programs to run in each thread
        public string KThread1 { get; set; }    // the main thread - always running
        public string KThread2 { get; set; }    // the secondary thread - commands from this program
        public string KThread3 { get; set; }    // third thread because the control for spindle speed is weird.
        public string KThread4 { get; set; }    // other threads
        public string KThread5 { get; set; }
        public string KThread6 { get; set; }
        public string KThread7 { get; set; }
        public string KFlopCCodePath { get; set; }  // the C program file path name
        // G Code programs
        public string LastGCode { get; set; }   // the last G Code file that was loaded
        public string Highlight { get; set; }   // G Code Highlighting for Avalon Edit
        public string GCodePath { get; set; }   // the G Code file path name
        public string MDIFile { get; set; }
        // Configuration files 
        public string ConfigPath { get; set; }  
        public string ThisFile { get; set; }    // the name of the configuration file 


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
