using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

using DICOMViewer.Helper;
using DICOMViewer.Parsing;

namespace DICOMViewer.Volume
{
    public partial class VolumeView : Window
    {
        private GeometryModel3D mGeometryModel;
        private bool            mDown;
        private Point           mLastPos; 

        public VolumeView()
        {
            InitializeComponent();
        }

        // Creates the Volume View out of a given list of CT Slices for the specified Iso Value. 
        public void CreateVolume(List<IOD> theImageIODList, int theIsoValue)
        {
            // 1. Calculate the Center Point
            // =============================
            // For moving the 3D model with the mouse, the implementation is taken from Code Project 'WPF 3D Primer'.
            // See also: 'http://www.codeproject.com/Articles/23332/WPF-3D-Primer#'
            // However, this implementation needs the 3D model to be centered in the origin of the coordinate system.
            // As a consequence, all CT Slices have to be shifted by the Center Point (method 'AdjustPatientPositionToCenterPoint()')
            CTSliceInfo aCTSliceFirst = new CTSliceInfo(theImageIODList[0].XDocument, theImageIODList[0].FileName);
            CTSliceInfo aCTSliceLast = new CTSliceInfo(theImageIODList[theImageIODList.Count - 1].XDocument, theImageIODList[theImageIODList.Count - 1].FileName);
            
            double Center_X = aCTSliceFirst.UpperLeft_X + (aCTSliceFirst.PixelSpacing_X * aCTSliceFirst.ColumnCount / 2);
            double Center_Y = aCTSliceFirst.UpperLeft_Y + (aCTSliceFirst.PixelSpacing_Y * aCTSliceFirst.RowCount / 2);

            // CT Slices are already sorted ascending in Z direction
            double Center_Z = aCTSliceFirst.UpperLeft_Z + ((aCTSliceLast.UpperLeft_Z - aCTSliceFirst.UpperLeft_Z) / 2);
            
            // Create the Center Point
            Point3D aCenterPoint = new Point3D(Center_X, Center_Y, Center_Z);

            // 2. The Marching Cubes algorithm
            // ===============================
            // For each Voxel of the CT Slice, an own GridCell is created. 
            // The IsoValue and the x/y/z information for each corner of the GridCell is taken from the direct neighbor voxels (of the same or adjacant CT Slice).
            // Looping has to be done over all CT Slices.

            List<Triangle> aTriangleList = new List<Triangle>();

            CTSliceInfo aCTSlice1 = null;
            CTSliceInfo aCTSlice2 = new CTSliceInfo(theImageIODList[0].XDocument, theImageIODList[0].FileName);
            aCTSlice2.AdjustPatientPositionToCenterPoint(aCenterPoint);

            for (int aSliceIndex = 1; aSliceIndex < theImageIODList.Count; aSliceIndex++)
            {
                aCTSlice1 = aCTSlice2;
                aCTSlice2 = new CTSliceInfo(theImageIODList[aSliceIndex].XDocument, theImageIODList[aSliceIndex].FileName);
                aCTSlice2.AdjustPatientPositionToCenterPoint(aCenterPoint);

                for (int aRowIndex = 0; aRowIndex < aCTSlice1.RowCount - 1; aRowIndex++)
                {
                    for (int aColumnIndex = 0; aColumnIndex < aCTSlice1.ColumnCount - 1; aColumnIndex++)
                    {
                        GridCell aGridCell = new GridCell(aSliceIndex, aRowIndex, aColumnIndex, aCTSlice1, aCTSlice2);
                        MarchingCubes.Polygonise(aGridCell, theIsoValue, ref aTriangleList);
                    }
                }
            }

            Model3DGroup a3DGroup = new Model3DGroup();
            MeshGeometry3D aMesh = new MeshGeometry3D();

            // 3. Mesh creation
            // ================
            // After executing the marching cubes algorithm, all triangles are stored in the TriangleList.
            // Adding all points to the mesh variable will finally form the mesh (=surface for the given IsoValue).
            foreach (Triangle aTriangle in aTriangleList)
            {
                aMesh.Positions.Add(aTriangle.p[0]);
                aMesh.Positions.Add(aTriangle.p[2]);
                aMesh.Positions.Add(aTriangle.p[1]);
            }

            // Once the mesh generation is done, freeze the mesh in order to improve performance.
            aMesh.Freeze();

            // Last step is to give the mesh to the WPF viewport in order to render it.
            mGeometryModel = new GeometryModel3D(aMesh, new DiffuseMaterial(new SolidColorBrush(Colors.Red)));
            mGeometryModel.Transform = new Transform3DGroup();
            a3DGroup.Children.Add(mGeometryModel);
            m3DModel.Content = a3DGroup;

            // Dump some useful size information.
            mInfoLabel.Text =  string.Format("CT Pixel Data\n");
            mInfoLabel.Text += string.Format("{0} x {1} x {2} = {3:### ### ### ###}\n\n", aCTSlice1.RowCount, aCTSlice1.ColumnCount, theImageIODList.Count, aCTSlice1.RowCount * aCTSlice1.ColumnCount * theImageIODList.Count);
            mInfoLabel.Text += string.Format("Marching Cubes\n");
            mInfoLabel.Text += string.Format("IsoValue: {0}, Triangle Count: {1:### ### ### ###}\n\n", theIsoValue, aTriangleList.Count);
            mInfoLabel.Text += string.Format("Mesh Size\n");
            mInfoLabel.Text += string.Format("Points: {0:### ### ### ###}", aTriangleList.Count * 3);

            // 4. Camera Setup
            // ===============
            // We assume the maximum size of the model in Z direction
            double aEstimatedModelSize = aCTSliceLast.UpperLeft_Z - aCTSliceFirst.UpperLeft_Z;

            // Setup the camera position. A reasonable value for the model/camera distance is choosen.
            // In order to rotate the 3D model via the mouse, the implementation from the Code Project 'WPF 3D Primer' is taken.
            // However, in order to use this proposal out of the box, the camera must be positioned on the Z axis only.
            mViewPortCamera.Position = new Point3D(0, 0, -(aEstimatedModelSize * 3));

            // The 3D model is centered in the origin of the coordinate system. 
            // Hence, camera and light direction should point to the origin.
            mViewPortCamera.LookDirection = new Point3D(0, 0, 0) - mViewPortCamera.Position;
            mViewPortLight.Direction = mViewPortCamera.LookDirection;
        }

