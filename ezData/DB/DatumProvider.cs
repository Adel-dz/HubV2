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
    public interface IDatumProvider<T>: IDatumAccessor<T>, IDisposable
    {
        event Action<int> DatumDeleting;
        event Action<int> DatumDeleted;
        event Action<IList<int>> DataDeleting;
        event Action<IList<int>> DataDeleted;
        event Action<int , T> DatumInserted;
        event Action<IList<int> , IList<T>> DataInserted;
        event Action<int , T> DatumReplacing;
        event Action<int , T> DatumReplaced;
        event Action Invalidated;

        bool IsConnected { get; }   //nothrow

        /* Pre:
         * - IsConnected
         */
        IDataSourceInfo SourceInfo { get; } //nothrow

        /* Pre:
         * - IsConnected
         */
        uint DataVersion { get; set; }  //nothrow

        /* Pre:
         * - IsConnected
         */
        uint GetNextAutoID();   //nothrow

        /* Post:
        * - IsConnected
        */
        void Connect();

        /* Post:
         * - !IsConnected
         */
        void Disconnect();

        /* Pre:
         * - IsConnected
         * 
         * Post:
         * - Count == 0
         */
        void Clear();
    }
    //----------------------------------------------------------------

    /* 
     * Version: 1
     */
    public interface IDatumProvider: IDatumProvider<IDatum>
    { }
    //-------------------------------------------------------------

    /*
     * Version: 1
     */
    public partial class DatumProvider: IDatumProvider
    {
        readonly IDatumProvider m_src;
        readonly ProviderMapper<IDatum> m_mapper;
        readonly object m_lock = new object();
        int m_refCount;


        public event Action<IList<int>> DataDeleted;
        public event Action<IList<int>> DataDeleting;
        public event Action<IList<int> , IList<IDatum>> DataInserted;
        public event Action<int> DatumDeleted;
        public event Action<int> DatumDeleting;
        public event Action<int , IDatum> DatumInserted;
        public event Action<int , IDatum> DatumReplaced;
        public event Action<int , IDatum> DatumReplacing;
        public event Action Invalidated;


        public DatumProvider(IDatumProvider source , Predicate<IDatum> filter , //nothrow
            AggregationMode_t aggMode = AggregationMode_t.Rejected)
        {
            Assert(source != null);

            m_mapper = new ProviderMapper<IDatum>(source , filter , aggMode);
        }


        public int Count => m_src.Count;    //nothrow
        public bool IsConnected { get; private set; }
        public bool CanRead => IsConnected;
        public bool CanWrite => IsConnected;


        public bool AutoFlush
        {
            get { return m_src.AutoFlush; }

            set { m_src.AutoFlush = value; }
        }

        public uint DataVersion
        {
            get { return m_src.DataVersion; }

            set { m_src.DataVersion = value; }
        }

        public IDataSourceInfo SourceInfo => m_src.SourceInfo;


        public void Connect()
        {
            Assert(!IsConnected);

            using (Lock())
            {
                if (!IsConnected)
                {
                    m_src.Connect();
                    m_mapper.Connect();
                    RegisterHandlers();
                    IsConnected = true;

                    ProvidersTracker.RegisterProvider(this , m_src);
                }
            }
        }

        public void Disconnect()
        {
            using (Lock())
                if (IsConnected)
                {
                    m_src.Disconnect();
                    UnregisterHandlers();
                    m_mapper.Disconnect();

                    IsConnected = false;
                }
        }

        public void Dispose()   //nothrow
        {
            try
            {
                Disconnect();

                DatumInserted = DatumReplaced = DatumReplacing = null;
                DatumDeleted = DatumDeleting = null;
                DataDeleted = DataDeleting = null;
                DataInserted = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception shut down in DatumProvider.Dispose: {ex.Message}");
            }
        }

        public uint GetNextAutoID() => m_src.GetNextAutoID();   //nothrow

        public IDisposable Lock()   //nothrow
        {
            Monitor.Enter(m_lock);
            IDisposable unlocker = m_src.Lock();

            return new AutoReleaser(() => Unlock(unlocker));
        }

        public IDisposable TryLock()    //nothrow
        {
            if (Monitor.TryEnter(m_lock))
            {
                IDisposable unlocker = m_src.Lock();

                if (unlocker != null)
                    return new AutoReleaser(() => Unlock(unlocker));

                Monitor.Exit(m_lock);
            }

            return null;
        }

        public void Clear()
        {
            Assert(IsConnected);

            m_src.Clear();

            Assert(Count == 0);
        }

        public void Delete(IList<int> indices)
        {
            Assert(IsConnected);
            Assert(indices != null);
            Assert(indices.Any(ndx => ndx < 0 || ndx >= Count) == false);

            using(Lock())
            {
                int[] srcIndices = indices.Select(n => m_mapper.ToSourceIndex(n)).ToArray();
                m_src.Delete(srcIndices);
            }
        }

        public void Delete(int ndx)
        {
            Assert(IsConnected);
            Assert(ndx < Count);
            Assert(0 <= ndx);

            using(Lock())
            {
                int srcIndex = m_mapper.ToSourceIndex(ndx);
                m_src.Delete(srcIndex);
            }
        }

        public void Insert(IList<IDatum> data)
        {
            Assert(IsConnected);
            Assert(data != null);
            Assert(data.Any(d => d != null) == false);

            m_src.Insert(data);
        }

        public void Insert(IDatum datum)
        {
            Assert(IsConnected);
            Assert(datum != null);

            m_src.Insert(datum);
        }

        public void Replace(int ndx , IDatum datum)
        {
            Assert(IsConnected);
            Assert(ndx < Count);
            Assert(0 <= ndx);
            Assert(datum != null);

            using(Lock())
            {
                int srcIndex = m_mapper.ToSourceIndex(ndx);
                m_src.Replace(srcIndex , datum);
            }
        }

        public IEnumerable<IDatum> Enumerate()
        {
            Assert(IsConnected);

            return m_src.Enumerate();
        }

        public IEnumerable<IDatum> Enumerate(int ndxFirst)
        {
            Assert(IsConnected);
            Assert(ndxFirst < Count);
            Assert(ndxFirst >= 0);

            return m_src.Enumerate(ndxFirst);
        }

        public IList<IDatum> Get(IList<int> indices)
        {
            Assert(IsConnected);
            Assert(indices != null);
            Assert(!indices.Any(x => x < 0 || Count <= x));

            using(Lock())
            {
                int[] srcIndices = indices.Select(x => m_mapper.ToSourceIndex(x)).ToArray();
                return m_src.Get(srcIndices);
            }
        }

        public IDatum Get(int ndx)
        {
            Assert(IsConnected);
            Assert(ndx < Count);
            Assert(ndx >= 0);

            using(Lock())
            {
                int srcIndex = m_mapper.ToSourceIndex(ndx);
                return m_src.Get(srcIndex);
            }
        }


        //private:
        void Unlock(IDisposable srcUnlocker)
        {
            srcUnlocker.Dispose();
            Monitor.Exit(m_lock);
        }

        void RegisterHandlers()
        {
            m_src.DataDeleted += Source_DataDeleted;
            m_src.DataDeleting += Source_DataDeleting;
            m_src.DataInserted += Source_DataInserted;
            m_src.DatumDeleted += Source_DatumDeleted;
            m_src.DatumDeleting += Source_DatumDeleting;
            m_src.DatumInserted += Source_DatumInserted;
            m_src.DatumReplaced += Source_DatumReplaced;
            m_src.DatumReplacing += Source_DatumReplacing;
            m_src.Invalidated += Source_Invalidated;
        }

        void UnregisterHandlers()
        {
            m_src.DataDeleted -= Source_DataDeleted;
            m_src.DataDeleting -= Source_DataDeleting;
            m_src.DataInserted -= Source_DataInserted;
            m_src.DatumDeleted -= Source_DatumDeleted;
            m_src.DatumDeleting -= Source_DatumDeleting;
            m_src.DatumInserted -= Source_DatumInserted;
            m_src.DatumReplaced -= Source_DatumReplaced;
            m_src.DatumReplacing -= Source_DatumReplacing;
            m_src.Invalidated -= Source_Invalidated;
        }


        //handlers:
        private void Source_Invalidated()
        {
            lock(m_lock)
            {
                m_mapper.Disconnect();
                m_mapper.Connect();

                Invalidated?.Invoke();
            }
        }

        private void Source_DatumReplacing(int srcIndex , IDatum datum)
        {
            lock (m_lock)
                if (m_mapper.IsSelected(srcIndex))
                {
                    int ndx = m_mapper.FromSourceIndex(srcIndex);

                    if (m_mapper.Filter(datum))
                        DatumReplacing?.Invoke(ndx , datum);
                    else
                        DatumDeleting?.Invoke(ndx);
                }
        }

        private void Source_DatumReplaced(int srcIndex , IDatum datum)
        {
            lock (m_lock)
            {
                bool wasIncluded = m_mapper.IsSelected(srcIndex);

                if (wasIncluded)
                {
                    int ndx = m_mapper.FromSourceIndex(srcIndex);
                    m_mapper.OnSourceItemReplaced(srcIndex , datum);

                    if (m_mapper.Filter(datum))
                        DatumReplaced?.Invoke(ndx , datum);
                    else
                        DatumDeleted?.Invoke(ndx);
                }
                else
                {
                    m_mapper.OnSourceItemReplaced(srcIndex , datum);

                    if (m_mapper.Filter(datum))
                    {
                        int ndx = m_mapper.FromSourceIndex(srcIndex);
                        DatumInserted?.Invoke(ndx , datum);
                    }
                }
            }
        }

        private void Source_DatumInserted(int srcIndex , IDatum datum)
        {
            var handler = DatumInserted;

            lock (m_lock)
            {
                m_mapper.OnSourceItemInserted(srcIndex , datum);

                if (handler != null && m_mapper.Filter(datum))
                {
                    int ndx = m_mapper.FromSourceIndex(srcIndex);
                    handler(ndx , datum);
                }
            }

        }

        private void Source_DatumDeleting(int srcIndex)
        {
            var handler = DatumDeleting;

            lock (m_lock)
                if (handler != null && m_mapper.IsSelected(srcIndex))
                {
                    int ndx = m_mapper.FromSourceIndex(srcIndex);
                    handler(ndx);
                }
        }

        private void Source_DatumDeleted(int srcIndex)
        {
            var handler = DatumDeleted;

            lock (m_lock)
                if (handler != null && m_mapper.IsSelected(srcIndex))
                {
                    int ndx = m_mapper.FromSourceIndex(srcIndex);

                    m_mapper.OnSourceItemDeleted(srcIndex);
                    handler(ndx);
                }
                else
                    m_mapper.OnSourceItemDeleted(srcIndex);
        }

        private void Source_DataInserted(IList<int> srcIndices , IList<IDatum> data)
        {
            Assert(srcIndices != null);
            Assert(data != null);
            Assert(srcIndices.Count == data.Count);
            Assert(data.Count(d => d == null) == 0);

            lock (m_lock)
            {
                for (int i = 0; i < data.Count; ++i)
                    m_mapper.OnSourceItemInserted(srcIndices[i] , data[i]);


                var handler = DataInserted;

                if (handler != null)
                {
                    var ndxList = new List<int>(srcIndices.Count);
                    var dataList = new List<IDatum>(data.Count);

                    for (int i = 0; i < data.Count; ++i)
                        if (m_mapper.Filter(data[i]))
                        {
                            ndxList.Add(m_mapper.FromSourceIndex(srcIndices[i]));
                            dataList.Add(data[i]);
                        }

                    handler(ndxList , dataList);
                }
            }
        }

        private void Source_DataDeleting(IList<int> srcIndices)
        {
            var handler = DataDeleting;

            if (handler != null)
            {
                var lst = new List<int>(srcIndices.Count);

                lock (m_lock)
                {
                    for (int i = 0; i < srcIndices.Count; ++i)
                    {
                        int srcIndex = srcIndices[i];

                        if (m_mapper.IsSelected(srcIndex))
                        {
                            int ndx = m_mapper.FromSourceIndex(srcIndex);
                            lst.Add(ndx);
                        }
                    }

                    if (lst.Count > 0)
                        handler(lst);
                }
            }
        }

        private void Source_DataDeleted(IList<int> srcIndices)
        {
            lock (m_lock)
            {
                var handler = DataDeleted;

                if (handler == null)
                    for (int i = 0; i < srcIndices.Count; i++)
                        m_mapper.OnSourceItemDeleted(srcIndices[i]);
                else
                {
                    var indices = new List<int>();

                    for (int i = 0; i < srcIndices.Count; ++i)
                    {
                        int srcIndex = srcIndices[i];

                        if (m_mapper.IsSelected(srcIndex))
                            indices.Add(m_mapper.FromSourceIndex(srcIndex));

                        m_mapper.OnSourceItemDeleted(srcIndex);
                    }

                    if (indices.Count > 0)
                        handler(indices);
                }
            }
        }
    }

}


