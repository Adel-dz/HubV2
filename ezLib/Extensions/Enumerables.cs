using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static System.Diagnostics.Debug;


namespace easyLib.Extensions
{
    public static class Enumerables
    {
        public static IEnumerable<T> AsEnumerable<T>(this IEnumerable seq)
        {
            Assert(seq != null);

            foreach (T item in seq)
                yield return item;
        }

        public static IEnumerable<T> Add<T>(this IEnumerable<T> seq , params T[] items) => 
            seq.Concat(items);
    }
}
