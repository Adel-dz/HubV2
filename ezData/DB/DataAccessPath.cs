using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace easyLib.DB
{
    public interface IDataAccessPath: IDisposable
    {
        bool IsDisposed { get; }

        IKeyIndexer GetIndexer(uint sourceID);
        IDatumProvider GetProvider(uint sourceID);
    }



    public abstract class DataAccessPath: IDataAccessPath
    {
        public const int DEFAULT_SIZE = 8;

        readonly object m_lock = new object();
        readonly List<Tuple<uint , DatumProvider>> m_providers;
        readonly List<Tuple<uint , KeyIndexer>> m_indexers;
        readonly int m_maxSize;


        protected DataAccessPath(int cacheSize = DEFAULT_SIZE)
        {
            m_providers = new List<Tuple<uint , DatumProvider>>();
            m_indexers = new List<Tuple<uint , KeyIndexer>>();
            m_maxSize = cacheSize == 0 ? DEFAULT_SIZE : cacheSize;
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            Monitor.Enter(m_lock);

            if (!IsDisposed)
            {
                foreach (Tuple<uint , DatumProvider> tpl in m_providers)
                    tpl.Item2.Disconnect();

                m_providers.Clear();


                foreach (Tuple<uint , KeyIndexer> tpl in m_indexers)
                    tpl.Item2.Disconnect();

                m_indexers.Clear();

                IsDisposed = true;
            }

            Monitor.Exit(m_lock);
        }

        public IKeyIndexer GetIndexer(uint sourceID)
        {
            lock (m_lock)
            {
                KeyIndexer ndxer = null;

                Tuple<uint , KeyIndexer> tpl = m_indexers.Find(t => t.Item1 == sourceID);

                if (tpl != null)
                    ndxer = tpl.Item2;
                else
                {
                    DatumProvider dp = QueryProvider(sourceID);
                    ndxer = new KeyIndexer(dp);

                    if (m_indexers.Count >= m_maxSize)
                        CleanTables();

                    m_indexers.Add(Tuple.Create(sourceID , ndxer));
                    ndxer.Connect();
                }

                return ndxer;
            }
        }

        public IDatumProvider GetProvider(uint sourceID)
        {
            lock(m_lock)            
                return QueryProvider(sourceID);
        }

        //protected:
        protected abstract IDatumProvider DoGetProvider(uint sourceID);


        //private:
        void CleanTables()
        {
            if (m_indexers.Count >= m_maxSize)
                for (int ndx = m_indexers.Count - 1; ndx >= 0; --ndx)
                    if (m_indexers[ndx].Item2.ConnectionsCount == 1)
                    {
                        m_indexers[ndx].Item2.Dispose();
                        m_indexers.RemoveAt(ndx);

                        if (m_indexers.Count < m_maxSize)
                            break;
                    }

            if(m_providers.Count >= m_maxSize)
                for (int ndx = m_providers.Count - 1; ndx >= 0; --ndx)
                    if (m_providers[ndx].Item2.ConnectionsCount == 1)
                    {
                        m_providers[ndx].Item2.Dispose();
                        m_providers.RemoveAt(ndx);

                        if (m_providers.Count < m_maxSize)
                            break;
                    }
        }

        DatumProvider QueryProvider(uint sourceID)
        {
            DatumProvider provider = (from tpl in m_providers
                                      where tpl.Item1 == sourceID
                                      select tpl.Item2).SingleOrDefault();

            if (provider != null)
                return provider;

            provider = new DatumProvider(DoGetProvider(sourceID) , d => true , AggregationMode_t.Rejected);

            if (m_providers.Count >= m_maxSize)
                CleanTables();

            m_providers.Add(Tuple.Create(sourceID , provider));
            provider.Connect();

            return provider;
        }
    }
}
