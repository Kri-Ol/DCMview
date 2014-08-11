using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml.Linq;

using DICOMViewer.Parsing;

namespace DICOMViewer.Helper
{
    // The CTSliceInfo class is a wrapper class in order to provide all important CT Image information.

    public class CTSliceInfo
    {
        private const string HOUNSFIELD_EXT = ".hsu";

        private XDocument _XDocument = null;
        private string    _FileName  = null;

        private int _ColumnCount = -1;
        private int _RowCount    = -1;
        private int _SliceLoc    = -1;

        private double _UpperLeft_X = -1;
        private double _UpperLeft_Y = -1;
        private double _UpperLeft_Z = -1;

        private double _PixelSpacing_X = -1;
        private double _PixelSpacing_Y = -1;

        private int    _windowCenter = -1;
        private int    _windowWidth  = -1;

        private short[,] _HounsfieldPixelBuffer = null;

        // Public properties
        public string FileName { get { return _FileName;  } }

        public int ColumnCount { get { return _ColumnCount; } }
        public int RowCount { get { return _RowCount; } }

        public int SliceLoc { get { return _SliceLoc; } }

        public double UpperLeft_X { get { return _UpperLeft_X; } }
        public double UpperLeft_Y { get { return _UpperLeft_Y; } }
        public double UpperLeft_Z { get { return _UpperLeft_Z; } }

        public double PixelSpacing_X { get { return _PixelSpacing_X; } }
        public double PixelSpacing_Y { get { return _PixelSpacing_Y; } }

        public int WindowCenter { get { return _windowCenter; } }
        public int WindowWidth  { get { return _windowWidth; } }

        public short[,] HounsfieldPixelBuffer { get { return _HounsfieldPixelBuffer; } }

        public CTSliceInfo(XDocument theXDocument, string theFileName)
        {
            _XDocument = theXDocument;
            _FileName = theFileName;

            _ColumnCount = DICOMParserUtility.GetDICOMAttributeAsInt(theXDocument, DICOMTAG.COLUMNS);
            _RowCount = DICOMParserUtility.GetDICOMAttributeAsInt(theXDocument, DICOMTAG.ROWS);

            _SliceLoc = DICOMParserUtility.GetDICOMAttributeAsInt(theXDocument, DICOMTAG.SLICE_LOCATION);

            string aImagePositionPatientString = DICOMParserUtility.GetDICOMAttributeAsString(theXDocument, DICOMTAG.IMAGE_POSITION_PATIENT);
            string[] aImagePositionPatientArray = aImagePositionPatientString.Split('\\');
            _UpperLeft_X = Convert.ToDouble(aImagePositionPatientArray[0], CultureInfo.InvariantCulture);
            _UpperLeft_Y = Convert.ToDouble(aImagePositionPatientArray[1], CultureInfo.InvariantCulture);
            _UpperLeft_Z = Convert.ToDouble(aImagePositionPatientArray[2], CultureInfo.InvariantCulture);

            string aPixelSpacingString = DICOMParserUtility.GetDICOMAttributeAsString(theXDocument, DICOMTAG.PIXEL_SPACING);
            string[] aPixelSpacingValueArray = aPixelSpacingString.Split('\\');
            _PixelSpacing_X = Convert.ToDouble(aPixelSpacingValueArray[0], CultureInfo.InvariantCulture);
            _PixelSpacing_Y = Convert.ToDouble(aPixelSpacingValueArray[1], CultureInfo.InvariantCulture);

            _windowCenter = DICOMParserUtility.GetDICOMAttributeAsInt(_XDocument, DICOMTAG.WINDOW_CENTER);
            _windowWidth  = DICOMParserUtility.GetDICOMAttributeAsInt(_XDocument, DICOMTAG.WINDOW_WIDTH);
        }

        // This method has to be called before the marching cubes algorithm is called.
        // The method shifts the x/y/z position of the CT slice by the expected center point of the 3D model.
        // By doing so, the resulting 3D model will be centered in the origin of the coordinate system.
        public void AdjustPatientPositionToCenterPoint(Point3D theCenterPoint)
        {
            _UpperLeft_X -= theCenterPoint.X;
            _UpperLeft_Y -= theCenterPoint.Y;
            _UpperLeft_Z -= theCenterPoint.Z;
        }

