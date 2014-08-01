using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Xml.Linq;

using DICOMViewer.Parsing;

namespace DICOMViewer.Helper
{
    // The CTSliceInfo class is a wrapper class in order to provide all important CT Image information.

    public class CTSliceInfo
    {
        private const string HOUNSFIELD_EXT = ".hsu";

        private XDocument mXDocument = null;
        private string    mFileName  = null;

        private int mColumnCount = -1;
        private int mRowCount    = -1;
        private int mSliceLoc    = -1;

        private double mUpperLeft_X = -1;
        private double mUpperLeft_Y = -1;
        private double mUpperLeft_Z = -1;

        private double mPixelSpacing_X = -1;
        private double mPixelSpacing_Y = -1;
        
        private short[,] mHounsfieldPixelBuffer = null;
        private WriteableBitmap mBitmap = null;

        // Public properties
        public string FileName { get { return mFileName;  } }
        public int ColumnCount { get { return mColumnCount; } }
        public int RowCount { get { return mRowCount; } }
        public int SliceLoc { get { return mSliceLoc; } }
        public double UpperLeft_X { get { return mUpperLeft_X; } }
        public double UpperLeft_Y { get { return mUpperLeft_Y; } }
        public double UpperLeft_Z { get { return mUpperLeft_Z; } }
        public double PixelSpacing_X { get { return mPixelSpacing_X; } }
        public double PixelSpacing_Y { get { return mPixelSpacing_Y; } }
        public short[,] HounsfieldPixelBuffer { get { return mHounsfieldPixelBuffer; } }

        public CTSliceInfo(XDocument theXDocument, string theFileName)
        {
            mXDocument = theXDocument;
            mFileName = theFileName;

            mColumnCount = DICOMParserUtility.GetDICOMAttributeAsInt(theXDocument, DICOMTAG.COLUMNS);
            mRowCount = DICOMParserUtility.GetDICOMAttributeAsInt(theXDocument, DICOMTAG.ROWS);

            mSliceLoc = DICOMParserUtility.GetDICOMAttributeAsInt(theXDocument, DICOMTAG.SLICE_LOCATION);

            string aImagePositionPatientString = DICOMParserUtility.GetDICOMAttributeAsString(theXDocument, DICOMTAG.IMAGE_POSITION_PATIENT);
            string[] aImagePositionPatientArray = aImagePositionPatientString.Split('\\');
            mUpperLeft_X = Convert.ToDouble(aImagePositionPatientArray[0], CultureInfo.InvariantCulture);
            mUpperLeft_Y = Convert.ToDouble(aImagePositionPatientArray[1], CultureInfo.InvariantCulture);
            mUpperLeft_Z = Convert.ToDouble(aImagePositionPatientArray[2], CultureInfo.InvariantCulture);

            string aPixelSpacingString = DICOMParserUtility.GetDICOMAttributeAsString(theXDocument, DICOMTAG.PIXEL_SPACING);
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

        private short[,] BuildHounsfieldPixelBuffer()
        {
            // Allocate the Hounsfield Pixel Buffer
            short[,] HounsfieldPixelBuffer = new short[mRowCount, mColumnCount];

            if (File.Exists(mFileName + HOUNSFIELD_EXT))
            {
                BinaryReader br = new BinaryReader(File.Open(mFileName + HOUNSFIELD_EXT, FileMode.Open, FileAccess.Read, FileShare.Read));

                for (int aRowIndex = 0; aRowIndex != mRowCount; ++aRowIndex)
                {
                    for (int aColumnIndex = 0; aColumnIndex != mColumnCount; ++aColumnIndex)
                    {
                        HounsfieldPixelBuffer[aRowIndex, aColumnIndex] = br.ReadInt16();
                    }
                }
                br.Dispose();

                return HounsfieldPixelBuffer;
            }

            int aRescaleIntercept = DICOMParserUtility.GetDICOMAttributeAsInt(mXDocument, DICOMTAG.RESCALE_INTERCEPT);
            int aRescaleSlope = DICOMParserUtility.GetDICOMAttributeAsInt(mXDocument, DICOMTAG.RESCALE_SLOPE);

            bool aPixelPaddingValueExist = DICOMParserUtility.DoesDICOMAttributeExist(mXDocument, "(0028,0120)");
            int aPixelPaddingValue = DICOMParserUtility.GetDICOMAttributeAsInt(mXDocument, "(0028,0120)");

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
            for (int aRowIndex = 0; aRowIndex != mRowCount; ++aRowIndex)
            {
                for (int aColumnIndex = 0; aColumnIndex != mColumnCount; ++aColumnIndex)
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
                        HounsfieldPixelBuffer[aRowIndex, aColumnIndex] = aPixelValue;
                    }
                }
            }

