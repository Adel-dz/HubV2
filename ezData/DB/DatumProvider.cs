using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    public interface IDatumProvider<T>: IDatumAccessor<T>, IDisposable
    {
        event Action<int> DatumDeleting;
        event Action<int> DatumDeleted;
        event Action<int[]> DataDeleting;
        event Action<int[]> DataDeleted;
        event Action<int , T> DatumInserted;
        event Action<int[] , T[]> DataInserted;
        event Action<int , T> DatumReplacing;
        event Action<int , T> DatumReplaced;
        event Action Invalidated;

        bool IsDisposed { get; }
        bool IsConnected { get; }

        void Connect();
        void Disconnect();
        uint GetNextAutoID();
        IDisposable Lock();
        IDisposable TryLock();
    }


    public interface IDatumProvider: IDatumProvider<IDatum>
    { }


    public class DatumProvider: IDatumProvider
    {
        readonly IDatumProvider m_src;
        readonly ProviderMapper<IDatum> m_mapper;
        readonly object m_lock = new object();


        public event Action<int[]> DataDeleted;
        public event Action<int[]> DataDeleting;
        public event Action<int[] , IDatum[]> DataInserted;
        public event Action<int> DatumDeleted;
        public event Action<int> DatumDeleting;
        public event Action<int , IDatum> DatumInserted;
        public event Action<int , IDatum> DatumReplaced;
        public event Action<int , IDatum> DatumReplacing;
        public event Action Invalidated;


        public DatumProvider(IDatumProvider source , Predicate<IDatum> filter , AggregationMode_t aggMode = AggregationMode_t.Rejected)
        {
            Assert(source != null);

            m_mapper = new ProviderMapper<IDatum>(source , filter , aggMode);
        }

        public int Count => m_src.Count;
        public bool IsConnected { get; private set; }
        public bool IsDisposed { get; private set; }
        public int ConnectionsCount { get; private set; }

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

        public void Connect()
        {
            Assert(!IsDisposed);

            lock (m_lock)
            {
                if (++ConnectionsCount == 1)
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
                if (ConnectionsCount > 0)
                    Close(true);

                DatumInserted = DatumReplaced = DatumReplacing = null;
                DatumDeleted = DatumDeleting = null;
                DataDeleted = DataDeleting = null;
                DataInserted = null;

                IsDisposed = true;

                ProvidersTracker.UnregisterProvider(this);
            }

            Monitor.Exit(m_lock);
        }

        public uint GetNextAutoID() => m_src.GetNextAutoID();

        public IDisposable Lock()
        {
            Monitor.Enter(m_lock);
            IDisposable unlocker = m_src.Lock();

            return new AutoReleaser(() => Unlock(unlocker));
        }

        public IDisposable TryLock()
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

        public void Delete(int[] indices)
        {
            Assert(IsConnected);
            Assert(indices != null);
            Assert(indices.Any(ndx => ndx < 0 || ndx >= Count) == false);

            Monitor.Enter(m_lock);
            int[] srcIndices = indices.Select(n => m_mapper.ToSourceIndex(n)).ToArray();
            Monitor.Exit(m_lock);

            m_src.Delete(srcIndices);
        }

        public void Delete(int ndx)
        {
            Assert(IsConnected);
            Assert(ndx < Count);
            Assert(0 <= ndx);

            Monitor.Enter(m_lock);
            int srcIndex = m_mapper.ToSourceIndex(ndx);
            Monitor.Exit(m_lock);

            m_src.Delete(srcIndex);
        }

        public void Insert(IDatum[] data)
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

            Monitor.Enter(m_lock);
            int srcIndex = m_mapper.ToSourceIndex(ndx);
            Monitor.Exit(m_lock);

            m_src.Replace(srcIndex , datum);
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

        public IDatum[] Get(int[] indices)
        {
            Assert(IsConnected);
            Assert(indices != null);
            Assert(!indices.Any(x => x < 0 || Count <= x));

            Monitor.Enter(m_lock);
            int[] srcIndices = indices.Select(x => m_mapper.ToSourceIndex(x)).ToArray();
            Monitor.Exit(m_lock);

            return m_src.Get(srcIndices);
        }

        public IDatum Get(int ndx)
        {
            Assert(IsConnected);
            Assert(ndx < Count);
            Assert(ndx >= 0);

            Monitor.Enter(m_lock);
            int srcIndex = m_mapper.ToSourceIndex(ndx);
            Monitor.Exit(m_lock);

            return m_src.Get(srcIndex);
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

        void Close(bool closeAll)
        {
            if (ConnectionsCount > 0)
            {
                if (closeAll)
                    ConnectionsCount = 1;

                if (--ConnectionsCount == 0)
                {
                    UnregisterHandlers();
                    m_src.Disconnect();
                    m_mapper.Disconnect();
                    IsConnected = false;
                }

                m_src.Disconnect();
            }
        }

        //handlers:
        private void Source_Invalidated()
        {
            lock (m_lock) ;
            {
                m_mapper.Disconnect();
                m_mapper.Connect();
            }

            Invalidated?.Invoke();
        }

        private void Source_DatumReplacing(int srcIndex , IDatum datum)
        {
            Monitor.Enter(m_lock);

            if (m_mapper.IsSelected(srcIndex))
            {
                int ndx = m_mapper.FromSourceIndex(srcIndex);

                if (m_mapper.Filter(datum))
                    DatumReplacing?.Invoke(ndx , datum);
                else
                    DatumDeleting?.Invoke(ndx);
            }

            Monitor.Exit(m_lock);
        }

        private void Source_DatumReplaced(int srcIndex , IDatum datum)
        {
            Assert(datum != null);

            Monitor.Enter(m_lock);

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

            Monitor.Exit(m_lock);
        }

        private void Source_DatumInserted(int srcIndex , IDatum datum)
        {
            Assert(datum != null);

            var handler = DatumInserted;

            Monitor.Enter(m_lock);

            m_mapper.OnSourceItemInserted(srcIndex , datum);

            if (handler != null && m_mapper.Filter(datum))
            {
                int ndx = m_mapper.FromSourceIndex(srcIndex);
                handler(ndx , datum);
            }

            Monitor.Exit(m_lock);
        }

        private void Source_DatumDeleting(int srcIndex)
        {
            var handler = DatumDeleting;

            Monitor.Enter(m_lock);

            if (handler != null && m_mapper.IsSelected(srcIndex))
            {
                int ndx = m_mapper.FromSourceIndex(srcIndex);
                handler(ndx);
            }

            Monitor.Exit(m_lock);
        }

        private void Source_DatumDeleted(int srcIndex)
        {
            var handler = DatumDeleted;

            Monitor.Enter(m_lock);

            if (handler != null && m_mapper.IsSelected(srcIndex))
            {
                int ndx = m_mapper.FromSourceIndex(srcIndex);

                m_mapper.OnSourceItemDeleted(srcIndex);
                handler(ndx);
            }
            else
                m_mapper.OnSourceItemDeleted(srcIndex);

            Monitor.Exit(m_lock);
        }

        private void Source_DataInserted(int[] srcIndices , IDatum[] data)
        {
            Assert(srcIndices != null);
            Assert(data != null);
            Assert(srcIndices.Length == data.Length);
            Assert(data.Count(d => d == null) == 0);

            Monitor.Enter(m_lock);

            for (int i = 0; i < data.Length; ++i)
                m_mapper.OnSourceItemInserted(srcIndices[i] , data[i]);


            var handler = DataInserted;

            if (handler != null)
            {
                var ndxList = new List<int>(srcIndices.Length);
                var dataList = new List<IDatum>(data.Length);

                for (int i = 0; i < data.Length; ++i)
                    if (m_mapper.Filter(data[i]))
                    {
                        ndxList.Add(m_mapper.FromSourceIndex(srcIndices[i]));
                        dataList.Add(data[i]);
                    }

                handler(ndxList.ToArray() , dataList.ToArray());
            }

            Monitor.Exit(m_lock);
        }

        private void Source_DataDeleting(int[] srcIndices)
        {
            var handler = DataDeleting;

            if (handler != null)
            {
                var lst = new List<int>(srcIndices.Length);

                Monitor.Enter(m_lock);

                for (int i = 0; i < srcIndices.Length; ++i)
                {
                    int srcIndex = srcIndices[i];

                    if (m_mapper.IsSelected(srcIndex))
                    {
                        int ndx = m_mapper.FromSourceIndex(srcIndex);
                        lst.Add(ndx);
                    }
                }

                Monitor.Exit(m_lock);

                if (lst.Count > 0)
                    handler(lst.ToArray());
            }
        }

        private void Source_DataDeleted(int[] srcIndices)
        {
            Assert(srcIndices != null);

            var handler = DataDeleted;

            Monitor.Enter(m_lock);

            if (handler == null)
                for (int i = 0; i < srcIndices.Length; i++)
                    m_mapper.OnSourceItemDeleted(srcIndices[i]);
            else
            {
                var indices = new List<int>();

                for (int i = 0; i < srcIndices.Length; ++i)
                {
                    int srcIndex = srcIndices[i];

                    if (m_mapper.IsSelected(srcIndex))
                        indices.Add(m_mapper.FromSourceIndex(srcIndex));

                    m_mapper.OnSourceItemDeleted(srcIndex);
                }

                if (indices.Count > 0)
                    handler(indices.ToArray());
            }

            Monitor.Exit(m_lock);
        }

    }

}