        // Helper method to support zooming
        private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // As the 3D model is centered in the origin of the coordinate system, we can simply decrease/increase the distance of the camera to the origin 
            // in order to achieve a zooming effect. 
            double aZoomFactor = e.Delta > 0 ? 0.9 : 1.1;
            mViewPortCamera.Position = new Point3D(mViewPortCamera.Position.X * aZoomFactor, mViewPortCamera.Position.Y * aZoomFactor, mViewPortCamera.Position.Z * aZoomFactor);
        }

        // Below mouse handlers allow to rotate the model via mouse. Code is taken from the Code Project 'WPF 3D Primer'.
        // See also: 'http://www.codeproject.com/Articles/23332/WPF-3D-Primer#'

        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            mDown = false;
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            mDown = true;
            Point pos = Mouse.GetPosition(mViewPort);
            mLastPos = new Point(pos.X - mViewPort.ActualWidth / 2, mViewPort.ActualHeight / 2 - pos.Y);
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mDown) 
                return;
            
            Point pos = Mouse.GetPosition(mViewPort);
            Point actualPos = new Point(pos.X - mViewPort.ActualWidth / 2, mViewPort.ActualHeight / 2 - pos.Y);

            //double dx = actualPos.X - mLastPos.X;
            double dx = mLastPos.X - actualPos.X;

            double dy = actualPos.Y - mLastPos.Y;
            double mouseAngle = 0;

            if (dx != 0 && dy != 0)
            {
                mouseAngle = Math.Asin(Math.Abs(dy) / Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2)));
                if (dx < 0 && dy > 0)
                    mouseAngle += Math.PI / 2;
                else if (dx < 0 && dy < 0)
                    mouseAngle += Math.PI;
                else if (dx > 0 && dy < 0)
                    mouseAngle += Math.PI * 1.5;
            }
            else if (dx == 0 && dy != 0)
            {
                mouseAngle = Math.Sign(dy) > 0 ? Math.PI / 2 : Math.PI * 1.5;
            }
            else if (dx != 0 && dy == 0)
            {
                mouseAngle = Math.Sign(dx) > 0 ? 0 : Math.PI;
            }

            double axisAngle = mouseAngle + Math.PI / 2;

            Vector3D axis = new Vector3D( Math.Cos(axisAngle) * 4, Math.Sin(axisAngle) * 4, 0);

            double rotation = 0.02 * Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));

            Transform3DGroup group = mGeometryModel.Transform as Transform3DGroup;
            QuaternionRotation3D r = new QuaternionRotation3D(new Quaternion(axis, rotation * 180 / Math.PI));
            group.Children.Add(new RotateTransform3D(r));

            mLastPos = actualPos;
        } 
    }
}
