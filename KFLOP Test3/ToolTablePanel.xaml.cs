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
using System.IO;
using System.Data;
using System.Windows.Controls.Primitives;

using KMotion_dotNet;

// for the JSON stuff
using Newtonsoft.Json;

// for file dialog libraries
using Microsoft.Win32;
using System.Threading;

//

namespace KFLOP_Test3
{
    /// <summary>
    /// Interaction logic for ToolTablePanel.xaml
    /// </summary>
    public partial class ToolTablePanel : UserControl
    {
        // a copy of the KM controller 
        private KM_Controller KMx { get; set; }
        // used to point to the configuration file list. 
        static ConfigFiles CFx { get; set; }
        // Tool Carousel configuration
        static ToolCarousel CarouselList;
        // tool table 
        static ToolTable TTable;
        // tool changer
        static ToolChanger TCx;
        static ToolSetter TSx;

        int selectedPocket;
        Tool selectedTool;

        // mouse event elements
        public delegate Point GetPosition(IInputElement element);
        int rowIndex = -1; // the row position when a left mouse button event occurs
        int rowIndexRtBtn = -1; // the row position when a right mouse button event occurs
        int rowIndexRtBtn_Carousel = -1;

        Tool DraggedObject; // maybe think of a different name for this?


        public ToolTablePanel(ref KM_Controller X, ref ConfigFiles CfgFiles, ref ToolInfo toolInfo, ref ToolChanger toolChanger, ref ToolSetter toolSetter)
        {
            InitializeComponent();
            KMx = X;    // point to the KM controller - this exposes all the KFLOP .net library functions
            CFx = CfgFiles; // point to the configuration files
            CarouselList = toolChanger.GetCarousel();
            //CarouselList = ToolChanger.CarouselList1;   // point to the Tool Carousel 

            TTable = toolInfo.toolTable;            // point to the Tool Table
            TCx = toolChanger;  // point to the Tool Changer
            TSx = toolSetter;

            selectedPocket = 0;
            selectedTool = new Tool();

            // add a handler to the preview left button down event on the tool table datagrid
            dgToolList.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(ToolTable_PreviewMouseLeftButtonDown);
            dgToolList.Drop += new DragEventHandler(ToolListDrop);

            // add a handler to drop a tool into the Tool Carousel
            dgCarousel.Drop += new DragEventHandler(CarouselDrop);
            // add a handler to get the tool row in for the Context Menu
            dgToolList.PreviewMouseRightButtonDown += new MouseButtonEventHandler(ToolTable_PreviewRightMouseButtonDown);

            dgCarousel.PreviewMouseRightButtonDown += new MouseButtonEventHandler(Carousel_PreviewRightMouseButtonDown);
        }
        

        private void btnToolList_Click(object sender, RoutedEventArgs e)
        {
            ListToolTable();
            ListCarousel();
        }

        #region Mouse Event Handlers

        // preview mouse left button down selects the tool to be loaded into the tool changer
        void ToolTable_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            rowIndex = GetCurrentRowIndex(e.GetPosition, dgToolList);
            if(rowIndex < 0)
            { return; }

 //           MessageBox.Show($"Selected Row {rowIndex}");
            dgToolList.SelectedIndex = rowIndex; // set the selectedIndex to the row that was left buttoned
            selectedTool = dgToolList.Items[rowIndex] as Tool;
            if(selectedTool == null)
            { return; }

            dgToolList.SelectedItem = selectedTool;
            DraggedObject = dgToolList.Items[rowIndex] as Tool;

            DragDropEffects ddEffects = DragDropEffects.Copy;
            if (DragDrop.DoDragDrop(dgToolList, selectedTool, ddEffects) != DragDropEffects.None)
            {
                // it seems like doing this here would select late...
                //                dgToolList.SelectedItem = SelectedTool;
                //                DraggedObject = dgToolList.Items[rowIndex] as Tool;
               //  MessageBox.Show("Success");
            }

           //  MessageBox.Show($"Selected Row {rowIndex}");
        }

        void ToolTable_PreviewRightMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            rowIndexRtBtn = GetCurrentRowIndex(e.GetPosition, dgToolList);
            if (rowIndexRtBtn < 0)
            { return; }

