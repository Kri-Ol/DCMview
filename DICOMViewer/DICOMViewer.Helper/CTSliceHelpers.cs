using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DICOMViewer.Helper
{
    sealed public class CTSliceHelpers
    {
        public static byte[,] BuildNormalizedPixelBuffer(CTSliceInfo ct,
                                                         int windowCenter,
                                                         int windowWidth,
                                                         int windowLeftBorder)
        {
            Debug.Assert(windowWidth > 0);

            byte[,] normalizedPixelBuffer = new byte[ct.RowCount, ct.ColumnCount];

            // Normalize the Pixel value to [0,255]
            for (int r = 0; r != ct.RowCount; ++r)
            {
                for (int c = 0; c != ct.ColumnCount; ++c)
                {
                    short aPixelValue = ct[r, c];
                    int aPixelValueNormalized = (255 * (aPixelValue - windowLeftBorder)) / windowWidth;

                    if (aPixelValueNormalized <= 0)
                        normalizedPixelBuffer[r, c] = 0;
                    else
                        if (aPixelValueNormalized >= 255)
                            normalizedPixelBuffer[r, c] = 255;
                    else
                        normalizedPixelBuffer[r, c] = Convert.ToByte(aPixelValueNormalized);
                }
            }
            return normalizedPixelBuffer;
        }

        public static WriteableBitmap BuildColorBitmap(CTSliceInfo ct,
                                                       byte[,]     normalizedPixelBuffer)
        {
            byte[] imageDataArray = new byte[ct.RowCount * ct.ColumnCount * 4];

            int i = 0;
            for (int r = 0; r != ct.RowCount; ++r)
            {
                for (int c = 0; c != ct.ColumnCount; ++c)
                {
                    byte aGrayValue = normalizedPixelBuffer[r, c];

                    // Black/White image: all RGB values are set to same value
                    // Alpha value is set to 255
                    imageDataArray[i * 4] = aGrayValue;
                    imageDataArray[i * 4 + 1] = aGrayValue;
                    imageDataArray[i * 4 + 2] = aGrayValue;
                    imageDataArray[i * 4 + 3] = 255;

                    ++i;
                }
            }

            // Allocate the Bitmap
            WriteableBitmap bitmap = new WriteableBitmap(ct.ColumnCount, ct.RowCount, 96, 96, PixelFormats.Pbgra32, null);

            // Write the Pixels
            Int32Rect imageRectangle = new Int32Rect(0, 0, ct.ColumnCount, ct.RowCount);
            int imageStride = ct.ColumnCount * bitmap.Format.BitsPerPixel / 8;
            bitmap.WritePixels(imageRectangle, imageDataArray, imageStride, 0);

            return bitmap;
        }

        public static WriteableBitmap BuildGrey8Bitmap(CTSliceInfo ct,
                                                       byte[,]     normalizedPixelBuffer)
        {
            byte[] imageDataArray = new byte[ct.RowCount * ct.ColumnCount * 1];

            int i = 0;
            for (int r = 0; r != ct.RowCount; ++r)
            {
                for (int c = 0; c != ct.ColumnCount; ++c)
                {
                    byte grayValue = normalizedPixelBuffer[r, c];

                    imageDataArray[i] = grayValue;

                    ++i;
                }
            }

            // Allocate the Bitmap
            WriteableBitmap bitmap = new WriteableBitmap(ct.ColumnCount, ct.RowCount, 96, 96, PixelFormats.Gray8, null);

            // Write the Pixels
            Int32Rect imageRectangle = new Int32Rect(0, 0, ct.ColumnCount, ct.RowCount);
            int imageStride = ct.ColumnCount * bitmap.Format.BitsPerPixel / 8;
            bitmap.WritePixels(imageRectangle, imageDataArray, imageStride, 0);

            return bitmap;
        }

        // build bitmap directly from Hounsfield map, shifted by 1024
        public static WriteableBitmap BuildGrey16Bitmap(CTSliceInfo ct,
                                                        byte[,]     normalizedPixelBuffer)
        {
            byte[] imageDataArray = new byte[ct.RowCount * ct.ColumnCount * 2];

            int i = 0;
            for (int r = 0; r != ct.RowCount; ++r)
            {
                for (int c = 0; c != ct.ColumnCount; ++c)
                {
                    ushort aGrayValue = (ushort)(ct[r, c] + 1024);

                    imageDataArray[i * 2 + 0] = (byte)((aGrayValue >> 8) & 0x00FF);
                    imageDataArray[i * 2 + 1] = (byte)(aGrayValue & 0x00FF);

                    ++i;
                }
            }

            // Allocate the Bitmap
            WriteableBitmap bitmap = new WriteableBitmap(ct.ColumnCount, ct.RowCount, 96, 96, PixelFormats.Gray16, null);

            // Write the Pixels
            Int32Rect imageRectangle = new Int32Rect(0, 0, ct.ColumnCount, ct.RowCount);
            int imageStride = ct.ColumnCount * bitmap.Format.BitsPerPixel / 8;
            bitmap.WritePixels(imageRectangle, imageDataArray, imageStride, 0);

            return bitmap;
        }

        // Helper method, which returns the pixel data of a CT slice as gray-scale bitmap.
        public static WriteableBitmap GetPixelBufferAsBitmap(CTSliceInfo ct)
        {
            int windowLeftBorder = ct.WindowCenter - (ct.WindowWidth / 2);

            byte[,] normalizedPixelBuffer = BuildNormalizedPixelBuffer(ct, ct.WindowCenter, ct.WindowWidth, windowLeftBorder);

            WriteableBitmap bitmap = BuildGrey16Bitmap(ct, normalizedPixelBuffer);

            return bitmap;
        }
    }
}
