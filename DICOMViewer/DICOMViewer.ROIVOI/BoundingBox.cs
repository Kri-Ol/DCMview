using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DICOMViewer.ROIVOI
{
    struct BoundingBox
    {
        public Point3f _min;
        public Point3f _max;

        public BoundingBox(Point3f min, Point3f max)
        {
            _min = min;
            _max = max;
        }

        public Point3f Max { get { return _max; } set { _max = value; } }
        public Point3f Min { get { return _min; } set { _min = value; } }

        public void Clear()
        {
            _min.X = _min.Y = _min.Z = Single.MaxValue;
            _max.X = _max.Y = _max.Z = Single.MinValue;
        }
    }
}
