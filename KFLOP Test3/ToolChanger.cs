// Must put this in every file that will look for it!
#define TESTBENCH  // defining this will allow operation on the testbench
// The other TESTBENCH is in ToolChangerPanel.xaml.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using System.Diagnostics;
using System.Threading;
using System.ComponentModel;
// for KMotion
using KMotion_dotNet;

// for the JSON stuff
using Newtonsoft.Json;

// for file dialog libraries
using Microsoft.Win32;
using System.IO;


namespace KFLOP_Test3
{

    enum CarouselUpdateStates : int
    {
        Error,
        Done,
        Update,
        Reset,
        Empty,
        Put,
        Get
    }

    // try to keep the scope of this to only the basic machine level control

    public class ToolChanger : MachineMotion
    {
        // Tool Carousel configuration
        static public ToolInfo xToolInfo;
        static public ToolCarousel CarouselList1;
        // tool table 
        static public ToolTable TTable;

        // The back ground workers 
        static BackgroundWorker _bw2;   // process background worker.
        static BWResults BW2Res;

        // these static variable flags indicate tool change progress
        static bool TCActionProgress; // true indicates a process action (group of steps) is in progress, false indicates the action has finished 

        // some constants used in the tool changer

        const bool bARM_IN = true;
        const bool bARM_OUT = false;
        const bool bCLAMP = true;
        const bool bRELEASE = false;

        const int cMsgOffset = 4000;

        public static int ToolInSpindle { get; set; }  // the number of the tool in the spindle, 0 = spindle empty, 1 - 999, Tool Number from the tool table
        public static double ToolInSpinLen { get; set; }
        // these are the variables that the UI looks at to display the tool number and offset.

        public event dStatusMsg ProcessUpdate;
        public event dStatusMsg ProcessError;
        public event dUpdateToolInfo UpdateCarousel;
        public event dStageDone ProcessCompleted;

        public ToolChanger(ref ToolInfo toolInfo)   // class construtor
        {
            xToolInfo = toolInfo;
            CarouselList1 = toolInfo.toolCarousel;
            TTable = toolInfo.toolTable;
            // TExPos = new TExchangePosition();

            _bw2 = new BackgroundWorker();
            BW2Res = new BWResults();

            LoadCarouselCfg();
        }

        public ToolTable GetToolTable()
        {
            return (TTable);
        }

        #region Load and Save Carousel Configuration File
        public ToolCarousel GetCarousel()
        {
            return (CarouselList1);
        }

        public void SetCarousel(ToolCarousel TC)
        {
            // check for null carousel list
            if (CarouselList1.Items == null) { CarouselList1.Items = new List<CarouselItem>(); }

            CarouselList1.Items.Clear();

            foreach (CarouselItem CI in TC.Items)
            {
                CarouselList1.Items.Add(CI);
            }
        }

        // load carousel from the standard file
        public bool LoadCarouselCfg()
        {
            string CFileName = System.IO.Path.Combine(CFx.ConfigPath, CFx.ToolCarouselCfg);
            // load the tool changer files
            // xToolChanger.LoadCarCfg(CFileName);
            if (System.IO.File.Exists(CFileName) == true)
            {
                try
                {
                    JsonSerializer Jser = new JsonSerializer();
                    StreamReader sr = new StreamReader(CFileName);
                    JsonReader Jreader = new JsonTextReader(sr);
                    CarouselList1 = Jser.Deserialize<ToolCarousel>(Jreader);
                    sr.Close();
                }
                catch (JsonSerializationException ex)
                {
                    MessageBox.Show(String.Format("{0} Exception! Carousel Not Loaded!", ex.Message));
                    return false;
                }
            }
            else
            {
                MessageBox.Show("File not found - Reinialize the Carousel");
                // ResetCarousel();
                // saveCarouselCfg(FileName);
                return false;
            }

            return true;
        }

        // load carousel from another filename;
        public bool LoadCarouselCfg(string CFileName)
        {
            var OpenFile = new OpenFileDialog();
            OpenFile.FileName = CFileName;
            if(OpenFile.ShowDialog() == true)
            {
                if (System.IO.File.Exists(OpenFile.FileName) == true)
                {
                    try
                    {
                        JsonSerializer Jser = new JsonSerializer();
                        StreamReader sr = new StreamReader(OpenFile.FileName);
                        JsonReader Jreader = new JsonTextReader(sr);
                        CarouselList1 = Jser.Deserialize<ToolCarousel>(Jreader);
                        sr.Close();
                        return true;
                    }
                    catch (JsonSerializationException ex)
                    {
                        MessageBox.Show(String.Format("{0} Exception! Carousel Not Loaded!", ex.Message));
                        return false;
                    }
                }
                else
                {
                    MessageBox.Show("File not found - Reinialize the Carousel");
                    // ResetCarousel();
                    // saveCarouselCfg(FileName);
                    return false;
                }
            }
            return true;
        }

        // save the carousel file
        // save the carousel under another filename
        public void SaveCarouselCfg(string CFileName)
        {
            var SaveFile = new SaveFileDialog();
            SaveFile.FileName = CFileName;
            if (SaveFile.ShowDialog() == true)
            {
                saveCarouselCfg1(SaveFile.FileName);
            }
        }

        // save the carousel in the standard file
        public void SaveCarouselCfg()
        {
            string CFileName = System.IO.Path.Combine(CFx.ConfigPath, CFx.ToolCarouselCfg);
            var SaveFile = new SaveFileDialog();

            SaveFile.FileName = CFileName;
            if (SaveFile.ShowDialog() == true)
            {
                saveCarouselCfg1(SaveFile.FileName);
            }
        }

        void saveCarouselCfg1(string FileName)
        {
            try
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamWriter sw = new StreamWriter(FileName);
                JsonTextWriter Jwrite = new JsonTextWriter(sw);
                Jser.NullValueHandling = NullValueHandling.Ignore;
                Jser.Formatting = Newtonsoft.Json.Formatting.Indented;
                Jser.Serialize(Jwrite, CarouselList1);
                sw.Close();
            }
            catch (JsonSerializationException ex)
            {
                MessageBox.Show(String.Format("{0} Exception", ex.Message));
            }
        }
        #endregion

        // update the Interpreter current tool with the ToolSlot tool number
        public void SetCurrentTool(int ToolSlot)
        {
            KMx.CoordMotion.Interpreter.SetupParams.CurrentToolSlot = ToolSlot; // update the interpreter slot position
            // also update the global variable ToolInSpindle
            ToolInSpindle = ToolSlot;
            ToolInSpinLen = GetToolLength(ToolSlot);
        }

        public bool bwBusy()
        {
            // if either background worker is busy then return true.
            if (MotionBusy())
                return true;
            if (_bw2.IsBusy)
                return true;
            return false;
        }

        public int TLAUX_Status()
        {
            int status;
            lock (this)
            {
                status = KMx.GetUserData(PVConst.P_TLAUX_STATUS);
            }
            return status;
        }

        #region Tool Changer Actions

        // A bit of an explaination is warrented here. 
        // there are three basic tool change actions
        // 1. Get a tool - gets a tool from the tool carousel
        // 2. Put a tool - puts a tool into the the tool carousel
        // 3. Exchange a tool - puts a tool back and gets another tool - without the carousel retract.
        // 
        // It is important to understand when to use each of these. since I have crashed the tool changer a few times this evening...
        // Get a tool assumes that the spindle is empty ie. Tool Number = 0
        // Put a tool assumes that the carousel slot is empty
        // Exchange a tool assumes there is a tool in the spindle and an empty slot to put it into.
        // all this needs to be managed from a higher level in the program because these are very low level functions
        //
        // There are two tool change methods: ToolChangerDeluxe and ToolChangerSimple
        // ToolChangerDeluxe takes the Current and Selected Tool Numbers as arguments
        // these are the tool numbers from the tool table, or from the GCode Interpreter 
        // which should be the same as the tool table.
        // It also checks for the tools to be in carousel and if they are not prompts for a manual tool change.
        // this will also 
        //
        // ToolChangerSimple takes Current and Selected Carousel Pockets as arguments
        // It will not put a tool into a pocket that it thinks has something in it already.
        // other than than it is pretty "simple"
        // 

       // this function is called when loading a new tool into from the spindle into the carousel
       // 
       public void LoadTool(int ToolNumber, int PocketNumber)
        {
//               if (MeasureTool == true)
//                {
                    // do a tool measurement here...
                    // can we make this a call back to something that will measure the tool? 
//                }
                ToolChangerSimple(PocketNumber, 0); // put the tool into the pocket
                CarouselAddTool(ToolNumber, PocketNumber);
            
        }

        // this function is called when unloading a tool from the carousel.
        public void UnloadTool(int PocketNumber)
        {
            ToolChangerSimple(0, PocketNumber); // get the tool from the carousel into the spindle
            CarouselDeleteTool(PocketNumber);  // remove the tool from the carousel list
            MessageBox.Show("Remove the Tool from the Spindle!", "ATTENTION!"); // prompt the user to manually remove the tool from the spindle
            ToolInSpindle = 0;  // clear the tool in spindle
            ToolInSpinLen = 0;


        }

        // In all of this who or what is managing what is currently in the carousel!!! have to figure this out soon!
        public bool ToolChangeM6()
        {
            int current_tool = KMx.CoordMotion.Interpreter.SetupParams.CurrentToolSlot;
            int selected_tool = KMx.CoordMotion.Interpreter.SetupParams.SelectedToolSlot;
            //      MessageBox.Show($"Current Tool: {current_tool} Selected tool {selected_tool}");

            // need to decide which tool action to take - see comments on tool change actions
            // see the flow chart for details! 
            ToolChangerDeluxe(current_tool, selected_tool);
            int TimeoutCount = 0;   // this may be unnessary
            do {
                Thread.Sleep(100);
                if (TimeoutCount++ > 1200)  // two minutes
                {
                    MessageBoxResult MSR = MessageBox.Show("Tool Changer Timeout\rContinue?", "Tool Change Error", MessageBoxButton.YesNo);
                    if (MSR == MessageBoxResult.No)
                    {
                        BW2Res.Result = false;
                        break;  // break out of the do loop
                    }
                    else
                    {
                        BW2Res.Result = true;
                        break;
                    }
                }
            } while (TCActionProgress); // wait for the tool change operation to finish
            //            MessageBox.Show($"Tool {current_tool} changed to {selected_tool}");

            if(BW2Res.Result == false)  // something went wrong with the tool change
            {
                MessageBox.Show("Got to here!");
                // take care of anything that needs to be cleaned up.
                return false;
            }
            else
            {
                // update the tool in spindle and tool in use?
            }
            // Tool changer is done.
            // Check for an error.
            return true;
            // if not an error then update the tool carousel
            // since this is in another thread what to do here?
        }

