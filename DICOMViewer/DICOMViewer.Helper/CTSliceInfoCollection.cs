using System;
using System.Collections.Generic;

namespace DICOMViewer.Helper
{
    class CTSliceInfoCollection
    {
        private Dictionary<string, CTSliceInfo> _slices_by_name = null;
        private Dictionary<int, CTSliceInfo>    _slices_by_locn = null;

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
    }
}
