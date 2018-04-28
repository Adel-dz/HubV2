using System;
using static System.Diagnostics.Debug;



namespace easyLib
{
    /*
     * Version: 1
     * 
     * Fire an event at specified intervals. The delegate attached to the event
     * is invoked on new thread from the threads pool.
     */
    public sealed class Timer: IDisposable
    {
        readonly System.Threading.Timer m_timer;
        int m_interval;

        public event Action TimeElapsed;


        public Timer(int msInterval)    //nothrow
        {
            Assert(msInterval >= 0);

            m_timer = new System.Threading.Timer(ProcessTimer ,
                null ,
                System.Threading.Timeout.Infinite ,
                System.Threading.Timeout.Infinite);

            m_interval = msInterval;
        }


        public bool IsDisposed { get; private set; }    //nothrow
        public bool IsRunning { get; private set; } //nothrow

        public int Interval //nothrow
        {
            get
            {
                Assert(!IsDisposed);

                return m_interval;
            }

            set
            {
                Assert(!IsDisposed);

                lock (m_timer)
                    if (m_timer.Change(IsRunning ? value : System.Threading.Timeout.Infinite , value))
                        m_interval = value;
            }
        }

        public void Start(bool startNow = false)    //nothrow
        {
            Assert(!IsDisposed);
            Assert(!IsRunning);

            lock (m_timer)
                IsRunning = m_timer.Change(startNow ? 0 : m_interval , m_interval);

            Assert(IsRunning);
        }

        public void Stop()  //nothrow
        {
            Assert(!IsDisposed);

            lock (m_timer)
                if (IsRunning)
                    IsRunning = !m_timer.Change(System.Threading.Timeout.Infinite , System.Threading.Timeout.Infinite);

            Assert(!IsRunning);
        }

        public void Dispose()   //nothrow
        {
            lock (m_timer)
                if (!IsDisposed)
                {
                    TimeElapsed = null;
                    m_timer.Change(System.Threading.Timeout.Infinite , System.Threading.Timeout.Infinite);

                    IsRunning = false;

                    m_timer.Dispose();
                    IsDisposed = true;
                }

            Assert(IsDisposed);
        }


        //private:
        void ProcessTimer(object unused) => TimeElapsed?.Invoke();
    }
}
