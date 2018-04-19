using System;
using System.Collections.Generic;

namespace easyLib.DB
{
    public interface IDatumGetter<out T>
    {
        int Count { get; }
        T Get(int ndx);
        T[] Get(int[] indices);

        IEnumerable<T> Enumerate(int ndxFirst);
        IEnumerable<T> Enumerate();
    }
}
