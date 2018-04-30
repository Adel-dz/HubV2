using System;
using System.Collections.Generic;
using System.Threading;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    partial class DataAccessPath
    {
        /*
         * Version: 1
         */
        class ProviderProxy: IDatumProvider
        {
            int m_refCount;


            public event Action<IList<int>> DataDeleted
            {
                add { Provider.DataDeleted += value; }
                remove { Provider.DataDeleted -= value; }
            }

            public event Action<IList<int>> DataDeleting
            {
                add { Provider.DataDeleting += value; }
                remove { Provider.DataDeleting -= value; }
            }

            public event Action<IList<int> , IList<IDatum>> DataInserted
            {
                add { Provider.DataInserted += value; }
                remove { Provider.DataInserted -= value; }
            }

            public event Action<int> DatumDeleted
            {
                add { Provider.DatumDeleted += value; }
                remove { Provider.DatumDeleted -= value; }
            }

            public event Action<int> DatumDeleting
            {
                add { Provider.DatumDeleting += value; }
                remove { Provider.DatumDeleting -= value; }
            }

            public event Action<int , IDatum> DatumInserted
            {
                add { Provider.DatumInserted += value; }
                remove { Provider.DatumInserted += value; }
            }

            public event Action<int , IDatum> DatumReplaced
            {
                add { Provider.DatumReplaced += value; }
                remove { Provider.DatumReplaced -= value; }
            }

            public event Action<int , IDatum> DatumReplacing
            {
                add { Provider.DatumReplacing += value; }
                remove { Provider.DatumReplacing -= value; }

            }

            public event Action Invalidated
            {
                add { Provider.Invalidated += value; }
                remove { Provider.Invalidated -= value; }
            }


            public ProviderProxy(IDatumProvider dp) //nothrow
            {
                Assert(dp != null);
                Assert(dp.IsConnected);

                Provider = dp;
            }


            public IDatumProvider Provider { get; } //nothrow
            public bool IsConnected => ConnectionsCount > 0;    //nothrow
            public IDataSourceInfo SourceInfo => Provider.SourceInfo;   //nothrow   
            public int ConnectionsCount => m_refCount;  //nothrow
            public int Count => Provider.Count; //nothrow
            public bool CanRead => Provider.CanRead;    //nothrow
            public bool CanWrite => Provider.CanWrite;  //nothrow

            public bool AutoFlush   //nothrow
            {
                get { return Provider.AutoFlush; }
                set { Provider.AutoFlush = value; }
            }

            public uint DataVersion //nothrow
            {
                get { return Provider.DataVersion; }
                set { Provider.DataVersion = value; }
            }

            public void Connect() => Interlocked.Increment(ref m_refCount);

            public void Disconnect()
            {
                int cnCount = m_refCount;

                while (cnCount > 0)
                {
                    if (Interlocked.CompareExchange(ref m_refCount , cnCount - 1 , cnCount) == cnCount)
                        break;

                    cnCount = m_refCount;
                }

                Assert(m_refCount >= 0);
            }

            public void Delete(IList<int> indices) => Provider.Delete(indices);
            public void Delete(int ndx) => Provider.Delete(ndx);
            public void Dispose() => Provider.Dispose();    //nothrow
            public IEnumerable<IDatum> Enumerate() => Provider.Enumerate();
            public IEnumerable<IDatum> Enumerate(int ndxFirst) => Provider.Enumerate(ndxFirst);
            public IList<IDatum> Get(IList<int> indices) => Provider.Get(indices);
            public IDatum Get(int ndx) => Provider.Get(ndx);
            public uint GetNextAutoID() => Provider.GetNextAutoID();
            public void Insert(IList<IDatum> items) => Provider.Insert(items);
            public void Insert(IDatum item) => Provider.Insert(item);
            public IDisposable Lock() => Provider.Lock();   //nothrow
            public void Replace(int ndx , IDatum item) => Provider.Replace(ndx , item);
            public IDisposable TryLock() => Provider.TryLock(); //nothrow

            public void Clear() => Provider.Clear();
        }

    }
}
