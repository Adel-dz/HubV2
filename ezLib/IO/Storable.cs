using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace easyLib.IO
{
    public interface IStorable
    {
        void Read(IReader reader);
        void Write(IWriter writer);
    }


    public static class Storables
    {
        public static byte[] GetBytes(this IStorable storable)
        {
            var ms = new System.IO.MemoryStream();
            var writer = new RawDataWriter(ms);

            storable.Write(writer);

            return ms.ToArray();
        }

        public static void SetBytes(this IStorable storable , byte[] data)
        {
            var ms = new System.IO.MemoryStream(data);
            var reader = new RawDataReader(ms);

            storable.Read(reader);
        }
    }
}
