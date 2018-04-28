using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace easyLib.DB
{
    partial class DataAccessPath
    {
        class IndexerProxy: IKeyIndexer
        {
            readonly object m_lock = new object();


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


            public IndexerProxy(KeyIndexer ndxer)
            {
                Assert(ndxer != null);
                Assert(ndxer.IsConnected);

                Indexer = ndxer;
            }

            public int ConnectionCount { get; private set; }
            public KeyIndexer Indexer { get; }
            public bool IsConnected { get; private set; }

            public IDatum this[uint datumID]
            {
                get
                {
                    Assert(IsConnected);
                    return Indexer[datumID];
                }
            }

            public IEnumerable<uint> Keys
            {
                get
                {
                    Assert(IsConnected);
                    return Indexer.Keys;
                }
            }

            public IDatumAccessor<IDatum> Source => Indexer.Source;

            public void Connect()
            {
                lock
                Interlocked.Increment(ref m_refCount);
            }

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

            public void Dispose() =>
            public abstract IDatum Get(uint datumID);
            public abstract int IndexOf(uint datumID);
            public abstract IDisposable Lock();
            public abstract IDisposable TryLock();
        }

    }
}