        public void ToolChangerDeluxe(int CurrentTool, int SelectedTool)
        {
            // changing to reflect the Tool Carousel Management
            // Note: Tool Slot 0 always means empty spindle - this is always the first row in the tool table and has zero length
            // need to have some kind of tool change complete callback - to handle things when the thread is done.
            // should only have to do this once...

            int sPocket = getPocket(SelectedTool);  // selected pocket
            int cPocket = getPocket(CurrentTool);   // current pocket

            TCActionProgress = true;   // initialize the TCActionProgress to true - means in progress


            if (CurrentTool == 0) // there is no tool in the spindle - just get the selected tool
            {
                if (SelectedTool == 0)
                {
                    TCActionProgress = false; // action done.
                    BW2Res.Result = true; // completed successfully
                    return; // the do nothing case - no tool change
                }

                if (sPocket == -1)
                {
                    Manual_GetTool(SelectedTool);   // selected tool is not in the carousel - do a manual get tool
                }
                else
                {
                    // check for tool not in use here???

                    Start_GetTool(sPocket);    // automatically get a tool from the carousel
                }
            }
            else if (cPocket == -1) // current tool requires a manual removal
            {
                Manual_PutTool(CurrentTool);   // removes the current tool from the spindle
                if (SelectedTool == 0)
                {
                    ToolInSpindle = 0; // don't need to do anything here - spindle was just unloaded manually
                    ToolInSpinLen = 0;
                    return;
                }
                if (sPocket == -1)
                {
                    Manual_GetTool(SelectedTool);   // selected tool is not in the carousel - do a manual get tool
                }
                else
                {
                    Start_GetTool(sPocket);    // automatically get a tool from the carousel
                }
            }
            else
            {
                // current tool can be put into the carousel
                if (SelectedTool == 0)
                {
                    Start_PutTool(cPocket); // just put away the current tool
                    ToolInSpindle = 0;
                    ToolInSpinLen = 0;
                    ClearToolInUse(cPocket);
                    return;
                }
                if (sPocket == -1)
                {
                    Start_PutTool(cPocket); // put away the current tool
                    ToolInSpindle = 0;
                    ToolInSpinLen = 0;
                    ClearToolInUse(cPocket);
                    Manual_GetTool(SelectedTool); // get the selected tool manually - not in the carousel
                }
                else
                {
                    Start_ExchangeTool(cPocket, sPocket);  // if none of the other conditions apply then do a tool exchange!
                    
                }
            }
        }

        // this tool changer will only put and get, no exchanges. 
        public void ToolChangerSimple(int CurrentSlot, int SelectedSlot)
        {
            if((CurrentSlot < 1) || (CurrentSlot > xTCP.CarouselSize))
            {
                // no valid tool in the spindle - should have been removed before calling
                // get Selected Slot
                if((SelectedSlot < 1) || (SelectedSlot > xTCP.CarouselSize))
                {
                    // not a valid slot!
                    return;
                }
                else
                {
                    Start_GetTool(SelectedSlot);
                    // wait for the process to finish
                }
            }
            else
            {
                // put current spindle into carousel slot
                Start_PutTool(CurrentSlot);
                // wait for the process to finish

                // get Selected Slot
                if ((SelectedSlot < 1) || (SelectedSlot > xTCP.CarouselSize))
                {
                    // not a valid slot!
                    return;
                }
                else
                {
                    Start_GetTool(SelectedSlot);
                    // wait for the process to finish

                }
            }
        }

        #region Get a Tool from the Tool Changer
        public void Start_GetTool(int xPocketNumber)   // changing this to Pocket Number instead of Tool Number
        {

            TCActionProgress = true; // process action is happening...
            _bw2.WorkerReportsProgress = true;

            _bw2.DoWork += GetToolWorker;
            _bw2.ProgressChanged += GetToolProgressChanged;
            _bw2.RunWorkerCompleted += GetToolCompleted;
            _bw2.RunWorkerAsync(xPocketNumber);
        }

        private void GetToolWorker(object sender, DoWorkEventArgs e)
        {
            // Assume the following state:
            // 1. Spindle is empty
            // 2. The tool arm is retracted.
            // 3. Tool Pocket Number to get is passed in the argument

            TCProgress = true;
            ToolChangeStatus = false;
            int progress_cnt = 0;
            BW2Res.Result = true; // start out at true;

            _bw2.ReportProgress(progress_cnt++);

            SingleAxis tSAx = new SingleAxis();

            tSAx.ToolPocket = (int)e.Argument;

            // start a new background worker
            if (MotionBusy()) // if the BW worker is busy
            {
                BW2Res.Result = false;
                BW2Res.Comment = "BW is busy";
                e.Result = BW2Res;
                return;
            }

            BW2Res.Comment = "In Get Tool Move to Z1";
            _bw2.ReportProgress(progress_cnt++);
            // Move to H1 
            //!!!!!
            tSAx.Pos = xTCP.TC_H1_Z; // Z Height position and feedrate
            tSAx.Rate = xTCP.TC_H1_FR;
            Start_MoveZ_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Z1 Height Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            BW2Res.Comment = "Spindle Indexing";
            _bw2.ReportProgress(progress_cnt++);

            // Index the spindle
            tSAx.Pos = xTCP.TC_Index;    // Spindle position and feedrate 
            tSAx.Rate = xTCP.TC_S_FR;
            Start_Spindle_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "TC Index Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // rotate carousel to tool number

            BW2Res.Comment = $"Carousel Indexing to {tSAx.ToolPocket}";
            _bw2.ReportProgress(progress_cnt++);

            Start_Carousel_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Carousel Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            BW2Res.Comment = "Tool Arm Out";
            _bw2.ReportProgress(progress_cnt++);
            // Arm Out
            tSAx.Move = bARM_OUT;
            Start_ARM_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Arm Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            BW2Res.Comment = "Tool Release";
            _bw2.ReportProgress(progress_cnt++);
            // Clamp Release
            tSAx.Move = bRELEASE;
            Start_TClamp_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Release Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }
            // at this point the new tool is clamped in the spindle // move this to the location from where it was called from 

            BW2Res.Comment = "Move to Z2";
            _bw2.ReportProgress(progress_cnt++);
            // move to H2
            tSAx.Pos = xTCP.TC_H2_Z;
            tSAx.Rate = xTCP.TC_H2_FR;
            Start_MoveZ_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Z2 Height Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            BW2Res.Comment = "Tool Clamp";
            _bw2.ReportProgress(progress_cnt++);
            // Clamp Engage
            tSAx.Move = bCLAMP;
            Start_TClamp_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Clamp Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            BW2Res.Comment = "Tool Arm In";
            _bw2.ReportProgress(progress_cnt++);
            // Arm In
            tSAx.Move = bARM_IN;
            Start_ARM_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Arm Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // report the carousel update 
            _bw2.ReportProgress(cMsgOffset + tSAx.ToolPocket);

            // since the GCode S command will automatically set the RPM mode 
            // this has been changed to just Spindle Disable
            // BW2Res.Comment = "Spindle RPM Mode";
            BW2Res.Comment = "Spindle Disable";
            _bw2.ReportProgress(progress_cnt++);
            // Spindle back to RPM Mode
//            Start_SpindleRPM_Process(tSAx);
//            if (WaitForProgress() == false)
//            {
//                // There was an error of some kind!
//                BW2Res.Comment = "Spindle Mode Error";
//                BW2Res.Result = false;
//                e.Result = BW2Res;
//                return;
//            }

//            _bw2.ReportProgress(progress_cnt++);

            if(SpindleDisable() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Spindle Disable Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            SetToolInUse(tSAx.ToolPocket);

            BW2Res.Comment = "Get Tool Success";
            BW2Res.Result = true;
            e.Result = BW2Res;
            // done!
        }

