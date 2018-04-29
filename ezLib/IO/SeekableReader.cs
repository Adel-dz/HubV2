using static System.Diagnostics.Debug;

namespace easyLib.IO
{
    /*
     * Version: 1
     */ 
    public interface ISeekableReader: IReader
    {
        long Position { get; set; }
        long Length { get; }
    }


    /*
     * Version: 1
     */ 
    public sealed class SeekableReader: RawDataReader, ISeekableReader
    {
        public SeekableReader(System.IO.FileStream input) : //nothrow
            base(input)
        {
            Assert(input != null);
            Assert(input.CanRead);
        }

        public SeekableReader(System.IO.MemoryStream input) :   //nothrow
            base(input)
        {
            Assert(input != null);
        }
        

        public long Length => Reader.BaseStream.Length; 

        public long Position
        {
            get { return Reader.BaseStream.Position; }

            set
            {
                Assert(value <= Length);

                Reader.BaseStream.Position = value;
            }
        }
    }
}
