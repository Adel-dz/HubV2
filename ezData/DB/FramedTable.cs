using easyLib.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    public abstract partial class FramedTable<T>: DataTable<T>
        where T : IDatum, IO.IStorable
    {
        const int NULL_NDX = -1;
        readonly TableHeader m_header;
        readonly List<int> m_deletedFrames = new List<int>();
        readonly SeekableWriter m_buffWriter;
        readonly byte[] m_wrBuffer;


        public FramedTable(uint id , string filePath) :
            base(id , filePath)
        {
            m_header = new TableHeader();
            m_wrBuffer = new byte[DatumSize];
            var ms = new MemoryStream(m_wrBuffer);
            m_buffWriter = new SeekableWriter(ms);
        }
        

        //protected:
        protected override FileHeader Header => m_header;        

        protected abstract int DatumSize { get; }
        protected abstract T CreateEmptyDatum();
        

        protected override void DoClear()
        {
            m_header.FreeSlotsHead = NULL_NDX;
            m_header.SlotsCount = 0;

            m_deletedFrames.Clear();
        }

        protected override void DoDelete(int ndx)
        {
            int ndxFrame = ItemIndexToFrameIndex(ndx);
            Writer.Position = GetFramePosition(ndxFrame);
            Writer.Write(m_header.FreeSlotsHead);
            m_header.FreeSlotsHead = ndxFrame;

            int loc = m_deletedFrames.BinarySearch(ndxFrame);

            Assert(loc < 0);

            m_deletedFrames.Insert(~loc , ndxFrame);
        }

        protected override void DoDispose() => m_deletedFrames.Clear();

        protected override T DoGet(int ndx)
        {
            int ndxFrame = ItemIndexToFrameIndex(ndx);
            Reader.Position = GetFramePosition(ndxFrame);

            T item = CreateEmptyDatum();
            item.Read(Reader);

            return item;
        }

        protected override int DoInsert(T datum)
        {
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
            throw new NotImplementedException();
        }

        protected override int GetDataCount()
        {
            throw new NotImplementedException();
        }

        protected override void Init()
        {
            int ndxFrame = m_header.FreeSlotsHead;

            Assert(m_deletedFrames.Count == 0);

            while (ndxFrame != NULL_NDX)
            {
                m_deletedFrames.Add(ndxFrame);
                Reader.Position = GetFramePosition(ndxFrame);
                ndxFrame = Reader.ReadInt();
            }

            m_deletedFrames.Sort();            
        }


        //private:
        int FrameSize => Math.Max(DatumSize , sizeof(long));        

        private long GetFramePosition(int ndxFrame)
        {
            throw new NotImplementedException();
        }

        private int ItemIndexToFrameIndex(object ndxItem)
        {
            throw new NotImplementedException();
        }

        private int FrameIndexToItemIndex(int ndxNew)
        {
            throw new NotImplementedException();
        }

    }
}
