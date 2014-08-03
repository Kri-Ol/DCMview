using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace DICOMViewer.ROIVOI
{
    abstract class Evaluator
    {
#region Data
        protected ContourCollection _ccol = null;

        protected BoundingBox       _bbox;
        protected Point3f[]         _flatten = null;
#endregion

        public Evaluator(ContourCollection ccol)
        {
            _ccol = ccol;
        }

        private int FlattenOneContour( List<Contour> contours, int k,
                                       ref float minX, ref float minY, ref float minZ,
                                       ref float maxX, ref float maxY, ref float maxZ )
        {
            foreach (var c in contours)
            {
                var pts = c.points;
                foreach (Point3D p in pts)
                {
                    float x = (float)p.X;
                    float y = (float)p.Y;
                    float z = (float)p.Z;
                    if (x < minX)
                        minX = x;
                    if (x > maxX)
                        maxX = x;
                    if (y < minY)
                        minY = y;
                    if (y > maxY)
                        maxY = y;
                    if (z < minZ)
                        minZ = z;
                    if (z > maxZ)
                        maxZ = z;

                    _flatten[k] = new Point3f(x, y, z);
                    ++k;
                }
            }
            return k;
        }

        public void Flatten()
        {
            int n = _ccol.Count(Direction.AXIAL) + _ccol.Count(Direction.CORONAL) + _ccol.Count(Direction.SAGITTAL);

            _flatten = new Point3f[n];

            float minX = Single.MaxValue;
            float minY = Single.MaxValue;
            float minZ = Single.MaxValue;
            float maxX = Single.MinValue;
            float maxY = Single.MinValue;
            float maxZ = Single.MinValue;

            int k = 0;
            k = FlattenOneContour(_ccol[Direction.AXIAL], k, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            k = FlattenOneContour(_ccol[Direction.CORONAL], k, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            k = FlattenOneContour(_ccol[Direction.SAGITTAL], k, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);

            Debug.Assert(k == n);
        }

        public abstract float Evaluate(Point3f pt);

        protected abstract void Compute();

        protected virtual void Invalidate()
        {
            _bbox.Clear();
            _flatten = null;
        }

        public ContourCollection ccol
        {
            get { return _ccol; }
            set
            {
                _ccol = value;
                Invalidate();
            }
        }
    }
}
