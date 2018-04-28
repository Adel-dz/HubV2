using System;

namespace easyLib
{
    /*
     * Version: 1
     */ 
    public interface ILockable
    {
        IDisposable Lock(); //nothrow
        IDisposable TryLock();  //nothrow
    }
}
