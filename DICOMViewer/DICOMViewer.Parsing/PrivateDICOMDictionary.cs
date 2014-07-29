using System.Collections.Generic;

namespace DICOMViewer.Parsing
{
    // Dictionary for private DICOM attributes.
    // Please note: Group Number must be odd for all private DICOM attributes!

    public class PrivateDICOMDictionary
    {
        static private readonly List<PrivateDICOMTagInfo> mPrivateDICOMDictionary = new List<PrivateDICOMTagInfo>
        {
            // In case you want to support private DICOM attribute names, the information has to be provided here.
            // Private attribute information has to be provided in form:
            // "Private Creator Code", "Group Number", "Element ID", "Attribute Name", "VR (Value Representation)"

            // Sample private DICOM attribute:
            { new PrivateDICOMTagInfo("YOUR PRIVATE CREATOR CODE", "300B", "ED", "My private DICOM attribute", "OB") },
        };

        static public List<PrivateDICOMTagInfo> GetPrivateDICOMDictionary()
        {
            return mPrivateDICOMDictionary;
        }
    }

    public class PrivateDICOMTagInfo
    {
        public PrivateDICOMTagInfo(string thePrivateCreatorCode, string theGroupNumber, string theElementID, string theName, string theVR)
        {
            PrivateCreatorCode = thePrivateCreatorCode;
            GroupNumber = theGroupNumber;
            ElementID = theElementID;
            Name = theName;
            VR = theVR;
        }

        public string PrivateCreatorCode { get; set; }
        public string GroupNumber { get; set; }
        public string ElementID { get; set; }
        public string Name { get; set; }
        public string VR { get; set; }
    }
}
