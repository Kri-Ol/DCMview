using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace DICOMViewer.ROIVOI
{
    class Contour
    {
#region Data
        protected Direction           _dir;
        protected double              _planepos;

        // only two coordinates will be uzed
        protected Point3DCollection   _points;
#endregion

        Contour(Direction dir, double planepos)
        {
            _dir = dir;
            _planepos = planepos;
            _points = new Point3DCollection(20);
        }

        Contour(Direction dir, double planepos, IEnumerable<Point3D> points)
        {
            _dir = dir;
            _planepos = planepos;
            _points = new Point3DCollection(points);
        }

        void Add(Point3D pt)
        {
            _points.Add(pt);
        }

        Point3DCollection points
        {
            get { return _points; }
        }
    }
}
