using System;

namespace easyLib.DB
{
    /*
     * Version: 1
     */ 
    public interface IDataSourceInfo
    {
        uint DataVersion { get; }   //nothrow
        DateTime LastAccessTime { get; }    //nothrow
        DateTime CreationTime { get; }  //nothrow
        DateTime LastWriteTime { get; } //nothrow
    }
}
