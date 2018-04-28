using System;
using static System.Diagnostics.Debug;



namespace easyLib
{
    /*
     * Version: 1
     * Assume the releaser method is a nothrow one
     */ 
    public sealed class AutoReleaser : IDisposable
    {
        readonly Action m_releaser;


        public AutoReleaser(Action releaser)    //nothrow
        {
            Assert(releaser != null);

            m_releaser = releaser;
        }


        public bool IsDisposed { get; private set; }    //nothrow

        public void Dispose()   //nothrow
        {
            if (!IsDisposed)
            {
                m_releaser();
                IsDisposed = true;
            }
        }
    }
}