        private void GetToolProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage > cMsgOffset)
            {
                int Pocket = e.ProgressPercentage - cMsgOffset;
                // just grabbed the tool from pocket number "Pocket"
                SetToolInUse(Pocket);   // update the ToolInSpindle number and the tool in use flag.
                OnUpdateCarousel(Pocket, (int)CarouselUpdateStates.Get );
            }
            else
            {
                BWProgressUpdate(BW2Res, e.ProgressPercentage);
            }
        }

        //private void GetToolCompleted(object sender, AsyncCompletedEventArgs e)
        private void GetToolCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw2.DoWork -= GetToolWorker;
            _bw2.ProgressChanged -= GetToolProgressChanged;
            _bw2.RunWorkerCompleted -= GetToolCompleted;
            CompleteActionStatus((BWResults)e.Result);
        }
        #endregion

        #region Manual Tool insert
        // this looks kind of redundant... have to see how this works out.
        public void Manual_GetTool(int ToolNumber)
        {
            TCActionProgress = true;
            int ToolSlot;
            ToolSlot = getSlot(ToolNumber);
            string toolmsg = string.Format("Insert Tool Number {0} into the spindle", ToolSlot);
            MessageBox.Show(toolmsg, "WARNING - DO NOT IGNORE!");
            // can I somehow make a red blinking warning on this? maybe a custom message box?
            ToolInSpindle = ToolSlot;
            ToolInSpinLen = GetToolLength(ToolSlot);
            TCActionProgress = false;
            OnProcessComplete();
            BW2Res.Result = true; // completed successfully 
        }
        #endregion

        #region Put a tool in the Tool Changer
        private void Start_PutTool(int xPocketNumber)
        {
            TCActionProgress = true;

            _bw2.WorkerReportsProgress = true;
            _bw2.WorkerSupportsCancellation = true;

            _bw2.DoWork += PutToolWorker;
            _bw2.ProgressChanged += PutToolProgressedChanged;
            _bw2.RunWorkerCompleted += PutToolCompleted;
            _bw2.RunWorkerAsync(xPocketNumber);
        }

        private void PutToolWorker(object sender, DoWorkEventArgs e)
        {
            // Assume the following state:
            // 1. Spindle has a tool in it
            // 2. The tool arm is retracted.
            // 3. the Carousel Slot to put the tool in is empty
            // 4. Tool Pocket Number to get is passed in the argument

            SingleAxis tSAx = new SingleAxis();

            TCProgress = true;
            ToolChangeStatus = false;
            BW2Res.Result = true; // start out at true;
            int progress_cnt = 0;

            if (MotionBusy()) // if the BW worker is busy then don't do this. - should probably set some kind of flag here...
            {
                // There was an error of some kind!
                BW2Res.Comment = "BW is Busy Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            BW2Res.Comment = "In Put Tool. Move to Z2";
            _bw2.ReportProgress(progress_cnt++);

            // start a new background worker
            // Move to H2 
            tSAx.Pos = xTCP.TC_H2_Z; // Z Height position and feedrate
            tSAx.Rate = xTCP.TC_H2_FR;
            Start_MoveZ_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Z Move Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // Index the spindle
            BW2Res.Comment = "Spindle Indexing";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Pos = xTCP.TC_Index;    // Spindle position and feedrate 
            tSAx.Rate = xTCP.TC_S_FR;
            Start_Spindle_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Spindle Index Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // rotate carousel to tool number
            tSAx.ToolPocket = (int)e.Argument; // carousel position number
            BW2Res.Comment = $"Carousel Indexing to {tSAx.ToolPocket}";
            _bw2.ReportProgress(progress_cnt++);

            // Check to see if the Carousel pocket is available.
//            if(CheckPocketEmpty(tSAx.ToolPocket) == false)
////            if (GetToolInUse(tSAx.ToolPocket) == false)
//            {
//                // this check to see if the pocket is assigned and the tool is in use ie the pocket is empty 
//                // waiting for the tool to be returned
//                BW2Res.Comment = $"Carousel {tSAx.ToolPocket} Not Empty Error";
//                BW2Res.Result = false;
//                e.Result = BW2Res;
//                return;
//            }

            Start_Carousel_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Carousel Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // Arm Out
            BW2Res.Comment = $"TLAUX Arm Out";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Move = bARM_OUT;
            Start_ARM_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Arm Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // Clamp Release
            BW2Res.Comment = $"Clamp Release";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Move = bRELEASE;
            Start_TClamp_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Clamp Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }
            // at this point the tool clamp has been released and has let go of the tool

            ToolInSpindle = 0;
            ToolInSpinLen = 0;

            BW2Res.Comment = $"Move to Z1";
            _bw2.ReportProgress(progress_cnt++);

            // move to H1
            tSAx.Pos = xTCP.TC_H1_Z;
            tSAx.Rate = xTCP.TC_H1_FR;
            Start_MoveZ_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Z Move Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // Clamp Engage
            BW2Res.Comment = $"Clamp Engage";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Move = bCLAMP;
            Start_TClamp_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Clamp Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // Arm In
            BW2Res.Comment = $"TLAUX Arm In";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Move = bARM_IN;
            Start_ARM_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Arm Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            _bw2.ReportProgress(cMsgOffset + tSAx.ToolPocket);

            //// Spindle back to RPM Mode
            //BW2Res.Comment = $"Spindle to RPM";
            //_bw2.ReportProgress(progress_cnt++);

            //Start_SpindleRPM_Process(tSAx);
            //if (WaitForProgress() == false)
            //{
            //    // There was an error of some kind!
            //    BW2Res.Comment = "Spindle Mode Error";
            //    BW2Res.Result = false;
            //    e.Result = BW2Res;
            //    return;
            //}

            // since the GCode S command will automatically set the RPM mode 
            // this has been changed to just Spindle Disable

            BW2Res.Comment = "Spindle Disable";
            _bw2.ReportProgress(progress_cnt++);

            if (SpindleDisable() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Spindle Disable Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            BW2Res.Comment = "Put Tool Success";
            BW2Res.Result = true;
            e.Result = BW2Res;
        }

        private void PutToolProgressedChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage > cMsgOffset)
            {
                int Pocket = e.ProgressPercentage - cMsgOffset;
                OnUpdateCarousel(Pocket, (int)CarouselUpdateStates.Put);
            }
            else
            {
                BWProgressUpdate(BW2Res, e.ProgressPercentage);
            }
        }

        private void PutToolCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw2.DoWork -= PutToolWorker;
            _bw2.ProgressChanged -= PutToolProgressedChanged;
            _bw2.RunWorkerCompleted -= PutToolCompleted;
            CompleteActionStatus((BWResults)e.Result);
            // update tool in holder status

            // MessageBox.Show("In Put Completed");
            // report that the tool change was a success!
        }
        #endregion

        #region Manual Tool Removal
        public void Manual_PutTool(int ToolNumber)
        {
            TCActionProgress = true;
            int ToolSlot;
            ToolSlot = getSlot(ToolNumber);
            string toolmsg = string.Format("Remove Tool number {0} from the spindle", ToolSlot);
            MessageBox.Show(toolmsg, "WARNING - DO NOT IGNORE!");
            ToolInSpindle = 0;
            ToolInSpinLen = 0;
            TCActionProgress = false;
            BW2Res.Result = true; // completed successfully
            OnProcessComplete();
        }
        #endregion

        #region Tool Exchange - Put A Get B without intermediate Arm retract

        public void Start_ExchangeTool(int PutTool, int GetTool)
        {
            TCActionProgress = true; // process action is happening...
            TExchangePosition TExPos = new TExchangePosition();
            TExPos.PutTool = PutTool;
            TExPos.GetTool = GetTool;
            _bw2.WorkerReportsProgress = true;
            _bw2.DoWork += ExchangeToolWorker;
            _bw2.ProgressChanged += ExchangeToolProgressChanged;
            _bw2.RunWorkerCompleted += ExchangeToolCompleted;
            _bw2.RunWorkerAsync(TExPos);    // will this get cleaned up before the other thread can access it?
        }

        private void ExchangeToolWorker(object sender, DoWorkEventArgs e)
        {
            // a combination of both put and get tools

            // Assume the following state:
            // 1. Spindle contains the "Put Tool"
            // 2. The tool arm is retracted.
            // 3. The Carousel space for the Put Tool is empty
            // 4. Tool Pocket Numbers to exchange are passed in the argument
            // this means that the tool numbers (not the carousel pocket) was already 
            // vetted and that both exist in the carousel 
            // - otherwise it a get tool and put tool would have been called by the ToolChangerDelux

            TCProgress = true;
            ToolChangeStatus = false;
            BW2Res.Result = true; // start out at true;
            int progress_cnt = 0;

            int PutPocket = ((TExchangePosition)e.Argument).PutTool;
            int GetPocket = ((TExchangePosition)e.Argument).GetTool;

            SingleAxis tSAx = new SingleAxis();

            if (MotionBusy()) // if the BW worker is busy
            {
                BW2Res.Result = false;
                BW2Res.Comment = "BW is busy";
                e.Result = BW2Res;
                return;
            }

            BW2Res.Comment = "Tool Exchange Move to Z2";
            _bw2.ReportProgress(progress_cnt++);
            // start a new background worker

            // Move to H2 
            tSAx.Pos = xTCP.TC_H2_Z; // Z Height position and feedrate
            tSAx.Rate = xTCP.TC_H2_FR;
            Start_MoveZ_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Z Move Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // Index the spindle
            BW2Res.Comment = "Spindle Indexing";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Pos = xTCP.TC_Index;    // Spindle position and feedrate 
            tSAx.Rate = xTCP.TC_S_FR;
            Start_Spindle_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Spindle Index Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // Check to see if the Carousel pocket is available.
//            if(CheckPocketEmpty(PutPocket) == false)
////            if (GetToolInUse(PutPocket) == false)
//            {
//            // this check is to see if the pocket is assigned and the tool is in use ie the pocket is empty 
//            // waiting for the tool to be returned
//                     BW2Res.Comment = $"Carousel {PutPocket} Not Empty Error";
//                     BW2Res.Result = false;
//                     e.Result = BW2Res;
//                     return;
//            }

            // rotate carousel to put tool number
            BW2Res.Comment = $"Carousel index to {PutPocket}";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.ToolPocket = PutPocket; // carousel position number
            Start_Carousel_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Carousel Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // Arm Out
            BW2Res.Comment = "TLAUX Arm Out";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Move = bARM_OUT;
            Start_ARM_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Arm Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // Clamp Release
            BW2Res.Comment = "Clamp Release";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Move = bRELEASE;
            Start_TClamp_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Clamp Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            _bw2.ReportProgress(cMsgOffset + cMsgOffset + PutPocket);
            // at this point the tool clamp has been released and has let go of the tool
     //       ClearToolInUse(PutPocket); // clear the tool in use flag for that carousel pocket
            ToolInSpindle = 0;  // if we do this here, then need to call setToolInUse once the selected tool is in the spindle
            ToolInSpinLen = 0;

            // move to H1
            BW2Res.Comment = "Move to Z1";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Pos = xTCP.TC_H1_Z;
            tSAx.Rate = xTCP.TC_H1_FR;
            Start_MoveZ_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Z Move Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // rotate carousel to the get tool number
            BW2Res.Comment = $"Carousel Index to {GetPocket}";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.ToolPocket = GetPocket;
            Start_Carousel_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Carousel Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }


            // move to H2
            BW2Res.Comment = "Move to Z2";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Pos = xTCP.TC_H2_Z;
            tSAx.Rate = xTCP.TC_H2_FR;
            Start_MoveZ_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Z2 Height Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // Clamp Engage
            BW2Res.Comment = "Clamp Engage";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Move = bCLAMP;
            Start_TClamp_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Clamp Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }
            // at this point the spindle contains the new tool
            //            SetToolInUse(GetPocket);
            //            ToolInSpindle = GetPocket;

            // Arm In
            BW2Res.Comment = "TLAUX Arm In";
            _bw2.ReportProgress(progress_cnt++);

            tSAx.Move = bARM_IN;
            Start_ARM_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Tool Arm Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            //// Spindle back to RPM Mode
            //BW2Res.Comment = "Spindle to RPM";
            //_bw2.ReportProgress(progress_cnt++);

            //Start_SpindleRPM_Process(tSAx);
            //if (WaitForProgress() == false)
            //{
            //    // There was an error of some kind!
            //    BW2Res.Comment = "Spindle Mode Error";
            //    BW2Res.Result = false;
            //    e.Result = BW2Res;
            //    return;
            //}
            //_bw2.ReportProgress(progress_cnt++);

            // since the GCode S command will automatically set the RPM mode 
            // this has been changed to just Spindle Disable

            BW2Res.Comment = "Spindle Disable";
            _bw2.ReportProgress(progress_cnt++);

            if (SpindleDisable() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Spindle Disable Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            SetToolInUse(GetPocket);  

            BW2Res.Comment = "Get Tool Success";
            BW2Res.Result = true;
            e.Result = BW2Res;
            // done!

        }

        private void ExchangeToolProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage > (cMsgOffset + cMsgOffset))
            {
                int Pocket = e.ProgressPercentage - cMsgOffset - cMsgOffset;
                OnUpdateCarousel(Pocket, (int)CarouselUpdateStates.Put);
            }
            else if (e.ProgressPercentage > cMsgOffset)
            {
                int Pocket = e.ProgressPercentage - cMsgOffset;
                OnUpdateCarousel(Pocket, (int)CarouselUpdateStates.Get);
            }
            else
            {
                BWProgressUpdate(BW2Res, e.ProgressPercentage);
            }
        }

        private void ExchangeToolCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw2.DoWork -= ExchangeToolWorker;
            _bw2.ProgressChanged -= ExchangeToolProgressChanged;
            _bw2.RunWorkerCompleted -= ExchangeToolCompleted;
            CompleteActionStatus((BWResults)e.Result);
        }


        #endregion

        private bool WaitForProgress()
        {
            // sleep while waiting for the global variable TCProgress to be set true by the 
            // background worker thread.
            // should there be a count here so it doesn't get stuck forever?
            int stuckCount = 0;
            do
            {
                if(stuckCount++ > 200)  // 20 seconds - seems like a long time for some action
                {
                    TCProgress = false;
                    ToolChangeStatus = false;
                    return ToolChangeStatus; 
                }
                Thread.Sleep(100);
            } while (TCProgress);
            TCProgress = true;
            // check the results for faults and errors
            return ToolChangeStatus;
        }

        private void CompleteActionStatus(BWResults res)
        {
            if (res.Result == true)
            {
                // Action completed successfully!
                OnProcessUpdate(("OK " + res.Comment)); // send the status message
            }
            else
            {
                // Action had an error somewhere.
                MessageBox.Show(res.Comment);
                OnProcessError(res.Comment);
                OnUpdateCarousel(0, (int)CarouselUpdateStates.Error);
            }
            TCActionProgress = false; // the action is done
            OnProcessComplete();
        }

        private void BWProgressUpdate(BWResults br, int cnt)
        {
            string ps = br.Comment + " " + cnt.ToString();
            if (br.Result == false)
            {

                OnProcessError(ps); // send the error message
            }
            else
            {
                OnProcessUpdate(ps); // send the status message
            }
        }

        #endregion

        #region Tool Table and Carousel Management

        // get the carousel pocket number given the tool index (SLOT in the tool table)
        // can also be used to determine if the tool is in the carousel. a -1 means that the tool is not in the carousel
        // a non negative number is the carousel pocket where the tool is.
        public int getPocket(int Index)
        {
            int slot = getSlot(Index);
            foreach (CarouselItem CI in CarouselList1.Items)
            {
                if (CI.ToolIndex == slot)
                { return CI.Pocket; }
            }
            return -1;
        }



        public int getSlot(int index)
        {
            int s, i;
            s = i = 0;
            double l, d, x, y;
            l = d = x = y = 0.00;
            KMx.CoordMotion.Interpreter.SetupParams.GetTool(index, ref s, ref i, ref l, ref d, ref x, ref y);
            return s;
        }

        private int getIndex(int Slot)
        {
            return 0;
        }

        public bool CarouselAddTool(int toolNumber, int Pocket)
        {
           foreach(CarouselItem CI in CarouselList1.Items)
            {
                if (CI.Pocket == Pocket)
                {
                    CI.ToolIndex = toolNumber;
                    CI.ToolInUse = false;
                    return true;    // success!
                }
            }
            return false;   // Problem
        }

        public bool CarouselDeleteTool(int Pocket)
        {
            foreach(CarouselItem CI in CarouselList1.Items)
            {
                if(CI.Pocket == Pocket)
                {
                    CI.ToolIndex = 0;   // clear the tool
                    CI.ToolInUse = false;
                    return true;    // success!
                }
            }
            return false;   // Problem
        }

        // set the tool in use flag for the current pocket, clear all others
        private void SetToolInUse(int Pocket)
        {
            bool toolExists = false;
            foreach (CarouselItem CI in CarouselList1.Items)
            {
                if (CI.Pocket == Pocket)    
                {
                    CI.ToolInUse = true;
                    ToolInSpindle = CI.ToolIndex;   // update the tool in spindle
                    ToolInSpinLen = GetToolLength(ToolInSpindle);
                    toolExists = true;
                }
                else  // can only have one tool in use at a time... so make everything else false.
                {
                    CI.ToolInUse = false;
                }
            }
            if(!toolExists) // if toolExists
            {
                MessageBox.Show("unknown tool int the spindle!");
            }
        }

        private void ClearToolInUse(int Pocket)
        {
            foreach (CarouselItem CI in CarouselList1.Items)
            {
                if (CI.Pocket == Pocket)
                {
                    CI.ToolInUse = false;
                    break;
                }
            }
        }

        private bool GetToolInUse(int Pocket)
        {
            foreach (CarouselItem CI in CarouselList1.Items)
            {
                if (CI.Pocket == Pocket)
                { return CI.ToolInUse; }
            }
            return false;   // should never get here... assuming a valid pocket is given
        }

        public bool CheckPocketEmpty(int Pocket)
        {
            // check for an empty space in the tool carousel
            // the criteria for emtpy carousel are either the tool number is 0 or the tool in use is checked. 
            // ToolInUse true means that the carousel pocket is assigned a tool, but the pocket is empty 
            // becasue that tool is in the spindle.
            foreach(CarouselItem CI in CarouselList1.Items)
            {
                if(CI.Pocket == Pocket)
                {
                    if (CI.ToolIndex == 0)
                    {
                        // pocket is not assigned
                        return true;
                    }
                    if(CI.ToolInUse == true)
                    {
                        // current pocket's tool is in the spindle so pocket is empty
                        return true;
                    }
                }
            }
            return false;
        }

        // check for a tool already in the carousel
        public bool ToolInCarousel(int ToolNumber)
        {
            if (ToolNumber == 0) // no tool 
                return false;
            foreach(CarouselItem CI in CarouselList1.Items)
            {
                if (CI.ToolIndex == ToolNumber)
                    return true;
            }
            return false;
        }

        public int ToolInCarouselPocket(int ToolNumber)
        {
            foreach(CarouselItem CI in CarouselList1.Items)
            {
                if (CI.ToolIndex == ToolNumber)
                    return CI.Pocket;
            }
            return -1;
        }

        public bool ToolInTable(int ToolNumber)
        {
            if(ToolNumber == 0)
            {
                return true;    // this tool number is always in the table....
            }
            foreach (Tool tl in TTable.Tools)
            {
                if (tl.ID == ToolNumber) return true;
                if (tl.slot == ToolNumber) return true;
            }
            return false;
        }

        public double GetToolLength(int ToolNumber)
        {
            foreach (Tool tool in TTable.Tools)
            {
                if((tool.ID == ToolNumber) || (tool.slot == ToolNumber))
                {
                    return tool.Length;
                }
            }
            return 0;
        }


        #endregion

        #region Events

        protected virtual void OnProcessUpdate(string x)
        {
            ProcessUpdate?.Invoke(x);   // if the ProcessUpdate delegate has been defined then call the delegate.
        }

        protected virtual void OnProcessError(string x)
        {
            ProcessError?.Invoke(x); // if the ProcessError delegate has been defined then call that delegate
        }

        protected virtual void OnUpdateCarousel(int pocket, int state)
        {
            UpdateCarousel?.Invoke(pocket, state); // if the UpdateCarousel delegate has been defined then call that delegate.
        }

        protected virtual void OnProcessComplete()
        {
            ProcessCompleted?.Invoke(); // need to figure out what should be passed here.
            // this should call the GUI delegate 
        }
        #endregion
    }

    public class ToolSetter : MachineMotion
    {

        // The back ground workers 
        static BackgroundWorker _bw3;   // process background worker.
        static BackgroundWorker _bwts;  // tool setter motion worker
        static BWResults BW3Res;
        static BWResults BWtsRes;

        // these static variable flags indicate tool change progress
        static bool TSChangeStatus;
        static bool TSProgress; // true indicates a process step is in progress, false indicates the process has finished. 
        public static bool TSActionInProgress;

        public static ProbeResult TSProbeState;

        public event dStatusMsg ProcessUpdate;
        public event dStatusMsg ProcessError;
        public event dStageDone ProcessCompleted;
        public event dStageDone ProbingCompleted;

        static private ToolChangeParams TSP;

        public static TS_Actions SetterAction;

        public ToolSetter(ref ToolInfo toolInfo)
        {
            TSProbeState = new ProbeResult();
            TSProbeState = ProbeResult.Idle;

            _bw3 = new BackgroundWorker();
            _bwts = new BackgroundWorker();
            BW3Res = new BWResults();
            BWtsRes = new BWResults();
            TSP = new ToolChangeParams();

            SetterAction = (int)TS_Actions.NoAction; // initialize to NoAction

        }

        public bool Start_ToolSetter(ToolSetterArguments TSArgs)
        {

            // check that the background process isn't already running.
            if (_bw3.IsBusy)
            { return false; } // don't run if the background worker is busy
            TSProbeState = ProbeResult.Probing;

            TSActionInProgress = true; // process action is happening...
            // need a class to hold the tool setter arguments?
            _bw3.WorkerReportsProgress = true;


            _bw3.DoWork += ToolSetter_Worker;
            _bw3.ProgressChanged += ToolSetterProgressChanged;
            _bw3.RunWorkerCompleted += ToolSetterCompleted;
            _bw3.RunWorkerAsync(TSArgs);
            return true;

        }

        private void ToolSetter_Worker(object sender, DoWorkEventArgs e)
        {
            ToolSetterArguments tTSArg;
            tTSArg = (ToolSetterArguments)e.Argument;    // get the arguments

            //            MessageBox.Show("Move to Safe Z");

            TSProgress = true;
            TSChangeStatus = true;
            TCProgress = true;     // Forgot to do this! Have to do this so the step processes will start correctly

            BW3Res.Result = true; // start out with this set.
            BW3Res.Comment = "Move to Safe Z";
            _bw3.ReportProgress(0);
            // Move to safe Z
            SingleAxis tSAx = new SingleAxis();
            tSAx.Pos = xTCP.TS_SAFE_Z;
            tSAx.Rate = 0;  // move at Traverse rate
            Start_MoveZ_Process(tSAx);

            if (WaitForTSProgress() == false)
            {
                // There was an error of some kind!
                BW3Res.Comment = "Safe Z Move Error";
                BW3Res.Result = false;
                e.Result = BW3Res;
                return;
            }

            //                MessageBox.Show("Move to X Y");
            BW3Res.Comment = "Move to X Y";
            _bw3.ReportProgress(0);
            // move to Tool Setter X, Y
            PlaneAxis tPAx = new PlaneAxis();
            tPAx.PosX = xTCP.TS_X + tTSArg.X_Offset;    // tool setter X + tool X offset
            tPAx.PosY = xTCP.TS_Y + tTSArg.Y_Offset;    // tool setter Y + tool Y offset
            tPAx.Rate = 0;  // move at traverse rate
            Start_MoveXY_Process(tPAx);
            if (WaitForTSProgress() == false)
            {
                // There was an error of some kind!
                BW3Res.Comment = "X Y Move Error";
                BW3Res.Result = false;
                e.Result = BW3Res;
                return;
            }

            // index the spindle here!


            BW3Res.Comment = "Spindle Indexing";
            _bw3.ReportProgress(0);

            // Index the spindle
            tSAx.Pos = xTCP.TS_S;    // Spindle position and feedrate 
            tSAx.Rate = xTCP.TC_S_FR;   // use the same feedrate as a tool change.
            Start_Spindle_Process(tSAx);
            if (WaitForTSProgress() == false)
            {
                // There was an error of some kind!
                BW3Res.Comment = "TC Index Error";
                BW3Res.Result = false;
                e.Result = BW3Res;
                return;
            }

            // disable the spindle? maybe move this to the end?

            if (SpindleDisable() == false)
            {
                // There was an error of some kind!
                BW3Res.Comment = "Spindle Disable Error";
                BW3Res.Result = false;
                e.Result = BW3Res;
                return;
            }

            // MessageBox.Show("Move to Tool Z");
            BW3Res.Comment = "Move to Tool Z";
            _bw3.ReportProgress(0);

            // move to Tool Setter Z (somewhere above the tool setter TBD inches)
            tSAx.Pos = xTCP.TS_Z;
            tSAx.Rate = 0;
            Start_MoveZ_Process(tSAx);
            if (WaitForTSProgress() == false)
            {
                // There was an error of some kind!
                BW3Res.Comment = "Tool Z Move Error";
                BW3Res.Result = false;
                e.Result = BW3Res;
                return;
            }

            // - estimated tool or default tool length


            //MessageBox.Show("Probing");
            BW3Res.Comment = "Probing";
            _bw3.ReportProgress(0);
            // move while waiting for the tool setter to detect
            // the probing command
            // Check whether to use the full length of the probe cycle or to limit it to slightly more than the e
            // expected tool length
            double ProbeDistance = 0;
            double ProbeRate;

            if (tTSArg.UseExpectedZ)
            {
                ProbeDistance = tTSArg.Z_Offset + 0.25;
            }
            else
            {
                // double check this math!
                ProbeDistance = xTCP.TS_Z - xTCP.TS_RefZ - 1.5; // this is a 1.5 inch buffer...  because the spindle can't go all the way down to the tool setter.
            }

            ProbeRate = xTCP.TS_FR1;    // the hunting rate
            Start_ToolSetterZProbe(ProbeDistance, ProbeRate);    // this should be a little more than the current offset
            if (WaitForTSProgress() == false)
            {
                // There was an error
                BW3Res.Comment = "Probing Error";
                BW3Res.Result = false;
                e.Result = BW3Res;
                return;
            }
            // save the length / offset - the probe complete call back should take care of this

            tTSArg.AverageCount--;  // decrement the average counter
            while (tTSArg.AverageCount > 0)  // if it is still greater than 0 then 
            {
                BW3Res.Comment = "Averaging Backoff";
                _bw3.ReportProgress(0);
                // repeat cycle times for the averaging
                // move to back off Z
                tSAx.Pos = TSCoord.Z + xTCP.TS_AveBackoff; // get the current z position and add the backoff distance
                tSAx.Rate = 0;  // rapid move

                // move back to this position - rapid move 
                Start_MoveZ_Process(tSAx);
                if (WaitForTSProgress() == false)
                {
                    // There was an error of some kind!
                    BW3Res.Comment = "Average Move Error";
                    BW3Res.Result = false;
                    e.Result = BW3Res;
                    return;
                }

                BW3Res.Comment = "Averaging Probing";
                _bw3.ReportProgress(0);
                // probe again to previous probe position - backoff Z + just a little bit.
                ProbeDistance = xTCP.TS_AveBackoff + 0.125; // backoff plus 0.125 inch - if it is off by more than this there is big trouble!
                ProbeRate = xTCP.TS_FR1;    // the hunting rate

                Start_ToolSetterZProbe(ProbeDistance, ProbeRate);    // this should be a little more than the current offset

                if (WaitForTSProgress() == false)
                {
                    // There was an error
                    BW3Res.Comment = "Probing Error";
                    BW3Res.Result = false;
                    e.Result = BW3Res;
                    return;
                }

                tTSArg.AverageCount--; // decrement AveCount and try again!
            }

            // move back to Safe Z
            // MessageBox.Show("Back to Safe Z");
            BW3Res.Comment = "Move back to Safe Z";
            _bw3.ReportProgress(0);
            TSProgress = true;

            // !!!!
            tSAx.Pos = xTCP.TS_SAFE_Z;
            tSAx.Rate = 0;  // move at Traverse rate
            Start_MoveZ_Process(tSAx);

            if (WaitForTSProgress() == false)
            {
                // There was an error of some kind!
                BW3Res.Comment = "Safe Z Move Error";
                BW3Res.Result = false;
                e.Result = BW3Res;
                return;
            }

            BW3Res.Comment = "Tool Setter Success";
            BW3Res.Result = true;
            e.Result = BW3Res;

        }

        private void ToolSetterProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            TSProgressUpdate(BW3Res);
        }

        private void ToolSetterCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // check the ProcessError!
            // remove the process from background worker 2
            _bw3.DoWork -= ToolSetter_Worker;
            _bw3.ProgressChanged -= ToolSetterProgressChanged;
            _bw3.RunWorkerCompleted -= ToolSetterCompleted;
            CompleteActionStatus((BWResults)e.Result);
            OnProcessComplete();
        }

        private bool WaitForTSProgress()
        {
            // sleep while waiting for the global variable TCProgress to be set true by the 
            // background worker thread.
            // should there be a count here so it doesn't get stuck forever?
            int stuckCount = 0;
            do
            {
                Thread.Sleep(100);
                if (stuckCount++ > 300) // 30 seconds seems like long enough
                {
                    // TCProgress = false;
                    TSChangeStatus = false;
                    break; // return TSChangeStatus;
                }
            } while (TCProgress);
            TCProgress = true;
            // check the results for faults and errors
            return TSChangeStatus;  // this cannot be correct
        }

        private void CompleteActionStatus(BWResults res)
        {
            if (res.Result == true)
            {
                // Action completed successfully!

            }
            else
            {
                // Action had an error somewhere.
                MessageBox.Show(res.Comment);
            }
            TSActionInProgress = false; // the action is done
        }

        #region Tool Setter Probing

        // this probing routine will probe in the negative Z direction for "dist" distance in inches 
        // at "rate" inches per min 
        // it calls the T2_TOOL_SET command running on thread 2 on the KFLOP board
        // it waits for detection, or timeout. 
        // it always updates the TSCoord variable (MachineMotion static variable) with the resulting position
        // detection or not. It also updates the TSProbeState variable (ToolSetter static variable) with the probe result. 

        private void Start_ToolSetterZProbe(double dist, double rate)
        {
            // double MotionRate = -15.0;    // inch per min - initial testing value
            double mrZ;
            double ProbeDistance = dist; // probe distance  in inches - this should take about 
                                         //            mrZ = (MotionRate * KMx.CoordMotion.MotionParams.CountsPerInchZ) / 60.0; // motion rate(in/min) * (counts/inch) / (60sec/ min)
            mrZ = (-rate * KMx.CoordMotion.MotionParams.CountsPerInchZ) / 60.0; // motion rate(in/min) * (counts/inch) / (60sec/ min)


            double PTimeout;
            // timeout is (distance/rate) * (ms/min)
            PTimeout = (ProbeDistance / Math.Abs(rate)) * 60.0;
            //            PTimeout = (ProbeDistance / Math.Abs(MotionRate)) * 60.0;
            // convert to seconds for timeout  -
            // this is a simplification because it doesn't account for acceleration and deceleration times, but it is a start.
            //            MessageBox.Show("In Tool Setter Probe");
            // set the Persist Variables
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT1, (float)mrZ);
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT2, (float)0.0);
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT3, (float)0.0);
            KMx.SetUserDataFloat(PVConst.P_NOTIFY_ARGUMENT4, (float)PTimeout);

            // execute program 2  to probe
            KMx.SetUserData(PVConst.P_NOTIFY, (T2Const.T2_TOOL_SET));    // probe Z
            KMx.ExecuteProgram(2);

            _bwts.WorkerReportsProgress = true;
            _bwts.WorkerSupportsCancellation = true;

            _bwts.DoWork += TSProbe_Worker; // Add the main thread method to call
            _bwts.ProgressChanged += TSProbe_ProgressChanged; // add the progress changed method
            _bwts.RunWorkerCompleted += TSProbe_Completed; // the the method to call when the function is done
            _bwts.RunWorkerAsync(PTimeout);

        }

        private void TSProbe_Worker(object sender, DoWorkEventArgs e)
        {
            // very similar to the probe worker in the probe panel

            double TimeOut = (double)e.Argument; // the delay time for the probing operation
            int Tdone = (int)(TimeOut * 10.0 * 1.05) + 25;  // convert the time to sleep counts (5% overhead for starting and stopping) plus 2.5 seconds to double touch
            // note that the timeout is only for error detection. motion timeout is controled by the call to T2_TOOL_SET
            int SleepCnt = 0;

            TSProbeState = ProbeResult.Probing;
            do
            {
                Thread.Sleep(100);
                // get the completion status
                if (SleepCnt++ > Tdone)
                {
                    TSProbeState = ProbeResult.SoftTimeOut;
                    MessageBox.Show("Probe Timeout");
                    BWtsRes.Comment = "Probe Timeout";
                    BWtsRes.Result = false; // must set the BWRes and e.Result before returning!
                    e.Result = BWtsRes;
                    return;
                }
            } while (CheckForTSProbeComplete() != true);
            // look at the probe results to determine what to do next
            string Pmsg = "Done";
            // MachineCoordinates MCx = new MachineCoordinates();
            GetCoordinates(ref TSCoord);
            switch (TSProbeState)
            {
                case ProbeResult.Detected:
                    {
                        // get the machine coordinates.
                        Pmsg = String.Format("Probe Detect at\n X:{0}\nY:{1}\nZ{2}", TSCoord.X, TSCoord.Y, TSCoord.Z);
                        BWtsRes.Result = true;
                        break;
                    }
                case ProbeResult.MachineTimeOut:
                    {
                        Pmsg = String.Format("Machine timeout. Currently at\n X:{0}\nY:{1}\nZ{2}", TSCoord.X, TSCoord.Y, TSCoord.Z);
                        BWtsRes.Result = true;
                        break;
                    }
                case ProbeResult.T2_ProbeError:
                    {
                        Pmsg = String.Format("Probe Error\nMachine at\n X:{0}\nY:{1}\nZ{2}", TSCoord.X, TSCoord.Y, TSCoord.Z);
                        BWtsRes.Result = false;
                        BWtsRes.Comment = "Probe Error";
                        break;
                    }
                default:
                    {
                        BWtsRes.Result = false;
                        BWtsRes.Comment = "Default Error";
                        break;
                    }
            }
            Pmsg += String.Format("\nProbe State = {0}", TSProbeState);
            // MessageBox.Show(Pmsg);
            e.Result = BWtsRes;
        }

        private void TSProbe_ProgressChanged(object sender, ProgressChangedEventArgs e)
        { }

        private void TSProbe_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            // remove the methods from background worker
            _bwts.DoWork -= TSProbe_Worker;
            _bwts.ProgressChanged -= TSProbe_ProgressChanged;
            _bwts.RunWorkerCompleted -= TSProbe_Completed;
            CompleteStatus((BWResults)e.Result);
            OnProbeComplete(); // the callback.
        }

        private bool CheckForTSProbeComplete()
        {
            // is thread 2 still running?
            if (KMx.ThreadExecuting(2) == false) // thread 2 has finished running
            {
                TSProbeState = ProbeResult.T2_ProbeError;
                // get the persist variables the indicate the probing has finished.
                int ProbeStatus = KMx.GetUserData(PVConst.P_STATUS);
                if (BitOps.AnyInMask(ProbeStatus, unchecked((int)PVConst.SB_PROBE_STATUS_MASK)))  // only check the probe status bits
                {
                    if (BitOps.BitIsSet(ProbeStatus, PVConst.SB_PROBE_DETECT))
                    { TSProbeState = ProbeResult.Detected; }
                    else if (BitOps.BitIsSet(ProbeStatus, PVConst.SB_PROBE_TIMEOUT))
                    { TSProbeState = ProbeResult.MachineTimeOut; }
                }

                return true;
            }
            return false;
        }
        #endregion
        private void TSProgressUpdate(BWResults br)
        {
            string ps = br.Comment;
            if (br.Result == false)
            {

                OnProcessError(ps); // send the error message
            }
            else
            {
                OnProcessUpdate(ps); // send the status message
            }
        }

        #region Tool update
        public void UpdateTool(Tool tool)
        {
            KMx.CoordMotion.Interpreter.SetupParams.SetTool(tool.index, tool.slot, tool.ID, tool.Length, tool.Diameter, tool.XOffset, tool.YOffset);
        }
        #endregion

        #region Events
        protected virtual void OnProcessUpdate(string x)
        {
            ProcessUpdate?.Invoke(x);   // if the ProcessUpdate delegate has been defined then call the delegate.
        }

        protected virtual void OnProcessError(string x)
        {
            ProcessError?.Invoke(x); // if the ProcessError delegate has been defined then call that delegate
        }

        protected virtual void OnProcessComplete()
        {
            ProcessCompleted?.Invoke(); // need to figure out what should be passed here.
            // this should call the GUI delegate 
        }

        protected virtual void OnProbeComplete()
        {
            ProbingCompleted?.Invoke(); // callback for probing completed.
        }
        #endregion

    }
    

    public class Probe : MachineMotion
    {

    }

    public class MachineMotion
    {

        #region Machine Motion variables and states
        // used to point to the instance of KM controller 
        static public KM_Controller KMx { get; set; }
        // used to point to the instance of KM_Axis that is the spindle axis
        static public KM_Axis SPx { get; set; }

        // used to point to the configuration file list. 
        static public ConfigFiles CFx { get; set; }

        static public ToolChangeParams xTCP;    // this will be initialized from the configuration files

        // background workers
        static BackgroundWorker _bw;    // motion background worker
        static BWResults BWRes;

        // these are used for locking the various stages of the tool changer 
        static readonly object _Tlocker = new object();
        static readonly object _Slocker = new object();
        static readonly object _SPlocker = new object();

        // these static variable flags indicate tool change progress
        // these two boolean variables should probably be named MotionStepStatus and MotionStepProgress
        // because they are for more than just the tool changer
        static public bool ToolChangeStatus; // true indicates everything OK false indcates an fault occured
        static public bool TCProgress; // true indicates a process step is in progress, false indicates the process has finished. 

        // global variables for the user control
        // since there is only one Machine and tool changer these are all static.
        public static bool SpindleEnabled;
        public static bool SpindlePID;
        public static bool SpindleRPM;
        public static bool SpindleHomed;
        static double Spindle_Position;
        static int iSpindle_Status;
        static bool bTC_Clamped;
        static bool bTC_UnClamped;
        static bool bTLAUX_ARM_IN;
        static bool bTLAUX_ARM_OUT;
        static int Carousel_Position;
        static bool bTLAUX_FAULT;
        static int iTLAUX_STATUS;

        static public bool TestBench { get; set; }

        static public MachineCoordinates MC; // some machine coordinate
        static public MachineCoordinates TSCoord;   // last known Tool setter coordinate
        static public MachineCoordinates ProbeCoord;  // last known Probe coordinate

        #endregion

        public MachineMotion()
        {
            if (KMx == null)
            {
                MessageBox.Show("MachineMotion Must be instaniated first!");
            }
        }

        public MachineMotion(ref KM_Controller X, ref KM_Axis SP, ref ConfigFiles CFiles)
        {
            KMx = X;
            SPx = SP;
            CFx = CFiles;

            _bw = new BackgroundWorker();
            BWRes = new BWResults();

#if TESTBENCH
            TestBench = true;
#else
            TestBench = false;
#endif

            LoadTCCfg();

        }

#region Configuration File methods
        // Configuration file - get the tool changer variables saved in the JSON file
        // these are all the tool change coordinates and speeds.
        public void LoadTCCfg()   // load the tool changer configuration
        {
            string LFileName = System.IO.Path.Combine(CFx.ConfigPath, CFx.ToolChangeParams);
            // load the tool changer files
            try
            {
                if (System.IO.File.Exists(LFileName) == true)
                {
                    JsonSerializer Jser = new JsonSerializer();
                    StreamReader sr = new StreamReader(LFileName);
                    JsonReader Jreader = new JsonTextReader(sr);
                    xTCP = Jser.Deserialize<ToolChangeParams>(Jreader);
                    sr.Close();
                }
            }
            catch
            {
                MessageBox.Show(LFileName, "TLAUX Parameters Load Error!");
            }
        }

        public void SaveTCCfg() // Save tool changer config
        {
            string FName = System.IO.Path.Combine(CFx.ConfigPath, CFx.ToolChangeParams);
            try
            {
                var SaveFile = new SaveFileDialog();
                SaveFile.FileName = FName;
                if (SaveFile.ShowDialog() == true)
                {
                    JsonSerializer Jser = new JsonSerializer();
                    StreamWriter sw = new StreamWriter(SaveFile.FileName);
                    JsonTextWriter Jwrite = new JsonTextWriter(sw);
                    Jser.NullValueHandling = NullValueHandling.Ignore;
                    Jser.Formatting = Newtonsoft.Json.Formatting.Indented;
                    Jser.Serialize(Jwrite, xTCP);
                    sw.Close();
                }
            }
            catch
            {
                MessageBox.Show(FName, "TLAUX Parameters Save Error!");
            }
        }

#endregion
        // load and save the parameters

        public bool MotionBusy()
        {
            return _bw.IsBusy;
        }

#region Individual tool changer motions

#region MoveZ background process
        // the move Z background process

        public void Start_MoveZ_Process(SingleAxis SA)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += MoveZ_Worker; // Add the main thread method to call
            _bw.ProgressChanged += MoveZ_ProgressChanged; // add the progress changed method
            _bw.RunWorkerCompleted += MoveZ_Completed; // the the method to call when the function is done
            _bw.RunWorkerAsync(SA);
        }

        private void MoveZ_Worker(object sender, DoWorkEventArgs e)
        {
            SingleAxis SAx = (SingleAxis)e.Argument;    // this gets the argument and interprets it as a single axis class

            //  // lets try this with StartMoveTo...
            //  var Zxs = KMx.GetAxis(AXConst.Z_AXIS, "Z-Axis");
            //  // get the CPU - counts per unit from the interpreter.
            //  Zxs.CPU = KMx.CoordMotion.MotionParams.CountsPerInchZ;
            ////  Zxs.Acceleration = KMx.CoordMotion.MotionParams.MaxAccelZ;
            //  double CRate = SAx.Rate * Zxs.CPU;
            //  Zxs.Velocity = CRate;
            //  Zxs.StartMoveTo(SAx.Pos);

            // check that the axis is enabled
            int ax, ay, az, aa, ab, ac;
            ax = ay = az = aa = ab = ac = 0;
            KMx.CoordMotion.GetAxisDefinitions(ref ax, ref ay, ref az, ref aa, ref ab, ref ac);
            if (az == -1)
            {
                BWRes.Result = false;
                BWRes.Comment = "Axis not enabled";
                // e.Result = "Axis not enabled";
                e.Result = BWRes;
                return;
            }

            double cX, cY, cZ, cA, cB, cC;
            cX = cY = cZ = cA = cB = cC = 0;

            KMx.CoordMotion.ReadAndSyncCurPositions(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC);

            if (SAx.Rate == 0)      // if the rate is 0 then assume traverse motion (fastest rate)
            {
                KMx.CoordMotion.StraightTraverse(cX, cY, SAx.Pos, cA, cB, cC, false);
            }
            else
            {
                // may want to change the last two variables to pass information to the 3D path generation
                KMx.CoordMotion.StraightFeed(SAx.Rate, cX, cY, SAx.Pos, cA, cB, cC, 0, 0);
            }
            KMx.CoordMotion.FlushSegments();    // push the segments out the buffer and into the KFLOP
            KMx.CoordMotion.WaitForSegmentsFinished(false); // - this works, but blocks the calling thread - so how to check if it is done?
            BWRes.Result = true;
            BWRes.Comment = "Z Motion done";
            // e.Result += "Motion done";
            e.Result = BWRes;

        }

        private void MoveZ_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void MoveZ_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            // MessageBox.Show(xBWR.Comment + xBWR.Result.ToString());
            _bw.DoWork -= MoveZ_Worker;
            _bw.ProgressChanged -= MoveZ_ProgressChanged;
            _bw.RunWorkerCompleted -= MoveZ_Completed;
            //           btnAbort.IsEnabled = false; // disable the abort button -- this doesn't work... need a completely thread safe way to do this...
            //            Application.Current.Dispatcher.Invoke(() =>    // Dispatcher to the rescue!
            //                btnAbort.IsEnabled = false
            //             );
            CompleteStatus((BWResults)e.Result);
        }
