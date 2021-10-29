﻿// Must put this in every file that will look for it!
#define TESTBENCH  // defining this will allow operation on the testbench

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

namespace KFLOP_Test3
{
    // Trying to orgainze this code a little better.
    // Thinking that the actual methods that run the tool changer would be better contained in its own class
    // and then it could be used from where ever, and not just from the toolCangerPanel

    // trying to keep the scope of this to only the basic machine level control

    public class ToolChanger : MachineMotion
    {
        // Tool Carousel configuration
        static ToolInfo xToolInfo;
        static ToolCarousel CarouselList1;
        // tool table 
        static ToolTable TTable;


        // The back ground workers 
        static BackgroundWorker _bw2;   // process background worker.
        static BWResults BW2Res;

        static TExchangePosition TExPos;

        // these static variable flags indicate tool change progress
        static bool TCActionProgress; // true indicates a process action (group of steps) is in progress, false indicates the action has finished 

        public static bool ToolChangerComplete; // true indicates that a series of steps is complete - like get a tool... false means it is still running

        const bool bARM_IN = true;
        const bool bARM_OUT = false;
        const bool bCLAMP = true;
        const bool bRELEASE = false;

        static int ToolInSpindle { get; set; }   // the number of the tool in the spindle, 0 = spindle empty, 1 - 8, Tool 



        public ToolChanger(ref ToolInfo toolInfo)   // class construtor
        {
            xToolInfo = toolInfo;
            CarouselList1 = toolInfo.toolCarousel;
            TTable = toolInfo.toolTable;
            TExPos = new TExchangePosition();

            _bw2 = new BackgroundWorker();
            BW2Res = new BWResults();



        }

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

        public ToolTable GetToolTable()
        {
            return (TTable);
        }

        public void TCMessage(string str)
        {
            MessageBox.Show(str);
        }

