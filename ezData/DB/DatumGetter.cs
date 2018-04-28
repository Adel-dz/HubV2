using System.Collections.Generic;

namespace easyLib.DB
{
    /*
     * Version: 1
     */ 
    public interface IDatumGetter<T>: ILockable
    {
        bool CanRead { get; }   //nothrow

        /* Pre: 
         * - CanRead
         */
        int Count { get; }  //nothrow

        /* Pre:
         * - CanRead
         * - ndx >= 0 && ndx < Count
         */
        T Get(int ndx);

        /* Pre:
         * - CanRead
         * - indices != null
         * - indices.Any(ndx => ndx < 0 || ndx >= Count) == false
         */
        IList<T> Get(IList<int> indices);

        /* Pre:
         * - CanRead
         * - ndxFirst >= 0 && ndxFirst < Count
         */
        IEnumerable<T> Enumerate(int ndxFirst);

        /* Pre:
         * - CanRead
         * 
         * Post: 
         * - Result != null
         */
        IEnumerable<T> Enumerate();
    }
}