#endregion

#region Move XY background process
        // move in the XY plane to a specific spot - like the tool setter position
        public void Start_MoveXY_Process(PlaneAxis PA)
        {
            // start a new background worker with move to X,Y
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += MoveXY_Worker; // Add the main thread method to call
            _bw.ProgressChanged += MoveXY_ProgressChanged; // add the progress changed method
            _bw.RunWorkerCompleted += MoveXY_Completed; // the the method to call when the function is done
            _bw.RunWorkerAsync(PA);
        }

        private void MoveXY_Worker(object sender, DoWorkEventArgs e)
        {
            PlaneAxis PAx = (PlaneAxis)e.Argument;

            // check that the axis is enabled
            int ax, ay, az, aa, ab, ac;
            ax = ay = az = aa = ab = ac = 0;
            KMx.CoordMotion.GetAxisDefinitions(ref ax, ref ay, ref az, ref aa, ref ab, ref ac);
            if ((az == -1) || (ax == -1) || (ay == -1)) // check all three axis
            {
                BWRes.Result = false;
                BWRes.Comment = "Axis not enabled";
                // e.Result = "Axis not enabled";
                e.Result = BWRes;
                return;
            }

            double cX, cY, cZ, cA, cB, cC;  // holder for the current positions
            cX = cY = cZ = cA = cB = cC = 0;

            KMx.CoordMotion.ReadAndSyncCurPositions(ref cX, ref cY, ref cZ, ref cA, ref cB, ref cC);

            if (PAx.Rate == 0)  // if rate is 0 assume traverse motion
            {
                KMx.CoordMotion.StraightTraverse(PAx.PosX, PAx.PosY, cZ, cA, cB, cC, false);
            }
            else
            {
                // may want to change the last two variables to pass information to the 3D path generation
                KMx.CoordMotion.StraightFeed(PAx.Rate, PAx.PosX, PAx.PosY, cZ, cA, cB, cC, 0, 0);
            }
            KMx.CoordMotion.FlushSegments();    // push the segments out the buffer and into the KFLOP
            KMx.CoordMotion.WaitForSegmentsFinished(false); // - this works, but blocks the calling thread - so how to check if it is done?
                                                            // this is why it is in a background worker...
            BWRes.Result = true;
            BWRes.Comment = "XY Motion done";
            // e.Result += "Motion done";
            e.Result = BWRes;
        }
        private void MoveXY_ProgressChanged(object sender, ProgressChangedEventArgs e)
        { }

        private void MoveXY_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            // MessageBox.Show(xBWR.Comment + xBWR.Result.ToString());
            // remove the workers from the background process
            _bw.DoWork -= MoveXY_Worker;
            _bw.ProgressChanged -= MoveXY_ProgressChanged;
            _bw.RunWorkerCompleted -= MoveXY_Completed;
            //           btnAbort.IsEnabled = false; // disable the abort button -- this doesn't work... need a completely thread safe way to do this...
            //            Dispatcher.Invoke(() =>    // Dispatcher to the rescue!
            //                btnAbort.IsEnabled = false
            //             );
            CompleteStatus((BWResults)e.Result);
        }
