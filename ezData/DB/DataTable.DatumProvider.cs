using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    partial class DataTable<T>
    {
        /*
         * Version: 1
         */
        class DatumProvider: IDatumProvider
        {
            readonly DataTable<T> m_table;
            int m_cxnCount;

            public event Action<IList<int>> DataDeleted;
            public event Action<IList<int>> DataDeleting;
            public event Action<IList<int> , IList<IDatum>> DataInserted;
            public event Action<int> DatumDeleted;
            public event Action<int> DatumDeleting;
            public event Action<int , IDatum> DatumInserted;
            public event Action<int , IDatum> DatumReplaced;
            public event Action<int , IDatum> DatumReplacing;
            public event Action Invalidated;


            public DatumProvider(DataTable<T> table)    //nothrow
            {
                Assert(table != null);

                m_table = table;
            }

            public bool IsConnected => m_cxnCount > 0;   //nothrow            
            public int ConnectionsCount => m_cxnCount;
            public bool CanRead => IsConnected; //nothrow
            public bool CanWrite => IsConnected; //nothrow
            public bool AutoFlush { get; set; } //nothrow

            public int Count    //nothrow
            {
                get
                {
                    Assert(IsConnected);

                    lock (m_table)
                        return m_table.DataCount;
                }
            }

            public uint DataVersion //nothrow
            {
                get
                {
                    Assert(IsConnected);

                    lock (m_table)
                        return m_table.Header.Version;
                }

                set
                {
                    Assert(IsConnected);

                    lock (m_table)
                        m_table.Header.Version = value;
                }
            }

            public IDataSourceInfo SourceInfo   //nothrow
            {
                get
                {
                    Assert(IsConnected);

                    lock (m_table)
                        return m_table.Header;
                }
            }

            public void Connect()
            {
                lock (m_table)
                {
                    if (!IsConnected)
                        m_table.Connect();

                    ++m_cxnCount;
                }

                ProvidersTracker.RegisterProvider(this , m_table.ID);

                Assert(IsConnected);
            }

            public void Delete(IList<int> indices)
            {
                Assert(IsConnected);
                Assert(indices != null);
                Assert(indices.Any(ndx => ndx < 0 || ndx >= Count) == false);

                lock (m_table)
                {
                    DataDeleting?.Invoke(indices);

                    m_table.Delete(indices);

                    if (AutoFlush)
                        m_table.Flush();

                    DataDeleted?.Invoke(indices);
                }
            }

            public void Delete(int ndx)
            {
                Assert(IsConnected);
                Assert(ndx >= 0 && ndx < Count);

                lock (m_table)
                {
                    DatumDeleting?.Invoke(ndx);

                    m_table.Delete(ndx);

                    if (AutoFlush)
                        m_table.Flush();

                    DatumDeleted?.Invoke(ndx);
                }
            }

            public void Disconnect()
            {
                lock (m_table)
                    if (IsConnected)
                    {
                        if (m_cxnCount == 1)
                            m_table.Disconnect();

                        --m_cxnCount;
                    }

                ProvidersTracker.UnregisterProvider(this);
            }

            public void Dispose()   //nothrow
            {
                try
                {
                    Disconnect();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Exception shut down in DataTable.DatumProvider.Dispose: {ex.Message}");
                }
            }

            public void Clear()
            {
                Assert(IsConnected);

                lock (m_table)
                {
                    m_table.Clear();

                    if (AutoFlush)
                        m_table.Flush();

                    Invalidated?.Invoke();
                }

                Assert(Count == 0);
            }

            public IEnumerable<IDatum> Enumerate()
            {
                Assert(IsConnected);

                lock (m_table)
                {
                    if (m_table.DataCount > 0)
                        return EnumerateData(0);
                }

                return Enumerable.Empty<T>().Cast<IDatum>();
            }

            public IEnumerable<IDatum> Enumerate(int ndxFirst)
            {
                Assert(IsConnected);
                Assert(ndxFirst >= 0 && ndxFirst < Count);

                return EnumerateData(ndxFirst);
            }

            public IList<IDatum> Get(IList<int> indices)
            {
                Assert(IsConnected);
                Assert(indices != null);
                Assert(indices.Any(ndx => ndx < 0 || ndx >= Count) == false);

                lock (m_table)
                    return m_table.Get(indices) as IList<IDatum>;
            }

            public IDatum Get(int ndx)
            {
                Assert(IsConnected);
                Assert(ndx >= 0 && ndx < Count);

                lock (m_table)
                    return m_table.Get(ndx);
            }

            public uint GetNextAutoID()
            {
                Assert(IsConnected);

                lock (m_table)
                    return m_table.GetNextAutoID();
            }

            public void Insert(IList<IDatum> items)
            {
                Assert(IsConnected);
                Assert(items.OfType<T>().Count() == items.Count);

                lock (m_table)
                {
                    IList<int> indices = m_table.Insert(items as IList<T>);

                    if (AutoFlush)
                        m_table.Flush();

                    DataInserted?.Invoke(indices , items);
                }
            }

            public void Insert(IDatum item)
            {
                Assert(IsConnected);
                Assert(item is T);

                lock (m_table)
                {
                    int ndx = m_table.Insert((T)item);

                    if (AutoFlush)
                        m_table.Flush();

                    DatumInserted?.Invoke(ndx , item);
                }
            }

            public IDisposable Lock()   //nothrow
            {
                Monitor.Enter(m_table);

                return new AutoReleaser(Unlock);
            }

            public IDisposable TryLock()    //nothrow
            {
                if (Monitor.TryEnter(m_table))
                    return new AutoReleaser(Unlock);

                return null;
            }

            public void Replace(int ndx , IDatum item)
            {
                Assert(IsConnected);
                Assert(item is T);
                Assert(ndx >= 0 && ndx < Count);

                lock (m_table)
                {
                    Action<int , IDatum> handler = DatumReplacing;

                    if (handler != null)
                    {
                        IDatum oldDatum = m_table.Get(ndx);
                        handler(ndx , item);
                    }

                    ndx = m_table.Replace(ndx , (T)item);

                    if (AutoFlush)
                        m_table.Flush();

                    DatumReplaced?.Invoke(ndx , item);
                }
            }


            //private:
            IEnumerable<IDatum> EnumerateData(int ndx)
            {
                IDatum datum;

                while (true)
                {
                    lock (m_table)
                    {
                        if (ndx >= m_table.DataCount)
                            break;

                        datum = m_table.Get(ndx++);
                    }

                    yield return datum;
                }
            }

            void Unlock() => Monitor.Exit(m_table); //nothrow
        }
    }
}
