using System;
using System.Diagnostics;

namespace DICOMViewer.ROIVOI
{
    public class GaussBlur
    {
#region Data
        private float     _sigma;

        private int       _br; // blur rows
        private int       _bc; // blur columns
        private float[,]  _blur;
#endregion

        public GaussBlur(float sigma, float sz, int dim)
        {
            Debug.Assert(sigma > 0.0f);
            Debug.Assert(sz > 0.0f);
            Debug.Assert(dim > 0);
            Debug.Assert(dim%2 > 0); // shall be odd number

            _br = dim / 2;
            _bc = dim / 2;

            _blur = new float[dim, dim];

            float invtwopis = 0.5f / ((float)Math.PI * _sigma * _sigma);

            float norm = 0.0f;
            for (int r = -_br; r <= _br; ++r)
            {
                float y = (float)r * sz;
                for (int c = -_bc; r <= _bc; ++c)
                {
                    float x = (float)c * sz;

                    float t = (x * x + y * y) * 0.5f / (_sigma * _sigma);
                    float q = (float)Math.Exp(-t) * invtwopis;
                    _blur[r + _br, c + _bc] = q;
                    norm += q;
                }
            }
            norm = 1.0f / norm;

            for (int r = -_br; r <= _br; ++r)
            {
                for (int c = -_bc; r <= _bc; ++c)
                {
                    _blur[r + _br, c + _bc] *= norm;
                }
            }
        }

        public short[,] Apply(short[,] hmap)
        {
            int nr = hmap.GetUpperBound(0);
            int nc = hmap.GetUpperBound(1);

            short[,] bm = new short[nr, nc];

            for(int r = 0; r != nr; ++r)
            {
                for (int c = 0; c != nc; ++c)
                {
                    bm[r, c] = 0;
                }
            }

            for (int r = 0; r != nr; ++r)
            {
                for (int c = 0; c != nc; ++c)
                {
                    float s = 0.0f;

                    for(int ir = -_br; ir <= _br; ++ir)
                    {
                        int sr = r + ir;
                        for (int ic = -_bc; ic <= _bc; ++ic)
                        {
                            int sc = c + ir;

                            s += (float)hmap[sr, sc] * _blur[ir + _br, ic + _bc];
                        }
                    }

                    bm[r, c] = (short)((float)Math.Round(s) + 0.1f);
                }
            }

            return bm;
        }
    }
}
