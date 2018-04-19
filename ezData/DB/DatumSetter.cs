namespace easyLib.DB
{
    public interface IDatumSetter<in T>
    {
        bool AutoFlush { get; set; }
        uint DataVersion { get; set; }

        void Insert(T item);
        void Insert(T[] items);
        void Replace(int ndx , T item);
        void Delete(int ndx);
        void Delete(int[] indices);
    }
}
