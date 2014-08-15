using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace DICOMViewer.Helper
{
    sealed public class MooreContour
    {
        /// <summary>
        /// For any given key point, the point that is clockwise to it in a moore-neighborhood
        /// </summary>

        private static readonly Dictionary<Point, Point> clockwiseOffset = new Dictionary<Point, Point>()
        {
             {new Point(1,0), new Point(1,-1) },    // right        => down-right
             {new Point(1,-1), new Point(0,-1)},    // down-right   => down
             {new Point(0,-1), new Point(-1,-1)},   // down         => down-left
             {new Point(-1,-1), new Point(-1,0)},   // down-left    => left
             {new Point(-1,0), new Point(-1,1)},    // left         => top-left
             {new Point(-1,1), new Point(0,1)},     // top-left     => top
             {new Point(0,1), new Point(1,1)},      // top          => top-right
             {new Point(1,1), new Point(1,0)}       // top-right    => right
        };

        /// <summary>
        /// returns all the points that make up the outline of a two dimensional black and white image as represented by a bool[,]
        ///
        /// Pseudo code for Moore-Neighborhood
        /// retrieved from http://en.wikipedia.org/wiki/Moore_neighborhood
        /// Begin
        ///     Set B to be empty.
        ///     From bottom to top and left to right scan the cells of T until a black pixel, s, of P is found.
        ///     Insert s in B.
        ///     Set the current boundary point p to s i.e. p=s
        ///     b = the pixel from which s was entered during the image scan.
        ///     Set c to be the next clockwise pixel (from b) in M(p).
        ///     While c not equal to s do
        ///     If c is black
        ///         insert c in B
        ///         b = p
        ///         p = c
        ///         (backtrack: move the current pixel c to the pixel from which p was entered)
        ///         c = next clockwise pixel (from b) in M(p).
        ///     else
        ///         (advance the current pixel c to the next clockwise pixel in M(p) and update backtrack)
        ///         b = c
        ///         c = next clockwise pixel (from b) in M(p).
        ///     end While
        /// End
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static Point[] Trace(bool[,] image, int nr, int nc)
        {
            HashSet<Point> outline = new HashSet<Point>();

            Point   prev,       // The point we entered curr from
                    curr,       // The point currently being inspected
                    boundary,   // current know black pixel we're finding neighbours of
                    first,      // the first black pxiel found
                    firstPrev;  // the point we entered first from

            // find the fist black pixel, searching from bottom-left to top-right
            for (int r = nr - 1; r >= 0; --r)
            {
                firstPrev = new Point(0, r - 1);
                for (int c = 0; c != nc; ++c)
                {
                    // is black then move on
                    if (image[r, c])
                    {
                        first = new Point(c, r);
                        goto FoundFirstPixel;
                    }
                    firstPrev = new Point(c, r);
                }
            }

            // Couldn't find any black pixels
            return outline.ToArray();

            FoundFirstPixel:
            prev = firstPrev;
            outline.Add(first);
            boundary = first;

            curr = Clockwise(boundary, prev);

            // Jacob's stopping criterion:
            // stop only when we enter the original pixel in the same way we entered it
            while (curr != first || prev != firstPrev)
            {
                // if the current pixel is black
                // then add it to the outline
                if (curr.Y >= 0 && curr.X >= 0 &&
                    curr.Y < nr && curr.X < nc &&
                    image[curr.Y, curr.X])
                {
                    outline.Add(curr);
                    prev = boundary;
                    boundary = curr;
                    curr = Clockwise(boundary, prev);
                }
                else
                {
                    prev = curr;
                    curr = Clockwise(boundary, prev);
                }
            }
            return outline.ToArray();
        }

        private static Point Clockwise(Point target, Point prev)
        {
            Point offset = clockwiseOffset[new Point(prev.X - target.X, prev.Y - target.Y)];
            return new Point(offset.X + target.X, offset.Y + target.Y);
        }
    }
}
