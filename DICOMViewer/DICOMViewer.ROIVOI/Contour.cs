using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace DICOMViewer.ROIVOI
{
    // assumed to be closed
    class Contour
    {
#region Data
        protected Direction           _dir;
        protected double              _planepos;

        // only two coordinates will be uzed, third would be set to plane position
        protected Point3DCollection   _points;
#endregion

        public Contour(Direction dir, double planepos)
        {
            _dir = dir;
            _planepos = planepos;
            _points = new Point3DCollection(20);
        }

        public Contour(Direction dir, double planepos, IEnumerable<Point3D> points)
        {
            _dir = dir;
            _planepos = planepos;
            _points = new Point3DCollection(points);
        }

        private Point3D SyncPoint(Point3D p)
        {
            switch (_dir)
            {
                case Direction.AXIAL:
                    p.Z = _planepos;
                    break;

                case Direction.SAGITTAL:
                    p.Y = _planepos;
                    break;

                case Direction.CORONAL:
                    p.X = _planepos;
                    break;

                default:
                    throw new ArgumentOutOfRangeException("direction", String.Format("Wrong one {0}", _dir));
            }
            return p;
        }

        private void SyncPoints()
        {
            for (int k = 0; k != _points.Count; ++k)
            {
                _points[k] = SyncPoint(_points[k]);
            }
        }

        public void Add(Point3D pt)
        {
            _points.Add(SyncPoint(pt));
        }

        public Point3DCollection points
        {
            get { return _points; }
        }

        public Direction dir
        {
            get { return _dir; }
        }

        public double planepos
        {
            get { return _planepos; }
        }
    }
}
