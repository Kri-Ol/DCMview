using System;
using System.Globalization;
using System.Xml.Linq;
using DICOMViewer.Helper;

namespace DICOMViewer.Parsing
{
    // One IOD class represents one DICOM Instance (= one physical DICOM file).
    // With the help of the DICOMParser class, the physical DICOM file is converted into a XDocument.
    // Afterwards, the XDocument is queried via LINQ to retrieve all necessary information.
    public class IOD
    {
        public XDocument XDocument { get; set; }
        public string FileName { get; set; }
        public string StudyInstanceUID { get; set; }
        public string SeriesInstanceUID { get; set; }
        public string SOPInstanceUID { get; set; }
        public string SOPClassUID { get; set; }
        public string SOPClassName { get; set; }
        public string PatientName { get; set; }
        public string TransferSyntaxUID { get; set; }
        public double SortOrder { get; set; }

        public IOD(string theFileName)
        {
            DICOMParser aDICOMParser = new DICOMParser(theFileName);
            this.XDocument = aDICOMParser.GetXDocument();
            
            this.FileName = theFileName;

            this.StudyInstanceUID = DICOMParserUtility.GetDICOMAttributeAsString(this.XDocument, "(0020,000D)");
            this.SeriesInstanceUID = DICOMParserUtility.GetDICOMAttributeAsString(this.XDocument, "(0020,000E)");

            this.SOPInstanceUID = DICOMParserUtility.GetDICOMAttributeAsString(this.XDocument, "(0008,0018)");

            this.SOPClassUID = DICOMParserUtility.GetDICOMAttributeAsString(this.XDocument, "(0008,0016)");
            this.SOPClassName = SOPClassDictionary.GetSOPClassName(this.SOPClassUID);

            this.PatientName = DICOMParserUtility.GetDICOMAttributeAsString(this.XDocument, DICOMTAG.PATIENT_NAME);

            this.TransferSyntaxUID = DICOMParserUtility.GetDICOMAttributeAsString(this.XDocument, "(0002,0010)");

            // The SortOrder attribute is used for sorting the CT Slices within one series.
            // For all other IOD's, the SortOrder attribute has no meaning.
            // For sorting the CT Slices, the Z-Value is used.
            // The Z-Value is tried to be determined either from 
            //    - 'Image Position Patient' attribute (0020,0032) or from
            //    - 'Slice Location' attribute (0020,1041)
            if (DICOMParserUtility.DoesDICOMAttributeExist(this.XDocument, DICOMTAG.IMAGE_POSITION_PATIENT))
            {
                // 'Image Position Patient' attribute will be encoded as "x\y\z"
                string aImagePositionPatient = DICOMParserUtility.GetDICOMAttributeAsString(this.XDocument, DICOMTAG.IMAGE_POSITION_PATIENT);
                string[] split = aImagePositionPatient.Split(new Char[] { '\\' });
                this.SortOrder = Convert.ToDouble(split[2], CultureInfo.InvariantCulture);
            }
            else
            {
                if (DICOMParserUtility.DoesDICOMAttributeExist(this.XDocument, DICOMTAG.SLICE_LOCATION))
                {
                    this.SortOrder = DICOMParserUtility.GetDICOMAttributeAsDouble(this.XDocument, DICOMTAG.SLICE_LOCATION);
                }
            }
        }

        public bool IsPixelDataProcessable()
        {
            // The 'DICOM Viewer' can only process the pixel data, if:

            // a) the IOD is a CT Slice (SOPClassName: 'Computed Tomography Image', SOPClassUID: '1.2.840.10008.5.1.4.1.1.2')
            if (!this.SOPClassUID.Equals("1.2.840.10008.5.1.4.1.1.2"))
                return false;

            // b) the Transfer Syntax of the IOD does not indicate a compression of the pixel data. Supported Transfer Syntaxes are:
            //    - Explicit VR Encoding, little endian: '1.2.840.10008.1.2.1'
            //    - Implicit VR Encoding, little endian: '1.2.840.10008.1.2'
            //    - Explicit VR Encoding, big endian:    '1.2.840.10008.1.2.2'
            //
            // Compressed pixel data is not supported by the 'DICOM Viewer'
            //
            if (!(this.TransferSyntaxUID.Equals("1.2.840.10008.1.2.1") || this.TransferSyntaxUID.Equals("1.2.840.10008.1.2") || this.TransferSyntaxUID.Equals("1.2.840.10008.1.2.2")))
                return false;

            // c) the IOD must contain pixel data (DICOM attribute (7FE0,0010) has to be present)
            if (!DICOMParserUtility.DoesDICOMAttributeExist(this.XDocument, "(7FE0,0010)"))
                return false;

            // All above criterias are fullfilled, return true
            return true;
        }
    }
}
