using System;
using System.Drawing;
using System.Collections.Generic;

using DICOMViewer.Helper;

namespace DICOMViewer.ROIVOI
{
    sealed public class Couch
    {
        const short COUCH_HU = 1;

        static public int CouchStart(short[,] bm, int nr, int nc, int sr, short threshold)
        {
            // going from the bottom up
            for (int r = sr; r >= 0; --r)
            {
                int nof_above = 0;
                for (int c = 0; c != nc; ++c)
                {
                    nof_above += Convert.ToInt32( bm[r, c] > threshold );
                }

                if ((float)nof_above / (float)nc > 0.3) // 30% of metal
                    return r;
            }

            return -1;
        }

        public static int CouchEnd(short[,] bm, int nr, int nc, int sr)
        {
            // going from the bottom up
            for (int r = sr; r >= 0; --r)
            {
                int nof_below = 0;
                for (int c = 0; c != nc; ++c)
                {
                    nof_below += Convert.ToInt32( bm[r, c] < 0 );
                }

                if ((float)nof_below / (float)nc > 0.9)
                    return r;
            }

            return -1;
        }

        public static int DetectCouchInOneSlice(short[,] bm, int nr, int nc)
        {
            int r = CouchStart(bm, nr, nc, nr-1, COUCH_HU);

            if (r >= 0)
                r = CouchEnd(bm, nr, nc, r);

            int saved_r = r;

            // repeat step one more 
            r = CouchStart(bm, nr, nc, r, COUCH_HU);
            if (r >= 0)
                if (saved_r - r < 15) // within 15 pixels or so we found another couch top
                    r = CouchEnd(bm, nr, nc, r);
                else
                    r = saved_r;

            if (r < 0)
                r = saved_r;

            return r > 0 ? r : nr-1;
        }

        public static Tuple<float, float> AverageThreshold(CTSliceInfo ct, int sr, short threshold,
                                                           short skipthr)
        {
            int nr = ct.RowCount;
            int nc = ct.ColumnCount;

            short[,] bm = ct.HounsfieldPixelBuffer;

            int av_high = 0;
            int av_low = 0;

            int nof_high = 0;
            int nof_low = 0;

            for (int r = 0; r != sr; ++r)
            {
                for (int c = 0; c != nc; ++c)
                {
                    short v = bm[r, c];

                    if (v < skipthr)
                        continue;

                    if (v > threshold)
                    {
                        av_high += v;
                        ++nof_high;
                        continue;
                    }

                    av_low += v;
                    ++nof_low;
                }
            }

            return new Tuple<float, float>((float)av_low/(float)nof_low, (float)av_high / (float)nof_high);
        }

        public static short Round( float x )
        {
            if (x > 0.0f)
                return (short)(x + 0.5f);

            if (x < 0.0f)
                return (short)(x - 0.5f);

            return 0;
        }

        // Gray Level Thresholding
        public static short GLThresholding(CTSliceInfo ct, int sr, short threshold,
                                           short tissc, short lungc)
        {
            int nr = ct.RowCount;
            int nc = ct.ColumnCount;

            short[,] bm = ct.HounsfieldPixelBuffer;

            // iterative 
            Tuple<float, float> q = AverageThreshold(ct, sr, threshold, -900);
            short newthr = Round(0.5f * (q.Item1 + q.Item2));

            q = AverageThreshold(ct, sr, newthr, -900);
            threshold = Round(0.5f * (q.Item1 + q.Item2));

            for (int r = 0; r != nr; ++r)
            {
                for (int c = 0; c != nc; ++c)
                {
                    short v = bm[r, c];

                    bm[r, c] = tissc;
                    if (v < threshold)
                        bm[r, c] = lungc;
                }
            }

            return threshold;
        }

        // this is floodFill4Stack, stack based 4-way filling
        // for 8-way filling uncomment code in the middle
        public static short[,] FloodFill(short[,] bm, int nr, int nc,
                                         int r, int c,
                                         short oldc, short newc)
        {
            if (oldc == newc)
                return null;

            Stack<Point> pixels = new Stack<Point>(128); // with some large initial capacity

            pixels.Push(new Point(r, c));

            while (pixels.Count > 0)
            {
                Point popped = pixels.Pop();
                r = popped.X;
                c = popped.Y;

                bm[r, c] = newc;

                if (r + 1 < nr && bm[r + 1, c] == oldc)
                {
                    pixels.Push(new Point(r + 1, c));
                }
                if (r - 1 >= 0 && bm[r - 1, c] == oldc)
                {
                    pixels.Push(new Point(r - 1, c));
                }
                if (c + 1 < nc && bm[r, c + 1] == oldc)
                {
                    pixels.Push(new Point(r, c + 1));
                }
                if (c - 1 >= 0 && bm[r, c - 1] == oldc)
                {
                    pixels.Push(new Point(r, c - 1));
                }
                /*
                if (r + 1 < nr && c + 1 < nc && bm[r + 1, c + 1] == oldc)
                {
                    pixels.Push(new Point(r + 1, c + 1));
                }
                if (r - 1 >= 0 && c + 1 < nc && bm[r - 1, c + 1] == oldc)
                {
                    pixels.Push(new Point(r - 1, c + 1));
                }
                if (r + 1 < nr && c - 1 >= 0 && bm[r + 1, c - 1] == oldc)
                {
                    pixels.Push(new Point(r + 1, c - 1));
                }
                if (r - 1 >= 0 && c - 1 >= 0 && bm[r - 1, c - 1] == oldc)
                {
                    pixels.Push(new Point(r - 1, c - 1));
                }
                */
            }

            return bm;
        }
    }
}
