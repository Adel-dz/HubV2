using easyLib.IO;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{

    /*
     * Version: 1
     */ 
    public interface IDatum //nothrow
    {
        uint ID { get; }
    }


    /*
     * Version: 1
     */ 
    public abstract class Datum: IDatum, IStorable
    {
        public const uint NULL_ID = 0;


        public Datum(uint id = NULL_ID) //nothrow
        {
            ID = id;
        }


        public uint ID { get; private set; }    //nothrow

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
        
        public override int GetHashCode() => ID.GetHashCode();  //nothrow


        //protected:
        /* Pre:
         * - reader != null
         */ 
        public abstract void DoRead(IReader reader);

        /* Pre:
         * - writer != null
         */
        public abstract void DoWrite(IWriter writer);
    }
}
