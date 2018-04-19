using easyLib.IO;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{

    public interface IDatum
    {
        uint ID { get; }
    }



    public abstract class Datum: IDatum, IStorable
    {
        public const uint NULL_ID = 0;


        public Datum(uint id = NULL_ID)
        {
            ID = id;
        }


        public uint ID { get; private set; }

        public void Read(IReader reader)
        {
            Assert(reader != null);

            uint ID = reader.ReadUInt();

            if (ID == NULL_ID)
                throw new CorruptedStreamException();

            DoRead(reader);
        }

        public void Write(IWriter writer)
        {
            Assert(writer != null);

            Assert(ID != NULL_ID);
            writer.Write(ID);

            DoWrite(writer);
        }
        
        public override int GetHashCode() => ID.GetHashCode();


        //protected:
        public abstract void DoRead(IReader reader);
        public abstract void DoWrite(IWriter writer);
    }
}
