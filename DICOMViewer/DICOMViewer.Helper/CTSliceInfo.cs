using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Xml.Linq;


namespace DICOMViewer.Helper
{
    // The CTSliceInfo class is a wrapper class in order to provide all important CT Image information.

    public class CTSliceInfo
    {
        private XDocument mXDocument = null;
        private string mFileName = null;

        private int mColumnCount = -1;
        private int mRowCount = -1;
        private double mUpperLeft_X = -1;
        private double mUpperLeft_Y = -1;
        private double mUpperLeft_Z = -1;
        private double mPixelSpacing_X = -1;
        private double mPixelSpacing_Y = -1;
        
        private int[,] mHounsfieldPixelBuffer = null;
        private WriteableBitmap mBitmap = null;

        // Public properties
        public int ColumnCount { get { return mColumnCount; } }
        public int RowCount { get { return mRowCount; } }
        public double UpperLeft_X { get { return mUpperLeft_X; } }
        public double UpperLeft_Y { get { return mUpperLeft_Y; } }
        public double UpperLeft_Z { get { return mUpperLeft_Z; } }
        public double PixelSpacing_X { get { return mPixelSpacing_X; } }
        public double PixelSpacing_Y { get { return mPixelSpacing_Y; } }

        public CTSliceInfo(XDocument theXDocument, string theFileName)
        {
            mXDocument = theXDocument;
            mFileName = theFileName;

            mColumnCount = DICOMParserUtility.GetDICOMAttributeAsInt(theXDocument, "(0028,0011)");
            mRowCount = DICOMParserUtility.GetDICOMAttributeAsInt(theXDocument, "(0028,0010)");

            // DICOM attribute 'Image Position Patient' (0020,0032)
            string aImagePositionPatientString = DICOMParserUtility.GetDICOMAttributeAsString(theXDocument, "(0020,0032)");
            string[] aImagePositionPatientArray = aImagePositionPatientString.Split('\\');
            mUpperLeft_X = Convert.ToDouble(aImagePositionPatientArray[0], CultureInfo.InvariantCulture);
            mUpperLeft_Y = Convert.ToDouble(aImagePositionPatientArray[1], CultureInfo.InvariantCulture);
            mUpperLeft_Z = Convert.ToDouble(aImagePositionPatientArray[2], CultureInfo.InvariantCulture);

            // DICOM attribute 'Pixel Spacing' (0028,0030)
            string aPixelSpacingString = DICOMParserUtility.GetDICOMAttributeAsString(theXDocument, "(0028,0030)");
            string[] aPixelSpacingValueArray = aPixelSpacingString.Split('\\');
            mPixelSpacing_X = Convert.ToDouble(aPixelSpacingValueArray[0], CultureInfo.InvariantCulture);
            mPixelSpacing_Y = Convert.ToDouble(aPixelSpacingValueArray[1], CultureInfo.InvariantCulture);
        }

        // This method has to be called before the marching cubes algorithm is called.
        // The method shifts the x/y/z position of the CT slice by the expected center point of the 3D model.
        // By doing so, the resulting 3D model will be centered in the origin of the coordinate system.
        public void AdjustPatientPositionToCenterPoint(Point3D theCenterPoint)
        {
            mUpperLeft_X -= theCenterPoint.X;
            mUpperLeft_Y -= theCenterPoint.Y;
            mUpperLeft_Z -= theCenterPoint.Z;
        }

