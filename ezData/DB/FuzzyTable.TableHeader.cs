using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using easyLib.IO;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    partial class FuzzyTable<T>
    {
        /*
         * Version: 1
         */
        class TableHeader: FileHeader
        {
            const string SIGNATURE = "EZDBFZ1";
            int m_dataCount;


            public int DataCount    //nothrow
            {
                get { return m_dataCount; }
                set
                {
                    Assert(value >= 0);

                    m_dataCount = value;
                    IsDirty = true;
                }
            }


            //protected:
            protected override byte[] Signature => Encoding.UTF8.GetBytes(SIGNATURE); //nothrow;

            protected override void DoRead(IReader reader)
            {
                Assert(reader != null);

                DataCount = (int)reader.ReadLong();
            }

            protected override void DoWrite(IWriter writer)
            {
                Assert(writer != null);

                writer.Write((long)DataCount);
            }
        }
    }
}
