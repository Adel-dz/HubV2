using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static System.Diagnostics.Debug;


namespace easyLib.IO
{
    /*
     * Version: 1
     */ 
    public class RawDataWriter: IWriter
    {
        readonly BinaryWriter m_writer;

        protected RawDataWriter(Stream output, Encoding encoding)   //nothrow
        {
            Assert(output != null);
            Assert(output.CanWrite);
            Assert(encoding != null);
            
            m_writer = new BinaryWriter(output , encoding , true);
        }
        
        public RawDataWriter(Stream output) :   //nothrrow
            this(output, new UTF8Encoding(false , true))
        { }
        

        public void Write(byte b) => m_writer.Write(b);
        public void Write(bool b) => m_writer.Write(b);
        public void Write(short s) => m_writer.Write(s);
        public void Write(int i) => m_writer.Write(i);
        public void Write(long l) => m_writer.Write(l);
        public void Write(float f) => m_writer.Write(f);
        public void Write(decimal d) => m_writer.Write(d);
        public void Write(DateTime time) => m_writer.Write(time.Ticks);
        public void Write(string s)
        {
            Assert(s != null);

            m_writer.Write(s);
        }

        public void Write(double d) => m_writer.Write(d);
        public void Write(ulong l) => m_writer.Write(l);
        public void Write(uint i) => m_writer.Write(i);
        public void Write(ushort s) => m_writer.Write(s);
        public void Write(char c) => m_writer.Write(c);
        public void Write(sbyte b) => m_writer.Write(b);

        public void Write(byte[] bytes)
        {
            Assert(bytes != null);

            m_writer.Write(bytes);
        }

        public void Write(byte[] buffer , int bufferOffset , int count)
        {
            Assert(buffer != null);
            Assert(bufferOffset + count <= buffer.Length);

            m_writer.Write(buffer , bufferOffset , count);
        }


        //protected:
        protected BinaryWriter Writer => m_writer;  //nothrow

    }
}