            // cache HounsfieldPixelBuffer
            {
                if (File.Exists(mFileName + HOUNSFIELD_EXT))
                    File.Delete(mFileName + HOUNSFIELD_EXT);

                FileStream f = File.Open(mFileName + HOUNSFIELD_EXT, FileMode.OpenOrCreate, FileAccess.Write);
                BinaryWriter bw = new BinaryWriter(f);

                for (int aRowIndex = 0; aRowIndex != mRowCount; ++aRowIndex)
                {
                    for (int aColumnIndex = 0; aColumnIndex != mColumnCount; ++aColumnIndex)
                    {
                        bw.Write(HounsfieldPixelBuffer[aRowIndex, aColumnIndex]);
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
            if (mHounsfieldPixelBuffer == null)
            {
                mHounsfieldPixelBuffer = BuildHounsfieldPixelBuffer();
            }

            return mHounsfieldPixelBuffer[theRowIndex, theColumnIndex];
        }

        private byte[,] BuildNormalizedPixelBuffer(int aWindowCenter, int aWindowWidth, int aWindowLeftBorder)
        {
            byte[,] aNormalizedPixelBuffer = new byte[mRowCount, mColumnCount];

            // Normalize the Pixel value to [0,255]
            for (int aRowIndex = 0; aRowIndex < mRowCount; aRowIndex++)
            {
                for (int aColumnIndex = 0; aColumnIndex < mColumnCount; aColumnIndex++)
                {
                    short aPixelValue = GetHounsfieldPixelValue(aRowIndex, aColumnIndex);
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
            return aNormalizedPixelBuffer;
        }

        private WriteableBitmap BuildColorBitmap(byte[,] aNormalizedPixelBuffer)
        {
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
            WriteableBitmap aBitmap = new WriteableBitmap(mColumnCount, mRowCount, 96, 96, PixelFormats.Pbgra32, null);

            // Write the Pixels
            Int32Rect anImageRectangle = new Int32Rect(0, 0, mColumnCount, mRowCount);
            int aImageStride = mColumnCount * aBitmap.Format.BitsPerPixel / 8;
            aBitmap.WritePixels(anImageRectangle, aImageDataArray, aImageStride, 0);

            return aBitmap;
        }

        private WriteableBitmap BuildGrey8Bitmap(byte[,] aNormalizedPixelBuffer)
        {
            byte[] aImageDataArray = new byte[mRowCount * mColumnCount * 1];

            int i = 0;
            for (int aRowIndex = 0; aRowIndex < mRowCount; aRowIndex++)
            {
                for (int aColumnIndex = 0; aColumnIndex < mColumnCount; aColumnIndex++)
                {
                    byte aGrayValue = aNormalizedPixelBuffer[aRowIndex, aColumnIndex];

                    aImageDataArray[i] = aGrayValue;

                    ++i;
                }
            }

            // Allocate the Bitmap
            WriteableBitmap aBitmap = new WriteableBitmap(mColumnCount, mRowCount, 96, 96, PixelFormats.Gray8, null);

            // Write the Pixels
            Int32Rect anImageRectangle = new Int32Rect(0, 0, mColumnCount, mRowCount);
            int aImageStride = mColumnCount * aBitmap.Format.BitsPerPixel / 8;
            aBitmap.WritePixels(anImageRectangle, aImageDataArray, aImageStride, 0);

            return aBitmap;
        }

        // build bitmap directly from Hounsfield map, shifted by 1024
        private WriteableBitmap BuildGrey16Bitmap(byte[,] aNormalizedPixelBuffer)
        {
            byte[] aImageDataArray = new byte[mRowCount * mColumnCount * 2];

            int i = 0;
            for (int aRowIndex = 0; aRowIndex != mRowCount; ++aRowIndex)
            {
                for (int aColumnIndex = 0; aColumnIndex != mColumnCount; ++aColumnIndex)
                {
                    ushort aGrayValue = (ushort)(mHounsfieldPixelBuffer[aRowIndex, aColumnIndex] + 1024);

                    aImageDataArray[i * 2 + 0] = (byte)((aGrayValue >> 8) & 0x00FF); 
                    aImageDataArray[i * 2 + 1] = (byte)(aGrayValue & 0x00FF);

                    ++i;
                }
            }

            // Allocate the Bitmap
            WriteableBitmap aBitmap = new WriteableBitmap(mColumnCount, mRowCount, 96, 96, PixelFormats.Gray16, null);

            // Write the Pixels
            Int32Rect anImageRectangle = new Int32Rect(0, 0, mColumnCount, mRowCount);
            int aImageStride = mColumnCount * aBitmap.Format.BitsPerPixel / 8;
            aBitmap.WritePixels(anImageRectangle, aImageDataArray, aImageStride, 0);

            return aBitmap;
        }

        // Helper method, which returns the pixel data of a CT slice as gray-scale bitmap.
        public WriteableBitmap GetPixelBufferAsBitmap()
        {
            if (mBitmap == null)
            {
                int aWindowCenter = DICOMParserUtility.GetDICOMAttributeAsInt(mXDocument, DICOMTAG.WINDOW_CENTER);
                int aWindowWidth = DICOMParserUtility.GetDICOMAttributeAsInt(mXDocument, DICOMTAG.WINDOW_WIDTH);
                int aWindowLeftBorder = aWindowCenter - (aWindowWidth / 2);

                byte[,] aNormalizedPixelBuffer = BuildNormalizedPixelBuffer(aWindowCenter, aWindowWidth, aWindowLeftBorder);

                // Build the Bitmap
                mBitmap = BuildGrey16Bitmap(aNormalizedPixelBuffer);
            }

            return mBitmap;
        }
    }
}
