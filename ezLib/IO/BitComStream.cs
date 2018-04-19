using System;
using System.IO;
using static System.Diagnostics.Debug;



namespace easyLib.IO
{
    public sealed class BitComStream: Stream
    {
        readonly Stream m_stm;

        public BitComStream(Stream stream)
        {
            Assert(stream != null);

            m_stm = stream;
        }
        

        public bool IsDisposed { get; private set; }
        public override bool CanRead => m_stm.CanRead;
        public override bool CanSeek => m_stm.CanSeek;
        public override bool CanWrite => m_stm.CanWrite;
        public override long Length => m_stm.Length;

        public override long Position
        {
            get { return m_stm.Position; }
            set { m_stm.Position = value; }
        }


        public override void Flush() => m_stm.Flush();
        public override long Seek(long offset , SeekOrigin origin) => m_stm.Seek(offset , origin);
        public override void SetLength(long value) => m_stm.SetLength(value);


        public override int Read(byte[] buffer , int offset , int count)
        {
            int n = m_stm.Read(buffer , offset , count);

            for (int i = offset, sz = n + offset; i < sz; ++i)
                buffer[i] = (byte)~buffer[i];

            return n;
        }

        public override void Write(byte[] buffer , int offset , int count)
        {
            var data = new byte[count];
            Array.Copy(buffer , offset , data , 0 , count);

            for (int i = 0; i < count; ++i)
                data[i] = (byte)~buffer[i + offset];

            m_stm.Write(data , 0 , count);
        }

        public override int ReadByte()
        {
            int value = m_stm.ReadByte();

            if (value >= 0)
                value = ~value & 0XFF;

            return value;
        }

        public override void WriteByte(byte value)
        {
            m_stm.WriteByte((byte)~value);
        }

        //protected:
        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                m_stm.Dispose();
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