#endregion

#region Carousel background process
        // Rotate Carousel background process
        public void Start_Carousel_Process(SingleAxis SA)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += Carousel_Worker;
            _bw.ProgressChanged += Carousel_ProgressChanged;
            _bw.RunWorkerCompleted += Carousel_Completed;

            _bw.RunWorkerAsync(SA.ToolPocket);
        }

        private void Carousel_Worker(object sender, DoWorkEventArgs e)
        {
            int car_num = (int)e.Argument;
            getTLAUX_Status();
            if (Carousel_Position == car_num)
            {
                BWRes.Result = true;
                BWRes.Comment = "Carousel at " + car_num;
                e.Result = BWRes;
                return;
            }
            KMx.SetUserData(PVConst.P_NOTIFY, (T2Const.T2_SEL_TOOL | car_num));
            KMx.ExecuteProgram(2);
            int timeoutCnt = 0;
            do
            {
                Thread.Sleep(100);
                getTLAUX_Status();
                if (timeoutCnt++ > 50)
                {
                    BWRes.Result = false;
                    BWRes.Comment = "Carousel Timeout";
                    e.Result = BWRes;
                    return;
                }
            } while (Carousel_Position != car_num);
            BWRes.Result = true;
            BWRes.Comment = "Carousel moved to " + car_num;
            e.Result = BWRes;
            return;
        }

        private void Carousel_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void Carousel_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw.DoWork -= Carousel_Worker;
            _bw.ProgressChanged -= Carousel_ProgressChanged;
            _bw.RunWorkerCompleted -= Carousel_Completed;
            CompleteStatus((BWResults)e.Result);
        }
