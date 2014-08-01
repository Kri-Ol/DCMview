using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

using DICOMViewer.Helper;

namespace DICOMViewer.Volume
{
    public class Triangle
    {
        public Point3D[] p = new Point3D[3];
    }

    public class GridCell
    {
        public Point3D[] p = new Point3D[8];
        public Int32[] val = new Int32[8];

        // Creates a new GridCell for adjacent CT Slices.
        // For the index convention, please refer to the article 'Polygonising a scalar field' written by Paul Bourke.
        // http://paulbourke.net/geometry/polygonise/

        public GridCell(int theSliceIndex, int theRowIndex, int theColumnIndex, CTSliceInfo CTSliceFront, CTSliceInfo CTSliceBack)
        {
            double X_Left_Front = CTSliceFront.UpperLeft_X + (theColumnIndex * CTSliceFront.PixelSpacing_X);
            double X_Right_Front = X_Left_Front + CTSliceFront.PixelSpacing_X;

            double X_Left_Back = CTSliceBack.UpperLeft_X + (theColumnIndex * CTSliceBack.PixelSpacing_X);
            double X_Right_Back = X_Left_Back + CTSliceBack.PixelSpacing_X;

            double Y_Top_Front = CTSliceFront.UpperLeft_Y + (theRowIndex * CTSliceFront.PixelSpacing_Y);
            double Y_Botton_Front = Y_Top_Front + CTSliceFront.PixelSpacing_Y;

            double Y_Top_Back = CTSliceBack.UpperLeft_Y + (theRowIndex * CTSliceBack.PixelSpacing_Y);
            double Y_Botton_Back = Y_Top_Back + CTSliceBack.PixelSpacing_Y;

            double Z_Front = CTSliceFront.UpperLeft_Z;
            double Z_Back = CTSliceBack.UpperLeft_Z;

            p[0] = new Point3D( X_Left_Back,   Y_Botton_Back ,  Z_Back);
            p[1] = new Point3D( X_Right_Back,  Y_Botton_Back ,  Z_Back);
            p[2] = new Point3D( X_Right_Front, Y_Botton_Front , Z_Front);
            p[3] = new Point3D( X_Left_Front,  Y_Botton_Front , Z_Front);
            p[4] = new Point3D( X_Left_Back,   Y_Top_Back ,     Z_Back);
            p[5] = new Point3D( X_Right_Back,  Y_Top_Back ,     Z_Back);
            p[6] = new Point3D( X_Right_Front, Y_Top_Front ,    Z_Front);
            p[7] = new Point3D( X_Left_Front,  Y_Top_Front ,    Z_Front);

            val[0] = CTSliceBack.GetHounsfieldPixelValue(theRowIndex + 1, theColumnIndex);
            val[1] = CTSliceBack.GetHounsfieldPixelValue(theRowIndex + 1, theColumnIndex + 1);
            val[2] = CTSliceFront.GetHounsfieldPixelValue(theRowIndex + 1, theColumnIndex + 1);
            val[3] = CTSliceFront.GetHounsfieldPixelValue(theRowIndex + 1, theColumnIndex);
            val[4] = CTSliceBack.GetHounsfieldPixelValue(theRowIndex, theColumnIndex);
            val[5] = CTSliceBack.GetHounsfieldPixelValue(theRowIndex, theColumnIndex + 1);
            val[6] = CTSliceFront.GetHounsfieldPixelValue(theRowIndex, theColumnIndex + 1);
            val[7] = CTSliceFront.GetHounsfieldPixelValue(theRowIndex, theColumnIndex);
        }
    }

    // Implementation of the marching cubes algorithm.
    // For more details, please refer to the article 'Polygonising a scalar field' written by Paul Bourke.
    // http://paulbourke.net/geometry/polygonise/

