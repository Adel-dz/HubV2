using System;
using System.IO;
using static System.Diagnostics.Debug;



namespace easyLib.IO
{
    /*
     * Version: 1
     * 
     * Performs a bitwise complement operation on its inputs/outputs
     */
    public sealed class BitComplementStream: Stream
    {
        readonly Stream m_stm;

        public BitComplementStream(Stream stream)   //nothrow
        {
            Assert(stream != null);

            m_stm = stream;
        }


        public bool IsDisposed { get; private set; }    //nothrow
        public override bool CanRead => m_stm.CanRead;  //nothrow
        public override bool CanSeek => m_stm.CanSeek;  //nothrow
        public override bool CanWrite => m_stm.CanWrite;    //nothrow

        public override long Length
        {
            get
            {
                Assert(CanSeek);

                return m_stm.Length;
            }
        }

        public override long Position
        {
            get
            {
                Assert(CanSeek);

                return m_stm.Position;
            }

            set
            {
                Assert(CanSeek);

                m_stm.Position = value;
            }
        }


        public override void Flush() => m_stm.Flush();

        public override long Seek(long offset , SeekOrigin origin)
        {
            Assert(CanSeek);
            return m_stm.Seek(offset , origin);
        }

        public override void SetLength(long value)
        {
            Assert(CanSeek);
            Assert(CanWrite);

            m_stm.SetLength(value);
        }

        public override int Read(byte[] buffer , int offset , int count)
        {
            Assert(CanRead);
            Assert(buffer != null);
            Assert(offset + count <= buffer.Length);

            int n = m_stm.Read(buffer , offset , count);

            for (int i = offset, sz = n + offset; i < sz; ++i)
                buffer[i] = (byte)~buffer[i];

            return n;
        }

        public override void Write(byte[] buffer , int offset , int count)
        {
            Assert(CanWrite);
            Assert(buffer != null);
            Assert(offset + count <= buffer.Length);

            var data = new byte[count];
            Array.Copy(buffer , offset , data , 0 , count);

            for (int i = 0; i < count; ++i)
                data[i] = (byte)~buffer[i + offset];

            m_stm.Write(data , 0 , count);
        }

        public override int ReadByte()
        {
            Assert(CanRead);

            int value = m_stm.ReadByte();

            if (value >= 0)
                value = ~value & 0XFF;

            return value;
        }

        public override void WriteByte(byte value)
        {
            Assert(CanWrite);

            m_stm.WriteByte((byte)~value);
        }

        //protected:
        protected override void Dispose(bool disposing) //nothrow
        {
            if (!IsDisposed)
                try
                {
                    base.Dispose(disposing);

                    if (disposing)
                        m_stm.Dispose();

                    IsDisposed = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Exception shut down in BitComplementStream.Dispose(bool): {ex.Message}");
                }
        }
    }
}
