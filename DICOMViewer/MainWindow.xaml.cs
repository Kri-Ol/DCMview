using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using System.Windows.Media.Media3D;

using DICOMViewer.Helper;
using DICOMViewer.ImageFlow;
using DICOMViewer.Parsing;
using DICOMViewer.Volume;
using DICOMViewer.ROIVOI;

using Ceres.RBF;
using Ceres.Utilities;

namespace DICOMViewer
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
#region Data
        private IODRepository         _IODRepo = null;
        private CTSliceInfoCollection _scol    = null;
        private ContourCollection     _ccol    = null;

        private CTSliceInfo           _curCT = null;
#endregion

        public MainWindow()
        {
            InitializeComponent();

            _scol = new CTSliceInfoCollection();
        }

        // build slice list for a given patient
        private void ProcessAllCTs(string aPatientName, IODRepository mIODRepository)
        {
            foreach (string aSOPClass in mIODRepository.GetSOPClassNames(aPatientName))
            {
                foreach (string aStudy in mIODRepository.GetStudies(aPatientName, aSOPClass))
                {
                    foreach (string aSeries in mIODRepository.GetSeries(aPatientName, aSOPClass, aStudy))
                    {
                        foreach (IOD aIOD in mIODRepository.GetIODs(aPatientName, aSOPClass, aStudy, aSeries))
                        {
                            if (aIOD.IsPixelDataProcessable())
                            {
                                CTSliceInfo aCTSliceInfo = new Helper.CTSliceInfo(aIOD.XDocument, aIOD.FileName);
                                _scol.Add(aCTSliceInfo);
                            }
                        }
                    }
                }
            }
        }

        private void MenuItem_LoadClick(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();

            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                IODRepository mIODRepository = new IODRepository();
                this._IODTree.Items.Clear();

                string aSelectedFilePath = dialog.SelectedPath;
                string[] fileNameList = Directory.GetFiles(aSelectedFilePath, "*.dcm", SearchOption.AllDirectories);

                // For each physical DICOM file, an own IOD object is created.
                // After parsing the DICOM file, the newly created IOD is added to the IOD Repository.
                foreach (string fileName in fileNameList)
                    mIODRepository.Add(new IOD(fileName));

                // All DICOM files are now parsed. 
                // The IOD Repository is queried in order to build up the IOD model.
                // The grouping of the IOD's is as follows: Patient-SOPClass-Study-Series.
                foreach (string patientName in mIODRepository.GetPatients())
                {
                    TreeViewItem patientItem = new TreeViewItem() { Header = patientName };
                    this._IODTree.Items.Add(patientItem);

                    foreach (string aSOPClass in mIODRepository.GetSOPClassNames(patientName))
                    {
                        TreeViewItem SOPClassItem = new TreeViewItem() { Header = aSOPClass };
                        patientItem.Items.Add(SOPClassItem);

                        foreach (string aStudy in mIODRepository.GetStudies(patientName, aSOPClass))
                        {
                            TreeViewItem studyItem = new TreeViewItem() { Header = string.Format(@"Study: '{0}'", aStudy) };
                            SOPClassItem.Items.Add(studyItem);

                            foreach (string aSeries in mIODRepository.GetSeries(patientName, aSOPClass, aStudy))
                            {
                                TreeViewItem aSeriesItem = new TreeViewItem() { Header = string.Format(@"Series: '{0}'", aSeries) };
                                studyItem.Items.Add(aSeriesItem);

                                foreach (IOD IOD in mIODRepository.GetIODs(patientName, aSOPClass, aStudy, aSeries))
                                {
                                    TreeViewItem anIOD = new TreeViewItem() { Header = string.Format(@"{0}", IOD.SOPInstanceUID) };
                                    anIOD.Tag = IOD;
                                    aSeriesItem.Items.Add(anIOD);
                                }
                            }
                        }
                    }
                }

                _IODRepo = mIODRepository;

                Mouse.OverrideCursor = null;

                List<string> patients = mIODRepository.GetPatients();

                Mouse.OverrideCursor = System.Windows.Input.Cursors.Hand;

                ProcessAllCTs(patients.ElementAt(0), _IODRepo);

                Mouse.OverrideCursor = null;

                _scol.GenerateAllHounsfields();
            }
        }

        private void MenuItem_LoadContoursClick(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();

            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                _ccol = new ContourCollection();
                _ccol.Fill(dialog.FileName);
            }
        }

        private void MenuItem_ExitClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuItem_AboutClick(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("DICOM WorkBench\n\nwww.codeproject.com/Articles/466955/Medical-image-visualization-using-WPF\nd3dal3.blogspot.com/2008/10/wpf-cover-flow-tutorial-part-1.html\nwww.codeproject.com/Articles/36014/DICOM-Image-Viewer\n...@xcision.com");
        }

        // Helper method to add one DICOM attribute to the DICOM Tag Tree.
        private void AddDICOMAttributeToTree(TreeViewItem theParentNode, XElement theXElement)
        {
            string aTag = theXElement.Attribute("Tag").Value;
            string aTagName = theXElement.Attribute("TagName").Value;
            string aTagData = theXElement.Attribute("Data").Value;

            // Enrich the Transfer Syntax attribute (0002,0010) with human-readable string from dictionary
            if (aTag.Equals("(0002,0010)"))
                aTagData = string.Format("{0} ({1})", aTagData, TransferSyntaxDictionary.GetTransferSyntaxName(aTagData));

            // Enrich the SOP Class UID attribute (0008,0016) with human-readable string from dictionary
            if (aTag.Equals("(0008,0016)"))
                aTagData = string.Format("{0} ({1})", aTagData, SOPClassDictionary.GetSOPClassName(aTagData));

            string s = string.Format("{0} {1}", aTag, aTagName);

            // Do some cut-off in order to allign the TagData
            if (s.Length > 50)
                s = s.Remove(50);
            else
                s = s.PadRight(50);

            s = string.Format("{0} {1}", s, aTagData); 

            TreeViewItem aNewItem = new TreeViewItem() { Header = s };
            theParentNode.Items.Add(aNewItem);

            // In case the DICOM attributes has childrens (= Sequence), call the helper method recursively.
            if (theXElement.HasElements)
                foreach (XElement xe in theXElement.Elements("DataElement"))
                    AddDICOMAttributeToTree(aNewItem, xe); 
        }

        // Helper method to handle the selection change event of the IOD Tree.
        // a) In case the selected tree node represents only group information (Patient, SOPClass, Study, Series), the detailed view is cleared.
        // b) In case the selected tree node represents an IOD, the DICOM Metainformation is displayed in the DICOM Tag Tree.
        // c) In case the selected tree node represents a CT Slice, in addition to the DICOM Metainformation, 
        //    the ImageFlow button, the volume buttons and the bitmap is shown.
        private void mIODTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem aSelectedNode = _IODTree.SelectedItem as TreeViewItem;
            if (aSelectedNode == null)
                return;

            // Clear old content
            _DICOMTagTree.Items.Clear();
            _Grid.RowDefinitions.First().Height = new GridLength(0);
            _Grid.RowDefinitions.Last().Height  = new GridLength(0);

            IOD anIOD = aSelectedNode.Tag as IOD;
            if (anIOD == null)
                return;

            // Set the FileName as root node

            string aFileName = Path.GetFileName(anIOD.FileName);

            TreeViewItem rootNode = new TreeViewItem() { Header = string.Format("File: {0}", aFileName) };
            _DICOMTagTree.Items.Add(rootNode);

            // Expand the root node
            rootNode.IsExpanded = true;

            // Add all DICOM attributes to the tree
            foreach (XElement xe in anIOD.XDocument.Descendants("DataSet").First().Elements("DataElement"))
            {
                AddDICOMAttributeToTree(rootNode, xe);
            }

            // In case the IOD does have a processable pixel data, the ImageFlow button, the volume buttons and the bitmap is shown.
            // Otherwise, only the DICOM attributes are shown and the first and last grid row is hided.
            if (anIOD.IsPixelDataProcessable())
            {
                CTSliceInfo ct = _scol.Retrieve(anIOD.FileName);
                if (ct == null)
                {
                    ct = new Helper.CTSliceInfo(anIOD.XDocument, anIOD.FileName);
                    _scol.Add(ct);
                }
                _Grid.RowDefinitions.First().Height = new GridLength(30);
                _Grid.RowDefinitions.Last().Height  = new GridLength(ct.RowCount+16);

                _Image.Source = CTSliceHelpers.GetPixelBufferAsBitmap(ct);
                _curCT = ct;
            }
            else
            {
                _Grid.RowDefinitions.First().Height = new GridLength(0);
                _Grid.RowDefinitions.Last().Height  = new GridLength(0);

                _curCT = null;
            }
        }

        // Helper method to create and show the Image Flow Dialog
        private void ButtonImageFlow_Click(object sender, RoutedEventArgs e)
        {
            bool rc = _scol.BuildSortedSlicesArray();
            if (!rc)
            {
                System.Windows.MessageBox.Show("There are skips in CTs!");
                return;
            }

            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            ImageFlowView imageFlowWindow = new ImageFlowView();

            // Add each CT Slice of the series to the Image Flow Window.
            // Remember: the CT Slices have already been added to the array in sorted order (Z-Value ascending).
            foreach (var ct in _scol.Slices)
            {
                // Each CT Slice is added to the Image Flow Window as an own slice.
                // For a CT Slice, the SortOrder corresponds to its Z-Value. This parameter is only passed for display purposes.
                imageFlowWindow.AddImageSlice(ct);
            }

            imageFlowWindow.PostInitialize();
            imageFlowWindow.Title = "DICOM Viewer - Image Flow";

            Mouse.OverrideCursor = null;
            imageFlowWindow.ShowDialog();
        }

        private void ButtonVolumeBones_Click(object sender, RoutedEventArgs e)
        {
            bool rc = _scol.BuildSortedSlicesArray();
            if (!rc)
            {
                System.Windows.MessageBox.Show("There are skips in CTs!");
                return;
            }
            // Create Volume for Bones, IsoValue = +500 Hounsfield Units
            CreateVolumeView(+500, _scol.Slices);
        }

        private void ButtonVolumeSkin_Click(object sender, RoutedEventArgs e)
        {
            bool rc = _scol.BuildSortedSlicesArray();
            if (!rc)
            {
                System.Windows.MessageBox.Show("There are skips in CTs!");
                return;
            }
            // Create Volume for Skin, IsoValue = -800 Hounsfield Units
            CreateVolumeView(-800, _scol.Slices);
        }

        private static byte[][,] MakeMask(int nz, int nr, int nc)
        {
            byte[][,] mask = new byte[nz][,];
            for (int iz = 0; iz != nz; ++iz)
            {
                byte[,] plane = new byte[nr, nc];
                mask[iz] = plane;
                for (int r = 0; r != nr; ++r)
                {
                    for(int c = 0; c != nc; ++c)
                    {
                        plane[r,c] = 0;
                    }
                }
            }
            return mask;
        }

        private void ButtonRBF_Click(object sender, RoutedEventArgs e)
        {
            bool rc = _scol.BuildSortedSlicesArray();
            if (!rc)
            {
                System.Windows.MessageBox.Show("There are skips in CTs!");
                return;
            }

            int nz = _scol.Slices.Length;

            int nr = _scol.Slices[0].RowCount;
            int nc = _scol.Slices[0].ColumnCount;

            // Create Volume for Structure
            byte[][,] mask = MakeMask(nz, nr, nc);

            Point3f[] points = _ccol.Flatten();
            Evaluator.InOut[] inout = new Evaluator.InOut[points.Length];
            for(int k = 0; k != inout.Length; ++k)
                inout[k] = (k == inout.Length-1) ? Evaluator.InOut.IN : Evaluator.InOut.BND;

            EvaluatorRBF eval = new EvaluatorRBF(points, inout);
            eval.Evaluate(new Point3f(0.0f, 0.0f, 0.0f));
            float[] weights = eval.Weights;

            Point3D shift = _ccol.Shift;

            for (int iz = (int)shift.Z - 6; iz != (int)shift.Z + 6; ++iz)
            {
                float z = (float)iz + 0.5f;

                byte[,] plane = mask[iz];
                for (int r = 0; r != nr; ++r)
                {
                    float x = (float)r + 0.5f;
                    for(int c = 0; c != nc; ++c)
                    {
                        float y = (float)c + 0.5f;

                        float q = eval.Evaluate(new Point3f(x - (float)shift.X, y - (float)shift.Y, z - (float)shift.Z));

                        if (q > -0.01f)
                            plane[r, c] = 1;
                    }
                }
            }

            CreateMaskedVolumeView(+500, mask, _scol.Slices);
        }

        // Helper method to create and show the Volume View Dialog
        private void CreateVolumeView(short theIsoValueInHounsfield, CTSliceInfo[] slices)
        {
            if (slices.Length > 2)
            {
                VolumeView aVolumeViewWindow = new VolumeView();

                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // Create the Volume for the specified IOD list / IsoValue.
                aVolumeViewWindow.CreateVolume(slices, theIsoValueInHounsfield);
                aVolumeViewWindow.Title = string.Format("DICOM Viewer - Volume View (IsoValue = {0} in Hounsfield Units)", theIsoValueInHounsfield.ToString());
                
                Mouse.OverrideCursor = null;
                
                aVolumeViewWindow.ShowDialog();
            }
            else
                System.Windows.MessageBox.Show("The series does not have suffcient CT Slices in order to generate a Volume View!");
        }

        private void CreateMaskedVolumeView(short theIsoValueInHounsfield, byte[][,] mask, CTSliceInfo[] slices)
        {
            // shall be used only once, because original slice data will be altered
            // require data reloading
            for (int k = 0; k != slices.Length; ++k)
            {
                CTSliceInfo ct = slices[k];
                short[,] buffer = ct.HounsfieldPixelBuffer;
                byte[,] maska   = mask[k];

                for (int r = 0; r != ct.RowCount; ++r)
                {
                    for (int c = 0; c != ct.ColumnCount; ++c)
                    {
                        if (maska[r, c] > 0)
                            buffer[r, c] = theIsoValueInHounsfield;
                    }
                }
            }

            if (slices.Length > 2)
            {
                VolumeView aVolumeViewWindow = new VolumeView();

                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // Create the Volume for the specified IOD list / IsoValue.
                aVolumeViewWindow.CreateVolume(slices, theIsoValueInHounsfield);
                aVolumeViewWindow.Title = string.Format("DICOM Viewer - Volume View (IsoValue = {0} in Hounsfield Units)", theIsoValueInHounsfield.ToString());

                Mouse.OverrideCursor = null;

                aVolumeViewWindow.ShowDialog();
            }
            else
                System.Windows.MessageBox.Show("The series does not have suffcient CT Slices in order to generate a Volume View!");
        }

        private void _imageMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            int column = (int)e.GetPosition(_Image).X;
            int row    = (int)e.GetPosition(_Image).Y;

            /// double h = _Image.Height;
            /// double w = _Image.Width;

            short HUa = _curCT.GetHounsfieldPixelValue(row, column);
            short HUb = _curCT.GetHounsfieldPixelValue(column, row);
            //short HUb = _curCT.GetHounsfieldPixelValue(511 - row, 511 - column);
            //short HUc = _curCT.GetHounsfieldPixelValue(row, 511 - column);
            //short HUd = _curCT.GetHounsfieldPixelValue(511 - row, column);

            _labelHU.Content = String.Format("{0},{1}: {2} {3}", row, column, HUa, HUb);
        }
    }
}
