using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

using Ceres.Utilities;

namespace DICOMViewer.ROIVOI
{
    class ContourCollection
    {
#region Data
        private Point3D       _shift;

        private List<Contour> _axialContours = null;
        private List<Contour> _sagittalContours = null;
        private List<Contour> _coronalContours = null;
#endregion

        public ContourCollection()
        {
            _sagittalContours = new List<Contour>();
            _coronalContours = new List<Contour>();
            _axialContours = new List<Contour>();

            _shift = new Point3D();
        }

        public void Add(Contour ctr)
        {
            switch (ctr.dir)
            {
                case Direction.AXIAL:
                    _axialContours.Add(ctr);
                    break;

                case Direction.SAGITTAL:
                    _sagittalContours.Add(ctr);
                    break;

                case Direction.CORONAL:
                    _coronalContours.Add(ctr);
                    break;
            }
        }

        public void Fill(string filename)
        {
            StreamReader file = new StreamReader(filename);

            string line;
            int what = 0;

            int dir = 0;
            int nofpts = 0;
            double planepos = 0.0;
            Contour ctr = null;

            // reading shift value
            line = file.ReadLine();
            string[] liness = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            _shift = new Point3D(Convert.ToDouble(liness[0]), Convert.ToDouble(liness[1]), Convert.ToDouble(liness[2]));

            while((line = file.ReadLine()) != null)
            {
                switch (what)
                {
                    case 0:
                        dir = Convert.ToInt32(line);
                        ctr = null;
                        ++what;
                        break;

                    case 1:
                        planepos = Convert.ToDouble(line);
                        ++what;
                        break;

                    case 2:
                        nofpts = Convert.ToInt32(line);
                        ++what;
                        break;

                    case 3:
                        if (ctr == null)
                            ctr = new Contour((Direction)dir, planepos);

                        for (int k = 0; k != nofpts; ++k)
                        {
                            string[] lines = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                            double x = Convert.ToDouble(lines[0]);
                            double y = Convert.ToDouble(lines[1]);
                            double z = Convert.ToDouble(lines[2]);

                            ctr.points.Add(new Point3D(x, y, z));

                            if (k == nofpts - 1)
                            {

                                break;
                            }
                            line = file.ReadLine();
                        }
                        what = 0;
                        Add(ctr);
                        break;

                    default:
                        throw new InvalidOperationException("Something wrong with contour points list");
                }
            }
        }

        public Point3D Shift { get { return _shift; } }

        public List<Contour> AxialContours { get { return _axialContours; } }

        public List<Contour> SagittalContours { get { return _sagittalContours; } }

        public List<Contour> CoronalContours { get { return _coronalContours; } }

        public List<Contour> this[Direction dir]
        {
            get
            {
                switch (dir)
                {
                    case Direction.AXIAL:
                        return _axialContours;

                    case Direction.SAGITTAL:
                        return _sagittalContours;

                    case Direction.CORONAL:
                        return _coronalContours;
                }
                return null;
            }
        }

        public bool Empty(Direction dir)
        {
            switch (dir)
            {
                case Direction.AXIAL:
                    return _axialContours.Count == 0;

                case Direction.SAGITTAL:
                    return _sagittalContours.Count == 0;

                case Direction.CORONAL:
                    return _coronalContours.Count == 0;
            }
            return true;
        }

        // returns 
        public int Count(Direction dir)
        {
            switch (dir)
            {
                case Direction.AXIAL:
                    {
                        int s = 0;
                        foreach (var c in _axialContours)
                            s += c.points.Count;
                        return s;
                    }

                case Direction.SAGITTAL:
                    {
                        int s = 0;
                        foreach (var c in _sagittalContours)
                            s += c.points.Count;
                        return s;
                    }

                case Direction.CORONAL:
                    {
                        int s = 0;
                        foreach (var c in _coronalContours)
                            s += c.points.Count;
                        return s;
                    }
            }
            return 0;
        }

        public bool Invariant()
        {
            if (_axialContours == null)
                return false;

            if (_sagittalContours == null)
                return false;

            if (_coronalContours == null)
                return false;

            return true;
        }

        // 
        private static bool IsHere(Point3f pt, List<Point3f> points)
        {
            foreach (var point in points)
            {
                if (Point3f.AlmostEqual(pt, point))
                    return true;
            }
            return false;
        }

        private int FlattenOneDimension(List<Contour> contours, List<Point3f> points)
        {
            int k = 0;
            foreach (var c in contours)
            {
                var pts = c.points;
                foreach (Point3D p in pts)
                {
                    Point3f pt = new Point3f((float)p.X, (float)p.Y, (float)p.Z);

                    if (!IsHere(pt, points))
                    {
                        points.Add(pt);
                        ++k;
                    }
                }
            }
            return k;
        }

        public Point3f[] Flatten()
        {
            int n = this.Count(Direction.AXIAL) + this.Count(Direction.CORONAL) + this.Count(Direction.SAGITTAL);
              
            List<Point3f> points = new List<Point3f>();

            int sk = 0, k;
            k = FlattenOneDimension(this[Direction.AXIAL], points);
            sk += k;

            k = FlattenOneDimension(this[Direction.CORONAL], points);
            sk += k;

            k = FlattenOneDimension(this[Direction.SAGITTAL], points);
            sk += k;

            Debug.Assert(sk <= n);
            Debug.Assert(sk == points.Count);

            Point3f[] flatten = new Point3f[sk + 1];

            k = 0;
            foreach(var pt in points)
            {
                flatten[k] = pt;
                ++k;
            }

            // last point comes from the intersection of the first contours planes
            flatten[k] = new Point3f((float)_sagittalContours.ElementAt(0).planepos,
                                     (float)_coronalContours.ElementAt(0).planepos,
                                     (float)_axialContours.ElementAt(0).planepos);

            return flatten;
        }
    }
}