#endregion

#region Spindle Control
#region Spindle PID Mode Enable
        // put the spindle axis into PID mode 

#endregion

#region Align the Spindle
        // enable and align the spindle to the tool change position. 
        public bool AlignSpindle(double pos, double rate)
        {
            lock (_SPlocker)
            {
                int timeoutCnt = 0;
                // if the spindle has not been homed then home it.
                // if the spindle index > 10000 or < -10000 (+/- 5 turns) then re-home it
                getSpindle_Status();

                if ((SpindleHomed == false)   // the spindle has not been homed.
                    || (Spindle_Position > 10000)                       // or position is far high
                    || (Spindle_Position < -10000))                     // or position is far low
                {
                    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_ZERO);
                    KMx.ExecuteProgram(2);
                    // wait until it is done.
                    do
                    {
                        Thread.Sleep(100);
                        if (timeoutCnt++ > 50) { return false; }
                    } while (SPx.MotionComplete() != true); // I think this will indicate a completed motion.
                }
                getSpindle_Status();
                // if the spindle is not enabled then enable it
                if (SpindlePID == false)
                {
                    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_PID);
                    KMx.ExecuteProgram(2);
                    Thread.Sleep(50);
                }
                getSpindle_Status();
                if (SpindleEnabled == false)
                {
                    KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_EN);
                    KMx.ExecuteProgram(2);
                    Thread.Sleep(20);
                }
                SPx.Velocity = rate;
                SPx.StartMoveTo(pos);
                // done
                // wait until it is done.
                timeoutCnt = 0;
                do
                {
                    Thread.Sleep(100);
                    if (timeoutCnt++ > 30) { return false; }
                } while (SPx.MotionComplete() != true);

                // note - this leaves the spindle enabled!
                // All Done!
                return true;
            }
        }
