using System;
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
        #region Global Variables
        // global variables in the MainWindow class

        static bool KFLOP_Connected = false;  // used in the status time to indicate when the KFLOP is connected
        static int skip = 0;            // skip timer used to delay when KFLOP is not connected
        static bool ExecutionInProgress = false;    // true while interpreter is executing
        static bool InFeedHold = false;             // true when in feed hold
        static bool InMotion = false;               // true when jogging
        static int TimerEntry = 0;                  // used in the dispatch timer to avoid multiple entry.

        // these flags indicate the status of the machine - as opposed to status of the software.
        // they are updated by reading the P_STATUS persist variable from the KFLOP board controlling the machine
        // the current persist variable for this is #104 - so it is always read in the MainStatus as the PC_comm[4] variable
        static bool T1Active = false;   // indicates that Thread1 is running on the KFLOP Board
        static bool ESTOP_FLAG = true;
        static bool MachineIsHomed = false;
        static bool MachineWarning = false;
        static bool MachineError = false;
        static bool SingleStepping = false;
        static bool RestoreStoppedState = false;
        static bool InterpreterInitialized = false;
        static bool m_MDI = false;
        static bool m_M30 = false;
        static bool Halted1 = false;


        // machine offsets
        // 
        static double[] fixG28;
        static double[] fixG30;
        static double Stopped_X;
        static double Stopped_Y;
        static double Stopped_Z;
        static double Stopped_A;
        static double Stopped_B;
        static double Stopped_C;
        static double Safe_Z;
        static bool SafeRelAbs;
        static KMotion_dotNet.CANON_DIRECTION StoppedSpindleDirection;
        static double StoppedSpindleSpeed;
        static double StoppedFeedrate;


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

        // fixture stuff
        Fixtures WorkOffsets;

        // console window
        static ConsolWindow ConWin;

        int[] PVars;

        // this is a timing variable for debugging purposes 
        // can be used to time processes 
        System.Diagnostics.Stopwatch tickTimer;

        // tab items 
        JogPanel JogPanel1;
        StatusPanel StatusPanel1;
        OffsetPanel OffsetPanel1;
        private readonly int LineNumber;

        // more tab items here...

        #endregion


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

            // Initialize the global variables
            PVars = new int[14];

            fixG28 = new double[6];
            fixG30 = new double[6];

            Safe_Z = 0.0;
            SafeRelAbs = false;


            // Machine instance for status and bit control
            // BP308 = new Machine(ref KM);
            // get the configuration file names
            CFiles = new ConfigFiles();
            OpenConfig(ref CFiles);

            // Initialize the GCode Interpreter
            GetInterpVars();    // load the emcvars and tool files
            // copy of the motion parameters that the JSON reader can use.
            Xparam = new MotionParams_Copy();
            OpenMotionParams(ref CFiles, ref Xparam);
            // console window - for receiving messages from KFLOP
            ConWin = new ConsolWindow();
            // GCode Viewer - Avalon Edit window for viewing GCode
            GCodeView_Init();
            // add the callbacks
            AddHandlers();
            // Initialize the buttons
            

            // ******* // so I need to wait for the KFLOP to connect before I can do this? - Nope.

            // Initialize DROs
            XDRO.axis = AXConst.X_AXIS;
            XDRO.AxisName.Text = "X:";
            XDRO.SetBigValue(0.0);
            XDRO.ZeroButtonClickedCallback += ZeroSetCallback; // the zero callback method

            YDRO.axis = AXConst.Y_AXIS;
            YDRO.AxisName.Text = "Y:";
            YDRO.SetBigValue(0.0);
            YDRO.ZeroButtonClickedCallback += ZeroSetCallback; // the zero callback method

            ZDRO.axis = AXConst.Z_AXIS;
            ZDRO.AxisName.Text = "Z:";
            ZDRO.SetBigValue(0.0);
            ZDRO.ZeroButtonClickedCallback += ZeroSetCallback; // the zero callback method


            // start a timer for the status update
            var Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromMilliseconds(100);    // timer tick every 100 ms (1/10 sec)
            Timer.Tick += dispatchTimer_Tick;
            Timer.Start();

            // debug timer
            tickTimer = new System.Diagnostics.Stopwatch(); // for debuging the timing
            tickTimer.Reset();

            // Work offset functions
            WorkOffsets = new Fixtures(ref KM);

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

            // the Work Offset Panel
            OffsetPanel1 = new OffsetPanel(ref KM);
            var Tab3 = new TabItem();
            Tab3.Name = "tabItemContent2";
            Tab3.Header = "Work Offsets";
            Tab3.Content = OffsetPanel1;
            tcMainTab.Items.Add(Tab3);
            OffsetPanel1.InitG28(fixG28);
            OffsetPanel1.InitG30(fixG30);


            // Probing tab panel
            // Tools tab panel etc.
            GCodeButtonsInit();
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

            if (KFLOP_Connected)
            {
                #region KFLOP_Connected
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
                    KFLOP_Connected = false;
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
                        KFLOP_Connected = true;
                        Title = String.Format("C# WPF App - Connected = USB location {0:X} count = {1}", BoardList[0], DisCount);
                    }
                    else
                    {
                        KFLOP_Connected = false;
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

        public void KM_ErrorUpdated(string msg)
        {
            Dispatcher.BeginInvoke(new System.Threading.ThreadStart(() => KM_ErrorUpdated2(msg)));
        }

        public void KM_ErrorUpdated2(string msg)
        {
            MessageBox.Show(msg);
        }

        #region GCode Interpreter callback handlers
        // there are 4 possible interpreter callbacks
        // 1 - Interpreter Complete
        // 2 - Interpreter Status
        // 3 - User Callback Requested
        // 4 - User MCode Callback Requested

        /// <summary>
        /// MCode8Callback - this is the method that is called when ever the MCode callback is specified
        /// // need to figure out what happens with each mcode - ie M3, M4, M5, M6 and S
        /// </summary>
        // 'code' is the action# see https://www.dynomotion.com/wiki/index.php/KMotion_Libraries_-_GCode_Interpreter_-_Trajectory_Planner_-_Coordinated_Motion_-_.NET#GCode_Actions
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
                    case 2: // M2 End Program
                        m_M30 = true;   // set the end of program flag
                        break;
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
                    case 24:    // M30 End Program
                        m_M30 = true; // set the end of program flag
                        break;
                    case 42: // Cycle start
                    case 43: // Halt
                    case 44: // Stop
                    case 45: // FeedHold
                    case 46: // Resume
                    case 47: // Prog Start
                    case 48: // Prog Exit
                        break;
                    default: break;
                }
            }
            // MessageBox.Show(String.Format("Code = {0}", code));
            return 0;
        }

        private void GCodeUserCallback(string msg)
        {
            MessageBox.Show(msg);
        }


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
            // check if this was an MDI command
            if (m_MDI == false)
            {
                tbStatus.Text = status.ToString();
                tbLineNo.Text = lineno.ToString();
                CurrentLineNo = lineno;
                GCodeView_GotoLine(lineno);
                tbSeq.Text = seq.ToString();
                tbErr.Text = err;
            }
            if ((status != 0) && (status != 1005))
            {
                // if there is no GCode file then this will happen!
                // MessageBox.Show(err); // status 1005 = successful halt
                // restart
                btnMDI.IsEnabled = true;
                btnGCode.IsEnabled = true;
                SingleStepping = false;
                GCodeButtonsInit();
            }
            if(m_M30)   // at the end of the program - set the cycle start button
            {
                m_M30 = false;
                GCodeButtonsInit();
                btnGCode.IsEnabled = true; // enable the load GCode button
                SingleStepping = false;
            }
            if (SingleStepping)
            {
                // what should we do here if single stepping? - get the stopped state?
                // MessageBox.Show("Single Stepping!");
                btnCycleStart.Content = "Continue";
            }
            if(status == 1005)
            {
                // get the stopped state coordinates
                KM.CoordMotion.Interpreter.ReadCurMachinePosition(ref Stopped_X, ref Stopped_Y, ref Stopped_Z, ref Stopped_A, ref Stopped_B, ref Stopped_C);
                StoppedSpindleDirection = KM.CoordMotion.Interpreter.SetupParams.SpindleDirection;
                StoppedSpindleSpeed = KM.CoordMotion.Interpreter.SetupParams.SpindleSpeed;
                StoppedFeedrate = KM.CoordMotion.Interpreter.SetupParams.FeedRate;
                string msg = string.Format("Halted1 happend first\n");
                if (ExecutionInProgress) msg += "Execution in progress\n";
                msg += string.Format("X: {0:F4}\n", Stopped_X);
                msg += string.Format("Y: {0:F4}\n", Stopped_Y);
                msg += string.Format("Z: {0:F4}\n", Stopped_Z);
                msg += string.Format("A: {0:F4}\n", Stopped_A);
                msg += string.Format("B: {0:F4}\n", Stopped_B);
                msg += string.Format("C: {0:F4}\n", Stopped_C);
                msg += string.Format("FR: {0:F3}\n", StoppedFeedrate);
                msg += string.Format("Spindle Speed: {0:F4}\n", StoppedSpindleSpeed);
                msg += string.Format("SpindleDir: {0}", StoppedSpindleDirection);
                MessageBox.Show(msg);
            }

            ExecutionInProgress = false; // not running anymore.
            m_MDI = false;  // any manual commands are done.

            JogPanel1.EnableJog();  // reenable the Jog Buttons
            btnSingleStep.IsEnabled = true;
            btnCycleStart.IsEnabled = true;
            btnFeedHold.IsEnabled = false;
            btnHalt.IsEnabled = false;
            OffsetPanel1.OffsetButtons.EnableButtons();            // Offsets enabled
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

            // Interpreter call back methods
            // Coordinated Motion Callback - Gcode file completed
            KM.CoordMotion.Interpreter.InterpreterCompleted += new KMotion_dotNet.KM_Interpreter.KM_GCodeInterpreterCompleteHandler(Interpreter_InterpreterCompleted);
            KM.CoordMotion.Interpreter.InterpreterStatusUpdated += new KMotion_dotNet.KM_Interpreter.KM_GCodeInterpreterStatusHandler(InterpStatus);

            // Other interpreter callbacks.
            KM.CoordMotion.Interpreter.InterpreterUserMCodeCallbackRequested += new KM_Interpreter.KM_GCodeInterpreterUserMcodeCallbackHandler(MCode8Callback);
            KM.CoordMotion.Interpreter.InterpreterUserCallbackRequested += new KM_Interpreter.KM_GCodeInterpreterUserCallbackHandler(GCodeUserCallback);

            // KM.CoordMotion.Interpreter. M Code handlers
            SetMCodeHandlers();

            // Call backs for motion
            // add the motion callbacks here for 3D plotting of paths


        }

        #endregion

        #region MCODE Handlers
        private void SetMCodeHandlers()
        {
            KM_Interpreter KMI;

            // testing all the possible MCode Actions

            KMI = KM.CoordMotion.Interpreter;   // just for convinecne so I don't have to type so much...

            // the first argument of SetMcodeAction is the index# see https://www.dynomotion.com/wiki/index.php/KMotion_Libraries_-_GCode_Interpreter_-_Trajectory_Planner_-_Coordinated_Motion_-_.NET#GCode_Actions
            // startup action 42.
            KMI.SetMcodeAction(42, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, ""); // Cycle Start
            // set up MCODE actions for the Spindle Control
            // KMI.SetMcodeAction(10, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");
            // M3 action
            KMI.SetMcodeAction(3, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, ""); // M3 Spindle On CW
            KMI.SetMcodeAction(4, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, ""); // M4 Spindle On CCW
            KMI.SetMcodeAction(5, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, ""); // M5 Spindle Off
            KMI.SetMcodeAction(6, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, ""); // M6 Tool Change
            KMI.SetMcodeAction(7, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, ""); // M7 Mist On
            KMI.SetMcodeAction(8, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, ""); // M8 Flood On
            KMI.SetMcodeAction(9, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, ""); // M9 Coolant Off
            KMI.SetMcodeAction(2, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, ""); // M2 Stop 
            KMI.SetMcodeAction(41, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, ""); // M30 Stop and Rewind

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
            // process any commands from KFLOP board
            // commands passed in persist.UserData variables.
        }
        #endregion

        #region User Interface updates
        private void UpdateUI(ref KM_MainStatus KStat)
        {
            // Set DRO Colors - based on active axis
            if ((KStat.Enables & AXConst.A_AXIS) != 0)
            {
                XDRO.SetBigColor(Brushes.Green);
            }
            else
            {
                XDRO.SetBigColor(Brushes.Red);
            }
            if ((KStat.Enables & AXConst.Y_AXIS_MASK) != 0)
            {
                YDRO.SetBigColor(Brushes.Green);
            }
            else
            {
                YDRO.SetBigColor(Brushes.Red);
            }
            if ((KStat.Enables & AXConst.Z_AXIS_MASK) != 0)
            {
                ZDRO.SetBigColor(Brushes.Green);
            }
            else
            {
                ZDRO.SetBigColor(Brushes.Red);
            }

            // Get Ablosule Machine Coordinates
            double x = 0, y = 0, z = 0, a = 0, b = 0, c = 0;
            KM.CoordMotion.UpdateCurrentPositionsABS(ref x, ref y, ref z, ref a, ref b, ref c, false);


            // Machine Coordinates
            double Mx = 0, My = 0, Mz = 0, Ma = 0, Mb = 0, Mc = 0;
            // Interpreter Coordinates
            double Ix = 0, Iy = 0, Iz = 0, Ia = 0, Ib = 0, Ic = 0;

            KM.CoordMotion.Interpreter.ConvertAbsoluteToMachine(x, y, z, a, b, c, ref Mx, ref My, ref Mz, ref Ma, ref Mb, ref Mc);
            KM.CoordMotion.Interpreter.ConvertAbsoluteToInterpreterCoord(x, y, z, a, b, c, ref Ix, ref Iy, ref Iz, ref Ia, ref Ib, ref Ic);
            // 
            // show the values of Machine and Intrepeter coordinates
            XDRO.SetBigValue(Ix);
            YDRO.SetBigValue(Iy);
            ZDRO.SetBigValue(Iz);

            XDRO.SetMachValue(x);
            YDRO.SetMachValue(y);
            ZDRO.SetMachValue(z);

            int units = (int)(KM.CoordMotion.Interpreter.SetupParams.LengthUnits);
            XDRO.SetUnits(units);
            YDRO.SetUnits(units);
            ZDRO.SetUnits(units);

            // manage the current line number for the gcode listing
            if (ExecutionInProgress && (m_MDI == false))
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
            { cbSpindleEnable.IsChecked = true; }
            else
            { cbSpindleEnable.IsChecked = false; }
            // update the Spindle RPM
            tbSpindleSpeedRPM.Text = KStat.PC_comm[CSConst.P_RPM].ToString();

            // check the current work offset and set the button color
            // see if this will reflect the GCode setting...
            // only do this if the G Code is executing
            if (ExecutionInProgress == true)
            {
                OffsetPanel1.OffsetButtons.SetButton(KM.CoordMotion.Interpreter.SetupParams.OriginIndex);
                
            }
            OffsetPanel1.SetOffsetDisplay();
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


        #region GCode file open
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

                // check the file for G20/G21
                CheckForG20G21(GCodeFileName);

                // load the file into the Gcode View
                GCodeView.Load(GCodeFileName);
            }
        }

        private void CheckForG20G21(string filename)
        {
            // open the file
            System.IO.StreamReader infile = new StreamReader(filename);
            // scan the first 10 or so lines for G20 or G21
            const int MaxLineCount = 15;
            int lineCount = 0;
            string line;
            while((line = infile.ReadLine()) != null) 
            {
                if((line.Contains("G20")) || (line.Contains("g20")))
                {
                    // inches
                    // MessageBox.Show("Inches");
                    SwitchToInch();
                    break;
                }
                if((line.Contains("G21")) || (line.Contains("g21")))
                {
                    // metric
                    // MessageBox.Show("Metric");
                    SwitchToMM();
                    break;
                }
                if(lineCount++ > MaxLineCount)
                {
                    MessageBox.Show("No G20/G21 found - assuming Inches");
                    SwitchToInch();
                }
            }
            infile.Close(); // don't forget to close the file so the Interpreter can use it!


        }

        private void SwitchToInch()
        {
            // check the current units of the interpreter - if already in inches then don't do anything.
            if(KM.CoordMotion.Interpreter.SetupParams.LengthUnits != CANON_UNITS.CANON_UNITS_INCHES)
            {
                int fixture;
                // get the current fixture number
                fixture = KM.CoordMotion.Interpreter.SetupParams.OriginIndex;
                // switch to inches

                // convert the current offsets from metric to inch
                // write to the EMVars file
                string EMCVarsFileName = System.IO.Path.Combine(CFiles.ConfigPath, CFiles.EMCVarsFile_inch);
                WorkOffsets.SaveFixtures(EMCVarsFileName, (1 / 25.400), fixG28, fixG30);
                // reload the EMVars file
                KM.CoordMotion.Interpreter.VarsFile = EMCVarsFileName;
                KM.CoordMotion.Interpreter.InitializeInterpreter();
                InterpreterInitialized = true;

                GetG28G30(EMCVarsFileName);
                OffsetPanel1.InitG28(fixG28);
                OffsetPanel1.InitG30(fixG30);
                SendGCodeLine("G20");

                // reset the fixture number to reload the offsets - now in inches
                KM.CoordMotion.Interpreter.ChangeFixtureNumber(fixture); 
                // this doesn't seem to do what I wanted which was to have the DRO screen update when switching units
            }
        }

        private void SwitchToMM()
        {
            // check the current units of the interpreter
            if (KM.CoordMotion.Interpreter.SetupParams.LengthUnits != CANON_UNITS.CANON_UNITS_MM)
            {
                int fixture;
                // get the current fixture number
                fixture = KM.CoordMotion.Interpreter.SetupParams.OriginIndex;
                // switch to mm
                // convert the current offsets from inch to mm
                string EMCVarsFileName = System.IO.Path.Combine(CFiles.ConfigPath, CFiles.EMCVarsFile_mm);
                WorkOffsets.SaveFixtures(EMCVarsFileName, (25.400), fixG28, fixG30);
                // reload the EMVars file
                KM.CoordMotion.Interpreter.VarsFile = EMCVarsFileName;
                KM.CoordMotion.Interpreter.InitializeInterpreter();
                InterpreterInitialized = true;

                GetG28G30(EMCVarsFileName);
                OffsetPanel1.InitG28(fixG28);
                OffsetPanel1.InitG30(fixG30);
                SendGCodeLine("G21");
                // reset the fixture number to reload the offsets - now in mm
                KM.CoordMotion.Interpreter.ChangeFixtureNumber(fixture);
            }
        }

        #endregion

        #region GCode execution buttons

        private void btnCycleStart_Click(object sender, RoutedEventArgs e)
        {
            if (Halted1)
            {
                // resume from stopped state
                Halted1 = false;
            }
            btnGCode.IsEnabled = false; // disable the load GCode button
            btnMDI.IsEnabled = false;
            btnCycleStart.IsEnabled = false;    // disable the buttons
            btnReStart.IsEnabled = false;
            btnSingleStep.IsEnabled = false;
            // enable feedhold
            btnFeedHold.IsEnabled = true;
            // enable Halt
            btnHalt.IsEnabled = true;

            

            if (ExecutionInProgress == true)    
            {
                if(InFeedHold)
                {
                    KM.ResumeFeedhold();
                    InFeedHold = false;
                    HideFR();
                    btnCycleStart.Content = "Resume";

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

                // disable the Jog buttons
                JogPanel1.DisableJog();
                // disable single step

                OffsetPanel1.OffsetButtons.DisableButtons();
                // set the Execution in progress flag
                ExecutionInProgress = true;

                if (SingleStepping == true)
                {
                    // if in the middle of single stepping, then start from the current line.
                    KM.CoordMotion.Interpreter.Interpret(GCodeFileName, CurrentLineNo, -1, 0);
                    SingleStepping = false;
                }
                else
                {
                    KM.CoordMotion.Interpreter.Interpret(GCodeFileName);  // Execute the File!
                    SingleStepping = false;
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
                // 
                string EMCVarsFileName = System.IO.Path.Combine(CFiles.ConfigPath, CFiles.EMCVarsFile);
                string EMCSetupFileName = System.IO.Path.Combine(CFiles.ConfigPath, CFiles.EMCSetupFile);
                string EMC_ToolFileName = System.IO.Path.Combine(CFiles.ConfigPath, CFiles.ToolFile);

                KM.CoordMotion.Interpreter.SetupFile = EMCSetupFileName;
                KM.CoordMotion.Interpreter.VarsFile = EMCVarsFileName;
                KM.CoordMotion.Interpreter.ToolFile = EMC_ToolFileName;

                KM.CoordMotion.Interpreter.InitializeInterpreter();
                InterpreterInitialized = true;

                GetG28G30(EMCVarsFileName);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"File not found '{ex}'");
                return false;
            }
        }

        #region G28 and G30 Variables
        private void GetG28G30(string FileName)
        {
            // get the G28 and G30 variables.
            using (TextReader reader = File.OpenText(FileName))
            {
                int G28Lines = 0;
                int G30Lines = 0;
                int LineCount = 0;
                while ((G28Lines < 6) && (G30Lines < 6))
                {
                    string text = reader.ReadLine();    // read a line
                    string[] bits = text.Split('\t');   // split at the tab
                    int varNumber = int.Parse(bits[0]);
                    if (varNumber == (5161 + G28Lines))
                    {
                        fixG28[G28Lines] = double.Parse(bits[1]);
                        G28Lines++;
                    }
                    if (varNumber == (5181 + G30Lines))
                    {
                        fixG30[G30Lines] = double.Parse(bits[1]);
                        G30Lines++;
                    }
                    if (LineCount++ > 20) break;    // if we get to here we have a problem!
                }
                reader.Close();
            }
        }
        #endregion
        // replaced by reading stopped state location in interpreter callback
        //private void WaitForStop()
        //{
        //    // wait until the machine is stopped and recored the stopped axis variables
        //    // get the stop state
        //    string StopRecord = "";
        //    bool StopCondition = false;
        //    System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        //    stopwatch.Start();
        //    long time1, time2;
        //    time2 = stopwatch.ElapsedMilliseconds;
        //    Thread.Sleep(40);   // sleep for just a little bit.

        //    do
        //    {
        //        time1 = stopwatch.ElapsedMilliseconds;
        //        if (time1 - time2 > 10)
        //        {
        //            time2 = time1; // setup for next delay
        //            string sres = KM.WriteLineReadLine("GetStopState");
        //            if ((sres == "3") || (sres == "4") || (sres == "0")) StopCondition = true;
        //            StopRecord += String.Format("Time: {0} ", time1) + sres + "\n";

        //        }
        //        if (time1 > 3000) StopCondition = true; // timeout in 3ms

        //    }
        //    while (StopCondition != true);
        //    // MessageBox.Show(StopRecord);
        //    // get the absolute postitions at stopped
        //    KM.CoordMotion.Interpreter.ReadCurMachinePosition(ref Stopped_X, ref Stopped_Y, ref Stopped_Z, ref Stopped_A, ref Stopped_B, ref Stopped_C);
        //}

        private void btnHalt_Click(object sender, RoutedEventArgs e)
        {
            KM.CoordMotion.Interpreter.Halt();
//            WaitForStop();  // stop and save the stopped state. 
            // better place to do this is to check for status 1005 in the interpreter callback.
            Halted1 = true;
            HideFR();   // put the feed hold button back 
            btnCycleStart.Content = "Resume";
            btnCycleStart.IsEnabled = true;
            JogPanel1.EnableJog();
            btnSingleStep.IsEnabled = true;
            btnReStart.IsEnabled = true;
            btnMDI.IsEnabled = true;

        }

        #region Feed Hold Buttons
        private void btnFeedHold_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // https://dynomotion.com/Help/Cmd.htm#GetStopState
                // GetStopState = 0 is not stopping - which means it must be moving...
                if (KM.WriteLineReadLine("GetStopState") == "0") 
                {
                    KM.Feedhold();
                    btnCycleStart.Content = "Resume";
                    btnCycleStart.IsEnabled = true;
                    InFeedHold = true;
                    ShowFR();
                    // WaitForStop(); // don't do this here... only in Halt
                }

                //else
                //{
                //    KM.ResumeFeedhold();
                //    btnFeedHold.Content = "Feed Hold";
                //    btnSingleStep.IsEnabled = true;
                //}
            }
            catch (Exception)
            {
                KM.CoordMotion.Interpreter.Halt();
                JogPanel1.EnableJog();  // reenable the jog buttons when halted
                GCodeButtonsInit();
                // 
            }
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
        #endregion

        private void btnSingleStep_Click(object sender, RoutedEventArgs e)
        {
            if (Halted1)
            {
                // resume from stopped state
                Halted1= false;
            }

            if (!ExecutionInProgress)
            {
                if (!InterpreterInitialized)
                {
                    if (GetInterpVars() == false)
                    { return; }
                }
                btnGCode.IsEnabled = false; // disable the load GCode button

                btnCycleStart.IsEnabled = false;
                RestoreStoppedState = false;
                btnMDI.IsEnabled = false;
                ExecutionInProgress = true;
                SingleStepping = true;
                btnFeedHold.IsEnabled = true;
                btnHalt.IsEnabled = true;
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
                SingleStepping = false;
                GCodeButtonsInit();
                btnGCode.IsEnabled = true; // enable the load GCode button
                btnMDI.IsEnabled = true;
            }

        }

        // Initial button enables
        private void GCodeButtonsInit()
        {
            btnCycleStart.Content = "Cycle Start";
            btnCycleStart.IsEnabled = true;

            btnFeedHold.IsEnabled = false;
            btnHalt.IsEnabled = false;

            btnSingleStep.IsEnabled = true;
            btnReStart.IsEnabled = true;

            btnMDI.IsEnabled = true;
            // enable the fixture offsets buttons
            OffsetPanel1.OffsetButtons.EnableButtons();
        }

        // Manual Data Input - Single GCode Line
        private void btnMDI_Click(object sender, RoutedEventArgs e)
        {
            SendGCodeLine(tbManualGcode.Text);
        }

        // send a single command line to the Interpreter
        private void SendGCodeLine(string GCodeLine)
        {
            // manual GCode line.
            // write the line to a file
            // then execute the file. - should be pretty simple...
            int i = 0;  // this should wait until the last gcode command is done.
            while (ExecutionInProgress)
            {
                Thread.Sleep(10);
                if (i++ > 1000) // this is a 10 second delay - that's pretty long...
                {
                    MessageBox.Show("Interpreter is busy");
                    return;
                }
            }
            bool f = false;
            string MDIFileName = CFiles.fPath + CFiles.MDIFile;
            // should check for an existing file name
            if (System.IO.File.Exists(MDIFileName) == false)
            {
                // make a new file name...
                var MDIF = new SaveFileDialog();
                MDIF.DefaultExt = ".gcode";
                MDIF.InitialDirectory = GetStr(CFiles.fPath);
                MDIF.FileName = GetStr(CFiles.MDIFile);
                if (MDIF.ShowDialog() == true)
                {
                    CFiles.MDIFile = System.IO.Path.GetFileName(MDIF.FileName);
                    MDIFileName = MDIF.FileName;
                }
                else { return; }
            }
            using (StreamWriter writer = new StreamWriter(MDIFileName))
            {
                // writer.WriteLine("%");    // don't need this but leaving it in because I learned something 
                writer.WriteLine(GCodeLine);
                // writer.WriteLine("%");
                writer.WriteLine();
                writer.Close();
                f = true;
            }
            if (f)
            {
                // do I need to check for simulation mode here?
                // NOTE! the GCode Interpreter line numbering starts at 0!
                KM.CoordMotion.Interpreter.Interpret(MDIFileName, 0, 0, 0);
                m_MDI = true;
                ExecutionInProgress = true;
            }
        }

        private int CheckforResumeCircumstances()
        {
            double cx, cy, cz, ca, cb, cc;  // current axis values
            cx = cy = cz = ca = cb = cc = 0;
            // trying to copy the functionality of KMotionCNC
            if(KM.CoordMotion.IsPreviouslyStopped == PREV_STOP_TYPE.Prev_Stopped_None)
            {
                //KM.CoordMotion.Interpreter.ReadAndSynchCurInterpreterPosition(ref cx, ref cy, ref cz, ref ca, ref cb, ref cc);
                //KM.CoordMotion.Interpreter.SetupParams.X_AxisPosition = cx;
                //KM.CoordMotion.Interpreter.SetupParams.Y_AxisPosition = cy;
                //KM.CoordMotion.Interpreter.SetupParams.Z_AxisPosition = cz;
                //KM.CoordMotion.Interpreter.SetupParams.A_AxisPosition = ca;
                //KM.CoordMotion.Interpreter.SetupParams.B_AxisPosition = cb;
                //KM.CoordMotion.Interpreter.SetupParams.C_AxisPosition = cc;
                return 0;
            }
            // read the current position
            KM.CoordMotion.UpdateCurrentPositionsABS(ref cx, ref cy, ref cz, ref ca, ref cb, ref cc, true);
            int dx, dy, dz, da, db, dc;
            dx = dy = dz = da = db = dc = 0;
            
            KM.CoordMotion.GetAxisDefinitions(ref dx, ref dy, ref dz, ref da, ref db, ref dc);
            // if the axis is enabled and hasn't moved...
            if(((dx < 0) || (round(cx) == round(Stopped_X))) &&
                ((dy < 0) || (round(cy) == round(Stopped_Y))) &&
                ((dz < 0) || (round(cz) == round(Stopped_Z))) &&
                ((da < 0) || (round(ca) == round(Stopped_A))) &&
                ((db < 0) || (round(cb) == round(Stopped_B))) &&
                ((dc < 0) || (round(cc) == round(Stopped_C))))
            {
                return 0;
            }
                

            return 0;

        }

        // round to a reasonablly small value
        private double round(double val)
        {
            if (KM.CoordMotion.Interpreter.SetupParams.LengthUnits == CANON_UNITS.CANON_UNITS_INCHES)
            {
                if (val < 0)
                {
                    val = (Math.Ceiling((val * 1e6 - 0.5) / (1e6)));
                }
                else
                {
                    val = (Math.Floor((val * 1e6 + 0.5) / (1e6)));
                }
            } else
            {
                if(val < 0)
                {
                    val = (Math.Ceiling((val * 1e5 - 0.5) / (1e5)));
                }
                else
                {
                    val = (Math.Floor((val * 1e5 + 0.5) / (1e5)));
                }
            }
            return val;
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

        #region Zero and offsets
        // this is the call back for setting the axis offset from the DROs
        private void ZeroSetCallback(int axis, double value)
        {
            int currfixture;
            currfixture = KM.CoordMotion.Interpreter.SetupParams.OriginIndex;
            double ax, ay, az, aa, ab, ac;  // absolute coordinates
            ax = ay = az = aa = ab = ac = 0;
            double fx, fy, fz, fa, fb, fc;  // fixture coordinates
            fx = fy = fz = fa = fb = fc = 0;
            double tx, ty, tz;
            tx = ty = tz = 0;
//            tz = KM.CoordMotion.Interpreter.SetupParams.ToolLengthOffset;
//            tx = KM.CoordMotion.Interpreter.SetupParams.ToolXOffset;
//            ty = KM.CoordMotion.Interpreter.SetupParams.ToolYOffset;

            KM.CoordMotion.Interpreter.GetOrigin(currfixture, ref fx, ref fy, ref fz, ref fa, ref fb, ref fc);
            KM.CoordMotion.ReadAndSyncCurPositions(ref ax, ref ay, ref az, ref aa, ref ab, ref ac);

            switch(axis)
            {
                case AXConst.X_AXIS: fx = ax - value - tx;  break;
                case AXConst.Y_AXIS: fy = ay - value - ty; break;
                case AXConst.Z_AXIS: fz = az - value - tz; break;
                case AXConst.A_AXIS: fa = aa - value; break;
                case AXConst.B_AXIS: fb = ab - value; break;
                case AXConst.C_AXIS: fc = ac - value; break;
                default : break;
            }
            KM.CoordMotion.Interpreter.SetOrigin(currfixture, fx, fy, fz, fa, fb, fc);
            // what does this do?
            KM.CoordMotion.Interpreter.SetupParams.OriginIndex = -1; // force update from GCode Vars
            KM.CoordMotion.Interpreter.ChangeFixtureNumber(currfixture); // reload the fixture

        }
        #endregion

    }
}
