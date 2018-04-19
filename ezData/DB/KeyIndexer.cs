using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{

    public interface IKeyIndexer<T>
    {
        IDatumAccessor<T> Source { get; }
        IEnumerable<uint> Keys { get; }
        T this[uint datumID] { get; }

        T Get(uint datumID);
        int IndexOf(uint datumID);
    }



    public interface IKeyIndexer: IKeyIndexer<IDatum>, IDisposable
    {
        event Action<IDatum> DatumInserted;
        event Action<IDatum[]> DataInserted;
        event Action<IDatum> DatumDeleted;
        event Action<IDatum[]> DataDeleted;
        event Action<IDatum> DatumReplaced;
        event Action Reset;


        bool IsDisposed { get; }
        bool IsConnected { get; }

        void Connect();
        void Disconnect();
        IDisposable Lock();
        IDisposable TryLock();
    }



    public class KeyIndexer: IKeyIndexer
    {
        readonly IDatumProvider m_source;
        readonly object m_lock = new object();
        readonly Dictionary<uint , int> m_ndxTable = new Dictionary<uint , int>();
        readonly Dictionary<int , IDatum> m_cache = new Dictionary<int , IDatum>();


        public event Action<IDatum[]> DataDeleted;
        public event Action<IDatum[]> DataInserted;
        public event Action<IDatum> DatumDeleted;
        public event Action<IDatum> DatumInserted;
        public event Action<IDatum> DatumReplaced;
        public event Action Reset;


        public KeyIndexer(IDatumProvider source)
        {
            Assert(source != null);

            m_source = source;
        }

        public IDatum this[uint datumID] => Get(datumID);

        public bool IsConnected { get; private set; }
        public bool IsDisposed { get; private set; }
        public IDatumAccessor<IDatum> Source => m_source;
        public int ConnectionsCount { get; private set; }

        public IEnumerable<uint> Keys
        {
            get
            {
                Assert(IsConnected);

                return m_ndxTable.Keys;
            }
        }


        public void Connect()
        {
            Assert(!IsDisposed);

            lock (m_lock)
            {
                if (!IsConnected)
                {
                    Assert(ConnectionsCount == 0);

                    m_source.Connect();
                    Load();
                    RegisterHandlers();

                    IsConnected = true;
                }

                ++ConnectionsCount;
            }
        }

        public void Disconnect()
        {
            Assert(!IsDisposed);

            Monitor.Enter(m_lock);

            if (IsConnected)
            {
                Close(false);

                if (ConnectionsCount == 0)
                    Dispose();
            }

            Monitor.Exit(m_lock);
        }

        public void Dispose()
        {
            Monitor.Enter(m_lock);

            if (!IsDisposed)
            {
                if (IsConnected)
                    Close(true);

                DatumDeleted = DatumInserted = DatumReplaced = null;
                DataDeleted = DataInserted = null;
                Reset = null;

                IsDisposed = true;
            }

            Monitor.Exit(m_lock);
        }

        public IDatum Get(uint datumID)
        {
            Assert(IsConnected);

            int ndx = IndexOf(datumID);
            return ndx < 0 ? null : m_source.Get(ndx);
        }

        public int IndexOf(uint datumID)
        {
            Assert(IsConnected);

            Monitor.Enter(m_lock);

            int ndx;

            if (!m_ndxTable.TryGetValue(datumID , out ndx))
                ndx = -1;

            Monitor.Exit(m_lock);

            return ndx;
        }

        public IDisposable Lock()
        {
            Monitor.Enter(m_lock);
            IDisposable srcLock = m_source.Lock();

            return new AutoReleaser(() => Unlock(srcLock));
        }

        public IDisposable TryLock()
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

        void Load()
        {
            for (int ndx = 0; ndx < m_source.Count; ++ndx)
                m_ndxTable.Add(m_source.Get(ndx).ID , ndx);
        }

        void RegisterHandlers()
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

        void Close(bool closeAll)
        {
            if (ConnectionsCount > 0)
            {
                if (closeAll)
                    ConnectionsCount = 1;

                if (--ConnectionsCount == 0)
                {
                    UnregisterHandlers();
                    m_ndxTable.Clear();
                    m_cache.Clear();
                    m_source.Disconnect();
                    IsConnected = false;
                }
            }
        }

        void UnregisterHandlers()
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
            lock (m_lock)
            {
                m_ndxTable.Clear();
                m_cache.Clear();

                Load();
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

        private void Source_DataInserted(int[] indices , IDatum[] data)
        {
            Monitor.Enter(m_lock);

            for (int i = 0; i < indices.Length; ++i) //le traitement en une seule passe est invalide.
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

        private void Source_DataDeleting(int[] indices)
        {
            lock (m_lock)
                for (int i = 0; i < indices.Length; ++i)
                {
                    int ndx = indices[i];
                    IDatum datum = m_source.Get(ndx);

                    m_cache.Add(ndx , datum);
                }
        }

        private void Source_DataDeleted(int[] indices)
        {
            var data = new IDatum[indices.Length];

            Monitor.Enter(m_lock);

            for (int i = 0; i < indices.Length; ++i)
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
