using System;
using System.Collections.Generic;
using System.Linq;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    partial class DataTable<T>
    {
        class DatumProvider: IDatumProvider
        {
            readonly DataTable<T> m_table;

            public event Action<int[]> DataDeleted;
            public event Action<int[]> DataDeleting;
            public event Action<int[] , IDatum[]> DataInserted;
            public event Action<int> DatumDeleted;
            public event Action<int> DatumDeleting;
            public event Action<int , IDatum> DatumInserted;
            public event Action<int , IDatum> DatumReplaced;
            public event Action<int , IDatum> DatumReplacing;
            public event Action Invalidated;
                        
            
            public DatumProvider(DataTable<T> table)
            {
                Assert(table != null);

                m_table = table;
            }


            public bool IsConnected => m_table.IsOpen;
            public bool IsDisposed => m_table.IsDisposed;
            public bool AutoFlush { get; set; }


            public int Count
            {
                get
                {
                    Assert(IsConnected);
                    return m_table.DataCount;
                }
            }

            public uint DataVersion
            {
                get
                {
                    Assert(IsConnected);
                    return m_table.Version;
                }

                set
                {
                    Assert(IsConnected);
                    m_table.Version = value;
                }
            }


            public void Connect()
            {
                Assert(!IsConnected);

                m_table.Open();
                RegisterHandlers();

                ProvidersTracker.RegisterProvider(this , m_table.ID);
            }

            public void Delete(int[] indices)
            {
                Assert(IsConnected);
                Assert(indices != null);
                Assert(indices.Any(ndx => ndx < 0 || ndx >= Count) == false);

                m_table.Delete(indices);

                if (AutoFlush)
                    m_table.Flush();
            }

            public void Delete(int ndx)
            {
                Assert(IsConnected);
                Assert(ndx >= 0 && ndx < Count);

                m_table.Delete(ndx);

                if (AutoFlush)
                    m_table.Flush();
            }

            public void Disconnect()
            {
                if (IsConnected)
                {
                    UnregisterHandlers();
                    m_table.Close();

                    ProvidersTracker.UnregisterProvider(this);
                }
            }

            public void Dispose() => Disconnect();

            public IEnumerable<IDatum> Enumerate()
            {
                Assert(IsConnected);

                if (Count > 0)
                    return EnumerateData(0);

                return Enumerable.Empty<T>().Cast<IDatum>();
            }

            public IEnumerable<IDatum> Enumerate(int ndxFirst)
            {
                Assert(IsConnected);
                Assert(ndxFirst >= 0 && ndxFirst < Count);

                return EnumerateData(ndxFirst);
            }

            public IDatum[] Get(int[] indices)
            {
                Assert(IsConnected);
                Assert(indices != null);
                Assert(indices.Any(ndx => ndx < 0 || ndx >= Count) == false);
                return m_table.Get(indices) as IDatum[];                
            }

            public IDatum Get(int ndx)
            {
                Assert(IsConnected);
                Assert(ndx >= 0 && ndx < Count);

                return m_table.Get(ndx);
            }

            public uint GetNextAutoID()
            {
                Assert(IsConnected);

                return m_table.GetNextAutoID();
            }

            public void Insert(IDatum[] items)
            {
                Assert(IsConnected);
                Assert(items.OfType<T>().Count() == items.Length);

                m_table.Insert(items as T[]);

                if (AutoFlush)
                    m_table.Flush();
            }

            public void Insert(IDatum item)
            {
                Assert(IsConnected);
                Assert(item is T);

                m_table.Insert((T)item);

                if (AutoFlush)
                    m_table.Flush();
            }

            public IDisposable Lock() => m_table.Lock();

            public IDisposable TryLock() => m_table.TryLock();

            public void Replace(int ndx , IDatum item)
            {
                Assert(IsConnected);
                Assert(item is T);
                Assert(ndx >= 0 && ndx < Count);

                m_table.Replace(ndx , (T)item);

                if (AutoFlush)
                    m_table.Flush();
            }


            //private:
            IEnumerable<IDatum> EnumerateData(int ndxFirst)
            {
                for (int ndx = ndxFirst; ndx < Count; ++ndx)
                    yield return m_table.Get(ndx);                   
            }

            void UnregisterHandlers()
            {
                m_table.DataDeleted -= Table_DataDeleted;
                m_table.DataDeleting -= Table_DataDeleting;
                m_table.DataInserted -= Table_DataInserted;
                m_table.DatumDeleted -= Table_DatumDeleted;
                m_table.DatumDeleting -= Table_DatumDeleting;
                m_table.DatumReplaced -= Table_DatumReplaced;
                m_table.DatumReplacing -= Table_DatumReplacing;
                m_table.DatumInserted -= Table_DatumInserted;
                m_table.ProvidersInvalidated -= Table_ProviderInvalidated;
            }

            void RegisterHandlers()
            {
                m_table.DataDeleted += Table_DataDeleted;
                m_table.DataDeleting += Table_DataDeleting;
                m_table.DataInserted += Table_DataInserted;
                m_table.DatumDeleted += Table_DatumDeleted;
                m_table.DatumDeleting += Table_DatumDeleting;
                m_table.DatumReplaced += Table_DatumReplaced;
                m_table.DatumReplacing += Table_DatumReplacing;
                m_table.DatumInserted += Table_DatumInserted;
                m_table.ProvidersInvalidated += Table_ProviderInvalidated;
            }

            //handlers:
            private void Table_ProviderInvalidated() => Invalidated?.Invoke();
            private void Table_DatumInserted(int ndx , T datum) => DatumInserted?.Invoke(ndx , datum);
            private void Table_DatumReplacing(int ndx , T datum) => DatumReplacing?.Invoke(ndx , datum);
            private void Table_DatumReplaced(int ndx , T datum) => DatumReplaced?.Invoke(ndx , datum);
            private void Table_DatumDeleting(int ndx) => DatumDeleting?.Invoke(ndx);
            private void Table_DatumDeleted(int ndx) => DatumDeleted?.Invoke(ndx);
            private void Table_DataInserted(int[] indices , T[] data) => DataInserted(indices , data as IDatum[]);
            private void Table_DataDeleting(int[] indices) => DataDeleting(indices);
            private void Table_DataDeleted(int[] indices) => DataDeleted(indices);
        }

    }
}
