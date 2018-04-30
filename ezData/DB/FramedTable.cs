using easyLib.IO;
using System;
using System.Collections.Generic;
using System.IO;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    /*
     * Version: 1
     */
    public abstract partial class FramedTable<T>: DataTable<T>
        where T : IDatum, IStorable
    {
        const int NULL_NDX = -1;
        readonly TableHeader m_header;
        readonly List<int> m_deletedFrames = new List<int>();
        readonly SeekableWriter m_buffWriter;
        readonly byte[] m_wrBuffer;


        public FramedTable(uint id , string filePath) : //nothrow
            base(id , filePath)
        {
            m_header = new TableHeader();
            m_wrBuffer = new byte[DatumSize];
            var ms = new MemoryStream(m_wrBuffer);
            m_buffWriter = new SeekableWriter(ms);
        }
        

        //protected:
        protected override FileHeader Header => m_header;   //nothrow

        protected abstract int DatumSize { get; }   //nothrow
        protected abstract T CreateEmptyDatum();    //nothrow
        

        protected override void DoClear()   //nothrow
        {
            Assert(IsConnected);

            m_header.FreeSlotsHead = NULL_NDX;
            m_header.SlotsCount = 0;

            m_deletedFrames.Clear();

            Assert(DataCount == 0);
        }

        protected override void DoDelete(int ndx)
        {
            Assert(IsConnected);
            Assert(ndx >= 0 && ndx < DataCount);

            int ndxFrame = ItemIndexToFrameIndex(ndx);
            Writer.Position = GetFramePosition(ndxFrame);
            Writer.Write(m_header.FreeSlotsHead);
            m_header.FreeSlotsHead = ndxFrame;

            int loc = m_deletedFrames.BinarySearch(ndxFrame);

            Assert(loc < 0);

            m_deletedFrames.Insert(~loc , ndxFrame);
        }

        protected override T DoGet(int ndx)
        {
            Assert(IsConnected);
            Assert(ndx >= 0 && ndx < DataCount);

            int ndxFrame = ItemIndexToFrameIndex(ndx);
            Reader.Position = GetFramePosition(ndxFrame);

            T item = CreateEmptyDatum();
            item.Read(Reader);

            return item;
        }

        protected override int DoInsert(T datum)
        {
            Assert(IsConnected);
            Assert(datum != null);

            int ndxNew;

            m_buffWriter.Position = 0;
            datum.Write(m_buffWriter);

            if (m_deletedFrames.Count > 0)
            {
                ndxNew = m_header.FreeSlotsHead;
                m_deletedFrames.RemoveAt(m_deletedFrames.BinarySearch(ndxNew));

                long framePos = GetFramePosition(ndxNew);

                if (m_deletedFrames.Count > 0)
                {
                    Reader.Position = framePos;
                    m_header.FreeSlotsHead = Reader.ReadInt();
                }
                else
                    m_header.FreeSlotsHead = NULL_NDX;

                Writer.Position = framePos;
                Writer.Write(m_wrBuffer);
            }
            else
            {
                ndxNew = GetDataCount();

                Writer.Position = GetFramePosition(ndxNew);
                Writer.Write(m_wrBuffer);
                ++m_header.SlotsCount;
            }

            return FrameIndexToItemIndex(ndxNew);
        }
        
        protected override int DoReplace(int ndx , T datum)
        {
            Assert(IsConnected);
            Assert(ndx >= 0 && ndx < DataCount);
            Assert(datum != null);

            m_buffWriter.Position = 0;
            datum.Write(m_buffWriter);

            int ndxFrame = ItemIndexToFrameIndex(ndx);
            Writer.Position = GetFramePosition(ndxFrame);
            Writer.Write(m_wrBuffer);

            return ndx;
        }

        protected override int GetDataCount()
        {
            Assert(IsConnected);

            int count = m_header.SlotsCount - m_deletedFrames.Count;

            Assert(count >= 0);
            return count;
        }

        protected override void Init()
        {
            Assert(Reader != null);
            Assert(Writer != null);

            int ndxFrame = m_header.FreeSlotsHead;

            m_deletedFrames.Clear();

            while (ndxFrame != NULL_NDX)
            {
                m_deletedFrames.Add(ndxFrame);
                Reader.Position = GetFramePosition(ndxFrame);
                ndxFrame = Reader.ReadInt();
            }

            m_deletedFrames.Sort();            
        }


        //private:
        int FrameSize => Math.Max(DatumSize , sizeof(long));    //nothrow
        long GetFramePosition(int ndxFrame) => Header.DataOffset + ndxFrame * FrameSize;  //nothrow

        int ItemIndexToFrameIndex(int ndxItem)  //nothrow
        {
            int k = m_deletedFrames.Count;

            if (k == 0 || ndxItem < m_deletedFrames[0])
                return ndxItem;

            if (ndxItem >= m_deletedFrames[k - 1] - k + 1)
                return ndxItem + k;

            int ndx = 1;

            while (m_deletedFrames[ndx] - ndx <= ndxItem)
                ++ndx;

            Assert(m_deletedFrames[ndx - 1] - ndx + 1 <= ndxItem);

            return ndxItem + ndx;
        }

        int FrameIndexToItemIndex(int ndxFrame)   //nothrow
        {
            int k = m_deletedFrames.Count;

            Assert(ndxFrame < m_header.SlotsCount);

            if (k == 0 || ndxFrame < m_deletedFrames[0])
                return ndxFrame;

            if (m_deletedFrames[k - 1] < ndxFrame)
                return ndxFrame - k;

            int pos = m_deletedFrames.BinarySearch(ndxFrame);

            Assert(pos < 0);
            return ndxFrame - ~pos;
        }
    }
}
