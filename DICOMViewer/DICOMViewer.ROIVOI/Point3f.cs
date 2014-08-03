namespace DICOMViewer.ROIVOI
{
    //
    // Summary:
    //     Represents an x-, y-, and z-coordinate point in 3-D space.
    //
    public struct Point3f
    {
        public float _x; 
        public float _y;
        public float _z;

        public Point3f(float x, float y, float z)
        {
            _x = x;
            _y = y;
            _z = z;
        }

        public float X { get { return _x; } set { _x = value; } }

        public float Y { get { return _y; } set { _y = value; } }

        public float Z { get { return _z;  } set { _z = value; } }
    }
}
