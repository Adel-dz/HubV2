using System;
using static System.Diagnostics.Debug;



namespace easyLib
{
    public sealed class AutoReleaser : IDisposable
    {
        readonly Action m_releaser;


        public AutoReleaser(Action releaser)
        {
            Assert(releaser != null);

            m_releaser = releaser;
        }


        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                m_releaser();
                IsDisposed = true;
            }
        }
    }
}
