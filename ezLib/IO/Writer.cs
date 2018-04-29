using System;

namespace easyLib.IO
{
    /*
     * Version: 1
     */ 
    public interface IWriter
    {
        /* Pre:
         * - bytes != null
         */ 
        void Write(byte[] bytes);

        /* pre:
         * - buffer != null
         * - bufferOffset + count <= buffer.Length
         */ 
        void Write(byte[] buffer , int bufferOffset , int count);

        void Write(byte b);
        void Write(sbyte b);
        void Write(bool b);
        void Write(char c);
        void Write(short s);
        void Write(ushort s);
        void Write(int i);
        void Write(uint i);
        void Write(long l);
        void Write(ulong l);
        void Write(float f);
        void Write(double d);
        void Write(decimal d);
        void Write(string s);
        void Write(DateTime time);
    }
}