        public void SetCurrentTool(int ToolSlot)
        {
            KMx.CoordMotion.Interpreter.SetupParams.CurrentToolSlot = ToolSlot; // update the interpreter slot position
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

        // In all of this who or what is managing what is currently in the carousel!!! have to figure this out soon!
        public void ToolChangeM6()
        {
            ToolChangerComplete = false;
            int current_tool = KMx.CoordMotion.Interpreter.SetupParams.CurrentToolSlot;
            int selected_tool = KMx.CoordMotion.Interpreter.SetupParams.SelectedToolSlot;
            //      MessageBox.Show($"Current Tool: {current_tool} Selected tool {selected_tool}");



//            Dispatcher.Invoke(() =>    // Dispatcher to the rescue!
//                btnAbort.IsEnabled = false
//            );
            // need to decide which tool action to take - see comments on tool change actions
            // 
            // see the flow chart for details! 

            ToolChangerDeluxe(current_tool, selected_tool);
            do { Thread.Sleep(100); } while (TCActionProgress);
            //            MessageBox.Show($"Tool {current_tool} changed to {selected_tool}");
            ToolChangerComplete = true;

            //Start_PutTool(current_tool);
            //do { Thread.Sleep(100); } while (TCActionProgress); // tool change completed ? TCAction progress = false...
            //                                                    // how to wait for the action to complete?

            //// check the results of the action.
            //s = string.Format("Tool {0} put away", current_tool);
            //MessageBox.Show(s);
            //Start_GetTool(selected_tool);
            //do { Thread.Sleep(100); } while (TCActionProgress); // wait for change complete. ? timeout needed?
            //s = string.Format("Tool {0} aquired", selected_tool);
            //MessageBox.Show(s);
            //ToolChangerComplete = true;
            return;
        }


        public void ToolChangerDeluxe(int CurrentTool, int SelectedTool)
        {

            // changing to reflect the Tool Carousel Management
            // Note: Tool Slot 0 always means empty spindle - this is always the first row in the tool table and has zero length

            // should only have to do this once...
            int sPocket = getPocket(SelectedTool);  // selected pocket
            int cPocket = getPocket(CurrentTool);   // current pocket

            if (CurrentTool == 0) // there is no tool in the spindle - just get the selected tool
            {
                if (SelectedTool == 0)
                {
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
                    // if there was no error then update the current tool
                    SetToolInUse(sPocket);
                }
            }
            else if (cPocket == -1) // current tool requires a manual removal
            {
                Manual_PutTool(CurrentTool);   // removes the current tool from the spindle
                if (SelectedTool == 0)
                {
                    ToolInSpindle = 0; // don't need to do anything here - spindle was just unloaded manually
                    return;
                }
                if (sPocket == -1)
                {
                    Manual_GetTool(SelectedTool);   // selected tool is not in the carousel - do a manual get tool
                }
                else
                {
                    Start_GetTool(sPocket);    // automatically get a tool from the carousel
                    SetToolInUse(sPocket);
                }
            }
            else
            {
                // current tool can be put into the carousel
                if (SelectedTool == 0)
                {
                    Start_PutTool(cPocket); // just put away the current tool
                    ToolInSpindle = 0;
                    ClearToolInUse(cPocket);
                    return;
                }
                if (sPocket == -1)
                {
                    Start_PutTool(cPocket); // put away the current tool
                    ToolInSpindle = 0;
                    ClearToolInUse(cPocket);
                    Manual_GetTool(SelectedTool); // get the selected tool manually - not in the carousel

                }
                else
                {
                    Start_ExchangeTool(CurrentTool, SelectedTool);  // if none of the other conditions apply then do a tool exchange!
                }
            }
        }

        public void ToolChangerSimple(int CurrentSlot, int SelectedSlot)
        {
            if((CurrentSlot < 1) || (CurrentSlot > xTCP.CarouselSize))
            {
           
                // get Selected Slot
                if((SelectedSlot < 1) || (SelectedSlot > xTCP.CarouselSize))
                {
                    // not a valid slot!
                    return;
                }
                else
                {
                    Start_GetTool(SelectedSlot);
                }
            }
            else
            {
                // put current spindle into carousel slot
                Start_PutTool(CurrentSlot);
                // get Selected Slot
                if ((SelectedSlot < 1) || (SelectedSlot > xTCP.CarouselSize))
                {
                    // not a valid slot!
                    return;
                }
                else
                {
                    Start_GetTool(SelectedSlot);
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
            // tool number to get is passed in the argument

//            Dispatcher.Invoke(() =>
//            {
//                lblProg2.Content = "In Get Tool";
//            });

            TCProgress = true;
            ToolChangeStatus = false;
            int progress_cnt = 0;
            BW2Res.Result = true; // start out at true;
            BW2Res.Comment = "In Get Tool";
            _bw2.ReportProgress(progress_cnt++);

            SingleAxis tSAx = new SingleAxis();
            // start a new background worker
            if (MotionBusy()) // if the BW worker is busy
            {
                BW2Res.Result = false;
                BW2Res.Comment = "BW is busy";
                e.Result = BW2Res;
                return;
            }

            BW2Res.Comment = "Move to Z1";
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

            BW2Res.Comment = "Carousel Indexing";
            _bw2.ReportProgress(progress_cnt++);
            // rotate carousel to tool number
            //tSAx.ToolNumber = (int)e.Argument;  
            // int PocketNumber = getPocket((int)e.Argument);
            // tSAx.ToolNumber = PocketNumber;   // carousel position number
            tSAx.ToolNumber = (int)e.Argument;  // this contains the Carousel Pocket Numbr - ie. carousel position
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
            //      ToolInSpindle = tSAx.ToolNumber;
            // update the Carousel Empty slot 
            //      SetToolInUse(PocketNumber);

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

            BW2Res.Comment = "Spindle RPM Mode";
            _bw2.ReportProgress(progress_cnt++);
            // Spindle back to RPM Mode
            Start_SpindleRPM_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Spindle Mode Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

//            _bw2.ReportProgress(progress_cnt++);
            BW2Res.Comment = "Get Tool Success";
            BW2Res.Result = true;
            e.Result = BW2Res;
            // done!
        }

        private void GetToolProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // update the progress label
            //            Dispatcher.Invoke(() =>    // Dispatcher to the rescue!
            //                lblTCProgress.Content = "Progress :" + e.ProgressPercentage.ToString()
            //            );
            string ps = BW2Res.Comment + " " + e.ProgressPercentage.ToString();
            if (BW2Res.Result == false)
            {
                
                OnProcessError(ps); // send the error message
            }
            else
            {
                OnProcessUpdate(ps); // send the status message
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
            int ToolSlot;
            ToolSlot = getSlot(ToolNumber);
            string toolmsg = string.Format("Insert Tool Number {0} into the spindle", ToolSlot);
            MessageBox.Show(toolmsg, "WARNING - DO NOT IGNORE!");
            // can I somehow make a red blinking warning on this? maybe a custom message box?
            ToolInSpindle = ToolSlot;
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
            // tool number to get is passed in the argument

//            Dispatcher.Invoke(() =>
//            {
//                lblProg2.Content = "In Put Tool";
//            });

            SingleAxis tSAx = new SingleAxis();

            TCProgress = true;
            ToolChangeStatus = false;
            int progress_cnt = 0;


            if (MotionBusy()) // if the BW worker is busy then don't do this. - should probably set some kind of flag here...
            {
                // There was an error of some kind!
                BW2Res.Comment = "BW is Busy Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }

            // start a new background worker
            _bw2.ReportProgress(progress_cnt++);
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
            _bw2.ReportProgress(progress_cnt++);

            // Index the spindle
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
            _bw2.ReportProgress(progress_cnt++);

            // rotate carousel to tool number
            // 
            // tSAx.ToolNumber = (int)e.Argument;  // carousel position number
            int PocketNumber = (int)e.Argument;
            if (GetToolInUse(PocketNumber) == false)
            {
                // this check to see if the pocket is assigned and the tool is in use ie the pocket is empty 
                // waiting for the tool to be returned
                BW2Res.Comment = "Carousel Not Empty Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }
            tSAx.ToolNumber = PocketNumber;   // carousel position number
            Start_Carousel_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Carousel Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }
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
            _bw2.ReportProgress(progress_cnt++);

            // Clamp Release
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
            _bw2.ReportProgress(progress_cnt++);

            // Spindle back to RPM Mode
            Start_SpindleRPM_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Spindle Mode Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }
            _bw2.ReportProgress(progress_cnt++);

            BW2Res.Comment = "Put Tool Success";
            BW2Res.Result = true;
            e.Result = BW2Res;
        }

        private void PutToolProgressedChanged(object sender, ProgressChangedEventArgs e)
        {
//            Dispatcher.Invoke(() =>    // Dispatcher to the rescue!
//                lblTCProgress.Content = "Progress :" + e.ProgressPercentage.ToString()
//            );
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
            int ToolSlot;
            ToolSlot = getSlot(ToolNumber);
            string toolmsg = string.Format("Remove Tool number {0} from the spindle", ToolSlot);
            MessageBox.Show(toolmsg, "WARNING - DO NOT IGNORE!");
            ToolInSpindle = 0;
        }
        #endregion

        #region Tool Exchange - Put A Get B without retract

        public void Start_ExchangeTool(int PutTool, int GetTool)
        {
            TCActionProgress = true; // process action is happening...
            TExPos.PutTool = PutTool;
            TExPos.GetTool = GetTool;
            _bw2.WorkerReportsProgress = true;
            _bw2.DoWork += ExchangeToolWorker;
            _bw2.ProgressChanged += ExchangeToolProgressChanged;
            _bw2.RunWorkerCompleted += ExchangeToolCompleted;
            _bw2.RunWorkerAsync(TExPos);
        }

        private void ExchangeToolWorker(object sender, DoWorkEventArgs e)
        {
            // a combination of both put and get tools

            // Assume the following state:
            // 1. Spindle contains the "Put Tool"
            // 2. The tool arm is retracted.
            // 3. The Carousel space for the Put Tool is empty
            // tool numbers to exchange are passed in the argument

//            Dispatcher.Invoke(() =>
//            {
//                lblProg2.Content = "In Exchange Tool";
//            });

            TCProgress = true;
            ToolChangeStatus = false;
            int progress_cnt = 0;

            TExchangePosition tTexch;
            tTexch = (TExchangePosition)e.Argument;

            SingleAxis tSAx = new SingleAxis();
            // start a new background worker
            if (MotionBusy()) // if the BW worker is busy
            {
                BW2Res.Result = false;
                BW2Res.Comment = "BW is busy";
                e.Result = BW2Res;
                return;
            }

            // start a new background worker
            _bw2.ReportProgress(progress_cnt++);
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
            _bw2.ReportProgress(progress_cnt++);

            // Index the spindle
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
            _bw2.ReportProgress(progress_cnt++);

            int PocketNumber = getPocket(tTexch.PutTool);
            if (GetToolInUse(PocketNumber) == false)
            {
                // this check to see if the pocket is assigned and the tool is in use ie the pocket is empty 
                // waiting for the tool to be returned
                BW2Res.Comment = "Carousel Not Empty Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }
            // rotate carousel to put tool number
            tSAx.ToolNumber = PocketNumber;   // carousel position number
            Start_Carousel_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Carousel Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }
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
            _bw2.ReportProgress(progress_cnt++);

            // Clamp Release
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
            ClearToolInUse(PocketNumber); // clear the tool in use flag for that carousel pocket
            ToolInSpindle = 0;
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

            // rotate carousel to the get tool number
            PocketNumber = getPocket(tTexch.GetTool);
            // tSAx.ToolNumber = tTexch.GetTool;  // carousel position number
            tSAx.ToolNumber = PocketNumber;
            Start_Carousel_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Carousel Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }
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
            // at this point the spindle contains the new tool
            SetToolInUse(PocketNumber);
            ToolInSpindle = getSlot(tTexch.GetTool);
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
            _bw2.ReportProgress(progress_cnt++);

            // Spindle back to RPM Mode
            Start_SpindleRPM_Process(tSAx);
            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW2Res.Comment = "Spindle Mode Error";
                BW2Res.Result = false;
                e.Result = BW2Res;
                return;
            }
            _bw2.ReportProgress(progress_cnt++);

            BW2Res.Comment = "Get Tool Success";
            BW2Res.Result = true;
            e.Result = BW2Res;
            // done!

        }

        private void ExchangeToolProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // update the progress label
//            Dispatcher.Invoke(() =>    // Dispatcher to the rescue!
//                lblTCProgress.Content = "Progress :" + e.ProgressPercentage.ToString()
//            );
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
                if(stuckCount++ > 200)  // 200 seconds
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
            }
            TCActionProgress = false; // the action is done
        }

