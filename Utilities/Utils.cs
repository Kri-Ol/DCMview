using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utilities
{
    public sealed class Utils
    {
        public static float squared(float x)
        {
            return x * x;
        }

        public static double squared(double x)
        {
            return x * x;
        }

        public static float cubed(float x)
        {
            return x * x * x;
        }

        public static double cubed(double x)
        {
            return x * x * x;
        }
    }
}
