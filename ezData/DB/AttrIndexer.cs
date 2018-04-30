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
    public interface IAttrIndexer<TAttr, TDatum>: ILockable
    {
        bool IsConnected { get; }   //nothrow
        int ConnectionsCount { get; }   //nothrow;

        IDatumAccessor<TDatum> Source { get; }

        /* Pre:         
         * - attr != null
         * - IsConnected
         * 
         * Post:
         * - Result != null
         */
        IEnumerable<TDatum> this[TAttr attr] { get; }

        /* Pre:
         * - IsConnected
         * 
         * Post:
        *  - Result != null
        */
        IEnumerable<TAttr> Attributes { get; }  //nothrow

        /* Pre:
         * - attr != null
         * - IsConnected
         * 
         * Post:
         * - Result != null
         */
        IEnumerable<TDatum> Get(TAttr attr);

        /* Pre:
         * - attr != null
         * - IsConnected
         * 
         * Post:
         * - Result != null
         */
        IEnumerable<int> IndexOf(TAttr attr);   //nothrow
    }
    //---------------------------------------------------

    /*
     * Version: 1
     */
    public interface IAttrIndexer<T>: IAttrIndexer<T , IDatum>
    {
        event Action<IDatum> DatumInserted;
        event Action<IList<IDatum>> DataInserted;
        event Action<IList<IDatum>> DataDeleted;
        event Action<IDatum> DatumDeleted;
        event Action<IDatum> DatumReplaced;
        event Action Invalidated;

        /* Post:
         * - IsConnected
         */
        void Connect();

        void Disconnect();
    }
    //--------------------------------------------------------------

    /*
     * Version: 1
     */
    public sealed class AttrIndexer<T>: IAttrIndexer<T>, IDisposable
    {
        readonly object m_lock = new object();
        readonly Dictionary<T , List<int>> m_dataIndices;//mapping attr -> list of datum ndxs
        readonly List<Tuple<int , IDatum>> m_cache = new List<Tuple<int , IDatum>>();
        readonly Func<IDatum , T> m_selector;
        readonly IDatumProvider m_dataProvider;
        int m_refCount;

        public event Action<IList<IDatum>> DataDeleted;
        public event Action<IList<IDatum>> DataInserted;
        public event Action<IDatum> DatumDeleted;
        public event Action<IDatum> DatumInserted;
        public event Action<IDatum> DatumReplaced;
        public event Action Invalidated;


        public AttrIndexer(IDatumProvider provider , Func<IDatum , T> selector ,    //noththrow
            IEqualityComparer<T> comparer)
        {
            Assert(provider != null);
            Assert(selector != null);
            Assert(comparer != null);

            m_selector = selector;
            m_dataProvider = provider;
            m_dataIndices = new Dictionary<T , List<int>>(comparer);
        }

        public AttrIndexer(IDatumProvider provider , Func<IDatum , T> selector) :   //nothrow
            this(provider , selector , EqualityComparer<T>.Default)
        { }


        public IEnumerable<IDatum> this[T attr] => Get(attr);
        public bool IsConnected => m_refCount > 0;  //nothrow
        public int ConnectionsCount => m_refCount;
        public IDatumAccessor<IDatum> Source => m_dataProvider; //nothrow

        public IEnumerable<T> Attributes    //nothrow
        {
            get
            {
                Assert(IsConnected);

                lock (m_lock)
                    return m_dataIndices.Keys;
            }
        }


        public void Connect()
        {
            IDisposable unlocker = Lock();

            try
            {
                if (!IsConnected)
                {
                    m_dataProvider.Connect();
                    LoadData();
                    RegisterHandlers();
                }

                ++m_refCount;
            }
            catch
            {
                m_dataIndices.Clear();
                m_cache.Clear();

                throw;
            }
            finally
            {
                unlocker.Dispose();
            }
        }

        public void Disconnect()
        {
            using (Lock())
                if (IsConnected)
                {
                    if (m_refCount == 1)
                    {
                        m_dataProvider.Disconnect();
                        UnregisterHandlers();
                        m_dataIndices.Clear();
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

                DatumDeleted = DatumInserted = DatumReplaced = null;
                DataDeleted = DataInserted = null;
                Invalidated = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception shut down in AttrIndexer.Dispose: {ex.Message}");
            }
        }

        public IEnumerable<IDatum> Get(T attr)
        {
            Assert(IsConnected);
            Assert(attr != null);

            List<int> locs;

            using (Lock())
            {
                m_dataIndices.TryGetValue(attr , out locs);

                return locs == null ? Enumerable.Empty<IDatum>() : YieldData(locs);
            }
        }

        public IEnumerable<int> IndexOf(T attr) //nothrow
        {
            Assert(IsConnected);
            Assert(attr != null);

            List<int> locs;

            using (Lock())
            {
                m_dataIndices.TryGetValue(attr , out locs);

                return locs ?? Enumerable.Empty<int>();
            }
        }

        public IDisposable Lock()   //nothrow
        {
            Monitor.Enter(m_lock);
            IDisposable dpUnlocker = m_dataProvider.Lock();

            return new AutoReleaser(() => Unlock(dpUnlocker));
        }

        public IDisposable TryLock()    //nothrow
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
        void UnregisterHandlers()   //nothrow
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
            var data = new List<IDatum>(indices.Count);

            for (int i = 0, count = Math.Min(indices.Count , m_dataProvider.Count); i < count; ++i)
                data.Add(m_dataProvider.Get(indices[i]));

            return data;
        }

        void RegisterHandlers() //nothrow
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

        void Unlock(IDisposable dpUnlocker) //nothrow
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

            using (Lock())
            {
                IDatum oldDatum = null;

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

                DatumReplaced?.Invoke(datum);
            }
        }

        private void DataProvider_DatumInserted(int ndx , IDatum datum)
        {
            using (Lock())
            {
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

                DatumInserted?.Invoke(datum);
            }
        }

        private void DataProvider_DatumDeleting(int ndx)
        {
            using (Lock())
            {
                IDatum datum = m_dataProvider.Get(ndx);

                m_cache.Add(Tuple.Create(ndx , datum));
            }
        }

        private void DataProvider_DatumDeleted(int ndx)
        {
            using (Lock())
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

                T key = m_selector(datum);
                m_dataIndices[key].Remove(ndx);

                //adjust indices
                foreach (List<int> lst in m_dataIndices.Values)
                    for (int k = 0; k < lst.Count; ++k)
                        if (ndx < lst[k])
                            --lst[k];

                DatumDeleted?.Invoke(datum);
            }
        }

        private void DataProvider_DataInserted(IList<int> indices , IList<IDatum> data)
        {
            using (Lock())
            {

                Assert(indices.Count == data.Count);

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

                DataInserted?.Invoke(data);
            }
        }

        private void DataProvider_DataDeleting(IList<int> indices)
        {
            using (Lock())
            {
                for (int i = 0; i < indices.Count; ++i)
                {
                    int ndx = indices[i];
                    IDatum datum = m_dataProvider.Get(ndx);

                    m_cache.Add(Tuple.Create(ndx , datum));
                }

            }
        }

        private void DataProvider_DataDeleted(IList<int> Indices)
        {
            using (Lock())
            {
                var data = new IDatum[Indices.Count];

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

                DataDeleted?.Invoke(data);
            }
        }

        private void DataProvider_Invalidated()
        {
            using (Lock())
            {
                m_dataIndices.Clear();
                m_cache.Clear();
                LoadData();

                Invalidated?.Invoke();
            }
        }
    }

}
