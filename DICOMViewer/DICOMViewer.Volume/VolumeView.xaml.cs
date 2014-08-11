using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

using DICOMViewer.Helper;

/*
private void convolution_with_a_gauss_filter(Int32 N)
{
    Int32 Nh = N / 2;
    Int32 x, xx, xxx, y, yy, yyy;
    Int64 t0, t1;
    float[,] kernel = new float[N, N]; //quadratic Gauss kernel
    float sum_kernel = 0.0f;
    float sumR, sumG, sumB, weight, Rf, Gf, Bf;
    Cursor.Current = Cursors.WaitCursor;
    t0 = DateTime.Now.Ticks;
    //Construction of a suitable 2D-Gaussian bell-shape kernel:
    //The corner elements of the kernel are Nh*sqrt(2) away from its center
    //and therefore obtain the lowest values.
    //We adjust the parameter a of the e-function y = e -(a*x2) so,
    //that always (at any kernel size except 3x3) these corners obtain at least 1% weight.
    double a = 1.0f;
    if (N > 3) a = -2 * Nh * Nh / Math.Log(0.01);
    //fill the kernel with elements depending on their distance and on a.
    for (y = 0; y < N; y++)
        for (x = 0; x < N; x++)
        {
            double dist = Math.Sqrt((x - Nh) * (x - Nh) + (y - Nh) * (y - Nh));
            sum_kernel += kernel[y, x] = (float)(Math.Exp(-dist * dist / a));
        }
    //Convolution
    for (y = Nh; y < b0.Height - Nh; y++) //==================
    {
        for (x = Nh; x < b0.Width - Nh; x++) //===============
        {
            sumR = sumG = sumB = 0.0f;
            for (yy = -Nh; yy <= Nh; yy++) //=============
            {
                yyy = y + yy;
                for (xx = -Nh; xx <= Nh; xx++)//========
                {
                    weight = kernel[yy + Nh, xx + Nh];
                    xxx = x + xx;
                    sumR += weight * R0[yyy, xxx];
                    sumG += weight * G0[yyy, xxx];
                    sumB += weight * B0[yyy, xxx];
                } //====== end for (int xx... ================
            } //======== end for (int yy... ==================
            Rf = sumR / sum_kernel; Gf = sumG / sum_kernel; Bf = sumB / sum_kernel;
            b2.SetPixel(x, y, Color.FromArgb(Convert.ToInt32(Rf),
                                               Convert.ToInt32(Gf),
                                               Convert.ToInt32(Bf)));
        } //============ end for (int x... =====================
    } //============== end for (int y... =======================
    t1 = DateTime.Now.Ticks;
    s2 = "Simple quadratic Gauss filter\r\n" +
         "Image:  " + b0.Width.ToString() + " x " + b0.Height.ToString() + "\r\n" +
         "Filter: " + N.ToString() + " x " + N.ToString() + "\r\n" +
         "Filter Time: " + String.Format("{0:F1}", (t1 - t0) / 1000000f) + " MegaTicks";
    Cursor.Current = Cursors.Arrow;
}
*/

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

        private static List<Triangle> ComputeTriangles(CTSliceInfo[] slices, int theIsoValue)
        {
            // 1. Calculate the Center Point
            // =============================
            // For moving the 3D model with the mouse, the implementation is taken from Code Project 'WPF 3D Primer'.
            // See also: 'http://www.codeproject.com/Articles/23332/WPF-3D-Primer#'
            // However, this implementation needs the 3D model to be centered in the origin of the coordinate system.
            // As a consequence, all CT Slices have to be shifted by the Center Point (method 'AdjustPatientPositionToCenterPoint()')
            CTSliceInfo firstCT = slices[0];
            CTSliceInfo lastCT  = slices[slices.Length - 1];

            double Center_X = firstCT.UpperLeft_X + (firstCT.PixelSpacing_X * firstCT.ColumnCount / 2);
            double Center_Y = firstCT.UpperLeft_Y + (firstCT.PixelSpacing_Y * firstCT.RowCount / 2);

            // CT Slices are already sorted ascending in Z direction
            double Center_Z = firstCT.UpperLeft_Z + ((lastCT.UpperLeft_Z - firstCT.UpperLeft_Z) / 2);

            // Create the Center Point
            Point3D aCenterPoint = new Point3D(Center_X, Center_Y, Center_Z);

            // 2. The Marching Cubes algorithm
            // ===============================
            // For each Voxel of the CT Slice, an own GridCell is created. 
            // The IsoValue and the x/y/z information for each corner of the GridCell is taken from the direct neighbor voxels (of the same or adjacant CT Slice).
            // Looping has to be done over all CT Slices.

            List<Triangle> triangles = new List<Triangle>();

            CTSliceInfo slice1 = null;
            CTSliceInfo slice2 = slices[0];

            slice2.AdjustPatientPositionToCenterPoint(aCenterPoint);

            for (int sliceIdx = 1; sliceIdx != slices.Length; ++sliceIdx)
            {
                slice1 = slice2;
                slice2 = slices[sliceIdx];
                slice2.AdjustPatientPositionToCenterPoint(aCenterPoint);

                for (int r = 0; r != slice1.RowCount - 1; ++r)
                {
                    for (int c = 0; c != slice1.ColumnCount - 1; ++c)
                    {
                        GridCell aGridCell = new GridCell(sliceIdx, r, c, slice1, slice2);
                        MarchingCubes.Polygonise(aGridCell, theIsoValue, ref triangles);
                    }
                }
            }
            return triangles;
        }

        private static MeshGeometry3D ComputeMesh(List<Triangle> triangles)
        {
            MeshGeometry3D mesh = new MeshGeometry3D();

            // 3. Mesh creation
            // ================
            // After executing the marching cubes algorithm, all triangles are stored in the TriangleList.
            // Adding all points to the mesh variable will finally form the mesh (=surface for the given IsoValue).
            foreach (Triangle triangle in triangles)
            {
                mesh.Positions.Add(triangle.p[0]);
                mesh.Positions.Add(triangle.p[2]);
                mesh.Positions.Add(triangle.p[1]);
            }

            // Once the mesh generation is done, freeze the mesh in order to improve performance.
            mesh.Freeze();

            return mesh;
        }

        // Creates the Volume View out of a given list of CT Slices for the specified Iso Value. 
        public void CreateVolume(CTSliceInfo[] slices, int theIsoValue)
        {
            CTSliceInfo firstCT = slices[0];
            CTSliceInfo lastCT  = slices[slices.Length - 1];

            List<Triangle> triangles = ComputeTriangles(slices, theIsoValue);
            MeshGeometry3D mesh      = ComputeMesh(triangles);

            // Last step is to give the mesh to the WPF viewport in order to render it.
            mGeometryModel = new GeometryModel3D(mesh, new DiffuseMaterial(new SolidColorBrush(Colors.Red)));
            mGeometryModel.Transform = new Transform3DGroup();

//            GeometryModel3D qqq = new GeometryModel3D(ComputeMesh(ComputeTriangles(slices, theIsoValue-100)),
//                                                      new DiffuseMaterial(new SolidColorBrush(Colors.Blue)));
//            qqq.Transform = mGeometryModel.Transform;

            Model3DGroup a3DGroup = new Model3DGroup();
            a3DGroup.Children.Add(mGeometryModel);
//            a3DGroup.Children.Add(qqq);
            m3DModel.Content = a3DGroup;

            // Dump some useful size information.
            mInfoLabel.Text =  string.Format("CT Pixel Data\n");
            mInfoLabel.Text += string.Format("{0} x {1} x {2} = {3:### ### ### ###}\n\n", firstCT.RowCount, firstCT.ColumnCount, slices.Length, firstCT.RowCount * firstCT.ColumnCount * slices.Length);
            mInfoLabel.Text += string.Format("Marching Cubes\n");
            mInfoLabel.Text += string.Format("IsoValue: {0}, Triangle Count: {1:### ### ### ###}\n\n", theIsoValue, triangles.Count);
            mInfoLabel.Text += string.Format("Mesh Size\n");
            mInfoLabel.Text += string.Format("Points: {0:### ### ### ###}", triangles.Count * 3);

            // 4. Camera Setup
            // ===============
            // We assume the maximum size of the model in Z direction
            double aEstimatedModelSize = lastCT.UpperLeft_Z - firstCT.UpperLeft_Z;

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
                mouseAngle = Math.Asin(Math.Abs(dy) / Math.Sqrt(dx*dx + dy*dy));
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
            else if (dx != 0.0 && dy == 0.0)
            {
                mouseAngle = Math.Sign(dx) > 0 ? 0.0 : Math.PI;
            }

            double axisAngle = mouseAngle + Math.PI / 2;

            Vector3D axis = new Vector3D( Math.Cos(axisAngle) * 4.0, Math.Sin(axisAngle) * 4.0, 0.0);

            double rotation = 0.02 * Math.Sqrt(dx*dx + dy*dy);

            Transform3DGroup group = mGeometryModel.Transform as Transform3DGroup;
            QuaternionRotation3D r = new QuaternionRotation3D(new Quaternion(axis, rotation * 180.0 / Math.PI));
            group.Children.Add(new RotateTransform3D(r));

            mLastPos = actualPos;
        } 
    }
}
