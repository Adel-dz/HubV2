using System;
using System.Collections.Generic;
using System.Linq;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    /*
     * Version: 1
     */
    public interface IDataAccessPath
    {
        IKeyIndexer GetIndexer(uint sourceID);
        IDatumProvider GetProvider(uint sourceID);
    }
    //-----------------------------------------------------

    /*
     * Version: 1
     */
    public abstract partial class DataAccessPath: IDataAccessPath, IDisposable
    {
        public const int DEFAULT_SIZE = 8;

        readonly object m_lock = new object();
        readonly List<Tuple<uint , IDatumProvider>> m_providers;
        readonly List<Tuple<uint , KeyIndexer>> m_indexers;
        readonly int m_maxSize;


        protected DataAccessPath(int cacheSize = DEFAULT_SIZE)  //nothrow
        {
            Assert(cacheSize > 0);

            m_providers = new List<Tuple<uint , IDatumProvider>>();
            m_indexers = new List<Tuple<uint , KeyIndexer>>();
            m_maxSize = cacheSize;
        }


        public void Dispose()   //nothrow
        {
            lock (m_lock)
            {

                foreach (Tuple<uint , IDatumProvider> tpl in m_providers)
                    tpl.Item2.Dispose();

                m_providers.Clear();


                foreach (Tuple<uint , KeyIndexer> tpl in m_indexers)
                    tpl.Item2.Dispose();

                m_indexers.Clear();
            }
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
                    IDatumProvider dp = QueryProvider(sourceID);
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
            lock (m_lock)
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

            if (m_providers.Count >= m_maxSize)
                for (int ndx = m_providers.Count - 1; ndx >= 0; --ndx)
                    if (!m_providers[ndx].Item2.IsConnected)
                    {
                        m_providers[ndx].Item2.Dispose();
                        m_providers.RemoveAt(ndx);

                        if (m_providers.Count < m_maxSize)
                            break;
                    }
        }

        IDatumProvider QueryProvider(uint sourceID)
        {
            IDatumProvider provider = (from tpl in m_providers
                                       where tpl.Item1 == sourceID
                                       select tpl.Item2).SingleOrDefault();

            if (provider != null)
                return provider;

            provider = DoGetProvider(sourceID);
            provider.Connect();

            if (m_providers.Count >= m_maxSize)
                CleanTables();

            m_providers.Add(Tuple.Create(sourceID , provider));
            
            return provider;
        }
    }
}
