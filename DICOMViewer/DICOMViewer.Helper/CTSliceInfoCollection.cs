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
        }

        public bool Add(CTSliceInfo ct)
        {
            string fname = ct.FileName;
            int sliceloc = ct.SliceLoc;

            _slices_by_name.Add(fname, ct);
            _slices_by_locn.Add(sliceloc, ct);

            return true;
        }
    }
}