            // anything else we need to do here?
        }

        void ToolListDrop(object sender, DragEventArgs e)
        {
            // do nothing here...
        }

        void CarouselDrop(object sender, DragEventArgs e)
        {
            if (rowIndex < 0)   // check to see if this is a drop from a tool selection, -1 means no
            { return; }

            int index = GetCurrentRowIndex(e.GetPosition, dgCarousel);
            if (index < 0)      // make sure the drop is on the datagrid
            { return; }

            dgCarousel.SelectedIndex = index;   
            CarouselItem SelectedItem = dgCarousel.Items[index] as CarouselItem;
            if(SelectedItem == null)    // make sure the selected carousel item is not empty
            { return; }


            // TCx.CarouselAddTool()
            int newToolNumber;
            if(DraggedObject.slot < 100)
            {
                newToolNumber = DraggedObject.slot;
            } else
            {
                newToolNumber = DraggedObject.ID;
            }
            // Check if the tool is already in the carousel
            if (TCx.ToolInCarousel(newToolNumber))
            {
                return; // tool is already in the carousel don't put it in again
            }
            // MessageBox.Show($"putting tool {newToolNumber} into pocket {SelectedItem.Pocket}");
            // prompt to load the tool into the spindle
            // check auto measure check box
            bool measureTool = cbToolLoadMeasure.IsChecked ?? false;    // https://stackoverflow.com/questions/6075726/convert-nullable-bool-to-bool

            // Assuming that the calling function already checked that the ToolNumber and Pocket are valid
            MessageBoxResult MRB = MessageBox.Show($"Put Tool Number {newToolNumber} into the spindle", "Load Carousel", MessageBoxButton.OKCancel);
            if (MRB == MessageBoxResult.OK)
            {
                if (TCx.CheckPocketEmpty(SelectedItem.Pocket) == false) // check for empty pocket number
                {
                    MRB = MessageBox.Show($"Is Carousel Pocket {SelectedItem.Pocket} Empty?", "*WARNING* Pocket Not Empty", MessageBoxButton.YesNoCancel);
                    if ((MRB == MessageBoxResult.Cancel) || (MRB == MessageBoxResult.No))
                    {
                        MessageBox.Show($"You Must Remove the tool from Carousel Pocket {SelectedItem.Pocket} before loading", "*WARNING* Pocket Not Empty");
                        return;
                    }
                }
                if(measureTool == true)
                {
                    // set the toolsetter callback 
                    selectedPocket = SelectedItem.Pocket;
                    TCx.Manual_GetTool(selectedTool.index);
                    TSx.ProcessCompleted += MeasureStep3;
                    ToolMeasure();
                    TCx.CarouselAddTool(newToolNumber, SelectedItem.Pocket);
                }
                else
                { 
                    TCx.LoadTool(newToolNumber, SelectedItem.Pocket); 
                }
                dgCarousel.Items.Refresh(); // refresh the data grid.
            }
        }

        void Carousel_PreviewRightMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            rowIndexRtBtn_Carousel = GetCurrentRowIndex(e.GetPosition, dgCarousel);
            if (rowIndexRtBtn_Carousel < 0)
            { return; }
        }


        #region Mouse in the target 
        private bool GetMouseTargetRow(Visual theTarget, GetPosition position)
        {
            if(theTarget == null)
            { return false; }
            Rect rect = VisualTreeHelper.GetDescendantBounds(theTarget);
            Point point = position((IInputElement)theTarget);
            return rect.Contains(point);
        }

        private DataGridRow GetRowItem(int index, DataGrid dGrid)
        {
            //if(dgToolList.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated) // from System.Windows.Controls.Primitives.
            //{ return null; }
            //return dgToolList.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow;
            if(dGrid.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated) // from System.Windows.Controls.Primitives.
            { return null; }
            return dGrid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow;
        }

        private int GetCurrentRowIndex(GetPosition pos, DataGrid dGrid)
        {
            int curIndex = -1;
            for(int i = 0;i < dGrid.Items.Count; i++)
            {
                DataGridRow itm = GetRowItem(i, dGrid);
                if (GetMouseTargetRow(itm, pos))
                {
                    curIndex = i;
                    break;
                }
            }
            return curIndex;
        }
        #endregion

        #region Tool Table Drop Down menu actions
        private void ToolTableMenu_Add(object sender, RoutedEventArgs e)
        {
            // note: this will only update the tooltable, not the interpreter table. - have to think
            // on how to implement that. maybe...
           // MessageBox.Show($"Add a tool, current row = {rowIndexRtBtn}, but make a new row.");
            Tool NewTool = new Tool();
            ToolEditWindow tEditWindow = new ToolEditWindow(NewTool);
            tEditWindow.btnUpdate.Content = "New";
            if(tEditWindow.ShowDialog() == true)
            {
                if (CheckForDuplicateSlot(tEditWindow.tSlot) == false)
                {
                    tEditWindow.GetWindow(NewTool);
                    // add the new tool to the Tool List and to the data grid...
                    dgToolList.Items.Add(NewTool); //  -> not to the data grid...
                    TTable.Tools.Add(NewTool);
                    // ListToolTable();
                    dgToolList.Items.Refresh();
                }
                else
                {
                    MessageBox.Show($"Tool Slot Number {tEditWindow.tSlot} is already in the table!");
                }
            }
        }

        private void ToolTableMenu_Edit(object sender, RoutedEventArgs e)
        {
            //  MessageBox.Show($"Edit the tool on row = {rowIndexRtBtn}");

            Tool EditTool = new KFLOP_Test3.Tool();
            EditTool = dgToolList.Items[rowIndexRtBtn] as Tool;
            ToolEditWindow tEditWindow = new ToolEditWindow(EditTool);   // pass the tool to the tool edit box
//            ToolEditWindow tEditWindow = new ToolEditWindow(dgToolList.Items[rowIndexRtBtn] as Tool);   // pass the tool to the tool edit box

            tEditWindow.btnUpdate.Content = "Update";
            if(tEditWindow.ShowDialog() == true)
            {
                // if the tool number has changed.
                if (tEditWindow.tSlot != EditTool.slot)
                {
                    if (CheckForDuplicateSlot(tEditWindow.tSlot) == false)
                    {
                        tEditWindow.GetWindow(dgToolList.Items[rowIndexRtBtn] as Tool);
                        // update tool list
                        dgToolList.Items.Refresh();
                        //EditTool = dgToolList.Items[rowIndexRtBtn] as Tool; // this may be redundant...

                    }
                    else
                    {
                        MessageBox.Show($"Tool Slot Number {tEditWindow.tSlot} is already in the table!\nChanges not saved!");
                        return; // this will jump around the interperter tool update
                    }
                }
                else   // there is probably a more elegant way to do this...
                {
                    tEditWindow.GetWindow(dgToolList.Items[rowIndexRtBtn] as Tool);
                    // update tool list
                    dgToolList.Items.Refresh();
                }
                // save the tool updates to the Interpreter.
                KMx.CoordMotion.Interpreter.SetupParams.SetTool(EditTool.index, EditTool.slot,
                        EditTool.ID, EditTool.Length, EditTool.Diameter, EditTool.XOffset, EditTool.YOffset);
            }
        }

        private void ToolTableMenu_Delete(object sender, RoutedEventArgs e)
        {
            // MessageBox.Show($"Delete the tool on row = {rowIndexRtBtn}");
            // note, this does not remove the tool from the interpreter. save and reload to do that.
            // How to remove the correct row out of the TTable? do I need to search through the list until they agree?
            Tool itemToRemove = dgToolList.Items[rowIndexRtBtn] as Tool;
            for(int i = 0; i < TTable.Tools.Count; i++)
            {
                if(itemToRemove.slot == TTable.Tools[i].slot)
                {
                    // remove the tool slot from the list
                    TTable.Tools.RemoveAt(i);
                    break;
                }
            }
            dgToolList.Items.RemoveAt(rowIndexRtBtn);   // now remove it from the datagrid
            dgToolList.Items.Refresh();
        }

        private void ToolTableMenu_Load(object sender, RoutedEventArgs e)
        {
            // MessageBox.Show($"Load the tool table. Current Row = {rowIndexRtBtn}");
            if (MessageBox.Show("Loading will overwrite unsaved changes", "", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                // reload the tool table from the tool file
                LoadToolTable();
                // relist
                ListToolTable();
                // refresh the datagrid
                // dgToolList.Items.Refresh();
            }
        }

        private void ToolTableMenu_Save(object sender, RoutedEventArgs e)
        {
            SaveToolTable();
        }

        private void ToolTableMenu_Cancel(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"Cancel the current opperation, Current row = {rowIndexRtBtn}");
        }

        private void ToolTableMenu_Measure(object sender, RoutedEventArgs e)
        {
            // get the tool number

            // Tool SelectedTool = new KFLOP_Test3.Tool();
            selectedTool = dgToolList.Items[rowIndexRtBtn] as Tool;
            // check if it is the carousel
            // MessageBox.Show($"Selected Tool ID is {selectedTool.slot}");
            if (TCx.ToolInCarousel(selectedTool.slot))
            {
                selectedPocket = TCx.ToolInCarouselPocket(selectedTool.slot);
                MessageBox.Show($"Tool {selectedTool.slot} is in the Carousel at pocket {selectedPocket}");
                // prompt to make sure the spindle is empty cancel if necessary
                MessageBoxResult MBR = MessageBox.Show("Make Sure the Spindle is Empty!", "Spindle Check", MessageBoxButton.OKCancel);
                if (MBR == MessageBoxResult.Cancel)
                { return; }
                else
                {
                    // get the tool from the carousel
                    if (TCx.bwBusy() == true)
                    {
                        MessageBox.Show("Something is busy - try again...");
                    } else
                    {
                        // add something to the callback chain.
                        TCx.ProcessCompleted += MeasureStep2;
                        TCx.ToolChangerSimple(0, selectedPocket); // this should get "PocketNumber" from the carousel from an empty spindle
                            
                    }
                }
            }
            else
            {
                selectedPocket = -1;
                TCx.Manual_GetTool(selectedTool.index);

                TSx.ProcessCompleted += MeasureStep3;
                ToolMeasure();
                // MessageBoxResult MBR = MessageBox.Show($"Load tool number {SelectedTool.slot} into the Spindle is Empty!", "Tool Load", MessageBoxButton.OKCancel);
                // if (MBR == MessageBoxResult.Cancel)
                //     return;
                // update the tool in use? 
            }
            // at this point there should be a tool in the spindle!     
             
            // tool setter cycle
            // update the table and the current tool 
        }

        void MeasureStep2()
        {
            // remove step 2 from the callback list
            TCx.ProcessCompleted -= MeasureStep2;
            // add the next step to the callback list
            TSx.ProcessCompleted += MeasureStep3;

            // MessageBox.Show("in step 2");
            // tool measure
            ToolMeasure();
            // need to know the tool number and estimated length. ??

        }
        void MeasureStep3()
        {
            // remove step 3 from the callback list
            TSx.ProcessCompleted -= MeasureStep3;
            // MessageBox.Show("Step 3 done");
            // if the measurement did not error then record the number in the tool table
            // check the result
            switch (ToolSetter.TSProbeState)
            {
                case ProbeResult.Detected:
                    // get the measured value and update the tooltable
                    double toolLength = MachineMotion.TSCoord.Z - MachineMotion.xTCP.TS_RefZ;
                    MessageBox.Show($"Tool length = {toolLength}");
                    // update the tool length in the table and the interpreter.
               
                    selectedTool.Length = toolLength;
                    KMx.CoordMotion.Interpreter.SetupParams.SetTool(selectedTool.index,
                                                selectedTool.slot, selectedTool.ID,
                                                selectedTool.Length, selectedTool.Diameter,
                                                selectedTool.XOffset, selectedTool.YOffset);
                    dgToolList.Items.Refresh();
                    break;
                case ProbeResult.MachineTimeOut: 
                case ProbeResult.SoftTimeOut: 
                case ProbeResult.T2_ProbeError:
                default:
                    MessageBox.Show($"Probing Error - Result:{ToolSetter.TSProbeState}");
                    break;
            }

            if (selectedPocket != -1)
            {
                // return the tool to the carousel
                TCx.ProcessCompleted += MeasureStep4;
                TCx.ToolChangerSimple(selectedPocket, 0); // put the tool into the active pocket.
            }
            else
            {
                // MessageBox.Show("Remove the tool from the Spindle!");
                TCx.Manual_PutTool(selectedTool.index);
            }
            ToolSetter.TSProbeState = ProbeResult.Idle; // all done.
        }
        void MeasureStep4()
        {
            // remove step 4 from the callback list
            TCx.ProcessCompleted -= MeasureStep4;   
            // 
        }

        void ToolMeasure()  // maybe pass in length and average 
        {
            // Actual tool measurment
            // get the arguments
            if (ToolSetter.TSProbeState != ProbeResult.Idle)
            {
                MessageBox.Show("Tool Setter is not ready!");
                return;
            }
            ToolSetterArguments TSAx = new ToolSetterArguments();
            // load the arguments from the Tool change parameters
            TSAx.X_Offset = 0;
            TSAx.Y_Offset = 0;
            TSAx.Z_Offset = MachineMotion.xTCP.TS_Z - MachineMotion.xTCP.TS_RefZ;
            TSAx.AverageCount = 1; // for now... maybe do something else here?
            // TSAx.Z_Offset = 2.0; // calculate the proper length here!

            TSAx.UseExpectedZ = false;   // use the full length of the probe routine
            //SetterAction = TS_Actions.ToolMeasurment;
            // ToolSetter_Action(TSAx);
            ToolSetter.SetterAction = TS_Actions.ToolMeasurment;
            TSx.Start_ToolSetter(TSAx);
        }
        #endregion

        #region Carousel Menu Drop Down Menu Actions
        private void CarouselMenu_Unload(object sender, RoutedEventArgs e)
        {
            CarouselItem ItemToUnload = dgCarousel.Items[rowIndexRtBtn_Carousel] as CarouselItem;
            TCx.UnloadTool(ItemToUnload.Pocket);
            dgCarousel.Items.Refresh();
        }

        private void CarouselMenu_Save(object sender, RoutedEventArgs e)
        {
            TCx.SaveCarouselCfg();  // save under the standard file name.
            // save the Carousel config
        }

        private void CarouselMenu_SaveAs(object sender, RoutedEventArgs e)
        {
            TCx.SaveCarouselCfg("");
        }

        private void CarouselMenu_Cancel(object sender, RoutedEventArgs e)
        {

        }

        private void CarouselMenu_Update(object sender, RoutedEventArgs e)
        {
            dgCarousel.Items.Refresh();
        }

        private bool CheckForDuplicateSlot(int Slot)
        {
            foreach (Tool tTool in TTable.Tools)
            {
                if (tTool.slot == Slot)
                { return true; }
            }
            return false;
        }
        #endregion

        #endregion

        #region Load and Save the tool table
        // load the tool table from the tool file 
        public void LoadToolTable()
        {
            // open the tool toolfile
            string TFileName = System.IO.Path.Combine(CFx.ToolFilePath, CFx.ToolFile);

            if (System.IO.File.Exists(TFileName) == true)
            {
                try
                {
                    // open the file
                    System.IO.StreamReader infile = new StreamReader(TFileName);
                    // if there is no tool class then start one
                    if (TTable.Tools == null) { TTable.Tools = new List<Tool>(); }
                    TTable.Tools.Clear();
                    // read a line
                    char[] delimiterChars = { ' ', '\t' };
                    string line;
                    bool FirstLine = true;
                    int indexCount = 0;

                    while ((line = infile.ReadLine()) != null)
                    {
                        // parse the line into the TTable class

                        if (FirstLine)   // look for the header line
                        { FirstLine = false; continue; }

                        string[] words = line.Split(delimiterChars, System.StringSplitOptions.RemoveEmptyEntries);
                        Tool Tx = new Tool();
                        int x;
                        double d;
                        

                        if (words.Length >= 6)  // make sure that the line has sufficent elements
                        {
                            Tx.index = indexCount++;
                            // get the parameters - skip the line if any of the entries don't work.
                            if (int.TryParse(words[0], out x))
                            {
                                Tx.slot = x;
                            }
                            else { continue; }

                            if (int.TryParse(words[1], out x))
                            {
                                Tx.ID = x;
                            }
                            else { continue; }

                            if (double.TryParse(words[2], out d))
                            {
                                Tx.Length = d;
                            }
                            else { continue; }

                            if (double.TryParse(words[3], out d))
                            {
                                Tx.Diameter = d;
                            }
                            else { continue; }

                            if (double.TryParse(words[4], out d))
                            {
                                Tx.XOffset = d;
                            }
                            else { continue; }

                            if (double.TryParse(words[5], out d))
                            {
                                Tx.YOffset = d;
                            }
                            else { continue; }

                            // find the comment and image file name - if they exist
                            int startpos = 0;
                            List<int> occurances = new List<int>();
                            int foundPos = -1;
                            do
                            {
                                foundPos = line.IndexOf('\"', startpos);
                                if (foundPos > -1)
                                {
                                    occurances.Add(foundPos);
                                    startpos = foundPos + 1;
                                }
                            } while (foundPos > -1);
                            if (occurances.Count > 0)
                            {
                                Tx.Comment = line.Substring(occurances[0] + 1, (occurances[1] - occurances[0] - 1));
                                Tx.Image = line.Substring(occurances[2] + 1, (occurances[3] - occurances[2] - 1));
                            } else
                            {
                                Tx.Comment = "";
                                Tx.Image = "";
                            }
                            TTable.Tools.Add(Tx);

                        }
                        else
                        { continue; }   // skip the line if there are not enough arguments
                    }
                    // close the file
                    infile.Close();
                    //    MessageBox.Show("tool file read!");
                    // verify the tooltable vs interpreter table

                    // re-initialize the interpreter - should cause it to re-read the table.
                    KMx.CoordMotion.Interpreter.InitializeInterpreter(); // will this do any other damage?

                    foreach (Tool xtool in TTable.Tools)
                    {
                        // get the tool from the interpreter
                        int toolslot = TCx.getSlot(xtool.index);
                        if (toolslot != xtool.slot)
                        {
                            MessageBox.Show($"Tool Table Error!\nIndex:{xtool.index} Slot:{xtool.slot}");
                        }
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Exception {e.ToString()}");
                }
            }
            else
            {
                MessageBox.Show(string.Format("Can't fine the tool file {0}", CFx.ToolFile));
            }
        }

        public void SaveToolTable()
        {

            // open the toolfile
            string TFileName = System.IO.Path.Combine(CFx.ToolFilePath, CFx.ToolFile);

            if (System.IO.File.Exists(TFileName) == true)
            {
                System.IO.StreamWriter outFile = new StreamWriter(TFileName, false); // open file and overwrite
                // write the header line
                string Line1 = "SLOT    ID        LENGTH         DIAMETER        XOFFSET        YOFFSET   COMMENT     IMAGE";
                outFile.WriteLine(Line1);
                // write a blank line
                outFile.WriteLine("");
                // write all the tools
                foreach (Tool Tx in TTable.Tools)
                {
                    string toolLine = string.Format("  {0}    {1}    {2:F6}    {3:F6}    {4:F6}    {5:F6}   \"{6}\"    \"{7}\"", Tx.slot, Tx.ID, Tx.Length, Tx.Diameter, Tx.XOffset, Tx.YOffset, Tx.Comment, Tx.Image);
                    outFile.WriteLine(toolLine);
                }
                // close the file
                outFile.Close();
            }
            else
            {
                MessageBox.Show($"Unable to save tool file {CFx.ToolFile}");
            }
        }

        public void SaveToolTable_mm()
        {
            // open the toolfile
            string TFileName = System.IO.Path.Combine(CFx.ToolFilePath, CFx.ToolTable_mm);

            if (System.IO.File.Exists(TFileName) == true)
            {
                System.IO.StreamWriter outFile = new StreamWriter(TFileName, false); // open file and overwrite
                // write the header line
                string Line1 = "SLOT    ID        LENGTH         DIAMETER        XOFFSET        YOFFSET   COMMENT     IMAGE";
                outFile.WriteLine(Line1);
                // write a blank line
                outFile.WriteLine("");
                // write all the tools
                foreach (Tool Tx in TTable.Tools)
                {
                    double mmDia = Tx.Diameter * 25.4;
                    double mmLen = Tx.Length * 25.4;
                    double mmXOff = Tx.XOffset * 25.4;
                    double mmYOff = Tx.YOffset * 25.4;
                    string toolLine = string.Format("  {0}    {1}    {2:F4}    {3:F4}    {4:F4}    {5:F4}   {6}    {7}", Tx.slot, Tx.ID, mmLen, mmDia, mmXOff, mmYOff, Tx.Comment, Tx.Image);
                    outFile.WriteLine(toolLine);
                }
                // close the file
                outFile.Close();
            }
            else
            {
                MessageBox.Show($"Unable to save tool file {CFx.ToolFile}");
            }
        }

        public void ListToolTable()
        {
            // list out all the tools 
            // MessageBox.Show($"tool list size = {TTable.Tools.Count}");
            dgToolList.Items.Clear();
            foreach (Tool xTool in TTable.Tools) // how am I going to remember this? Had to be TTable.Tools not TTable
            {
                dgToolList.Items.Add(xTool);
            }
        }

        public void ListCarousel()
        {
            dgCarousel.Items.Clear();

            foreach (CarouselItem xItem in CarouselList.Items)
            {
                dgCarousel.Items.Add(xItem);
            }
        }

        #endregion
        #region Carousel Actions
        private void UnloadCarousel_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Make Sure the Spindle is empty! Remove any tool NOW!", "URGENT", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {

                // unload the carousel
                foreach (CarouselItem cItem in CarouselList.Items)
                {
                    // is the spindle empty?

                    if (cItem.ToolIndex != 0) //  
                    {
                        // check for the Tool Changer busy
                        if (TCx.bwBusy())
                        {
                            int bwWaitCount = 0;
                            do
                            {
                                System.Threading.Thread.Sleep(100);
                                if(bwWaitCount++ > 80)  // wait up to 8 seconds.
                                {
                                    MessageBox.Show("Something is busy!\nAborting!");
                                    return;
                                }
                            } while (TCx.bwBusy());
                        }
                        TCx.UnloadTool(cItem.Pocket);
                        dgCarousel.Items.Refresh();
                    }
                }
            }
        }


        // load the carousel file
        public void LoadCarouselCfg()
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
                    CarouselList = Jser.Deserialize<ToolCarousel>(Jreader);
                    sr.Close();
                }
                catch (JsonSerializationException ex)
                {
                    MessageBox.Show(String.Format("{0} Exception! Carousel Not Loaded!", ex.Message));
                    return;
                }
            }
            else
            {
                MessageBox.Show("File not found - Reinialize the Carousel");
                // ResetCarousel();
                // saveCarouselCfg(FileName);
                return;
            }
        }

        // save the carousel file
        public void saveCarouselCfg(string CFileName)
        {

            var SaveFile = new SaveFileDialog();
            SaveFile.FileName = CFileName;
            if (SaveFile.ShowDialog() == true)
            {
                try
                {
                    JsonSerializer Jser = new JsonSerializer();
                    StreamWriter sw = new StreamWriter(SaveFile.FileName);
                    JsonTextWriter Jwrite = new JsonTextWriter(sw);
                    Jser.NullValueHandling = NullValueHandling.Ignore;
                    Jser.Formatting = Newtonsoft.Json.Formatting.Indented;
                    Jser.Serialize(Jwrite, CarouselList);
                    sw.Close();
                }
                catch (JsonSerializationException ex)
                {
                    MessageBox.Show(String.Format("{0} Exception", ex.Message));
                }
            }

        }
        // clear the carousel list and reset every thing
        private void ResetCarousel()
        {
            // check for null carousel list
            if (CarouselList.Items == null) { CarouselList.Items = new List<CarouselItem>(); }

            CarouselList.Items.Clear(); // clear the list

            // fill the list with nothing
            for (int i = 0; i < ToolChanger.xTCP.CarouselSize; i++)
            {
                CarouselItem CI = new CarouselItem();
                CI.Pocket = i + 1;
                CI.ToolIndex = 0;
                CI.ToolInUse = false;
                CI.Description = "Empty";
                CarouselList.Items.Add(CI);
                // CarouselList.Count = i + 1;
            }
        }

        #endregion

        private void bntCUpdate_Click(object sender, RoutedEventArgs e)
        {
            dgCarousel.Items.Refresh();
        }


    }
}
