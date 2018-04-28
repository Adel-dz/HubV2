using System;

namespace easyLib.DB
{
    public interface IDataSourceInfo
    {
        uint DataVersion { get; }
        DateTime LastAccessTime { get; }
        DateTime CreationTime { get; }
        DateTime LastWriteTime { get; }
    }
}