        #endregion

        // get the carousel pocket number given the tool index (SLOT in the tool table)
        // can also be used to determine if the tool is in the carousel. a -1 means that the tool is not in the carousel
        // a non negative number is the carousel pocket where the tool is.
        private int getPocket(int Index)
        {
            int slot = getSlot(Index);
            foreach (CarouselItem CI in CarouselList1.Items)
            {
                if (CI.ToolIndex == slot)
                { return CI.Pocket; }
            }
            return -1;
        }

        private int getSlot(int index)
        {
            int s, i;
            s = i = 0;
            double l, d, x, y;
            l = d = x = y = 0.00;
            KMx.CoordMotion.Interpreter.SetupParams.GetTool(index, ref s, ref i, ref l, ref d, ref x, ref y);
            return s;
        }

        // set get and clear the tool in use flag
        private void SetToolInUse(int Pocket)
        {
            foreach (CarouselItem CI in CarouselList1.Items)
            {
                if (CI.Pocket == Pocket)
                {
                    CI.ToolInUse = true;
                    break;
                }
            }
        }

        private void ClearToolInUse(int Pocket)
        {
            foreach (CarouselItem CI in CarouselList1.Items)
            {
                if (CI.Pocket == Pocket)
                {
                    CI.ToolInUse = true;
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

        // delegates - 
        public delegate void dStatusMsg(string s);
        public delegate void dTCDone();


        public event dStatusMsg ProcessUpdate;
        public event dStatusMsg ProcessError;

        protected virtual void OnProcessUpdate(string x)
        {
            ProcessUpdate?.Invoke(x);   // if the ProcessUpdate delegate has been defined then call the delegate.
        }

        protected virtual void OnProcessError(string x)
        {
            ProcessError?.Invoke(x); // if the ProcessError delegate has been defined then call that delegate
        }

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
        static bool TSActionProgress;

        static ProbeResult TSProbeState;

        static private ToolChangeParams TSP;

        ToolSetter(ref ToolInfo toolInfo)
        {
            TSProbeState = new ProbeResult();

            _bw3 = new BackgroundWorker();
            _bwts = new BackgroundWorker();
            BW3Res = new BWResults();
            BWtsRes = new BWResults();
            TSP = new ToolChangeParams();

        }

        private void Start_ToolSetter(ToolSetterArguments TSArgs)
        {
            // check that the background process isn't already running.
            if (_bw3.IsBusy)
            { return; } // don't run if the background worker is busy

            TSActionProgress = true; // process action is happening...
            // need a class to hold the tool setter arguments?

            _bw3.DoWork += ToolSetter_Worker;
            _bw3.ProgressChanged += ToolSetterProgressChanged;
            _bw3.RunWorkerCompleted += ToolSetterCompleted;
            _bw3.RunWorkerAsync(TSArgs);

        }

        private void ToolSetter_Worker(object sender, DoWorkEventArgs e)
        {
            ToolSetterArguments tTSArg;
            tTSArg = (ToolSetterArguments)e.Argument;    // get the arguments
            bool ProcessError = false;

            MessageBox.Show("Move to Safe Z");

            TSProgress = true;

            BW3Res.Result = true; // start out with this set.

            // Move to safe Z
            SingleAxis tSAx = new SingleAxis();
            tSAx.Pos = tTSArg.SafeZ;
            tSAx.Rate = 0;  // move at Traverse rate
            Start_MoveZ_Process(tSAx);

            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW3Res.Comment = "Safe Z Move Error";
                BW3Res.Result = false;
                e.Result = BW3Res;
                ProcessError = true;
            }

            if (ProcessError == false)
            {
                MessageBox.Show("Move to X Y");

                // move to Tool Setter X, Y
                PlaneAxis tPAx = new PlaneAxis();
                tPAx.PosX = tTSArg.X;
                tPAx.PosY = tTSArg.Y;
                tPAx.Rate = 0;  // move at traverse rate
                Start_MoveXY_Process(tPAx);
                if (WaitForProgress() == false)
                {
                    // There was an error of some kind!
                    BW3Res.Comment = "X Y Move Error";
                    BW3Res.Result = false;
                    e.Result = BW3Res;
                    ProcessError = true;
                }
            }

            if (ProcessError == false)
            {
                MessageBox.Show("Move to Tool Z");

                // move to Tool Setter Z (somewhere above the tool setter TBD inches)
                tSAx.Pos = tTSArg.ToolZ;
                tSAx.Rate = 0;
                Start_MoveZ_Process(tSAx);
                if (WaitForProgress() == false)
                {
                    // There was an error of some kind!
                    BW3Res.Comment = "Tool Z Move Error";
                    BW3Res.Result = false;
                    e.Result = BW3Res;
                    ProcessError = true;
                }
            }

            // - estimated tool or default tool length

            if (ProcessError == false)
            {
                MessageBox.Show("Probing");
                // move while waiting for the tool setter to detect
                // the probing command
                Start_ToolSetterZProbe(2.0);
                if (WaitForProgress() == false)
                {
                    // There was an error
                    ProcessError = true;
                }
            }
            // save the length / offset

            // move back to Safe Z
            MessageBox.Show("Back to Safe Z");

            TSProgress = true;

            // !!!!
            tSAx.Pos = xTCP.TS_SAFE_Z;
            tSAx.Rate = 0;  // move at Traverse rate
            Start_MoveZ_Process(tSAx);

            if (WaitForProgress() == false)
            {
                // There was an error of some kind!
                BW3Res.Comment = "Safe Z Move Error";
                BW3Res.Result = false;
                ProcessError = true;
            }

            if (ProcessError == false)
            {

                BW3Res.Comment = "Tool Setter Success";
                BW3Res.Result = true;

            }

            e.Result = BW3Res;

        }

        private void ToolSetterProgressChanged(object sender, ProgressChangedEventArgs e)
        { }

        private void ToolSetterCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // remove the process from background worker 2
            _bw3.DoWork -= ToolSetter_Worker;
            _bw3.ProgressChanged -= ToolSetterProgressChanged;
            _bw3.RunWorkerCompleted -= ToolSetterCompleted;
            CompleteActionStatus((BWResults)e.Result);
        }

        private bool WaitForProgress()
        {
            // sleep while waiting for the global variable TCProgress to be set true by the 
            // background worker thread.
            // should there be a count here so it doesn't get stuck forever?
            do
            {
                Thread.Sleep(50);
            } while (TSProgress);
            TSProgress = true;
            // check the results for faults and errors
            return TSChangeStatus;
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
            TSActionProgress = false; // the action is done
        }



        #region Tool Setter Probing

        private void Start_ToolSetterZProbe(double dist)
        {
            double MotionRate = -15.0;    // inch per min
            double mrZ;
            double ProbeDistance = dist; // probe distance  in inches - this should take about 
            mrZ = (MotionRate * KMx.CoordMotion.MotionParams.CountsPerInchZ) / 60.0; // motion rate(in/min) * (counts/inch) / (60sec/ min)

            double PTimeout;
            // timeout is (distance/rate) * (ms/min)
            PTimeout = (ProbeDistance / Math.Abs(MotionRate)) * 60.0;    // convert to seconds for timeout  -
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
            int Tdone = (int)(TimeOut * 10.0 * 1.1);  // convert the time to sleep counts with 10% extra time over machine delay
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
                    return;
                }
            } while (CheckForTSProbeComplete() != true);
            // look at the probe results to determine what to do next
            string Pmsg = "Done";
            MachineCoordinates MCx = GetCoordinates();
            switch (TSProbeState)
            {
                case ProbeResult.Detected:
                    {
                        // get the machine coordinates.
                        Pmsg = String.Format("Probe Detect at\n X:{0}\nY:{1}\nZ{2}", MCx.X, MCx.Y, MCx.Z);
                        BWtsRes.Result = true;
                        break;
                    }
                case ProbeResult.MachineTimeOut:
                    {
                        Pmsg = String.Format("Machine timeout. Currently at\n X:{0}\nY:{1}\nZ{2}", MCx.X, MCx.Y, MCx.Z);
                        BWtsRes.Result = true;
                        break;
                    }
                case ProbeResult.T2_ProbeError:
                    {
                        Pmsg = String.Format("Probe Error\nMachine at\n X:{0}\nY:{1}\nZ{2}", MCx.X, MCx.Y, MCx.Z);
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
            MessageBox.Show(Pmsg);
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


        static public ToolChangeParams xTCP;

        // global variables for the user control
        // since there is only one Machine and tool changer these are all static.
        static bool SpindleEnabled;
        static bool SpindlePID;
        static bool SpindleRPM;
        static bool SpindleHomed;
        static double Spindle_Position;
        static int iSpindle_Status;
        static int iPVStatus;
        static bool bTC_Clamped;
        static bool bTC_UnClamped;
        static bool bTLAUX_ARM_IN;
        static bool bTLAUX_ARM_OUT;
        static int Carousel_Position;
        static bool bTLAUX_FAULT;
        static int iTLAUX_STATUS;
        #endregion

        public MachineMotion()
        {
            if (KMx == null)
            {
                MessageBox.Show("MachineMotion Must be instaniated first!");
            }
        }

        public MachineMotion(ref KM_Controller X, ref KM_Axis SP)
        {
            KMx = X;
            SPx = SP;

            _bw = new BackgroundWorker();
            BWRes = new BWResults();

        }

        public void SetParams(ref ToolChangeParams tcp)
        {
            xTCP = tcp; // this sets the static item in the base class!
        }
        public bool MotionBusy()
        {
            if (_bw.IsBusy)
                return true;
            return false;
        }

        #region Individual tool changer motions

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

        public void SpindleDisable()
        {
            KMx.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_DIS);
            KMx.ExecuteProgram(2);
            if (KMx.WaitForThreadComplete(2, 2000) == false)
            {
                MessageBox.Show("Thread 2 stuck!");
            }
        }

// this is the part that really needs to be rewritten! 
// !!!!!
        public void CompleteStatus(BWResults res)
        {
            if (res.Result == true)  // process ended successfully - 
            {
                // maybe put something on a label - like the 
                //                Dispatcher.Invoke(() =>
                //                {
                //                    lblTCP2.Content = res.Comment;
                OnStepUpdate(BWRes.Comment);
                ToolChangeStatus = true; // everything is OK
                                         //                });
            }
            else
            {
                // process ended with a fault condition - set a flag???
                // update the UI label
                //                Dispatcher.Invoke(() =>
                //                {
                //                    lblTCP2.Content = res.Comment;
                OnStepError(BWRes.Comment);
                ToolChangeStatus = false; // there was some kind of fault or error.
                                          //                });
            }
            TCProgress = false; // the phase is done
        }


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

            _bw.RunWorkerAsync(SA.ToolNumber);
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
                || (Spindle_Position > 10000)                       // or position is far high
                || (Spindle_Position < -10000))                     // or position is far low
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
                    Thread.Sleep(50);
                    if (timeoutCnt++ > 20)
                    {
                        BWRes.Result = false;
                        BWRes.Comment = "Spindle PID Timeout";
                        e.Result = BWRes;
                        return;
                    }
                    // getSpindle_Status();
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
                } while (SpindleEnabled == false);
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

        private void getSpindle_Status()
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


#endregion

        // delegates - 
        public delegate void dStatusMsg(string s);
        public delegate void dTCDone();


        public event dStatusMsg StepUpdate;
        public event dStatusMsg StepError;

        protected virtual void OnStepUpdate(string x)
        {
            StepUpdate?.Invoke(x);   // if the ProcessUpdate delegate has been defined then call the delegate.
        }

        protected virtual void OnStepError(string x)
        {
            StepError?.Invoke(x); // if the ProcessError delegate has been defined then call that delegate
        }

#region Machine Coordinates
        public MachineCoordinates GetCoordinates()
        {
            MachineCoordinates MC = new MachineCoordinates();
            double x, y, z, a, b, c;
            x = y = z = a = b = c = 0.0;
            KMx.CoordMotion.UpdateCurrentPositionsABS(ref x, ref y, ref z, ref a, ref b, ref c, true);

            MC.X = x;
            MC.Y = y;
            MC.Z = z;
            MC.A = a;
            MC.B = b;
            MC.C = c;

            return MC;
        }
#endregion

    }




}