    class MarchingCubes
    {
        // Given a grid cell and an isolevel, calculate the triangular facets required to represent the isosurface through the cell.
        // Return the number of triangular facets, the array "triangles" will be loaded up with the vertices at most 5 triangular facets.
	    // 0 will be returned if the grid cell is either totally above of totally below the isolevel.
        public static void Polygonise(GridCell grid, double isolevel, ref List<Triangle> theTriangleList)
        {
            // Determine the index into the edge table which tells us which vertices are inside of the surface
            int cubeindex = 0;
            if (grid.val[0] < isolevel) cubeindex |= 1;
            if (grid.val[1] < isolevel) cubeindex |= 2;
            if (grid.val[2] < isolevel) cubeindex |= 4;
            if (grid.val[3] < isolevel) cubeindex |= 8;
            if (grid.val[4] < isolevel) cubeindex |= 16;
            if (grid.val[5] < isolevel) cubeindex |= 32;
            if (grid.val[6] < isolevel) cubeindex |= 64;
            if (grid.val[7] < isolevel) cubeindex |= 128;

            // Cube is entirely in/out of the surface 
            if (EdgeTable.LookupTable[cubeindex] == 0)
                return;

            Point3D[] vertlist = new Point3D[12];

            // Find the vertices where the surface intersects the cube 
            if ((EdgeTable.LookupTable[cubeindex] & 1) > 0)
                vertlist[0] = VertexInterp(isolevel, grid.p[0], grid.p[1], grid.val[0], grid.val[1]);

            if ((EdgeTable.LookupTable[cubeindex] & 2) > 0)
                vertlist[1] = VertexInterp(isolevel, grid.p[1], grid.p[2], grid.val[1], grid.val[2]);

            if ((EdgeTable.LookupTable[cubeindex] & 4) > 0)
                vertlist[2] = VertexInterp(isolevel, grid.p[2], grid.p[3], grid.val[2], grid.val[3]);

            if ((EdgeTable.LookupTable[cubeindex] & 8) > 0)
                vertlist[3] = VertexInterp(isolevel, grid.p[3], grid.p[0], grid.val[3], grid.val[0]);

            if ((EdgeTable.LookupTable[cubeindex] & 16) > 0)
                vertlist[4] = VertexInterp(isolevel, grid.p[4], grid.p[5], grid.val[4], grid.val[5]);

            if ((EdgeTable.LookupTable[cubeindex] & 32) > 0)
                vertlist[5] = VertexInterp(isolevel, grid.p[5], grid.p[6], grid.val[5], grid.val[6]);

            if ((EdgeTable.LookupTable[cubeindex] & 64) > 0)
                vertlist[6] = VertexInterp(isolevel, grid.p[6], grid.p[7], grid.val[6], grid.val[7]);

            if ((EdgeTable.LookupTable[cubeindex] & 128) > 0)
                vertlist[7] = VertexInterp(isolevel, grid.p[7], grid.p[4], grid.val[7], grid.val[4]);

            if ((EdgeTable.LookupTable[cubeindex] & 256) > 0)
                vertlist[8] = VertexInterp(isolevel, grid.p[0], grid.p[4], grid.val[0], grid.val[4]);

            if ((EdgeTable.LookupTable[cubeindex] & 512) > 0)
                vertlist[9] = VertexInterp(isolevel, grid.p[1], grid.p[5], grid.val[1], grid.val[5]);

            if ((EdgeTable.LookupTable[cubeindex] & 1024) > 0)
                vertlist[10] = VertexInterp(isolevel, grid.p[2], grid.p[6], grid.val[2], grid.val[6]);

            if ((EdgeTable.LookupTable[cubeindex] & 2048) > 0)
                vertlist[11] = VertexInterp(isolevel, grid.p[3], grid.p[7], grid.val[3], grid.val[7]);

            // Create the triangle 
            for (int i = 0; TriTable.LookupTable[cubeindex, i] != -1; i += 3)
            {
                Triangle aTriangle = new Triangle();

                aTriangle.p[0] = vertlist[TriTable.LookupTable[cubeindex, i]];
                aTriangle.p[1] = vertlist[TriTable.LookupTable[cubeindex, i + 1]];
                aTriangle.p[2] = vertlist[TriTable.LookupTable[cubeindex, i + 2]];

                theTriangleList.Add(aTriangle);
            }
        }

        public static Point3D VertexInterp(double isolevel, Point3D p1, Point3D p2, double valp1, double valp2)
        {
            if (Math.Abs(isolevel-valp1) < 0.00001)
                return(p1);
   
            if (Math.Abs(isolevel-valp2) < 0.00001)
                return(p2);
   
            if (Math.Abs(valp1-valp2) < 0.00001)
                return(p1);

            Point3D p = new Point3D();

            double mu = (isolevel - valp1) / (valp2 - valp1);
            
            p.X = p1.X + mu * (p2.X - p1.X);
            p.Y = p1.Y + mu * (p2.Y - p1.Y);
            p.Z = p1.Z + mu * (p2.Z - p1.Z);

            return p;
        }
    }
}
