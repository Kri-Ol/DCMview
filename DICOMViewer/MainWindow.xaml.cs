using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml.Linq;

using DICOMViewer.Helper;
using DICOMViewer.ImageFlow;
using DICOMViewer.Parsing;
using DICOMViewer.Volume;

namespace DICOMViewer
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IODRepository         _IODRepo = null;
        private CTSliceInfoCollection _scol    = null;

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
                this.mIODTree.Items.Clear();

                string aSelectedFilePath = dialog.SelectedPath;
                string[] aFileNameList = Directory.GetFiles(aSelectedFilePath, "*.dcm", SearchOption.AllDirectories);

                // For each physical DICOM file, an own IOD object is created.
                // After parsing the DICOM file, the newly created IOD is added to the IOD Repository.
                foreach (string aFileName in aFileNameList)
                    mIODRepository.Add(new IOD(aFileName));

                // All DICOM files are now parsed. 
                // The IOD Repository is queried in order to build up the IOD model.
                // The grouping of the IOD's is as follows: Patient-SOPClass-Study-Series.
                foreach (string aPatientName in mIODRepository.GetPatients())
                {
                    TreeViewItem aPatientItem = new TreeViewItem() { Header = aPatientName };
                    this.mIODTree.Items.Add(aPatientItem);

                    foreach (string aSOPClass in mIODRepository.GetSOPClassNames(aPatientName))
                    {
                        TreeViewItem aSOPClassItem = new TreeViewItem() { Header = aSOPClass };
                        aPatientItem.Items.Add(aSOPClassItem);

                        foreach (string aStudy in mIODRepository.GetStudies(aPatientName, aSOPClass))
                        {
                            TreeViewItem aStudyItem = new TreeViewItem() { Header = string.Format(@"Study: '{0}'", aStudy) };
                            aSOPClassItem.Items.Add(aStudyItem);

                            foreach (string aSeries in mIODRepository.GetSeries(aPatientName, aSOPClass, aStudy))
                            {
                                TreeViewItem aSeriesItem = new TreeViewItem() { Header = string.Format(@"Series: '{0}'", aSeries) };
                                aStudyItem.Items.Add(aSeriesItem);

                                foreach (IOD aIOD in mIODRepository.GetIODs(aPatientName, aSOPClass, aStudy, aSeries))
                                {
                                    TreeViewItem anIOD = new TreeViewItem() { Header = string.Format(@"{0}", aIOD.SOPInstanceUID) };
                                    anIOD.Tag = aIOD;
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

        private void MenuItem_ExitClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuItem_AboutClick(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("DICOM Viewer\nVisualization of DICOM CT Images using WPF\n\n\u00a9 2012, bernhard.kuegle@web.de");
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
            TreeViewItem aSelectedNode = this.mIODTree.SelectedItem as TreeViewItem;
            if (aSelectedNode == null)
                return;

            // Clear old content
            this.mDICOMTagTree.Items.Clear();
            mGrid.RowDefinitions.First().Height = new GridLength(0);
            mGrid.RowDefinitions.Last().Height = new GridLength(0);

            IOD anIOD = aSelectedNode.Tag as IOD;
            if (anIOD == null)
                return;

            // Set the FileName as root node
            string[] split = anIOD.FileName.Split(new Char[] { '\\' });
            string aFileName = split[split.Length-1];

            TreeViewItem aRootNode = new TreeViewItem() { Header = string.Format("File: {0}", aFileName) };
            this.mDICOMTagTree.Items.Add(aRootNode);

            // Expand the root node
            aRootNode.IsExpanded = true;

            // Add all DICOM attributes to the tree
            foreach (XElement xe in anIOD.XDocument.Descendants("DataSet").First().Elements("DataElement"))
                AddDICOMAttributeToTree(aRootNode, xe);

            // In case the IOD does have a processable pixel data, the ImageFlow button, the volume buttons and the bitmap is shown.
            // Otherwise, only the DICOM attributes are shown and the first and last grid row is hided.
            if (anIOD.IsPixelDataProcessable())
            {
                mGrid.RowDefinitions.First().Height = new GridLength(30);
                mGrid.RowDefinitions.Last().Height = new GridLength(400);
                CTSliceInfo aCTSliceInfo = _scol.Retrieve(anIOD.FileName);
                if (aCTSliceInfo == null)
                {
                    aCTSliceInfo = new Helper.CTSliceInfo(anIOD.XDocument, anIOD.FileName);
                    _scol.Add(aCTSliceInfo);
                }
                mImage.Source = aCTSliceInfo.GetPixelBufferAsBitmap();
            }
            else
            {
                mGrid.RowDefinitions.First().Height = new GridLength(0);
                mGrid.RowDefinitions.Last().Height = new GridLength(0);
            }
        }

        // Helper method to create and show the Image Flow Dialog
        private void ButtonImageFlow_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem SelectedNode = this.mIODTree.SelectedItem as TreeViewItem;
            if (SelectedNode == null)
                return;

            TreeViewItem ParentNode = SelectedNode.Parent as TreeViewItem;

            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            ImageFlowView aImageFlowWindow = new ImageFlowView();

            // Add each CT Slice of the series to the Image Flow Window.
            // Remember: the CT Slices have already been added to the IOD Tree in sorted order (Z-Value ascending).
            foreach (TreeViewItem aChildNode in ParentNode.Items)
            {
                IOD anIOD = aChildNode.Tag as IOD;
                if (anIOD == null)
                    break;

                // Each CT Slice is added to the Image Flow Window as an own slice.
                // XDocument and FileName are needed to access the pixel data in order to build up the image later.
                // For a CT Slice, the SortOrder corresponds to its Z-Value. This parameter is only passed for display purposes.
                if(anIOD.IsPixelDataProcessable())
                    aImageFlowWindow.AddImageSlice(anIOD.XDocument, anIOD.FileName, anIOD.SortOrder.ToString(CultureInfo.InvariantCulture));
            }

            aImageFlowWindow.PostInitialize();
            aImageFlowWindow.Title = "DICOM Viewer - Image Flow";

            Mouse.OverrideCursor = null;
            aImageFlowWindow.ShowDialog();
        }

        private void ButtonVolumeBones_Click(object sender, RoutedEventArgs e)
        {
            // Create Volume for Bones, IsoValue = +500 Hounsfield Units
            CreateVolumeView(+500);
        }

        private void ButtonVolumeSkin_Click(object sender, RoutedEventArgs e)
        {
            // Create Volume for Skin, IsoValue = -800 Hounsfield Units
            CreateVolumeView(-800);
        }

        private void ButtonRBF_Click(object sender, RoutedEventArgs e)
        {
            // Create Volume for Structure

            byte[,,] mask = new byte[512, 512, 167];

            for(int ix = 0; ix < 512; ++ix)
            {
                for(int iy = 0; iy < 512; ++iy)
                {
                    for (int iz = 0; iz < 167; ++iz)
                    {
                        mask[ix, iy, iz] = 0;

                        if (iz > 113 && iz < 143)
                        {
                            if ((ix-256)* (ix - 256) + (iy - 256) * (iy - 256) < 12*12)
                            {
                                mask[ix, iy, iz] = 1;
                            }
                        }
                    }
                }
            }

            CreateMaskedVolumeView(+600, mask);
        }

        // Helper method to create and show the Volume View Dialog
        private void CreateVolumeView(int theIsoValueInHounsfield)
        {
            TreeViewItem SelectedNode = this.mIODTree.SelectedItem as TreeViewItem;
            if (SelectedNode == null)
                return;

            TreeViewItem ParentNode = SelectedNode.Parent as TreeViewItem;

            VolumeView aVolumeViewWindow = new VolumeView();

            List<IOD> aIODList = new List<IOD>();

            // Add each CT Slice of the series to the IOD List.
            // Remember: the CT Slices have already been added to the IOD Tree in sorted order (Z-Value ascending).
            foreach (TreeViewItem ChildNode in ParentNode.Items)
            {
                IOD anIOD = ChildNode.Tag as IOD;
                if (anIOD == null)
                    break;

                if(anIOD.IsPixelDataProcessable())
                    aIODList.Add(anIOD);
            }

            if (aIODList.Count > 2)
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // Create the Volume for the specified IOD list / IsoValue.
                aVolumeViewWindow.CreateVolume(aIODList, theIsoValueInHounsfield);
                aVolumeViewWindow.Title = string.Format("DICOM Viewer - Volume View (IsoValue = {0} in Hounsfield Units)", theIsoValueInHounsfield.ToString());
                
                Mouse.OverrideCursor = null;
                
                aVolumeViewWindow.ShowDialog();
            }
            else
                System.Windows.MessageBox.Show("The series does not have suffcient CT Slices in order to generate a Volume View!");
        }

        private void CreateMaskedVolumeView(int theIsoValueInHounsfield, byte[,,] mask)
        {
            TreeViewItem SelectedNode = this.mIODTree.SelectedItem as TreeViewItem;
            if (SelectedNode == null)
                return;

            TreeViewItem ParentNode = SelectedNode.Parent as TreeViewItem;

            VolumeView aVolumeViewWindow = new VolumeView();

            List<IOD> aIODList = new List<IOD>();

            // Add each CT Slice of the series to the IOD List.
            // Remember: the CT Slices have already been added to the IOD Tree in sorted order (Z-Value ascending).
            foreach (TreeViewItem ChildNode in ParentNode.Items)
            {
                IOD anIOD = ChildNode.Tag as IOD;
                if (anIOD == null)
                    break;

                if (anIOD.IsPixelDataProcessable())
                    aIODList.Add(anIOD);
            }

            if (aIODList.Count > 2)
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // Create the Volume for the specified IOD list / IsoValue.
                aVolumeViewWindow.CreateVolume(aIODList, theIsoValueInHounsfield);
                aVolumeViewWindow.Title = string.Format("DICOM Viewer - Volume View (IsoValue = {0} in Hounsfield Units)", theIsoValueInHounsfield.ToString());

                Mouse.OverrideCursor = null;

                aVolumeViewWindow.ShowDialog();
            }
            else
                System.Windows.MessageBox.Show("The series does not have suffcient CT Slices in order to generate a Volume View!");
        }
    }
}
