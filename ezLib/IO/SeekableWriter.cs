using static System.Diagnostics.Debug;


namespace easyLib.IO
{
    public interface ISeekableWriter: IWriter
    {
        long Position { get; set; }
        long Length { get; }
    }



    public sealed class SeekableWriter: RawDataWriter, ISeekableWriter
    {
        public SeekableWriter(System.IO.MemoryStream output) :
            base(output)
        { }

        public SeekableWriter(System.IO.FileStream output) :
            base(output)
        { }
        

        public long Length => Writer.BaseStream.Length;

        public long Position
        {
            get { return Writer.BaseStream.Position; }

            set
            {
                Assert(value <= Length);

                Writer.BaseStream.Position = value;
            }
        }
    }
}
