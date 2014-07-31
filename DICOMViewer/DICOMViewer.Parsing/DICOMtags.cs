using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DICOMViewer.Parsing
{
    class DICOMTAG
    {
        public const string PATIENT_NAME = "(0010,0010)";

        public const string WINDOW_CENTER = "(0028,1050)";
        public const string WINDOW_WIDTH  = "(0028,1051)";
        public const string ROWS    = "(0028,0010)";
        public const string COLUMNS = "(0028,0011)";

        public const string PIXEL_SPACING = "(0028,0030)";

        public const string IMAGE_PLANE_PIXEL_SPACING = "(3002,0011)";

        public const string IMAGE_POSITION_PATIENT = "(0020,0032)";
        public const string SLICE_LOCATION         = "(0020,1041)";
    }
}
