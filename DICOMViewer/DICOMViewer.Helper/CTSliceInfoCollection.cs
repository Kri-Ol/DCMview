using System;
using System.Collections.Generic;
using System.Linq;

namespace DICOMViewer.Helper
{
    class CTSliceInfoCollection
    {
#region Data
        private Dictionary<string, CTSliceInfo> _slices_by_name = null;
        private Dictionary<int, CTSliceInfo>    _slices_by_locn = null;

        private CTSliceInfo[] _slices = null; // sorted slices
        private short[][,]    _volume = null; // whole volume in Hounsfield units, as jagged array
#endregion

        public CTSliceInfoCollection()
        {
            _slices_by_name = new Dictionary<string, CTSliceInfo>();
            _slices_by_locn = new Dictionary<int, CTSliceInfo>();
        }

        // true if value is replaced
        public bool Add(CTSliceInfo ct)
        {
            string fname = ct.FileName;
            int sliceloc = ct.SliceLoc;

            bool rc = _slices_by_locn.ContainsKey(sliceloc);

            _slices_by_name[fname]    = ct;
            _slices_by_locn[sliceloc] = ct;

            return true;
        }

        public CTSliceInfo Retrieve(string key)
        {
            if (_slices_by_name.ContainsKey(key))
                return _slices_by_name[key];

            return null;
        }

        public CTSliceInfo Retrieve(int key)
        {
            if (_slices_by_locn.ContainsKey(key))
                return _slices_by_locn[key];

            return null;
        }

        // returns true if final array is whole, without a skip of a slice
        private bool BuildSortedSlicesArray()
        {
            var count = _slices_by_locn.Count;
            _slices = new CTSliceInfo[count];

            // LINQ way to iterate
            for (int k = 0; k != count; ++k)
            {
                var item = _slices_by_locn.ElementAt(k);
                _slices[k] = item.Value;
            }

            Array.Sort(_slices, delegate (CTSliceInfo ctA, CTSliceInfo ctB)
                                { return ctA.SliceLoc.CompareTo(ctB.SliceLoc); }
                      );

            for (int k = 1; k != count; ++k)
            {
                if (_slices[k - 1].SliceLoc != _slices[k].SliceLoc - 1)
                    return false;
            }
            return true;
        }

        public void BuildVolume()
        {
            bool rc = true;
            if (_slices == null)
                rc = BuildSortedSlicesArray();

            if (!rc)
                return;

            var count = _slices.Length;

            var dimX = _slices[0].RowCount;
            var dimY = _slices[0].ColumnCount;
            var dimZ = count;

            _volume = new short[dimZ][,];

            for (int k = 0; k != count; ++k)
            {
                var sliceHU = _slices[k].HounsfieldPixelBuffer;
                _volume[k] = sliceHU;
            }
        }
    }
}
