﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
// for file dialog libraries
using Microsoft.Win32;
using System.IO;
// for the Dispatch Timer
using System.Windows.Threading;

// for KMotion libraries
using KMotion_dotNet;

// for XML save setup etc
using System.Xml;
using System.Xml.Serialization;
// for the JSON stuff
using Newtonsoft.Json;
// for the AvalonEdit component
using ICSharpCode.AvalonEdit.Rendering;
// for the Mutex
using System.Threading;



namespace KFLOP_Test3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // global variables in the MainWindow class
        
        static bool Connected = false;  // used in the status time to indicate when the KFLOP is connected
        static int skip = 0;            // skip timer used to delay when KFLOP is not connected
        static bool ExecutionInProgress = false;    // true while interpreter is executing
        static bool InFeedHold = false;             // true when in feed hold
        static bool InMotion = false;               // true when jogging
        static int TimerEntry = 0;

        // these flags indicate the status of the machine - as opposed to status of the software.
        // they are updated by reading the P_STATUS persist variable from the KFLOP board controlling the machine
        // the current persist variable for this is #104 - so it is always read in the MainStatus as the PC_comm[4] variable
        static bool T1Active = false;
        static bool ESTOP_FLAG = true;
        static bool MachineIsHomed = false;
        static bool MachineWarning = false;
        static bool MachineError = false;
        static bool RestoreStoppedState = false;
        static bool InterpreterInitialized = false;


        static KMotion_dotNet.KM_Controller KM; // this is the controller instance!
        static MotionParams_Copy Xparam;
        // static ConfigFiles CFiles;
        ConfigFiles CFiles;

        static Machine BP308;

        // not sure if I need the Mutex if I'm using the Dispatcher but we will see...
        static Mutex ConsoleMutex = new Mutex();


        static int DisCount = 0;
        string GCodeFileName;
        int CurrentLineNo;

        // console window
        static ConsolWindow ConWin;

        int[] PVars;

        // this is a timing variable for debugging purposes 
        // can be used to time processes 
        System.Diagnostics.Stopwatch tickTimer;

        // tab items 
        JogPanel JogPanel1;
        StatusPanel StatusPanel1;




        public MainWindow()
        {
            InitializeComponent();

            // create an instance of the KM controller - same instance is used in the entire app
            // ******** VERY IMPORTANT *********
            // inorder to make this work properly 
            // the following files needed to be copied to the Debug1/Release1 directories
            // -- and the build output directories need to be changed to Debug1/Release1 - or at least something besides Debug/Release
            // KMotionDLL.dll
            // KMotion_dotNet.dll
            // KMotion_dotNet_Interop.dll
            // GCodeInterpreter.dll
            // KMotionServer.exe
            // TCC67.exe(This is the compiler for their DSP C code)
            // emc.var
            // \DSP_KFLOP Sub directory.
            //
            // also copy the DSP_KFLOP folder into Debug1/Release1
            // reference this wiki page https://www.dynomotion.com/wiki/index.php?title=PC_Example_Applications
            #region Initialization 
            try
            {
                KM = new KMotion_dotNet.KM_Controller();
            }
            catch (Exception e)
            {
                MessageBox.Show("Unable to load KMotion_dotNet Libraries.  Check Windows PATH or .exe location " + e.Message);
                System.Windows.Application.Current.Shutdown();  // and shut down the application...
                return;
            }

            PVars = new int[14];

            // Machine instance for status and bit control
            // BP308 = new Machine(ref KM);
            // get the configuration file names
            CFiles = new ConfigFiles();
            OpenConfig(ref CFiles);
            // copy of the motion parameters that the JSON reader can use.
            Xparam = new MotionParams_Copy();
            OpenMotionParams(ref CFiles, ref Xparam);
            // console window
            ConWin = new ConsolWindow();
            // GCode Viewer
            GCodeView_Init();
            // add the callbacks
            AddHandlers();

            // start a timer for the status update
            var Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(100);    // timer tick every 100 ms (1/10 sec)
            Timer.Tick += dispatchTimer_Tick;
            Timer.Start();

            // debug timer
            tickTimer = new System.Diagnostics.Stopwatch(); // for debuging the timing
            tickTimer.Reset();



            // the tab controls
            // Add the usere controls to the Tab control area
            // The Jog Panel
            JogPanel1 = new JogPanel(ref KM);
            var Tab1 = new TabItem();
            Tab1.Name = "tabItemContent";
            Tab1.Header = "Jog";
            Tab1.Content = JogPanel1;
            tcMainTab.Items.Add(Tab1);

            // The Status Panel
            StatusPanel1 = new StatusPanel(ref KM);
            var Tab2 = new TabItem();
            Tab2.Name = "tabItemContent1";
            Tab2.Header = "Status";
            Tab2.Content = StatusPanel1;
            tcMainTab.Items.Add(Tab2);

            // Probing tab panel
            // Tools tab panel etc.

            // hide the fwd and rev buttons
            HideFR();

            #endregion
        }

        #region Status Timer Tick
        // Update Status Timer
        // currently set for 100 ms 
        // event handler for the Dispatch timer
        private void dispatchTimer_Tick(object sender, EventArgs e)
        {
            int[] BoardList;
            int nBoards = 0;

            // if for some reason the processes in the dispatch timer
            // takes more than the period of the timer, this should catch that
            // and make multiple entries into the timer tick not hang the process.
            // remember to decrement TimerEntry at the end of the method!
            // Note: TimerEntry is a static global variable - C# does't allow static local variables...
            if (TimerEntry > 0) return;  
            TimerEntry++;

            // Timer Tick sections
            // If the board is connected then 
            // check some things once every second 
            // check certain things every cycle

            if (Connected)
            {
                #region Connected
                if (++skip == 10)    // These actions happen every second
                {
                    skip = 0;
                    // update the elapsed time 
                    tbExTime.Text = KM.CoordMotion.TimeExecuted.ToString();
                }

                // these actions happen every cycle

                tickTimer.Restart();
                // lock the KFLOP board until the MainStatus is read.
                if (KM.WaitToken(100) == KMOTION_TOKEN.KMOTION_LOCKED) // KMOTION_LOCKED means the board is available
                {
                    try
                    {
                        // note that KMotionCNC does a KM.ServiceConsole() here... 
                        KM_MainStatus MainStatus = KM.GetStatus(false); // passing false does not lock to board while generating status
                        KM.ReleaseToken();

                        // service all the timely functions
                        //
                        // Service KFlopStatus - 
                        ServiceKFlopStatus(ref MainStatus);
                        //
                        // ServiceKFlopCommands - PC_Comm commands from KFlop
                        ServiceKFlopCommands(ref MainStatus);
                        //
                        // Service the UI Controls - all the visible buttons on the screen
                        //   This should include managing the GCode viewer - KMotionCNC manages
                        //   the viewer separatly in a Mutex block 
                        UpdateUI(ref MainStatus);
                        //
                        // KMotionCNC also services a JoyStick 
                        //
                    }
                    catch (DMException)  // in case disconnect in the middle of reading status
                    {
                        KM.ReleaseToken();  // make sure the token is released
                    }
                }
                else    // only get here if the KFlop board can not get a token.
                {
                    // if the KFlop board can't get a token (WaitToken) then we must not be connected anymore.
                    Connected = false;
                }
                #endregion
            }

            else
            {
                #region Not Connected
                if (++skip == 10)    // These actions happen every second - when not connected
                {
                    skip = 0;
                    DisCount++;

                    // check how many boards are connected
                    BoardList = KM.GetBoards(out nBoards);
                    if (nBoards > 0)
                    {
                        Connected = true;
                        Title = String.Format("C# WPF App - Connected = USB location {0:X} count = {1}", BoardList[0], DisCount);
                    }
                    else
                    {
                        Connected = false;
                        Title = String.Format("C# WPF App - KFLOP Disconnected count = {0}", DisCount);
                    }
                }
                #endregion
            }
            TimerEntry--;
        }
        #endregion

        #region Callback Handlers

        // console update
        int Console_Msg_Update(string msg)
        {

            // Paragraph par = new Paragraph();
            //par.Inlines.Add(new Run(msg));
            // fdConsole.Blocks.Add(new Paragraph(new Run(msg + "blocks:" + fdConsole.Blocks.Count.ToString())));
            // fdConsole.Blocks.Add(new Paragraph(new Run(msg)));

            ////try using the mutex here!
            //ConsoleMutex.WaitOne();
            //ConWin.AddSomeText(msg);
            //ConsoleMutex.ReleaseMutex();
            //return 0;

            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => Console_Update2(msg)));
            return 0;
                
        }

        public void Console_Update2(string msg)
        {
            // fdConsole.Blocks.Add(new Paragraph(new Run(msg)));
            ConWin.AddSomeText(msg);
        }

        static void KM_ErrorUpdated(string msg)
        {
            MessageBox.Show(msg);
        }

        /// <summary>
        /// MCode8Callback - this is the method that is called when ever the MCode callback is specified
        /// // need to figure out what happens with each mcode - ie M3, M4, M5, M6 and S
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private int MCode8Callback(int code)
        {
            if (KM.ThreadExecuting(2))
            {
                MessageBox.Show("Thread2 already executing!");
            }
            else
            {
                switch (code)
                {
                    case 3: // M3 callback - Spindle CW
                        KM.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_CW);
                        KM.ExecuteProgram(2);
                        break;
                    case 4: // M4 callback - Spindle CCW
                        KM.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_CCW);
                        KM.ExecuteProgram(2);
                        break;
                    case 5: // M5 Callback - Spindle Stop
                        KM.SetUserData(PVConst.P_NOTIFY, T2Const.T2_SPINDLE_STOP);
                        KM.ExecuteProgram(2);
                        break;
                    case 6: // M6 Callback - Tool Change
                        // lots to do in this function.
                        break;
                    case 7: // M7 Callback - Mist Coolant On?
                    case 8: // M8 Callback - Coolant On
                    case 9: // M9 Callback -Coolant Off
                        break;
                    default: break;
                }
            }
            MessageBox.Show(String.Format("Code = {0}", code));
            return 0;
        }

        private void GCodeUserCallback(string msg)
        {
            MessageBox.Show(msg);
        }

        #region GCode Interrpreter callback handlers
        //static 
        public void Interpreter_InterpreterCompleted(int status, int lineno, int sequence_number, string err)
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => InterpCompleted2(status, lineno, sequence_number, err)));

            //ConsoleMutex.WaitOne();
            //tbStatus.Text = status.ToString();
            //tbLineNo.Text = lineno.ToString();
            //GCodeView_GotoLine(lineno);
            //tbSeq.Text = sequence_number.ToString();
            //tbErr.Text = err;

            //if ((status != 0) && status != 1005)
            //{
            //    MessageBox.Show(err); // status 1005 = successful halt
            //}
            //ExecutionInProgress = false; // not running anymore.
            //ConsoleMutex.ReleaseMutex();
        }

        public void InterpCompleted2(int status, int lineno, int seq, string err)
        {
            tbStatus.Text = status.ToString();
            tbLineNo.Text = lineno.ToString();
            CurrentLineNo = lineno;
            GCodeView_GotoLine(lineno);
            tbSeq.Text = seq.ToString();
            tbErr.Text = err;

            if ((status != 0) && status != 1005)
            {
                MessageBox.Show(err); // status 1005 = successful halt
            }
            ExecutionInProgress = false; // not running anymore.
            JogPanel1.EnableJog();  // reenable the Jog Buttons
            btnSingleStep.IsEnabled = true;
        }

        void InterpStatus(int lineno, string msg)
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => InterpStatus2(lineno, msg)));

            // So why didn't this work?!!!
            //ConsoleMutex.WaitOne();
            //tbLineNo.Text = lineno.ToString();
            //tbErr.Text = msg;
            //// go to that line in the viewer
            //GCodeView_GotoLine(lineno);
            //ConsoleMutex.ReleaseMutex();
        }

        private void InterpStatus2(int lineno, string msg)
        {
            tbLineNo.Text = lineno.ToString();
            tbErr.Text = msg;
            // go to that line in the viewer
        //    CurrentLineNo = lineno;
        //    GCodeView_GotoLine(lineno);

        }
        #endregion

        private void AddHandlers()
        {
            // set the callback for various functions
            KM.MessageReceived += new KMotion_dotNet.KMConsoleHandler(Console_Msg_Update);

            // Callback for Errors
            KM.ErrorReceived += new KMotion_dotNet.KMErrorHandler(KM_ErrorUpdated);

            // Coordinated Motion Callback - Gcode file completed
            KM.CoordMotion.Interpreter.InterpreterCompleted += new KMotion_dotNet.KM_Interpreter.KM_GCodeInterpreterCompleteHandler(Interpreter_InterpreterCompleted);
            KM.CoordMotion.Interpreter.InterpreterStatusUpdated += new KMotion_dotNet.KM_Interpreter.KM_GCodeInterpreterStatusHandler(InterpStatus);

            // Other interpreter callbacks.
            KM.CoordMotion.Interpreter.InterpreterUserMCodeCallbackRequested += new KM_Interpreter.KM_GCodeInterpreterUserMcodeCallbackHandler(MCode8Callback);
            KM.CoordMotion.Interpreter.InterpreterUserCallbackRequested += new KM_Interpreter.KM_GCodeInterpreterUserCallbackHandler(GCodeUserCallback);

            // KM.CoordMotion.Interpreter.
            SetMCodeHandlers();
        }

        #endregion

        #region MCODE Handlers
        private void SetMCodeHandlers()
        {
            KM_Interpreter KMI;

            // testing all the possible MCode Actions

            KMI = KM.CoordMotion.Interpreter;   // just for convinecne so I don't have to type so much...

            // startup action 47.
            KMI.SetMcodeAction(42, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");
            // set up MCODE actions for the Spindle Control
            // KMI.SetMcodeAction(10, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");
            // M3 action
            KMI.SetMcodeAction(3, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");
            KMI.SetMcodeAction(4, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");
            KMI.SetMcodeAction(5, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");
            KMI.SetMcodeAction(6, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");
            KMI.SetMcodeAction(7, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");
            KMI.SetMcodeAction(8, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");
            KMI.SetMcodeAction(9, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");
            // Set S to run thread 3 code which is preloaded with variable in userdata 99
            KMI.SetMcodeAction(10, MCODE_TYPE.M_Action_Program, 3, PVConst.P_SPINDLE_RPM_CMD, 0, 0, 0, "");

            // MCode Action 1 - Set a bit high or low
            // the bit to  set is in the first agument, the state of the bit is in the second
            // Set M100 and M101 to set and clear bit 160 on the KFLOP Board - 160 - Kanalog output
            KMI.SetMcodeAction(21, MCODE_TYPE.M_Action_Setbit, 160, 1, 0, 0, 0, "");  // this should set the LED
            KMI.SetMcodeAction(22, MCODE_TYPE.M_Action_Setbit, 160, 0, 0, 0, 0, "");  // this should clear the LED

            // MCode Action 2 - Set two bits high or low
            // set M102 and M103 to set/clear bits 162 and 163. LEDs on these two pins should be opposite
            KMI.SetMcodeAction(23, MCODE_TYPE.M_Action_SetTwoBits, 162, 1, 163, 0, 0, ""); // set bit 162, clear bit 163
            KMI.SetMcodeAction(24, MCODE_TYPE.M_Action_SetTwoBits, 162, 0, 163, 1, 0, ""); // set bit 162, clear bit 163

            // MCode Action 3 - Set a DAC value on the Kanalog interface
            // DAC#, Scale, Offset, Min, Max
            // M104 set DAC3 offset to 600
            KMI.SetMcodeAction(25, MCODE_TYPE.M_Action_DAC, 3, 1, 600, -2000, 2000, "");
            // M105 set DAC3 offset to -600
            KMI.SetMcodeAction(26, MCODE_TYPE.M_Action_DAC, 3, 1, -600, -2000, 2000, "");
            // M106 set DAC3 offset to 0
            KMI.SetMcodeAction(26, MCODE_TYPE.M_Action_DAC, 3, 1, 0, -2000, 2000, "");

            // MCode Action 4 - Run a user C program on the KFLOP board 
            // Set M110 to run thread 3 code which has been preloaded.
            KMI.SetMcodeAction(31, MCODE_TYPE.M_Action_Program, 3, 0, 0, 0, 0, "");
            // Set M111 to run thread 3 code which has been preloaded.
            KMI.SetMcodeAction(32, MCODE_TYPE.M_Action_Program, 3, 0, 0, 0, 0, "");
            // Set M112 to run thread 3 code which has been preloaded.
            KMI.SetMcodeAction(33, MCODE_TYPE.M_Action_Program, 3, 0, 0, 0, 0, "");


            // MCode Action 5 - Run a user C program on the KFLOP board and wait for it to finish
            // set M119 to run thread 3 code which has be preloaded, pass the code name in persist.UserData[100]
            KMI.SetMcodeAction(40, MCODE_TYPE.M_Action_Program_wait, 3, 100, 0, 0, 0, "");


            // MCode Action 6 - Run a user C program on the KFLOP board, wait for it to finish and resync the position

            // MCode Action 7 - Run a PC Program
            // the name of the program to run is in the last (string) argument.
           // KMI.SetMcodeAction(7, MCODE_TYPE.M_Action_Program_PC, 0, 0, 0, 0, 0, "C:\\KMotion435c\\PC VCS\\MessageBoxTest2.exe");

            // MCode Action 8 - Callback to a user function in this app
            // M8 code should call the MCode8Callback function with something.
          //  KMI.SetMcodeAction(8, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");


            // MCode Action 9 - Wait until a bit on the KFLOP board is set/cleared.
            // wait for bit 1040 (extended IO from Konnect) to go to 1
           // KMI.SetMcodeAction(9, MCODE_TYPE.M_Action_Waitbit, 1040, 1, 0, 0, 0, "");

        }


        #endregion

        #region // Interpreter setup functions

        private void Set_Fixture_Offset(int Fixture_Number, double X, double Y, double Z)
        {
            // Set GVars for Offsets
            KM.CoordMotion.Interpreter.SetOrigin(Fixture_Number,
                KM.CoordMotion.Interpreter.InchesToUserUnits(X),
                KM.CoordMotion.Interpreter.InchesToUserUnits(Y),
                KM.CoordMotion.Interpreter.InchesToUserUnits(Z), 0, 0, 0);

//            KM.CoordMotion.Interpreter.SetupParams.OriginIndex = -1; // Force update from GCode Vars
//            KM.CoordMotion.Interpreter.ChangeFixtureNumber(Fixture_Number); // Load offset for fixture

        }
        #endregion


        #region KFLOP Status
        private void ServiceKFlopStatus(ref KM_MainStatus KStat)
        {
            BitOps B = new BitOps();

            // this first little bit is just for testing
            //int X = KM.GetUserData(120);
            tbStatus1.Text = KStat.GetPC_comm(4).ToString("X");
            // tickTimer.Stop();
            tbTickTime.Text = ($"{tickTimer.ElapsedMilliseconds} ms");
            //tbStatus1.Text = X.ToString("X");

            // check the status of the Machine P_STATUS is in PC_comm[4];
            int status = KStat.PC_comm[CSConst.P_STATUS];
            if (B.AnyInMask(status, PVConst.SB_ACTIVE_MASK))   // Active bit is set in P_STATUS means thread 1 is running properly 
            {
                T1Active = true;
                // check the estop
                if (B.AnyInMask(status, PVConst.SB_ESTOP_MASK))
                {
                    ESTOP_FLAG = false; // SB_ESTOP bit set means everything is OK - not in ESTOP
                    if(B.AnyInMask(status, PVConst.SB_WARNING_STATUS_MASK))
                    { MachineWarning = true; }
                    else
                    { MachineWarning = false; }
                    if(B.AllInMask(status, PVConst.SB_ERROR_STATUS_MASK))
                    { MachineError = false; }
                    else { MachineError = true; }
                }
                else
                {
                    ESTOP_FLAG = true;  // in ESTOP! don't do anything else!
                }

            }
            else
            {
                // if Active is not set, don't bother with anything else because there is nothing to talk to.
                T1Active = false;
            }

        }
        #endregion

        #region KFLOP PC_Comm commands
        private void ServiceKFlopCommands(ref KM_MainStatus KStat)
        {

        }
        #endregion

        #region User Interface updates
        private void UpdateUI(ref KM_MainStatus KStat)
        {
            // Set DRO Colors
            if ((KStat.Enables & AXConst.A_AXIS) != 0)
            {
                DROX.Foreground = Brushes.Green;
            }
            else
            {
                DROX.Foreground = Brushes.Red;
            }
            if ((KStat.Enables & AXConst.Y_AXIS_MASK) != 0)
            {
                DROY.Foreground = Brushes.Green;
            }
            else
            {
                DROY.Foreground = Brushes.Red;
            }
            if ((KStat.Enables & AXConst.Z_AXIS_MASK) != 0)
            {
                DROZ.Foreground = Brushes.Green;
            }
            else
            {
                DROZ.Foreground = Brushes.Red;
            }

            // Get Ablosule Machine Coordinates
            double x = 0, y = 0, z = 0, a = 0, b = 0, c = 0;
            KM.CoordMotion.UpdateCurrentPositionsABS(ref x, ref y, ref z, ref a, ref b, ref c, false);
            DROX.Text = String.Format("{0:F4}", x);
            DROY.Text = String.Format("{0:F4}", y);
            DROZ.Text = String.Format("{0:F4}", z);

            // manage the current line number for the gcode listing
            if (ExecutionInProgress)
            {
                CurrentLineNo = KM.CoordMotion.Interpreter.SetupParams.CurrentLine;
                GCodeView_GotoLine(CurrentLineNo +1);
            }
            //else { CurrentLineNo = 1; }

            // update the check boxes
            if(cbT1.IsEnabled)
            { if (KM.ThreadExecuting(1)) cbT1.IsChecked = true; }
            if (cbT2.IsEnabled)
            { if (KM.ThreadExecuting(2)) cbT2.IsChecked = true; }
            if (cbT3.IsEnabled)
            { if (KM.ThreadExecuting(3)) cbT3.IsChecked = true; }
            if (cbT4.IsEnabled)
            { if (KM.ThreadExecuting(4)) cbT4.IsChecked = true; }
            if (cbT5.IsEnabled)
            { if (KM.ThreadExecuting(5)) cbT5.IsChecked = true; }
            if (cbT6.IsEnabled)
            { if (KM.ThreadExecuting(6)) cbT6.IsChecked = true; }
            if (cbT7.IsEnabled)
            { if (KM.ThreadExecuting(7)) cbT7.IsChecked = true; }

            // update limit switches 
            StatusPanel1.CheckLimit(ref KStat);
            StatusPanel1.CheckHome(ref KStat);

            // update the Spindle enable
            if ((KStat.Enables & AXConst.SPINDLE_AXIS_MASK) != 0)
            { cbSimulate.IsChecked = true; }
            else
            { cbSimulate.IsChecked = false; }
            // update the Spindle RPM
            tbSpindleSpeedRPM.Text = KStat.PC_comm[CSConst.P_RPM].ToString();
        }
        #endregion

        #region AvalonEdit window for viewing GCode
        private void GCodeView_Init()
        {

            // initialize all the parts of the AvaloneEdit component - call this from MainWindow()
            GCodeView.TextArea.TextView.BackgroundRenderers.Add(
                new HighlightCurrentLineBackgroundRenderer(GCodeView));
            GCodeView.TextArea.Caret.PositionChanged += (sender, e) => GCodeView.TextArea.TextView.InvalidateLayer(KnownLayer.Background);

            // show the line numbers
            GCodeView.ShowLineNumbers = true;

            // load the code highlighting - make a GCODE highlighter xml?
            string HFile = GetPathFile(CFiles.ConfigPath, CFiles.Highlight);
            try
            {
                Stream InStream = default(Stream);
                
                InStream = File.OpenRead(HFile);
                XmlTextReader XTextReader = default(XmlTextReader);
                XTextReader = new XmlTextReader(InStream);
                GCodeView.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(XTextReader, ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);
            }
            catch
            {
                MessageBox.Show("Couldn't find AvalonEdit Highlight definition file " + HFile);
            }

        }
        private void GCodeView_GotoLine(int LineNo)
        {
            GCodeView.ScrollTo(LineNo, 0);  // this moves the active line to the middle of the view
            GCodeView.TextArea.Caret.Line = LineNo; // this highlights the line
        }

        #endregion

        #region Button clicks

        #region Cprogram stuff
        // open and run a Cprogram in thread 1 - 
        private void BtnCProgram_Click(object sender, RoutedEventArgs e)
        {
            // open a windows dialog to read in the cprogram
            var openFileDlg = new OpenFileDialog();
            openFileDlg.InitialDirectory = GetStr(CFiles.KFlopCCodePath);
            openFileDlg.FileName = GetStr(CFiles.KThread1);
            if(openFileDlg.ShowDialog() == true)
            {
                CFiles.KThread1 = System.IO.Path.GetFileName(openFileDlg.FileName); // save the filename for next time
                CFiles.KFlopCCodePath = System.IO.Path.GetDirectoryName(openFileDlg.FileName);
                try
                {
                    KM.ExecuteProgram(1, openFileDlg.FileName, true);  // trying false here...
                    cbT1.IsEnabled = true;
                }
                catch(DMException ex)
                {
                    MessageBox.Show("Unable to execute C Program in KFLOP\r\r" + ex.InnerException.Message);
                }
            }
            openFileDlg.FileName = GetStr(CFiles.KThread2);
            if(openFileDlg.ShowDialog() == true)
            {
                CFiles.KThread2 = System.IO.Path.GetFileName(openFileDlg.FileName); // save the filename for next time
                CFiles.KFlopCCodePath = System.IO.Path.GetDirectoryName(openFileDlg.FileName);
                try
                {
                    KM.SetUserData(PVConst.P_NOTIFY, T2Const.T2_NO_CMD);
                    KM.ExecuteProgram(2, openFileDlg.FileName, true);
                    cbT2.IsEnabled = true;
                }
                catch (DMException ex)
                {
                    MessageBox.Show("Unable to execute C Program in KFLOP\r\r" + ex.InnerException.Message);
                }
            }
        }

        private void btnProgHalt_Click(object sender, RoutedEventArgs e)
        {
            KM.KillProgramThreads(1);
            KM.KillProgramThreads(2);

        }

        #endregion


        #region Get Board Info
        private void BtnGetBoard_Click(object sender, RoutedEventArgs e)
        {
            // get the status of the KFLOP board
            int[] boardlist;
            int nBoards = 0;

            boardlist = KM.GetBoards(out nBoards);
            if(nBoards > 0)
            {
                Title = String.Format("C# WPF App - Connected = USB location {0:X}", boardlist[0]);
            }
            else
            {
                Title = "C# WPF App - KFLOP Disconnected";
            }

        }
        #endregion

        #region Configuration Files

        private void Window_Closed(object sender, EventArgs e)
        {
            KM.Disconnect();    // make sure KM is disposed
            CFiles.SaveConfig();
            // close any open windows or dialogs.
            Environment.Exit(0);
            Application.Current.Shutdown();
        }

        private void btnSaveJ_Click(object sender, RoutedEventArgs e)
        {
            SetupWindow St2 = new SetupWindow(KM.CoordMotion.MotionParams);
            St2.Show();
        }

        private void btnOpenJ_Click(object sender, RoutedEventArgs e)
        {
            var openFile = new OpenFileDialog();
            openFile.InitialDirectory = GetStr(CFiles.ConfigPath);
            openFile.FileName = GetStr(CFiles.MotionParams);
            if (openFile.ShowDialog() == true)
            {
                // CFiles.MotionParams = System.IO.Path.GetFileName(openFile.FileName); // this copies the new file name to the default
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader(openFile.FileName);
                JsonReader Jreader = new JsonTextReader(sr);
                Xparam = Jser.Deserialize<MotionParams_Copy>(Jreader);
                sr.Close();
                CFiles.MotionParams = System.IO.Path.GetDirectoryName(openFile.FileName);

                Xparam.CopyParams(KM.CoordMotion.MotionParams); // copy the motion parameters to the KM instance
                SetupWindow st1 = new SetupWindow(KM.CoordMotion.MotionParams);
                st1.Show();
            }
        }

        #region Motion Parameters
        private void OpenMotionParams(ref ConfigFiles cf, ref MotionParams_Copy Xp)
        {
            try
            {
                string CombinedPath = GetPathFile(cf.ConfigPath, cf.MotionParams);
                // MessageBox.Show(CombinedPath);

                if (System.IO.File.Exists(CombinedPath) == true)
                {
                    JsonSerializer Jser = new JsonSerializer();
                    StreamReader sr = new StreamReader(CombinedPath);
                    JsonReader Jreader = new JsonTextReader(sr);
                    Xp = Jser.Deserialize<MotionParams_Copy>(Jreader);
                    sr.Close();
                    Xp.CopyParams(KM.CoordMotion.MotionParams); // copy the motion parameters to the KM instance
                }
            }
            catch
            {
                MessageBox.Show(cf.MotionParams, "motion params error!");
            }

        }

        #endregion

        private void OpenConfig(ref ConfigFiles cf)
        {
            if (cf.FindConfig())
            {
                string combinedConfigFile = System.IO.Path.Combine(cf.ConfigPath, cf.ThisFile);
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader(combinedConfigFile);
                JsonReader Jreader = new JsonTextReader(sr);
                cf = Jser.Deserialize<ConfigFiles>(Jreader);
                sr.Close();
            }
        }

        // copy a config file to the default config file
        // note that this will change the {ThisFile} property to "KTestConfig.json"
        private void btnConfig_Click(object sender, RoutedEventArgs e)
        {
            var openFile = new OpenFileDialog();
            openFile.InitialDirectory = GetStr(CFiles.ConfigPath);
            if(openFile.ShowDialog() == true)
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader(openFile.FileName);
                JsonReader Jreader = new JsonTextReader(sr);
                CFiles = Jser.Deserialize<ConfigFiles>(Jreader);
                sr.Close();
                CFiles.SaveConfig();
            }
        }

        //  Save the configuration file
        // these functions were moved to the ConfigFiles class.


        #endregion

        #region GCode execution buttons

        private void btnGCode_Click(object sender, RoutedEventArgs e)
        {
            // open a GCode file
            var GFile = new OpenFileDialog();
            GFile.DefaultExt = ".ngc";
            GFile.Filter = "ngc Files (*.ngc)|*.ngc|Text Files (*.txt)|*.txt|Tap Files (*.tap)|*.tap|GCode (*.gcode)|*.gcode|All Files (*.*)|*.*";
            GFile.InitialDirectory = GetStr(CFiles.GCodePath);
            GFile.FileName = GetStr(CFiles.LastGCode);
            if (GFile.ShowDialog() == true)
            {
                CFiles.LastGCode = System.IO.Path.GetFileName(GFile.FileName);
                CFiles.GCodePath = System.IO.Path.GetDirectoryName(GFile.FileName);
                tbGCodeFile.Text = CFiles.LastGCode;

                GCodeFileName = GFile.FileName;

                // load the file into the Gcode View
                GCodeView.Load(GCodeFileName);

            }

        }

        private void btnCycleStart_Click(object sender, RoutedEventArgs e)
        {
            if (ExecutionInProgress == true)
            {
                if(InFeedHold)
                {
                    KM.ResumeFeedhold();
                    InFeedHold = false;
                    HideFR();
                    btnCycleStart.Content = "Cycle Start";
                }

                return;    // if already running then ignore
            }
            else
            {
                // see if it should be simulated
                if(cbSimulate.IsChecked == true)
                {
                    KM.CoordMotion.IsSimulation = true;
                } else
                {
                    KM.CoordMotion.IsSimulation = false;    // don't forget clear when not checked!
                }
                ExecutionInProgress = true; // set here but not cleared until the InterpreterCompleted callback
                KM.CoordMotion.Abort();     // make sure that everything is cleared
                KM.CoordMotion.ClearAbort();

                if (GetInterpVars())
                {
                    // disable the Jog buttons
                    JogPanel1.DisableJog();
                    // disable single step
                    btnSingleStep.IsEnabled = false;
                    Set_Fixture_Offset(2, 2, 3, 0); // Set X, Y, Z for G55
                    KM.CoordMotion.Interpreter.Interpret(GCodeFileName);  // Execute the File!
                }
            }
        }

        private bool GetInterpVars()
        {
            try
            {
                // note here - because I'm not running in the standard directory structure I needed to specify the VarsFile
                // https://www.dynomotion.com/forum/viewtopic.php?f=12&t=1252&p=3638#p3638
                //
                KM.CoordMotion.Interpreter.VarsFile = System.IO.Path.Combine(CFiles.ConfigPath, CFiles.EMCVarsFile);
                KM.CoordMotion.Interpreter.ToolFile = System.IO.Path.Combine(CFiles.ConfigPath, CFiles.ToolFile);

                KM.CoordMotion.Interpreter.InitializeInterpreter();
                InterpreterInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"File not found '{ex}'");
                return false;
            }
        }

        private void btnFeedHold_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (KM.WriteLineReadLine("GetStopState") == "0") // https://dynomotion.com/Help/Cmd.htm#GetStopState
                {
                    KM.Feedhold();
                    btnCycleStart.Content = "Resume";
                    InFeedHold = true;
                    ShowFR();
                }
                //else
                //{
                //    KM.ResumeFeedhold();
                //    btnFeedHold.Content = "Feed Hold";
                //    btnSingleStep.IsEnabled = true;
                //}
            }
            catch(Exception)
            {
                KM.CoordMotion.Interpreter.Halt();
                JogPanel1.EnableJog();  // reenable the jog buttons when halted
                btnSingleStep.IsEnabled = true;
            }
        }

        private void btnHalt_Click(object sender, RoutedEventArgs e)
        {
            KM.CoordMotion.Interpreter.Halt();
            HideFR();   // put the feed hold button back 
            btnCycleStart.Content = "Cycle Start";
            JogPanel1.EnableJog();
            btnSingleStep.IsEnabled = true;
        }

        private void FR_Write(String s)
        {
            try
            {
                KM.WriteLine(s);
            }
            catch (DMException ex) // In case disconnect in the middle of reading status
            {
                MessageBox.Show(ex.InnerException.Message);
            }
        }

        private void btnRev_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            FR_Write("SetFROTemp -0.2");
        }

        private void btnFwd_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            FR_Write("SetFROTemp 0.2");
        }

        private void btnFR_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            FR_Write("SetFROTemp 0.0");
        }
        private void HideFR()
        {
            btnFwd.Visibility = Visibility.Collapsed;
            btnRev.Visibility = Visibility.Collapsed;
            btnFeedHold.Visibility = Visibility.Visible;
        }
        private void ShowFR()
        {
            btnFwd.Visibility = Visibility.Visible;
            btnRev.Visibility = Visibility.Visible;
            btnFeedHold.Visibility = Visibility.Collapsed;
        }

        private void btnSingleStep_Click(object sender, RoutedEventArgs e)
        {
            if (!ExecutionInProgress)
            {
                if (!InterpreterInitialized)
                {
                    if (GetInterpVars() == false)
                    { return; }
                }
                RestoreStoppedState = false;
                ExecutionInProgress = true;
                KM.CoordMotion.Interpreter.Interpret(GCodeFileName, CurrentLineNo, CurrentLineNo, 0);
                
            }
        }


        private void btnReStart_Click(object sender, RoutedEventArgs e)
        {
            if(!ExecutionInProgress)
            {
                KM.CoordMotion.IsPreviouslyStopped = PREV_STOP_TYPE.Prev_Stopped_None;
                CurrentLineNo = 0;
                GCodeView_GotoLine(CurrentLineNo + 1);
                ExecutionInProgress = false;
            }

        }


        #endregion



        private void btnConsole_Click(object sender, RoutedEventArgs e)
        {
            if(ConWin.IsLoaded)
            {
                ConWin.Show();
            }
            else
            {
                ConWin = new ConsolWindow();
                ConWin.Show();
            }
            
        }



        #endregion
        // this is just a test button to start Thread3
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // open a windows dialog to read in the cprogram
            var openFileDlg = new OpenFileDialog();
            openFileDlg.InitialDirectory = GetStr(CFiles.KFlopCCodePath);
            openFileDlg.FileName = GetStr(CFiles.KThread3);
            if (openFileDlg.ShowDialog() == true)
            {
                CFiles.KThread3 = System.IO.Path.GetFileName(openFileDlg.FileName);
                CFiles.KFlopCCodePath = System.IO.Path.GetDirectoryName(openFileDlg.FileName);
                try
                {
                    KM.SetUserData(0, 100); // this is a test - 100 should be the init value
                    KM.ExecuteProgram(3, openFileDlg.FileName, true);
                    cbT3.IsEnabled = true;

                }
                catch (DMException ex)
                {
                    MessageBox.Show("Unable to execute C Program in KFLOP\r\r" + ex.InnerException.Message);
                }
            }
        }

        #region String helper functions 
        private string GetPathFile(string a, string b)
        {
            if ((a != null) && (b != null))
            {
                return System.IO.Path.Combine(a, b);
            }
            else
            {
                return "";
            }
        }
        private string GetStr(string a)
        {
            if (a != null)
                return a;
            else
                return "";
        }
        #endregion

        #region Spindle Buttons

        private void btnSpindleCW_Click(object sender, RoutedEventArgs e)
        {
            // send Sxxx and M3
            KM.CoordMotion.Interpreter.InvokeAction(10, false);
            KM.CoordMotion.Interpreter.InvokeAction(3, false);
        }

        private void btnSpindleCCW_Click(object sender, RoutedEventArgs e)
        {
            // send Sxxx and M4
            KM.CoordMotion.Interpreter.InvokeAction(10, false);
            KM.CoordMotion.Interpreter.InvokeAction(4, false);
        }

        private void btnSpindleStop_Click(object sender, RoutedEventArgs e)
        {
            // Send an M5
            KM.CoordMotion.Interpreter.InvokeAction(5, false);
        }

        private void tbSpindleSpeedSet_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            double SSpeed;
            if (e.Key == Key.Enter)
            {
                // check the spindle speed for the correct range
                if (double.TryParse(tbSpindleSpeedSet.Text, out SSpeed))
                {
                    if ((SSpeed >= AXConst.MIN_SPINDLE_RPM) && (SSpeed <= AXConst.MAX_SPINDLE_RPM))
                    {
                        KM.CoordMotion.Interpreter.SetupParams.SpindleSpeed = SSpeed;
                        KM.CoordMotion.Interpreter.InvokeAction(10, false);
                    }
                    else
                    {
                        MessageBox.Show("Spindle Speed out of range!");
                    }
                }
                else
                {
                    tbSpindleSpeedSet.Text = "0";
                }
                tbSpindleSpeedSet.Background = Brushes.White;
            }
            else
            {
                tbSpindleSpeedSet.Background = Brushes.Pink;
            }
        }
        #endregion

    }
}
