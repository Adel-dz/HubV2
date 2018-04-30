using System.Text;
using easyLib.IO;



namespace easyLib.DB
{
    partial class FramedTable<T>
    {
        /*
         * Version: 1
         */ 
        class TableHeader: FileHeader
        {
            const string SIGNATURE = "EZDBFR1";
            int m_slotsCount;
            int m_ndxFirstFreeSolt;

            //total slots
            public int SlotsCount   //nothrow
            {
                get { return m_slotsCount; }

                set
                {
                    m_slotsCount = value;
                    IsDirty = true;
                }
            } 

            public int FreeSlotsHead    //nothrow
            {
                get { return m_ndxFirstFreeSolt; }

                set
                {
                    m_ndxFirstFreeSolt = value;
                    IsDirty = true;
                }
            }
                 


            //proteced:
            protected override byte[] Signature => Encoding.UTF8.GetBytes(SIGNATURE);   //nothrow

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
