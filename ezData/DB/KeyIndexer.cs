using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    /*
     * Version: 1
     */
    public interface IKeyIndexer<T>: ILockable
    {
        bool IsConnected { get; }   //nothrow
        int ConnectionsCount { get; }   //nothrow
        IDatumAccessor<T> Source { get; }   //nothrow

        /* Pre:
         * - IsConnected
         */
        IEnumerable<uint> Keys { get; } //nothrow

        /* Pre:
         * - IsConnected
         */
        T this[uint datumID] { get; }

        /* Pre:
         * - IsConnected
         */
        T Get(uint datumID);

        /* Pre:
         * - IsConnected
         */
        int IndexOf(uint datumID);  //nothrow
    }
    //------------------------------------------------------


    /*
     * Version: 1
     */ 
    public interface IKeyIndexer: IKeyIndexer<IDatum>, IDisposable
    {
        event Action<IDatum> DatumInserted;
        event Action<IList<IDatum>> DataInserted;
        event Action<IDatum> DatumDeleted;
        event Action<IList<IDatum>> DataDeleted;
        event Action<IDatum> DatumReplaced;
        event Action Invalidated;

        /* Post:
         * - IsConnected == true
         */
        void Connect();

        void Disconnect();
    }
    //--------------------------------------------------------------------

    /*
     * Version: 1
     */ 
    public class KeyIndexer: IKeyIndexer
    {
        readonly IDatumProvider m_source;
        readonly object m_lock = new object();
        readonly Dictionary<uint , int> m_ndxTable = new Dictionary<uint , int>();
        readonly Dictionary<int , IDatum> m_cache = new Dictionary<int , IDatum>();
        int m_refCount;


        public event Action<IList<IDatum>> DataDeleted;
        public event Action<IList<IDatum>> DataInserted;
        public event Action<IDatum> DatumDeleted;
        public event Action<IDatum> DatumInserted;
        public event Action<IDatum> DatumReplaced;
        public event Action Invalidated;


        public KeyIndexer(IDatumProvider source)    //nothrow
        {
            Assert(source != null);

            m_source = source;
        }

        public IDatum this[uint datumID] => Get(datumID);

        public bool IsConnected => m_refCount > 0;   //nothrow
        public int ConnectionsCount => m_refCount;
        public IDatumAccessor<IDatum> Source => m_source;   //nothrow

        public IEnumerable<uint> Keys   //nothrow
        {
            get
            {
                Assert(IsConnected);

                lock(m_lock)
                return m_ndxTable.Keys;
            }
        }


        public void Connect()
        {
            IDisposable unlocker = Lock();

            try
            {
                if (!IsConnected)
                {
                    m_source.Connect();
                    LoadData();
                    RegisterHandlers();
                }

                ++m_refCount;
            }
            catch
            {
                m_ndxTable.Clear();

                throw;
            }
            finally
            {
                unlocker.Dispose();
            }

            Assert(IsConnected);
        }

        public void Disconnect()
        {
            using (Lock())
                if (IsConnected)
                {
                    if (m_refCount == 1)
                    {
                        m_source.Disconnect();
                        UnregisterHandlers();
                        m_ndxTable.Clear();
                        m_cache.Clear();
                    }

                    --m_refCount;
                }
        }

        public void Dispose()   //nothrow
        {
            try
            {
                Disconnect();
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception shut down in KeyIndexer.Dispose: {ex.Message}");
            }
        }

        public IDatum Get(uint datumID)
        {
            Assert(IsConnected);

            using (Lock())
            {
                int ndx = IndexOf(datumID);
                return ndx < 0 ? null : m_source.Get(ndx);
            }
        }

        public int IndexOf(uint datumID)    //nothrow
        {
            Assert(IsConnected);

            using (Lock())
            {
                int ndx;

                if (!m_ndxTable.TryGetValue(datumID , out ndx))
                    ndx = -1;

                return ndx;
            }
        }

        public IDisposable Lock()   //nothrow
        {
            Monitor.Enter(m_lock);
            IDisposable srcLock = m_source.Lock();

            return new AutoReleaser(() => Unlock(srcLock));
        }

        public IDisposable TryLock()    //nothrow
        {
            if (Monitor.TryEnter(m_lock))
            {
                IDisposable srcLock = m_source.TryLock();

                if (srcLock != null)
                    return new AutoReleaser(() => Unlock(srcLock));

                Monitor.Exit(m_lock);
            }

            return null;
        }


        //private:
        void Unlock(IDisposable unlocker)
        {
            unlocker.Dispose();
            Monitor.Exit(m_lock);
        }

        void LoadData()
        {
            for (int ndx = 0; ndx < m_source.Count; ++ndx)
                m_ndxTable.Add(m_source.Get(ndx).ID , ndx);
        }

        void RegisterHandlers() //nothrow
        {
            m_source.DataDeleted += Source_DataDeleted;
            m_source.DataDeleting += Source_DataDeleting;
            m_source.DataInserted += Source_DataInserted;
            m_source.DatumDeleted += Source_DatumDeleted;
            m_source.DatumDeleting += Source_DatumDeleting;
            m_source.DatumInserted += Source_DatumInserted;
            m_source.DatumReplaced += Source_DatumReplaced;
            m_source.DatumReplacing += Source_DatumReplacing;
            m_source.Invalidated += Source_Invalidated;
        }

        void UnregisterHandlers()   //nothrow
        {
            m_source.DataDeleted -= Source_DataDeleted;
            m_source.DataDeleting -= Source_DataDeleting;
            m_source.DataInserted -= Source_DataInserted;
            m_source.DatumDeleted -= Source_DatumDeleted;
            m_source.DatumDeleting -= Source_DatumDeleting;
            m_source.DatumInserted -= Source_DatumInserted;
            m_source.DatumReplaced -= Source_DatumReplaced;
            m_source.DatumReplacing -= Source_DatumReplacing;
            m_source.Invalidated -= Source_Invalidated;
        }


        //handlers:
        private void Source_Invalidated()
        {
            lock(m_lock)
            {
                m_ndxTable.Clear();
                m_cache.Clear();

                LoadData();
            }
        }

        private void Source_DatumReplacing(int ndx , IDatum datum)
        {
            Monitor.Enter(m_lock);

            m_cache.Add(ndx , datum);

            Monitor.Exit(m_lock);
        }

        private void Source_DatumReplaced(int ndx , IDatum datum)
        {
            Assert(m_ndxTable.ContainsKey(datum.ID));
            Assert(m_ndxTable[datum.ID] == ndx);
            Assert(m_cache.ContainsKey(ndx));

            //IDatum.ID is immutable => juste remove from cache

            Monitor.Enter(m_lock);
            m_cache.Remove(ndx);
            Monitor.Exit(m_lock);

            DatumReplaced?.Invoke(datum);
        }

        private void Source_DatumInserted(int ndx , IDatum datum)
        {
            Assert(!m_ndxTable.ContainsKey(datum.ID));

            Monitor.Enter(m_lock);

            var keys = from kv in m_ndxTable
                       where kv.Value >= ndx
                       select kv.Key;

            keys.ToList().ForEach(k => ++m_ndxTable[k]);

            m_ndxTable.Add(datum.ID , ndx);

            Monitor.Exit(m_lock);

            DatumInserted?.Invoke(datum);
        }

        private void Source_DatumDeleting(int ndx)
        {
            lock (m_lock)
            {
                IDatum datum = m_source.Get(ndx);
                m_cache.Add(ndx , datum);
            }
        }

        private void Source_DatumDeleted(int ndx)
        {
            Assert(m_cache.ContainsKey(ndx));

            Monitor.Enter(m_lock);

            IDatum datum = m_cache[ndx];
            m_cache.Remove(ndx);
            m_ndxTable.Remove(datum.ID);

            var keys = from kv in m_ndxTable
                       where kv.Value > ndx
                       select kv.Key;

            keys.ToList().ForEach(k => --m_ndxTable[k]);

            Monitor.Exit(m_lock);

            DatumDeleted?.Invoke(datum);
        }

        private void Source_DataInserted(IList<int> indices , IList<IDatum> data)
        {
            Monitor.Enter(m_lock);

            for (int i = 0; i < indices.Count; ++i) //le traitement en une seule passe est invalide.
            {
                IDatum datum = data[i];

                Assert(!m_ndxTable.ContainsKey(datum.ID));

                int ndx = indices[i];
                var keys = from kv in m_ndxTable
                           where kv.Value >= ndx
                           select kv.Key;

                keys.ToList().ForEach(k => ++m_ndxTable[k]);

                m_ndxTable.Add(datum.ID , ndx);
            }

            Monitor.Exit(m_lock);

            DataInserted?.Invoke(data);
        }

        private void Source_DataDeleting(IList<int> indices)
        {
            lock (m_lock)
                for (int i = 0; i < indices.Count; ++i)
                {
                    int ndx = indices[i];
                    IDatum datum = m_source.Get(ndx);

                    m_cache.Add(ndx , datum);
                }
        }

        private void Source_DataDeleted(IList<int> indices)
        {
            var data = new IDatum[indices.Count];

            Monitor.Enter(m_lock);

            for (int i = 0; i < indices.Count; ++i)
            {
                int ndx = indices[i];

                Assert(m_cache.ContainsKey(ndx));

                IDatum datum = m_cache[ndx];
                m_cache.Remove(ndx);
                m_ndxTable.Remove(datum.ID);

                data[i] = datum;

                var keys = from kv in m_ndxTable
                           where kv.Value > ndx
                           select kv.Key;

                keys.ToList().ForEach(k => --m_ndxTable[k]);
            }

            Monitor.Exit(m_lock);

            DataDeleted?.Invoke(data);
        }
    }
}
