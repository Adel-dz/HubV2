using System.Collections.Generic;
using System.Diagnostics;

namespace easyLib.DB
{
    static class ProvidersTracker
    {
        readonly static Dictionary<uint , List<object>> m_tableProviders = 
            new Dictionary<uint , List<object>>();


        [Conditional("DEBUG")]
        public static void RegisterProvider<T>(IDatumAccessor<T> provider , uint tblID)
        {
            Debug.Assert(provider != null);

            lock (m_tableProviders)
            {
                if (!m_tableProviders.ContainsKey(tblID))
                    m_tableProviders.Add(tblID , new List<object>());

                m_tableProviders[tblID].Add(provider);
            }
        }

        [Conditional("DEBUG")]
        public static void RegisterProvider<T>(IDatumAccessor<T> provider , IDatumAccessor<T> dpSame)
        {
            Debug.Assert(provider != null);
            Debug.Assert(dpSame != null);


            lock (m_tableProviders)
            {
                Debug.Assert(IsProviderRegistered(dpSame));

                uint tblID = Locate(dpSame);
                m_tableProviders[tblID].Add(provider);
            }
        }

        [Conditional("DEBUG")]
        public static void UnregisterProvider<T>(IDatumAccessor<T> provider)
        {
            if (provider != null)
                lock (m_tableProviders)
                {
                    uint key = Locate(provider);
                    m_tableProviders[key].Remove(provider);
                }
        }


        [Conditional("DEBUG")]
        public static void AssertAll()
        {
            int dpCount = 0;

            foreach (uint key in m_tableProviders.Keys)
            {
                int n = m_tableProviders[key].Count;

                if (n != 0)
                    System.Diagnostics.Debug.WriteLine("Warn! Table ID: {0}, # of dps  = {1}." , key , n);

                dpCount += n;
            }

            Debug.Assert(dpCount == 0);
        }

        
        //private:
        static uint Locate<T>(IDatumAccessor<T> provider)
        {
            foreach (uint key in m_tableProviders.Keys)
                if (m_tableProviders[key].Contains(provider))
                    return key;

            return 0;
        }


        static bool IsProviderRegistered<T>(IDatumAccessor<T> provider)
        {
            if (provider == null)
                return false;

            lock (m_tableProviders)
            {
                return Locate(provider) != 0;
            }
        }
    }
}
