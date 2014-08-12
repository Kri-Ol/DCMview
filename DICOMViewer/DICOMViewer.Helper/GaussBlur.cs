using System;
using System.Diagnostics;

namespace DICOMViewer.Helper
{
    public class GaussBlur
    {
#region Data
        private float _sigma;

        private int _br; // blur rows
        private int _bc; // blur columns
        private float[,] _blur;
#endregion

        public int br { get { return _br; } }
        public int bc { get { return _bc; } }
        public float[,] blur { get { return _blur; } }

        public GaussBlur(float sigma, float sz, int dim)
        {
            Debug.Assert(sigma > 0.0f);
            Debug.Assert(sz > 0.0f);
            Debug.Assert(dim > 0);

            _sigma = sigma;

            _br = dim / 2;
            _bc = dim / 2;

            _blur = new float[dim, dim];

            float invtwopis = 0.5f / ((float)Math.PI * _sigma * _sigma);

            float norm = 0.0f;
            for (int r = -_br; r <= _br; ++r)
            {
                int ir = r + _br; Debug.Assert(ir >= 0); Debug.Assert(ir < dim);
                float y = (float)r * sz;
                for (int c = -_bc; c <= _bc; ++c)
                {
                    int ic = c + _bc; Debug.Assert(ic >= 0); Debug.Assert(ic < dim);
                    float x = (float)c * sz;

                    float t = (x * x + y * y) * 0.5f / (_sigma * _sigma);
                    float q = (float)Math.Exp(-t) * invtwopis;
                    _blur[ir, ic] = q;
                    norm += q;
                }
            }
            norm = 1.0f / norm;

            for (int r = -_br; r <= _br; ++r)
            {
                int ir = r + _br;
                for (int c = -_bc; c <= _bc; ++c)
                {
                    int ic = c + _bc;
                    _blur[ir, ic] *= norm;
                }
            }
        }
    }
}
