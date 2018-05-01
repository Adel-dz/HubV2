using System.Collections.Generic;

namespace easyLib.DB
{
    /*
     * Version: 1
     */
    public abstract partial class FuzzyTable<T>: DataTable<T>
        where T : IDatum, IO.IStorable
    {
        const byte DELETED_DATUM_TAG = 0XFF;
        
        readonly TableHeader m_header = new TableHeader();
        readonly List<long> m_positions = new List<long>();
        long m_newItemPos;


        public FuzzyTable(uint id , string filePath) : //nothrow
            base(id , filePath)
        { }


        //protected:
        protected override FileHeader Header => m_header;   //nothrow

        protected abstract T CreateEmptyDatum();

        protected override void DoClear()
        {
            m_positions.Clear();
            m_newItemPos = 0;
        }

        protected override void DoDelete(int ndx)
        {
            Writer.Position = m_positions[ndx];
            Writer.Write(DELETED_DATUM_TAG);
            m_positions.RemoveAt(ndx);
        }

        protected override T DoGet(int ndx)
        {
            Reader.Position = m_positions[ndx] + sizeof(byte);

            T item = CreateEmptyDatum();
            item.Read(Reader);

            return item;
        }

        protected override int DoInsert(T datum)
        {
            Writer.Position = m_newItemPos;

            Writer.Write((byte)0);  //tagged as non deleted            
            datum.Write(Writer);

            m_positions.Add(m_newItemPos);
            m_newItemPos = Writer.Position;
            ++m_header.DataCount;

            return m_positions.Count - 1;
        }

        protected override int DoReplace(int ndx , T datum)
        {
            if (ndx == m_positions.Count - 1)
            {
                Writer.Position = m_positions[ndx] + sizeof(byte);

                datum.Write(Writer);
                m_newItemPos = Writer.Position;
            }
            else
            {
                Writer.Position = m_positions[ndx];
                Writer.Write(DELETED_DATUM_TAG);
                m_positions.RemoveAt(ndx);

                Writer.Position = m_newItemPos;
                Writer.Write((byte)0);  //tagged as non-deleted
                datum.Write(Writer);
                m_positions.Add(m_newItemPos);
                m_newItemPos = Writer.Position;
                ++m_header.DataCount;
            }


            return m_positions.Count - 1;
        }

        protected override int GetDataCount() => m_positions.Count; //nothrow

        protected override void Init()
        {
            Reader.Position = m_header.DataOffset;

            T datum = CreateEmptyDatum();

            for (int i = 0; i < m_header.DataCount; ++i)
            {
                long pos = Reader.Position;
                byte tag = Reader.ReadByte();
                datum.Read(Reader);

                if (tag != DELETED_DATUM_TAG)
                    m_positions.Add(pos);
            }

            m_newItemPos = Reader.Position;
        }
    }
}