        private short[,] BuildHounsfieldPixelBuffer()
        {
            // Allocate the Hounsfield Pixel Buffer
            short[,] HounsfieldPixelBuffer = new short[_RowCount, _ColumnCount];

            if (File.Exists(_FileName + HOUNSFIELD_EXT))
            {
                BinaryReader br = new BinaryReader(File.Open(_FileName + HOUNSFIELD_EXT, FileMode.Open, FileAccess.Read, FileShare.Read));

                for (int r = 0; r != _RowCount; ++r)
                {
                    for (int c = 0; c != _ColumnCount; ++c)
                    {
                        HounsfieldPixelBuffer[r, c] = br.ReadInt16();
                    }
                }
                br.Dispose();

                return HounsfieldPixelBuffer;
            }

            int aRescaleIntercept = DICOMParserUtility.GetDICOMAttributeAsInt(_XDocument, DICOMTAG.RESCALE_INTERCEPT);
            int aRescaleSlope = DICOMParserUtility.GetDICOMAttributeAsInt(_XDocument, DICOMTAG.RESCALE_SLOPE);

            bool aPixelPaddingValueExist = DICOMParserUtility.DoesDICOMAttributeExist(_XDocument, "(0028,0120)");
            int aPixelPaddingValue = DICOMParserUtility.GetDICOMAttributeAsInt(_XDocument, "(0028,0120)");

            // Find the pixel data DICOM attribute (7FE0,0010)
            var aPixelDataQuery = from Element in _XDocument.Descendants("DataElement")
                                  where Element.Attribute("Tag").Value.Equals("(7FE0,0010)")
                                  select Element;

            // Get the start position of the stream for the pixel data attribute 
            long aStreamPosition = Convert.ToInt64(aPixelDataQuery.Last().Attribute("StreamPosition").Value);

            // Open the binary reader
            BinaryReader aBinaryReader = new BinaryReader(File.Open(_FileName, FileMode.Open, FileAccess.Read, FileShare.Read));

            // Set the stream position of the binary reader to first pixel
            aBinaryReader.BaseStream.Position = aStreamPosition;

            // Loop over all pixel data values
            for (int r = 0; r != _RowCount; ++r)
            {
                for (int c = 0; c != _ColumnCount; ++c)
                {
                    // For some images, the pixel buffer is smaller than '2Byte * RowCount * ColumnCount'
                    // That's why we need the check...
                    if(aBinaryReader.BaseStream.Position - 2 < aBinaryReader.BaseStream.Length) /// @#@ test is wrong?
                    {
                        byte aByte0 = aBinaryReader.ReadByte();
                        byte aByte1 = aBinaryReader.ReadByte();

                        short aPixelValue = Convert.ToInt16((aByte1 << 8) + aByte0);

                        // Check for Pixel Padding Value  
                        if (aPixelPaddingValueExist)
                            if (aPixelValue == aPixelPaddingValue)
                                aPixelValue = Int16.MinValue;

                        // Rescale handling
                        aPixelValue = (short)((int)aPixelValue* aRescaleSlope + aRescaleIntercept);

                        // Value of the voxel is stored in Hounsfield Units
                        HounsfieldPixelBuffer[r, c] = aPixelValue;
                    }
                }
            }

            // cache HounsfieldPixelBuffer
            {
                if (File.Exists(_FileName + HOUNSFIELD_EXT))
                    File.Delete(_FileName + HOUNSFIELD_EXT);

                FileStream f = File.Open(_FileName + HOUNSFIELD_EXT, FileMode.OpenOrCreate, FileAccess.Write);
                BinaryWriter bw = new BinaryWriter(f);

                for (int r = 0; r != _RowCount; ++r)
                {
                    for (int c = 0; c != _ColumnCount; ++c)
                    {
                        bw.Write(HounsfieldPixelBuffer[r, c]);
                    }
                }
                bw.Flush();

                f.Close();
            }

            return HounsfieldPixelBuffer;
        }

        // Helper method, which will return the Hounsfield value of a specified voxel (RowIndex/ColumnIndex).
        public short GetHounsfieldPixelValue(int theRowIndex, int theColumnIndex)
        {
            // Encoding of the pixel buffer is only done for the first call.
            if (_HounsfieldPixelBuffer == null)
                _HounsfieldPixelBuffer = BuildHounsfieldPixelBuffer();

            return _HounsfieldPixelBuffer[theRowIndex, theColumnIndex];
        }

        public short this[int r, int c]
        {
            get
            {
                return _HounsfieldPixelBuffer[r, c];
            }
        }
    }
}
