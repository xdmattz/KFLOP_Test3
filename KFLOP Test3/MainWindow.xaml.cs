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
        // global variables in the MainWindow class
        static bool ExecutionInProgress = false;
        static bool InFeedHold = false;
        static bool InMotion = false;

        static KMotion_dotNet.KM_Controller KM; // this is the controller instance!
        static MotionParams_Copy Xparam;
        static ConfigFiles CFiles;

        // not sure if I need the Mutex if I'm using the Dispatcher but we will see...
        static Mutex ConsoleMutex = new Mutex();

        // these two globals are used in the status timer 
        static bool Connected = false;
        static int skip = 0;
        static int DisCount = 0;
        string GCodeFileName;
        int CurrentLineNo;

        // console window
        static ConsolWindow ConWin;



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
            try
            {
                KM = new KMotion_dotNet.KM_Controller();
            }
            catch(Exception e)
            {
                MessageBox.Show("Unable to load KMotion_dotNet Libraries.  Check Windows PATH or .exe location " + e.Message);
                System.Windows.Application.Current.Shutdown();  // and shut down the application...
                return;
            }

            // copy of the motion parameters that the JSON reader can use.
            Xparam = new MotionParams_Copy();
            // get the configuration file names
            CFiles = new ConfigFiles();
            // check if the the config file exists
            if(File.Exists("KTestConfig.json") == true)
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader("KTestConfig.json");
                JsonReader Jreader = new JsonTextReader(sr);
                CFiles = Jser.Deserialize<ConfigFiles>(Jreader);
                sr.Close();
            } else
            {
                MessageBox.Show("No configureation file found");
                // what to do here? 
                // Initialize the strings to null and save the file for next time
                SaveConfig(CFiles);
            }
            if(File.Exists(CFiles.MotionParams) == true)
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader(CFiles.MotionParams);
                JsonReader Jreader = new JsonTextReader(sr);
                Xparam = Jser.Deserialize<MotionParams_Copy>(Jreader);
                sr.Close();
                Xparam.CopyParams(KM.CoordMotion.MotionParams); // copy the motion parameters to the KM instance
            }

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

            // the tab controls
            JogPanel1.KMx = KM;
            // Status tab panel
            // Probing tab panel
            // Tools tab panel etc.

            // hide the fwd and rev buttons
            HideFR();

        }

        #region Status Timer Tick
        // Update Status Timer
        // currently set for 100 ms 
        // event handler for the Dispatch timer
        private void dispatchTimer_Tick(object sender, EventArgs e)
        {
            int[] BoardList;
            int nBoards = 0;
            // several sections
            // if the board is connected then 
            // then check certain things every cycle
            // check some things every second 
            if (Connected)
            {

                if (++skip == 10)    // These actions happen every second
                {
                    skip = 0;
                    // update the elapsed time 
                    tbExTime.Text = KM.CoordMotion.TimeExecuted.ToString();
                }
                else // these actions happen every cycle
                {
                    if (KM.WaitToken(100) == KMOTION_TOKEN.KMOTION_LOCKED) // KMOTION_LOCKED means the board is available
                    {
                        try
                        {
                            KM_MainStatus MainStatus = KM.GetStatus(false); // passing false does not lock to board while generating status
                            KM.ReleaseToken();

                            // Set DRO Colors
                            if ((MainStatus.Enables & 1) != 0)
                            {
                                DROX.Foreground = Brushes.Green;
                            }
                            else
                            {
                                DROX.Foreground = Brushes.Red;
                            }
                            if ((MainStatus.Enables & 2) != 0)
                            {
                                DROY.Foreground = Brushes.Green;
                            }
                            else
                            {
                                DROY.Foreground = Brushes.Red;
                            }
                            if ((MainStatus.Enables & 4) != 0)
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
                            if(ExecutionInProgress)
                            {
                                CurrentLineNo = KM.CoordMotion.Interpreter.SetupParams.CurrentLine;
                                GCodeView_GotoLine(CurrentLineNo);
                            }
                            //else { CurrentLineNo = 1; }

                        }
                        catch (DMException)  // in case disconnect in the middle of reading status
                        {
                            KM.ReleaseToken();  // make sure the token is released
                        }
                    }
                    else
                    {
                        Connected = false;
                    }
                    // Manage the Cycle Start button
                    // btnCycleStart.IsEnabled = !ExecutionInProgress;
                }
            }
            else
            {
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

            }
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

        private int MCode8Callback(int code)
        {
            MessageBox.Show(String.Format("Code = {0}", code));
            return 0;
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
            // Set S to run thread 3 code which is preloaded with variable in userdata 99
            KMI.SetMcodeAction(10, MCODE_TYPE.M_Action_Program, 3, 99, 0, 0, 0, "");

            // MCode Action 5 - Run a user C program on the KFLOP board and wait for it to finish
            // set M119 to run thread 3 code which has be preloaded, pass the code name in persist.UserData[100]
            KMI.SetMcodeAction(40, MCODE_TYPE.M_Action_Program_wait, 3, 100, 0, 0, 0, "");


            // MCode Action 6 - Run a user C program on the KFLOP board, wait for it to finish and resync the position

            // MCode Action 7 - Run a PC Program
            // the name of the program to run is in the last (string) argument.
            KMI.SetMcodeAction(7, MCODE_TYPE.M_Action_Program_PC, 0, 0, 0, 0, 0, "C:\\KMotion435c\\PC VCS\\MessageBoxTest2.exe");

            // MCode Action 8 - Callback to a user function in this app
            // M8 code should call the MCode8Callback function with something.
            KMI.SetMcodeAction(8, MCODE_TYPE.M_Action_Callback, 0, 0, 0, 0, 0, "");


            // MCode Action 9 - Wait until a bit on the KFLOP board is set/cleared.
            // wait for bit 1040 (extended IO from Konnect) to go to 1
            KMI.SetMcodeAction(9, MCODE_TYPE.M_Action_Waitbit, 1040, 1, 0, 0, 0, "");

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

            try
            {
                Stream InStream = default(Stream);
                InStream = File.OpenRead(CFiles.Highlight);
                XmlTextReader XTextReader = default(XmlTextReader);
                XTextReader = new XmlTextReader(InStream);
                GCodeView.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(XTextReader, ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);
            }
            catch
            {
                MessageBox.Show("Couldn't find AvalonEdit Highlight definition file " + CFiles.Highlight);
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
            openFileDlg.FileName = CFiles.KThread1;
            if(openFileDlg.ShowDialog() == true)
            {
                CFiles.KThread1 = openFileDlg.FileName; // save the filename for next time
                try
                {
                    KM.ExecuteProgram(1, openFileDlg.FileName, true);
                }
                catch(DMException ex)
                {
                    MessageBox.Show("Unable to execute C Program in KFLOP\r\r" + ex.InnerException.Message);
                }
            }
        }

        private void btnProgHalt_Click(object sender, RoutedEventArgs e)
        {
            KM.KillProgramThreads(1);

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
            SaveConfig(CFiles);
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
            openFile.FileName = CFiles.MotionParams;
            if (openFile.ShowDialog() == true)
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader(openFile.FileName);
                JsonReader Jreader = new JsonTextReader(sr);
                Xparam = Jser.Deserialize<MotionParams_Copy>(Jreader);
                sr.Close();
                CFiles.MotionParams = openFile.FileName;

                Xparam.CopyParams(KM.CoordMotion.MotionParams); // copy the motion parameters to the KM instance
                SetupWindow st1 = new SetupWindow(KM.CoordMotion.MotionParams);
                st1.Show();
            }
        }

        // copy a config file to the default config file
        private void btnConfig_Click(object sender, RoutedEventArgs e)
        {
            var openFile = new OpenFileDialog();
            if(openFile.ShowDialog() == true)
            {
                JsonSerializer Jser = new JsonSerializer();
                StreamReader sr = new StreamReader(openFile.FileName);
                JsonReader Jreader = new JsonTextReader(sr);
                CFiles = Jser.Deserialize<ConfigFiles>(Jreader);
                sr.Close();
                SaveConfig(CFiles);
            }
        }

        //  Save the configuration file
        private void SaveConfig(ConfigFiles cf)
        {
            cf.fPath = System.AppDomain.CurrentDomain.BaseDirectory;
            string combinedConfigFile = System.IO.Path.Combine(cf.fPath, "KTestConfig.json");

            JsonSerializer Jser = new JsonSerializer();
            StreamWriter sw = new StreamWriter(combinedConfigFile);
            JsonTextWriter Jwrite = new JsonTextWriter(sw);
            Jser.NullValueHandling = NullValueHandling.Ignore;
            Jser.Formatting = Newtonsoft.Json.Formatting.Indented;
            Jser.Serialize(Jwrite, cf);
            sw.Close();
        }
        #endregion

        #region GCode execution buttons

        private void btnGCode_Click(object sender, RoutedEventArgs e)
        {
            // open a GCode file
            var GFile = new OpenFileDialog();
            GFile.DefaultExt = ".ngc";
            GFile.Filter = "ngc Files (*.ngc)|*.ngc|Text Files (*.txt)|*.txt|Tap Files (*.tap)|*.tap|GCode (*.gcode)|*.gcode|All Files (*.*)|*.*";
            if (GFile.ShowDialog() == true)
            {
                tbGCodeFile.Text = System.IO.Path.GetFileName(GFile.FileName);
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
                try
                {
                    // note here - because I'm not running in the standard directory structure I needed to specify the VarsFile
                    // https://www.dynomotion.com/forum/viewtopic.php?f=12&t=1252&p=3638#p3638
                    //
                    KM.CoordMotion.Interpreter.VarsFile = CFiles.EMCVarsFile;
                    KM.CoordMotion.Interpreter.InitializeInterpreter();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"File not found '{ex}'");
                }
                // disable the Jog buttons
                JogPanel1.DisableJog();
                // disable single step
                btnSingleStep.IsEnabled = false;
                Set_Fixture_Offset(2, 2, 3, 0); // Set X, Y, Z for G55
                KM.CoordMotion.Interpreter.Interpret(GCodeFileName);  // Execute the File!
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
            if(!ExecutionInProgress)
            {

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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // open a windows dialog to read in the cprogram
            var openFileDlg = new OpenFileDialog();
            if (openFileDlg.ShowDialog() == true)
            {
               try
                {
                    KM.SetUserData(0, 100); // this is a test - 100 should be the init value
                    KM.ExecuteProgram(3, openFileDlg.FileName, true);
                }
                catch (DMException ex)
                {
                    MessageBox.Show("Unable to execute C Program in KFLOP\r\r" + ex.InnerException.Message);
                }
            }
        }
    }
}
