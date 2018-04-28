using System.Collections.Generic;

namespace easyLib.DB
{
    /*
     * Version: 1
     */ 
    public interface IDatumSetter<T>
    {
        bool CanWrite { get; }   //nothrow
        bool AutoFlush { get; set; }    //nothrow

        /* Pre:
         * - CanWrite
         * - item != null
         */
        void Insert(T item);

        /* Pre:
         * - CanWrite
         * - items != null
         * - !items.Any( x => x == null)
         */
        void Insert(IList<T> items);

        /* Pre:
         * - CanWrite
         * - ndx >= 0 && ndx < Count
         * item != null
         */
        void Replace(int ndx , T item);

        /* Pre:
         * - CanWrite
         * - ndx >= 0 && ndx < Count
         */
        void Delete(int ndx);

        /* Pre:
         * - CanWrite
         * - indices != null
         * - !indices.Any(ndx => ndx <= 0 || ndx >= Count)
         */
        void Delete(IList<int> indices);
    }
}
