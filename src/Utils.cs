using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShowDamageNumber
{
    public static class Utils
    {

        static int seed = Environment.TickCount;

        public static readonly ThreadLocal<System.Random> randSeed =
            new ThreadLocal<System.Random>(() => new System.Random(Interlocked.Increment(ref seed)));
    }
}
