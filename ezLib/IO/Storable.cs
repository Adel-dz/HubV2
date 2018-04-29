using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;


namespace easyLib.IO
{
    /*
     * Version: 1
     */ 
    public interface IStorable
    {
        void Read(IReader reader);
        void Write(IWriter writer);
    }


    /*
     * Version: 1
     */ 
    public static class Storables
    {
        public static byte[] GetBytes(this IStorable storable)
        {
            Assert(storable != null);

            var ms = new System.IO.MemoryStream();
            var writer = new RawDataWriter(ms);

            storable.Write(writer);

            return ms.ToArray();
        }

        public static void SetBytes(this IStorable storable , byte[] data)
        {
            Assert(storable != null);
            Assert(data != null);

            var ms = new System.IO.MemoryStream(data);
            var reader = new RawDataReader(ms);

            storable.Read(reader);
        }
    }
}
