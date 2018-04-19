using static System.Diagnostics.Debug;

namespace easyLib.IO
{
    public interface ISeekableReader: IReader
    {
        long Position { get; set; }
        long Length { get; }
    }



    public sealed class SeekableReader: RawDataReader, ISeekableReader
    {
        public SeekableReader(System.IO.FileStream input) :
            base(input)
        { }

        public SeekableReader(System.IO.MemoryStream input) :
            base(input)
        { }
        

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
