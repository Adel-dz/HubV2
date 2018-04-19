using System;
using System.Collections.Generic;
using static System.Diagnostics.Debug;



namespace easyLib.DB
{
    public enum AggregationMode_t
    {
        Accepted,
        Rejected
    }


    sealed class ProviderMapper<T>
    {
        readonly Predicate<T> m_filter;
        readonly List<int> m_srcIndices = new List<int>(); 
        readonly AggregationMode_t m_aggMode;
        readonly IDatumGetter<T> m_source;


        public ProviderMapper(IDatumGetter<T> source , Predicate<T> filter , AggregationMode_t mode)
        {
            Assert(source != null);
            Assert(filter != null);

            m_filter = filter;
            m_aggMode = mode;
            m_source = source;
        }


        public IDatumGetter<T> Source => m_source;
        public AggregationMode_t AggregationMode => m_aggMode;
        public Predicate<T> Filter => m_filter;
        public bool IsConnected { get; private set; }

        public int Count
        {
            get
            {
                Assert(IsConnected);

                return m_srcIndices.Count;
            }
        }


        public void Connect()
        {
            Assert(!IsConnected);

            if (m_aggMode == AggregationMode_t.Accepted)
            {
                for (int i = 0; i < m_source.Count; ++i)
                    if (m_filter(m_source.Get(i)))
                        m_srcIndices.Add(i);
            }
            else
                for (int i = 0; i < m_source.Count; ++i)
                    if (!m_filter(m_source.Get(i)))
                        m_srcIndices.Add(i);

            IsConnected = true;
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                m_srcIndices.Clear();
                IsConnected = false;
            }
        }

        public bool IsSelected(int ndxItem)
        {
            Assert(IsConnected);

            bool isOk;

            if (m_aggMode == AggregationMode_t.Accepted)
                isOk = m_srcIndices.BinarySearch(ndxItem) >= 0;
            else
                isOk = m_srcIndices.BinarySearch(ndxItem) < 0;


            return isOk;
        }

        public int FromSourceIndex(int ndxSrcItem)
        {
            Assert(IsConnected);
            Assert(IsSelected(ndxSrcItem));

            if (m_aggMode == AggregationMode_t.Accepted)
            {
                int ndxItem = m_srcIndices.BinarySearch(ndxSrcItem);
                Assert(ndxItem >= 0);

                return ndxItem;
            }


            int ndx;
            int k = m_srcIndices.Count;

            if (k == 0 || ndxSrcItem < m_srcIndices[0])
                ndx = ndxSrcItem;
            else if (m_srcIndices[k - 1] < ndxSrcItem)
                ndx = ndxSrcItem - k;
            else
            {

                int pos = m_srcIndices.BinarySearch(ndxSrcItem);

                Assert(pos < 0);
                ndx = ndxSrcItem - ~pos;

            }

            return ndx;
        }

        public int ToSourceIndex(int ndxItem)
        {
            Assert(IsConnected);

            if (m_aggMode == AggregationMode_t.Accepted)
            {
                Assert(ndxItem < m_srcIndices.Count);
                return m_srcIndices[ndxItem];
            }

            int result;
            int k = m_srcIndices.Count;

            if (k == 0 || ndxItem < m_srcIndices[0])
                result = ndxItem;
            else if (ndxItem >= m_srcIndices[k - 1] - k + 1)
                result = ndxItem + k;
            else
            {
                int ndx = 1;

                while (m_srcIndices[ndx] - ndx <= ndxItem)
                    ++ndx;

                Assert(m_srcIndices[ndx - 1] - ndx + 1 <= ndxItem);

                result = ndxItem + ndx;
            }

            return result;
        }

        public void OnSourceItemInserted(int ndxItem , T item)
        {
            Assert(IsConnected);

            int pos = m_srcIndices.BinarySearch(ndxItem);

            if (pos < 0)
                pos = ~pos;

            for (int i = pos; i < m_srcIndices.Count; ++i)
                ++m_srcIndices[i];


            if (m_aggMode == AggregationMode_t.Accepted)
            {
                if (m_filter(item))
                    m_srcIndices.Insert(pos , ndxItem);    //TODO: optimize      
            }
            else
                if (!m_filter(item))
                m_srcIndices.Insert(pos , ndxItem);    //TODO: optimize                     
        }

        public void OnSourceItemDeleted(int ndxItem)
        {
            Assert(IsConnected);
            
            int pos = m_srcIndices.BinarySearch(ndxItem);

            if (pos < 0)
                pos = ~pos;
            else
                m_srcIndices.RemoveAt(pos); //TODO: Optimize

            for (int i = pos; i < m_srcIndices.Count; ++i)
                --m_srcIndices[i];

        }

        public void OnSourceItemReplaced(int ndxItem , T item)
        {
            Assert(IsConnected);

            bool accepted = m_filter(item);

            int pos = m_srcIndices.BinarySearch(ndxItem);

            if (m_aggMode == AggregationMode_t.Accepted)
                if (accepted)
                {
                    if (pos < 0)
                        m_srcIndices.Insert(~pos , ndxItem);
                }
                else
                {
                    if (pos >= 0)
                        m_srcIndices.RemoveAt(pos);
                }
            else if (accepted)
            {
                if (pos >= 0)
                    m_srcIndices.RemoveAt(pos);
            }
            else if (pos < 0)
                m_srcIndices.Insert(~pos , ndxItem);
        }
    }
}
