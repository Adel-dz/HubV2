using System.Text;
using easyLib.IO;
using static System.Diagnostics.Debug;



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
                    Assert(value >= 0);

                    m_slotsCount = value;
                    IsDirty = true;
                }
            } 

            public int FreeSlotsHead    //nothrow
            {
                get { return m_ndxFirstFreeSolt; }

                set
                {
                    Assert(value == NULL_NDX || value >= 0);

                    m_ndxFirstFreeSolt = value;
                    IsDirty = true;
                }
            }
                 


            //proteced:
            protected override byte[] Signature => Encoding.UTF8.GetBytes(SIGNATURE);   //nothrow

            protected override void DoRead(IReader reader)
            {
                Assert(reader != null);

                m_slotsCount = reader.ReadInt();
                m_ndxFirstFreeSolt = reader.ReadInt();               
            }

            protected override void DoWrite(IWriter writer)
            {
                Assert(writer != null);

                writer.Write(m_slotsCount);
                writer.Write(m_ndxFirstFreeSolt);
            }
        }
    }
}