#endregion

#region Disable Spindle

        public bool SpindleDisable()
        {
            KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_DIS);
            KMx.ExecuteProgram(2);
            if (KMx.WaitForThreadComplete(2, 2000) == false)
            {
                MessageBox.Show("Thread 2 stuck!");
                return false;
            }
            return true;
        }
#endregion

#region Spindle Indexing process
        // Index Spindle background process
        public void Start_Spindle_Process(SingleAxis SA)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += Spindle_Worker;
            _bw.ProgressChanged += Spindle_ProgressChanged;
            _bw.RunWorkerCompleted += Spindle_Completed;

            _bw.RunWorkerAsync(SA);
        }

        private void Spindle_Worker(object sender, DoWorkEventArgs e)
        {
            SingleAxis SAs = (SingleAxis)e.Argument;

            int timeoutCnt = 0;
            // if the spindle has not been homed then home it.
            // if the spindle index > 10000 or < -10000 (+/- 5 turns) then re-home it
            getSpindle_Status();

            if ((SpindleHomed == false)   // the spindle has not been homed.
                || (Spindle_Position > 5000)                       // or position is far high
                || (Spindle_Position < -5000)                     // or position is far low
                || (SpindleRPM == true))                          // or it is in RPM mode.
            {
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_ZERO);
                KMx.ExecuteProgram(2);
                // wait until it is done.
                do
                {
                    Thread.Sleep(100);
                    if (timeoutCnt++ > 50)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle Timeout- DANG!";
                        e.Result = BWRes;
                        return;
                    }
                    getSpindle_Status();
                } while (Math.Abs(Spindle_Position) > 2); // I think this will indicate a completed Home routine
            }
            // getSpindle_Status();
            // if the spindle is not enabled then enable it
            if (SpindlePID == false)
            {
                timeoutCnt = 0;
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_PID);  // send the command
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(100);
                    if (timeoutCnt++ > 20)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle PID Timeout";
                        e.Result = BWRes;
                        return;
                    }
                    getSpindle_Status();
                } while (SpindlePID == false);
            }

            if (SpindleEnabled == false)
            {
                timeoutCnt = 0;
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_EN);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(50);
                    if (timeoutCnt++ > 20)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle Enable Timeout";
                        e.Result = BWRes;
                        return;
                    }
                    getSpindle_Status();
                } while (SpindleEnabled == false);
            }

            SPx.Velocity = SAs.Rate;
            SPx.StartMoveTo(SAs.Pos);
            timeoutCnt = 0;

#if TESTBENCH
            Thread.Sleep(100);  // give it a little pause just for simulation sake.
            SPx.Stop();
            SPx.SetCurrentPosition(SAs.Pos);
            // stop the commanded motion.
#else

            do
            {    // Wait until done or timeout
                Thread.Sleep(100);
                // can I force the spindle position here for the test bench?

                if (timeoutCnt++ > 50)
                {
                    BWRes.Result = false;
                    BWRes.Comment = "Spindle Index Timeout";
                    e.Result = BWRes;
                    return;
                }
            } while (SPx.MotionComplete() != true);
#endif
            // note - this leaves the spindle enabled!
            // All Done!

            BWRes.Result = true;
            BWRes.Comment = "Spindle Index Done";
            e.Result = BWRes;

            return;
            //            }
        }

        private void Spindle_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void Spindle_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw.DoWork -= Spindle_Worker;
            _bw.ProgressChanged -= Spindle_ProgressChanged;
            _bw.RunWorkerCompleted -= Spindle_Completed;
            CompleteStatus((BWResults)e.Result);
        }
#endregion

#region Spindle Return to RPM mode.
        // return the Spindle back to RPM mode.
        public void Start_SpindleRPM_Process(SingleAxis SA)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += SpindleRPM_Worker;
            _bw.ProgressChanged += SpindleRPM_ProgressChanged;
            _bw.RunWorkerCompleted += SpindleRPM_Completed;

            _bw.RunWorkerAsync(SA);
        }

        private void SpindleRPM_Worker(object sender, DoWorkEventArgs e)
        {

#if TESTBENCH
#else
            int timeoutCnt;
            // Disable the spindle and set the spindle back to RPM Mode 
            getSpindle_Status();
            if (SpindleEnabled == true)
            {
                timeoutCnt = 0;
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_DIS);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(50);
                    if (timeoutCnt++ > 20)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle Disable Timeout";
                        e.Result = BWRes;
                        return;
                    }
                    getSpindle_Status();
                } while (SpindleEnabled == true);
            }
            if (SpindleRPM == false)
            {
                timeoutCnt = 0;
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_RPM);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(50);
                    if (timeoutCnt++ > 30)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle RPM Timeout";
                        e.Result = BWRes;
                        return;
                    }
                    getSpindle_Status();
                } while (SpindleRPM == false);
            }
