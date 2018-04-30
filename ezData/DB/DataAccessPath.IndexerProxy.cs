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
        class IndexerProxy: IKeyIndexer
        {
            int m_refCount;


            public event Action<IDatum[]> DataDeleted
            {
                add { Indexer.DataDeleted += value; }
                remove { Indexer.DataDeleted -= value; }
            }

            public event Action<IDatum[]> DataInserted
            {
                add { Indexer.DataInserted += value; }
                remove { Indexer.DataInserted -= value; }
            }

            public event Action<IDatum> DatumDeleted
            {
                add { Indexer.DatumDeleted += value; }
                remove { Indexer.DatumDeleted -= value; }
            }

            public event Action<IDatum> DatumInserted
            {
                add { Indexer.DatumInserted += value; }
                remove { Indexer.DatumInserted -= value; }
            }

            public event Action<IDatum> DatumReplaced
            {
                add { Indexer.DatumReplaced += value; }
                remove { Indexer.DatumReplaced -= value; }
            }

            public event Action Invalidated
            {
                add { Indexer.Invalidated += value; }
                remove { Indexer.Invalidated -= value; }
            }


            public IndexerProxy(KeyIndexer ndxer)   //nothrow
            {
                Assert(ndxer != null);
                Assert(ndxer.IsConnected);

                Indexer = ndxer;
            }

            public int ConnectionsCount => m_refCount;   //nothrow
            public KeyIndexer Indexer { get; }  //nothrow
            public bool IsConnected => ConnectionsCount > 0;    //nothrow

            public IDatum this[uint datumID] => Indexer[datumID];
            public IEnumerable<uint> Keys => Indexer.Keys;
            public IDatumAccessor<IDatum> Source => Indexer.Source; //nothrow

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

            public void Dispose() => Indexer.Dispose(); //nothrow
            public IDatum Get(uint datumID) => Indexer.Get(datumID);
            public int IndexOf(uint datumID) => Indexer.IndexOf(datumID);   //nothrow
            public IDisposable Lock() => Indexer.Lock();    //nothrow
            public IDisposable TryLock() => Indexer.TryLock();  //nothrow
        }

    }
}
