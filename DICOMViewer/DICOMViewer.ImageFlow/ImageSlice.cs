using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Xml.Linq;

using DICOMViewer.Helper;

// Implementation of the Image Slice for the Image Flow View using Windows Presentation Foundation (WPF).
// Major parts of the code have been taken from the 'WPF Cover Flow Tutorial':
// http://d3dal3.blogspot.com/2008/10/wpf-cover-flow-tutorial-part-1.html

namespace DICOMViewer.ImageFlow
{
    class ImageSlice : ModelVisual3D
    {
        private XDocument _XDocument = null;
        private string    _FileName = null;
        private string    _ZValue = null;

        private ModelVisual3D _3DModel = null;
        private Model3DGroup  _ModelGroup = null;

        private AxisAngleRotation3D  _Rotation = null;
        private TranslateTransform3D _Translation = null;

        private WriteableBitmap _Bitmap = null;
        private ImageBrush      _ImageBrush = null;

        // Create new ImageSlice
        public ImageSlice(XDocument theXDocument, string theFileName, string theZValue, ModelVisual3D the3DModel)
        {
            _XDocument = theXDocument;
            _FileName  = theFileName;

            _ZValue  = theZValue;
            _3DModel = the3DModel;
        }

        public void Animate(double aRotationAngle, double aTranslationX, double aTranslationY, double aTranslationZ, TimeSpan aAnimationDuration)
        {
            var rotateAnimation = new DoubleAnimation(aRotationAngle, aAnimationDuration);
            var xAnimation = new DoubleAnimation(aTranslationX, aAnimationDuration);
            var yAnimation = new DoubleAnimation(aTranslationY, aAnimationDuration);
            var zAnimation = new DoubleAnimation(aTranslationZ, aAnimationDuration);

            if(_Rotation != null)
                _Rotation.BeginAnimation(AxisAngleRotation3D.AngleProperty, rotateAnimation);

            if (_Translation != null)
            {
                _Translation.BeginAnimation(TranslateTransform3D.OffsetXProperty, xAnimation);
                _Translation.BeginAnimation(TranslateTransform3D.OffsetYProperty, yAnimation);
                _Translation.BeginAnimation(TranslateTransform3D.OffsetZProperty, zAnimation);
            }
        }

        public void SetBitmap()
        {
            if (_Bitmap == null)
            {
                CTSliceInfo ct = new CTSliceInfo(_XDocument, _FileName);
                ct.GetHounsfieldPixelValue(0, 0);

                _Bitmap = CTSliceHelpers.GetPixelBufferAsBitmap(ct);
                _Bitmap.Freeze();
                
                _ImageBrush             = new ImageBrush();
                _ImageBrush.ImageSource = _Bitmap;
                
                _ModelGroup = new Model3DGroup();
                _ModelGroup.Children.Add(new GeometryModel3D(Tessellate(), new DiffuseMaterial(_ImageBrush)));
                this.Content = _ModelGroup;
                
                _3DModel.Children.Add(this);
            }
        }

        public void ResetBitmap()
        {
            if (_ImageBrush != null)
                _ImageBrush.ImageSource = null;

            _ImageBrush = null;
            _Bitmap = null;
            _3DModel.Children.Remove(this);
        }

        new public void Transform(double aRotationAngle, double aTranslationX, double aTranslationY, double aTranslationZ)
        {
            _Rotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), aRotationAngle);
            _Translation = new TranslateTransform3D(aTranslationX, aTranslationY, aTranslationZ);

            var transformGroup = new Transform3DGroup();
            transformGroup.Children.Add(new RotateTransform3D(_Rotation));
            transformGroup.Children.Add(_Translation);

            if(_ModelGroup != null)
                _ModelGroup.Transform = transformGroup;
        }

        public string FileName { get { return _FileName; } }
        public string ZValue { get { return _ZValue; } }

        private static Geometry3D Tessellate()
        {
            var p0 = new Point3D(-1, -1, 0);
            var p1 = new Point3D(1, -1, 0);
            var p2 = new Point3D(1, 1, 0);
            var p3 = new Point3D(-1, 1, 0);

            var q0 = new Point(0, 0);
            var q1 = new Point(1, 0);
            var q2 = new Point(1, 1);
            var q3 = new Point(0, 1);

            return BuildMesh(p0, p1, p2, p3, q0, q1, q2, q3);
        }

        private static Vector3D CalculateNormal(Point3D p0, Point3D p1, Point3D p2)
        {
            var v0 = new Vector3D(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            var v1 = new Vector3D(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
            return Vector3D.CrossProduct(v0, v1);
        }

        private static MeshGeometry3D BuildMesh(Point3D p0, Point3D p1, Point3D p2, Point3D p3,
                                                Point q0, Point q1, Point q2, Point q3)
        {
            var mesh = new MeshGeometry3D();
            mesh.Positions.Add(p0);
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);

            var normal = CalculateNormal(p0, p1, p2);
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(2);
            mesh.Normals.Add(normal);
            mesh.Normals.Add(normal);
            mesh.Normals.Add(normal);
            mesh.TextureCoordinates.Add(q3);
            mesh.TextureCoordinates.Add(q2);
            mesh.TextureCoordinates.Add(q1);

            normal = CalculateNormal(p2, p3, p0);
            mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(0);
            mesh.Normals.Add(normal);
            mesh.Normals.Add(normal);
            mesh.Normals.Add(normal);
            mesh.TextureCoordinates.Add(q0);
            mesh.TextureCoordinates.Add(q1);
            mesh.TextureCoordinates.Add(q2);

            mesh.Freeze();
            return mesh;
        }
    }
}