#endif
            BWRes.Result = true;
            BWRes.Comment = "Spindle Disabled (RPM)";
            e.Result = BWRes;
        }

        private void SpindleRPM_ProgressChanged(object sender, ProgressChangedEventArgs e)
        { }

        private void SpindleRPM_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw.DoWork -= SpindleRPM_Worker;
            _bw.ProgressChanged -= SpindleRPM_ProgressChanged;
            _bw.RunWorkerCompleted -= SpindleRPM_Completed;
            CompleteStatus((BWResults)e.Result);
        }
#endregion

#endregion

#region Tool Changer Arm process
        // TC Arm In/Out background process
        public void Start_ARM_Process(SingleAxis SA)
        {
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += TLAUX_ARM_Worker; // Add the main thread method to call
            _bw.ProgressChanged += TLAUX_ARM_ProgressChanged; // add the progress changed method
            _bw.RunWorkerCompleted += TLAUX_ARM_Completed; // the the method to call when the function is done
            _bw.RunWorkerAsync(SA.Move);
        }

        private void TLAUX_ARM_Worker(object sender, DoWorkEventArgs e)
        {
            // get the TLAUX Status
            getTLAUX_Status();
            if (bTLAUX_FAULT)
            {
                BWRes.Result = false;
                BWRes.Comment = "TLAUX Fault";
                e.Result = BWRes;
                return;
            }
            int TimeoutCnt = 0;
            if ((bool)e.Argument)    // true is arm in, false is arm out
            {
                BWRes.Comment = "TC Arm In";
                if (bTLAUX_ARM_IN)  // if the arm in then already done
                {
                    BWRes.Result = true;
                    e.Result = BWRes;
                    return;
                }
                // send the command to move the arm in
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_ARM_IN);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(100);
                    getTLAUX_Status();
                    if (TimeoutCnt++ > 30) // about 3 seconds. 
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "TC Arm Timeout";
                        e.Result = BWRes;
                        return;
                    }
                } while (bTLAUX_ARM_IN == false);
            }
            else
            {
                BWRes.Comment = "TC Arm Out";
                if (bTLAUX_ARM_OUT)  // if the arm in then already done
                {
                    BWRes.Result = true;
                    e.Result = BWRes;
                    return;
                }

                // send the command to move the arm in
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_ARM_OUT);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(100);
                    getTLAUX_Status();
                    if (TimeoutCnt++ > 30) // about 3 seconds. 
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "TC Arm Timeout";
                        e.Result = BWRes;
                        return;
                    }
                } while (bTLAUX_ARM_OUT == false);
            }
            BWRes.Result = true;
            e.Result = BWRes;
        }

        private void TLAUX_ARM_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void TLAUX_ARM_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            //  MessageBox.Show(e.Result.ToString());
            _bw.DoWork -= TLAUX_ARM_Worker;
            _bw.ProgressChanged -= TLAUX_ARM_ProgressChanged;
            _bw.RunWorkerCompleted -= TLAUX_ARM_Completed;
            CompleteStatus((BWResults)e.Result);
        }
#endregion

#region Tool Clamp Process
        // Tool Clamp background process
        public void Start_TClamp_Process(SingleAxis SA)
        {
            // start a new background worker with move to H1
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;

            _bw.DoWork += TClamp_Worker;
            _bw.ProgressChanged += TClamp_ProgressChanged;
            _bw.RunWorkerCompleted += TClamp_Completed;
            _bw.RunWorkerAsync(SA.Move);
        }

        private void TClamp_Worker(object sender, DoWorkEventArgs e)
        {
            int timeoutCnt = 0;

            getTLAUX_Status();  // start by getting the latest TLAUX Status
            if ((bool)e.Argument) // true = engage clamp, false = release clamp
            {
                BWRes.Comment = "Tool Clamp Done";
                if (bTC_Clamped)
                {
                    BWRes.Result = true;
                    e.Result = BWRes;
                    return;
                }
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_GRAB);
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(100);
                    getTLAUX_Status();
                    if (timeoutCnt++ > 30)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Tool Clamp Timeout";
                        e.Result = BWRes;
                        return;
                    }
                } while (bTC_Clamped == false);
            }
            else
            {
                BWRes.Comment = "Tool Un-Clamp done";
                if (bTC_UnClamped)
                {
                    BWRes.Result = true;
                    e.Result = BWRes;
                    return;
                }
                KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_TOOL_RELA);    // release with a burst of air
                KMx.ExecuteProgram(2);
                do
                {
                    Thread.Sleep(100);
                    getTLAUX_Status();
                    if (timeoutCnt++ > 30)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Tool Clamp Timeout";
                        e.Result = BWRes;
                        return;
                    }
                } while (bTC_UnClamped == false);
            }
            BWRes.Result = true;
            e.Result = BWRes;
            return;
        }

        private void TClamp_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void TClamp_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            _bw.DoWork -= TClamp_Worker;
            _bw.ProgressChanged -= TClamp_ProgressChanged;
            _bw.RunWorkerCompleted -= TClamp_Completed;
            CompleteStatus((BWResults)e.Result);
        }
#endregion

        public void CompleteStatus(BWResults res)
        {
            if (res.Result == true)  // process ended successfully - 
            {
                OnStepUpdate(BWRes.Comment);
                ToolChangeStatus = true; // everything is OK
            }
            else
            {
                OnStepError(BWRes.Comment);
                ToolChangeStatus = false; // there was some kind of fault or error.
            }
            TCProgress = false; // the phase is done
        }
#endregion


#region Abort button
        // the abort button.
        private void Abort()
        {
            // if the background worker is running then abort movement and stop it
            if (_bw.IsBusy)
            {
                // KMx.CoordMotion.Abort(); // stop the motion! - maybe try a halt here instead?
                KMx.CoordMotion.Halt(); //  Halt seems to stop quicker than Abort does.
                //  _bw.CancelAsync();  // stop the background worker.
                // note here on canceling. This doesn't do anything because the async worker is not checking for the 
                // .CancellationPending flag. - 
                // The reason the Abort button works is because of the WaitForSegmentsFinished call ends the thread
                // if there was a way to timeout, I wouldn't need the Abort button...
                MessageBox.Show("Halted");
                KMx.CoordMotion.ClearHalt();
                KMx.CoordMotion.ClearAbort(); // but Halt requies ClearAbort inorder to resume.
                                              // MessageBox.Show("Abort Pressed");
                                              //                lblTCP2.Content = "Z Motion Aborted";

                // create an event to report the abort back to the main UI.
            }
        }

#endregion

#region Status methods 
        // get the status Tool Carousel and of the Spindle position
        private void getTLAUX_Status()
        {
            lock (_Tlocker)  // lock this so only one thread can access it at a time. - Tool locker
            {
                // get the TLAUX status and set the  apropriate flags
                // this is called every 100ms by the UI update timer
                int TStatus = KMx.GetUserData(PVConst.P_TLAUX_STATUS);
                iTLAUX_STATUS = TStatus;
                bTLAUX_ARM_IN = BitOps.BitIsSet(TStatus, PVConst.TLAUX_ARM_IN);
                bTLAUX_ARM_OUT = BitOps.BitIsSet(TStatus, PVConst.TLAUX_ARM_OUT);
                bTC_Clamped = BitOps.BitIsSet(TStatus, PVConst.TLAUX_CLAMP);
                bTC_UnClamped = BitOps.BitIsSet(TStatus, PVConst.TLAUX_UNCLAMP);
                Carousel_Position = TStatus & PVConst.TLAUX_TOOL_MASK;
                bTLAUX_FAULT = BitOps.AnyInMask(TStatus, PVConst.TLAUX_ERROR_MASK);
            }
        }

        public void getSpindle_Status()
        {
            lock (_Slocker) // lock so only one thread can access at a time - Spindle position lock
            {
                Spindle_Position = SPx.GetActualPositionCounts();
                                iSpindle_Status = KMx.GetUserData(PVConst.P_STATUS_REPORT);
                                SpindleHomed = !BitOps.BitIsSet(iSpindle_Status, PVConst.SB_SPIN_HOME);  // this inverts the logic of the bit in the status word. so true = homed
                                SpindleEnabled = BitOps.BitIsSet(iSpindle_Status, PVConst.SB_SPINDLE_ON);
                                SpindlePID = BitOps.BitIsSet(iSpindle_Status, PVConst.SB_SPINDLE_PID);
                                SpindleRPM = BitOps.BitIsSet(iSpindle_Status, PVConst.SB_SPINDLE_RPM);
            }
        }

#endregion

        // delegates - 
        public delegate void dStatusMsg(string s);
        public delegate void dStageDone();
        public delegate void dUpdateToolInfo(int pocket, int state);


        public event dStatusMsg StepUpdate;
        public event dStatusMsg StepError;
        public event dStageDone StepDone;

        protected virtual void OnStepUpdate(string x)
        {
            StepUpdate?.Invoke(x);   // if the ProcessUpdate delegate has been defined then call the delegate.
        }

        protected virtual void OnStepError(string x)
        {
            StepError?.Invoke(x); // if the ProcessError delegate has been defined then call that delegate
        }

#region Machine Coordinates
        public void GetCoordinates(ref MachineCoordinates MCx)
        {
            // MachineCoordinates MC = new MachineCoordinates();
            double x, y, z, a, b, c;
            x = y = z = a = b = c = 0.0;
            KMx.CoordMotion.UpdateCurrentPositionsABS(ref x, ref y, ref z, ref a, ref b, ref c, true);

            MCx.X = x;
            MCx.Y = y;
            MCx.Z = z;
            MCx.A = a;
            MCx.B = b;
            MCx.C = c;

            //return MC;
        }

#endregion

    }

    public struct MachineCoordinates
    {

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
    }

    public class SingleAxis
    {
        public double Pos { get; set; }
        public double Rate { get; set; }
        public bool Move { get; set; }  // in / out or clamp / release 
        public int ToolNumber { get; set; }
        public int ToolPocket { get; set; }
    }

    public class PlaneAxis
    {
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double Rate { get; set; }
        public bool Move { get; set; }
        public int ToolNumber { get; set; }
    }

    // Background Worker results 
    public class BWResults
    {
        public bool Result { get; set; }
        public string Comment { get; set; }
    }

    public class TExchangePosition
    {
        public int PutTool { get; set; }
        public int GetTool { get; set; }
    }

    public class ToolSetterArguments
    {
        public double X_Offset { get; set; }
        public double Y_Offset { get; set; }
        public double Z_Offset { get; set; }
        public bool UseExpectedZ { get; set; }
        public int AverageCount { get; set; }
    }
}
