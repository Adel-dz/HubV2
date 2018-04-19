using System;
using System.IO;
using System.Text;
using static System.Diagnostics.Debug;



namespace easyLib.IO
{
    public class RawDataReader: IReader
    {
        readonly BinaryReader m_reader;


        protected RawDataReader(Stream input, Encoding encoding)
        {
            Assert(input != null);
            Assert(input.CanRead);
            Assert(encoding != null);

            m_reader = new BinaryReader(input , encoding , true);
        }

        public RawDataReader(Stream input):
            this(input, new UTF8Encoding(false , true))
        { }



        public int Read(byte[] buffer , int bufferOffset , int count)
        {
            Assert(buffer != null);
            Assert(bufferOffset + count <= buffer.Length);

            return m_reader.Read(buffer , bufferOffset , count);
        }

        public bool ReadBool() => m_reader.ReadBoolean();
        public byte ReadByte() => m_reader.ReadByte();

        public byte[] ReadBytes(int byteCount)
        {
            Assert(byteCount >= 0);

            return m_reader.ReadBytes(byteCount);
        }

        public char ReadChar() => m_reader.ReadChar();
        public decimal ReadDecimal() => m_reader.ReadDecimal();
        public double ReadDouble() => m_reader.ReadDouble();
        public float ReadFloat() => m_reader.ReadSingle();
        public short ReadShort() => m_reader.ReadInt16();
        public int ReadInt() => m_reader.ReadInt32();
        public long ReadLong() => m_reader.ReadInt64();
        public sbyte ReadSByte() => m_reader.ReadSByte();
        public string ReadString() => m_reader.ReadString();
        public DateTime ReadTime() => new DateTime(m_reader.ReadInt64());
        public ushort ReadUShort() => m_reader.ReadUInt16();
        public uint ReadUInt() => m_reader.ReadUInt32();
        public ulong ReadULong() => m_reader.ReadUInt64();

        public void Skip(int byteCount)
        {
            Assert(byteCount >= 0);

            ReadBytes(byteCount);
        }


        //protected:
        protected BinaryReader Reader => m_reader;
    }
}