        // Helper method, which will return the Hounsfield value of a specified voxel (RowIndex/ColumnIndex).
        public int GetHounsfieldPixelValue(int theRowIndex, int theColumnIndex)
        {
            // Encoding of the pixel buffer is only done for the first call.
            if (mHounsfieldPixelBuffer == null)
            {
                int aRescaleIntercept = DICOMParserUtility.GetDICOMAttributeAsInt(mXDocument, "(0028,1052)");
                int aRescaleSlope = DICOMParserUtility.GetDICOMAttributeAsInt(mXDocument, "(0028,1053)");
                bool aPixelPaddingValueExist = DICOMParserUtility.DoesDICOMAttributeExist(mXDocument, "(0028,0120)");
                int aPixelPaddingValue = DICOMParserUtility.GetDICOMAttributeAsInt(mXDocument, "(0028,0120)");

                // Allocate the Hounsfield Pixel Buffer
                mHounsfieldPixelBuffer = new int[mRowCount, mColumnCount];

                // Find the pixel data DICOM attribute (7FE0,0010)
                var aPixelDataQuery = from Element in mXDocument.Descendants("DataElement")
                                      where Element.Attribute("Tag").Value.Equals("(7FE0,0010)")
                                      select Element;

                // Get the start position of the stream for the pixel data attribute 
                long aStreamPosition = Convert.ToInt64(aPixelDataQuery.Last().Attribute("StreamPosition").Value);

                // Open the binary reader
                BinaryReader aBinaryReader = new BinaryReader(File.Open(mFileName, FileMode.Open, FileAccess.Read, FileShare.Read));
                
                // Set the stream position of the binary reader to first pixel
                aBinaryReader.BaseStream.Position = aStreamPosition;

                // Loop over all pixel data values
                for (int aRowIndex = 0; aRowIndex < mRowCount; aRowIndex++)
                {
                    for (int aColumnIndex = 0; aColumnIndex < mColumnCount; aColumnIndex++)
                    {
                        // For some images, the pixel buffer is smaller than '2Byte * RowCount * ColumnCount'
                        // That's why we need the check...
                        if(aBinaryReader.BaseStream.Position - 2 < aBinaryReader.BaseStream.Length)
                        {
                            byte aByte0 = aBinaryReader.ReadByte();
                            byte aByte1 = aBinaryReader.ReadByte();

                            int aPixelValue = Convert.ToInt32((aByte1 << 8) + aByte0);

                            // Check for Pixel Padding Value  
                            if (aPixelPaddingValueExist)
                                if (aPixelValue == aPixelPaddingValue)
                                    aPixelValue = Int16.MinValue;

                            // Rescale handling
                            aPixelValue = aPixelValue * aRescaleSlope + aRescaleIntercept;

                            // Value of the voxel is stored in Hounsfield Units
                            mHounsfieldPixelBuffer[aRowIndex, aColumnIndex] = aPixelValue;
                        }
                    }
                }
            }

            return mHounsfieldPixelBuffer[theRowIndex, theColumnIndex];
        }

        // Helper method, which returns the pixel data of a CT slice as gray-scale bitmap.
        public WriteableBitmap GetPixelBufferAsBitmap()
        {
            if (mBitmap == null)
            {
                int aWindowCenter = DICOMParserUtility.GetDICOMAttributeAsInt(mXDocument, "(0028,1050)");
                int aWindowWidth = DICOMParserUtility.GetDICOMAttributeAsInt(mXDocument, "(0028,1051)");
                int aWindowLeftBorder = aWindowCenter - (aWindowWidth / 2);

                byte[,] aNormalizedPixelBuffer = new byte[mRowCount, mColumnCount];

                // Normalize the Pixel value to [0,255]
                for (int aRowIndex = 0; aRowIndex < mRowCount; aRowIndex++)
                {
                    for (int aColumnIndex = 0; aColumnIndex < mColumnCount; aColumnIndex++)
                    {
                        int aPixelValue = GetHounsfieldPixelValue(aRowIndex, aColumnIndex);
                        int aPixelValueNormalized = (255 * (aPixelValue - aWindowLeftBorder)) / aWindowWidth;

                        if (aPixelValueNormalized <= 0)
                            aNormalizedPixelBuffer[aRowIndex, aColumnIndex] = 0;
                        else
                            if (aPixelValueNormalized >= 255)
                                aNormalizedPixelBuffer[aRowIndex, aColumnIndex] = 255;
                            else
                                aNormalizedPixelBuffer[aRowIndex, aColumnIndex] = Convert.ToByte(aPixelValueNormalized);
                    }
                }

                // Allocate the Pixel Array for the Bitmap (4 Byte: R, G, B, Alpha value)
                byte[] aImageDataArray = new byte[mRowCount * mColumnCount * 4];

                int i = 0;
                for (int aRowIndex = 0; aRowIndex < mRowCount; aRowIndex++)
                {
                    for (int aColumnIndex = 0; aColumnIndex < mColumnCount; aColumnIndex++)
                    {
                        byte aGrayValue = aNormalizedPixelBuffer[aRowIndex, aColumnIndex];

                        // Black/White image: all RGB values are set to same value
                        // Alpha value is set to 255
                        aImageDataArray[i * 4] = aGrayValue;
                        aImageDataArray[i * 4 + 1] = aGrayValue;
                        aImageDataArray[i * 4 + 2] = aGrayValue;
                        aImageDataArray[i * 4 + 3] = 255;

                        i++;
                    }
                }

                // Allocate the Bitmap
                mBitmap = new WriteableBitmap(mColumnCount, mRowCount, 96, 96, PixelFormats.Pbgra32, null);

                // Write the Pixels
                Int32Rect anImageRectangle = new Int32Rect(0, 0, mColumnCount, mRowCount);
                int aImageStride = mColumnCount * mBitmap.Format.BitsPerPixel / 8;
                mBitmap.WritePixels(anImageRectangle, aImageDataArray, aImageStride, 0);
            }

            return mBitmap;
        }
    }
}
