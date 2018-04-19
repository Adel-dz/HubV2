using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    public interface IAttrIndexer<TAttr, TDatum>
    {
        IDatumAccessor<TDatum> Source { get; }
        IEnumerable<TDatum> this[TAttr attr] { get; }
        IEnumerable<TAttr> Attributes { get; }

        IEnumerable<TDatum> Get(TAttr attr);
        IEnumerable<int> IndexOf(TAttr attr);
    }



    public interface IAttrIndexer<T>: IAttrIndexer<T , IDatum>, IDisposable
    {
        event Action<IDatum> DatumInserted;
        event Action<IDatum[]> DataInserted;
        event Action<IDatum[]> DataDeleted;
        event Action<IDatum> DatumDeleted;
        event Action<IDatum> DatumReplaced;
        event Action Reset;


        bool IsDisposed { get; }
        bool IsConnected { get; }

        void Connect();
        void Disconnect();
        IDisposable Lock();
        IDisposable TryLock();
    }



    public sealed class AttrIndexer<T>: IAttrIndexer<T>
    {
        readonly object m_lock = new object();
        readonly Dictionary<T , List<int>> m_dataIndices;//mapping attr -> list of datum ndxs
        readonly List<Tuple<int , IDatum>> m_cache = new List<Tuple<int , IDatum>>();
        readonly Func<IDatum , T> m_selector;
        readonly IDatumProvider m_dataProvider;

        public event Action<IDatum[]> DataDeleted;
        public event Action<IDatum[]> DataInserted;
        public event Action<IDatum> DatumDeleted;
        public event Action<IDatum> DatumInserted;
        public event Action<IDatum> DatumReplaced;
        public event Action Reset;


        public AttrIndexer(IDatumProvider provider , Func<IDatum , T> selector , IEqualityComparer<T> comparer)
        {
            Assert(provider != null);
            Assert(selector != null);
            Assert(comparer != null);

            m_selector = selector;
            m_dataProvider = provider;
            m_dataIndices = new Dictionary<T , List<int>>(comparer);
        }

        public AttrIndexer(IDatumProvider provider , Func<IDatum , T> selector) :
            this(provider , selector , EqualityComparer<T>.Default)
        { }


        public IEnumerable<IDatum> this[T attr] => Get(attr);
        public bool IsConnected { get; private set; }
        public bool IsDisposed { get; private set; }
        public IDatumAccessor<IDatum> Source => m_dataProvider;
        public int ConnectionsCount { get; private set; }

        public IEnumerable<T> Attributes
        {
            get
            {
                Assert(IsConnected);

                return m_dataIndices.Keys;
            }
        }


        public void Connect()
        {
            Assert(!IsDisposed);

            lock (m_lock)
            {
                if (++ConnectionsCount == 1)
                {
                    m_dataProvider.Connect();
                    LoadData();
                    RegisterHandlers();

                    IsConnected = true;
                }
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

        public IEnumerable<IDatum> Get(T attr)
        {
            Assert(IsConnected);
            Assert(attr != null);

            List<int> locs;

            Monitor.Enter(m_lock);
            m_dataIndices.TryGetValue(attr , out locs);
            Monitor.Exit(m_lock);

            return locs == null ? Enumerable.Empty<IDatum>() : YieldData(locs);
        }

        public IEnumerable<int> IndexOf(T attr)
        {
            Assert(IsConnected);
            Assert(attr != null);

            List<int> locs;

            Monitor.Enter(m_lock);
            m_dataIndices.TryGetValue(attr , out locs);
            Monitor.Exit(m_lock);

            return locs ?? Enumerable.Empty<int>();
        }

        public IDisposable Lock()
        {
            Monitor.Enter(m_lock);
            IDisposable dpUnlocker = m_dataProvider.Lock();

            return new AutoReleaser(() => Unlock(dpUnlocker));
        }

        public IDisposable TryLock()
        {
            if (Monitor.TryEnter(m_lock))
            {
                IDisposable dpUnlocker = m_dataProvider.Lock();

                if (dpUnlocker != null)
                    return new AutoReleaser(() => Unlock(dpUnlocker));

                Monitor.Exit(m_lock);
            }

            return null;
        }


        //private:
        void Close(bool closeAll)
        {
            if (ConnectionsCount > 0)
            {
                if (closeAll)
                    ConnectionsCount = 1;

                if (--ConnectionsCount == 0)
                {
                    UnregisterHandlers();
                    m_dataIndices.Clear();
                    m_cache.Clear();
                    m_dataProvider.Disconnect();
                    IsConnected = false;
                }

            }
        }

        void UnregisterHandlers()
        {
            m_dataProvider.DataDeleted -= DataProvider_DataDeleted;
            m_dataProvider.DataDeleting -= DataProvider_DataDeleting;
            m_dataProvider.DataInserted -= DataProvider_DataInserted;
            m_dataProvider.DatumDeleted -= DataProvider_DatumDeleted;
            m_dataProvider.DatumDeleting -= DataProvider_DatumDeleting;
            m_dataProvider.DatumInserted -= DataProvider_DatumInserted;
            m_dataProvider.DatumReplaced -= DataProvider_DatumReplaced;
            m_dataProvider.DatumReplacing -= DataProvider_DatumReplacing;
            m_dataProvider.Invalidated -= DataProvider_Invalidated;
        }

        void LoadData()
        {
            for (int i = 0; i < m_dataProvider.Count; ++i)
            {
                IDatum d = m_dataProvider.Get(i);
                T key = m_selector(d);

                List<int> lst;

                if (!m_dataIndices.TryGetValue(key , out lst))
                {
                    lst = new List<int>();
                    m_dataIndices[key] = lst;
                }

                lst.Add(i);
            }
        }

        IEnumerable<IDatum> YieldData(List<int> indices)
        {
            int ndx = 0;

            while (true)
            {
                IDatum datum;

                lock (m_lock)
                    datum = ndx < indices.Count ? m_dataProvider.Get(indices[ndx++]) : null;

                if (datum == null)
                    yield break;

                yield return datum;
            }
        }

        void RegisterHandlers()
        {
            m_dataProvider.DataDeleted += DataProvider_DataDeleted;
            m_dataProvider.DataDeleting += DataProvider_DataDeleting;
            m_dataProvider.DataInserted += DataProvider_DataInserted;
            m_dataProvider.DatumDeleted += DataProvider_DatumDeleted;
            m_dataProvider.DatumDeleting += DataProvider_DatumDeleting;
            m_dataProvider.DatumInserted += DataProvider_DatumInserted;
            m_dataProvider.DatumReplaced += DataProvider_DatumReplaced;
            m_dataProvider.DatumReplacing += DataProvider_DatumReplacing;
            m_dataProvider.Invalidated += DataProvider_Invalidated;
        }

        void Unlock(IDisposable dpUnlocker)
        {
            dpUnlocker.Dispose();
            Monitor.Exit(m_lock);
        }


        //handlers:
        private void DataProvider_DatumReplacing(int ndx , IDatum datum)
        {
            Monitor.Enter(m_lock);
            m_cache.Add(Tuple.Create(ndx , datum));
            Monitor.Exit(m_lock);
        }

        private void DataProvider_DatumReplaced(int ndx , IDatum datum)
        {
            IDatum oldDatum = null;

            Monitor.Enter(m_lock);
            for (int i = 0; i < m_cache.Count; ++i)
                if (m_cache[i].Item1 == ndx)
                {
                    oldDatum = m_cache[i].Item2;
                    m_cache.RemoveAt(i);
                    break;
                }

            Assert(oldDatum != null);

            T oldKey = m_selector(oldDatum);
            T newKey = m_selector(datum);

            if (!m_dataIndices.Comparer.Equals(oldKey , newKey))
                m_dataIndices[oldKey].Remove(ndx);

            List<int> lst;

            if (!m_dataIndices.TryGetValue(newKey , out lst))
            {
                lst = new List<int>();
                m_dataIndices[newKey] = lst;
            }

            lst.Add(ndx);

            Monitor.Exit(m_lock);

            DatumReplaced?.Invoke(datum);
        }

        private void DataProvider_DatumInserted(int ndx , IDatum datum)
        {
            T key = m_selector(datum);

            List<int> lst;

            Monitor.Enter(m_lock);
            if (!m_dataIndices.TryGetValue(key , out lst))
            {
                lst = new List<int>();
                m_dataIndices[key] = lst;
            }

            //datum inserted not at the end => adjust indicies
            if (ndx < m_dataProvider.Count - 1)
                foreach (List<int> l in m_dataIndices.Values)
                    for (int i = 0; i < lst.Count; ++i)
                        if (ndx <= l[i])
                            ++l[i];
            lst.Add(ndx);

            Monitor.Exit(m_lock);

            DatumInserted?.Invoke(datum);
        }

        private void DataProvider_DatumDeleting(int ndx)
        {
            IDatum datum = m_dataProvider.Get(ndx);

            Monitor.Enter(m_lock);
            m_cache.Add(Tuple.Create(ndx , datum));
            Monitor.Exit(m_lock);
        }

        private void DataProvider_DatumDeleted(int ndx)
        {
            IDatum datum = null;

            int i = 0;

            Monitor.Enter(m_lock);

            while (i < m_cache.Count)
            {
                int index = m_cache[i].Item1;

                if (ndx < index)
                {
                    IDatum d = m_cache[i].Item2;
                    m_cache[i] = Tuple.Create(index - 1 , d);
                    ++i;
                }
                else if (index == ndx)
                {
                    datum = m_cache[i].Item2;
                    m_cache.RemoveAt(i);
                }
            }


            Assert(datum != null);

            T key = m_selector(datum);
            m_dataIndices[key].Remove(ndx);

            //adjust indices
            foreach (List<int> lst in m_dataIndices.Values)
                for (int k = 0; k < lst.Count; ++k)
                    if (ndx < lst[k])
                        --lst[k];

            Monitor.Exit(m_lock);

            DatumDeleted?.Invoke(datum);
        }

        private void DataProvider_DataInserted(int[] indices , IDatum[] data)
        {
            Monitor.Enter(m_lock);

            Assert(indices.Length == data.Length);

            foreach (int ndx in indices)
            {
                IDatum datum = data[ndx];
                T key = m_selector(datum);

                List<int> lst;

                if (!m_dataIndices.TryGetValue(key , out lst))
                {
                    lst = new List<int>();
                    m_dataIndices[key] = lst;
                }

                //datum inserted not at the end => adjust indicies
                if (ndx < m_dataProvider.Count - 1)
                    foreach (List<int> l in m_dataIndices.Values)
                        for (int i = 0; i < lst.Count; ++i)
                            if (ndx <= l[i])
                                ++l[i];
                lst.Add(ndx);
            }

            Monitor.Exit(m_lock);

            DataInserted?.Invoke(data);
        }

        private void DataProvider_DataDeleting(int[] indices)
        {
            Monitor.Enter(m_lock);

            for (int i = 0; i < indices.Length; ++i)
            {
                int ndx = indices[i];
                IDatum datum = m_dataProvider.Get(ndx);

                m_cache.Add(Tuple.Create(ndx , datum));
            }

            Monitor.Exit(m_lock);
        }

        private void DataProvider_DataDeleted(int[] Indices)
        {
            Monitor.Enter(m_lock);
            var data = new IDatum[Indices.Length];

            foreach (int ndx in Indices)
            {
                IDatum datum = null;
                int i = 0;

                while (i < m_cache.Count)
                {
                    int index = m_cache[i].Item1;

                    if (ndx < index)
                    {
                        IDatum d = m_cache[i].Item2;
                        m_cache[i] = Tuple.Create(index - 1 , d);
                        ++i;
                    }
                    else if (index == ndx)
                    {
                        datum = m_cache[i].Item2;
                        m_cache.RemoveAt(i);
                    }
                }


                Assert(datum != null);
                data[ndx] = datum;

                T key = m_selector(datum);
                m_dataIndices[key].Remove(ndx);

                //adjust indices
                foreach (List<int> lst in m_dataIndices.Values)
                    for (int k = 0; k < lst.Count; ++k)
                        if (ndx < lst[k])
                            --lst[k];
            }

            Monitor.Exit(m_lock);

            DataDeleted?.Invoke(data);
        }

        private void DataProvider_Invalidated()
        {
            Monitor.Enter(m_lock);
            Close(true);
            Monitor.Exit(m_lock);

            Reset?.Invoke();
        }
    }

}
