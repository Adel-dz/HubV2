using System;

namespace easyLib.DB
{
    public interface IDataSource<T>: IDisposable
    {
        uint ID { get; }
        uint Version { get; }
        DateTime LastAccessTime { get; }
        DateTime CreationTime { get; }
        DateTime LastWriteTime { get; }
        IDatumProvider DataProvider { get; }
        bool IsDisposed { get; }
        int DataCount { get; }
    }
}
