using System.Collections.Generic;

namespace DICOMViewer.ROIVOI
{
    class ContourCollection
    {
#region Data
        private List<Contour> _axialContours = null;
        private List<Contour> _sagittalContours = null;
        private List<Contour> _coronalContours = null;
#endregion

        public ContourCollection()
        {
            _axialContours = new List<Contour>();
            _sagittalContours = new List<Contour>();
            _coronalContours = new List<Contour>();
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
    }
}
