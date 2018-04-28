using System.Text;
using easyLib.IO;



namespace easyLib.DB
{
    partial class FramedTable<T>
    {
        class TableHeader: DataTable<T>.FileHeader
        {
            const string SIGNATURE = "EZDBFR1";
            int m_slotsCount;
            int m_ndxFirstFreeSolt;


            public int SlotsCount //total slots
            {
                get { return m_slotsCount; }

                set
                {
                    m_slotsCount = value;
                    IsDirty = true;
                }
            } 

            public int FreeSlotsHead
            {
                get { return m_ndxFirstFreeSolt; }

                set
                {
                    m_ndxFirstFreeSolt = value;
                    IsDirty = true;
                }
            }
                 


            //proteced:
            protected override byte[] Signature => Encoding.UTF8.GetBytes(SIGNATURE);

            protected override void DoRead(IReader reader)
            {
                m_slotsCount = reader.ReadInt();
                m_ndxFirstFreeSolt = reader.ReadInt();               
            }

            protected override void DoWrite(IWriter writer)
            {
                writer.Write(m_slotsCount);
                writer.Write(m_ndxFirstFreeSolt);
            }
        }
    }
}
