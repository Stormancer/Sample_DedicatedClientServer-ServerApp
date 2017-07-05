using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    internal static class StringExtensions
    {
        public static string With(this string format, params object[] p)
        {
            return string.Format(format, p);
        }
    }
}
