using easyLib.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using static System.Diagnostics.Debug;



namespace easyLib.DB
{
    /* 
     * Version: 1 
     */
    public interface IDataTable<T>: IDataSource<T>
    {
        string FilePath { get; }
    }
    //----------------------------------------------------


    /* 
     * Version: 1
     */
    public interface IDataTable: IDataTable<IDatum>
    { }
    //---------------------------------------------------


    /*
     * Version: 1
     */
    public abstract partial class DataTable<T>: IDataTable<T>
        where T : IDatum, IStorable
    {
        System.IO.FileStream m_dataFile;
        int m_dpCount;


        public DataTable(uint id , string filePath)
        {
            Assert(!string.IsNullOrWhiteSpace(filePath));

            FilePath = filePath;
            ID = id;
        }   //nothrow


        public string FilePath { get; } //nothrow
        public uint ID { get; } //nothrow
        public IDatumProvider DataProvider => new DatumProvider(this);  //nothrow


        //protected:
        protected bool IsConnected => m_dpCount > 0;    //nothrow
        protected SeekableReader Reader { get; private set; }   //nothrow
        protected SeekableWriter Writer { get; private set; }   //nothrow
        protected abstract FileHeader Header { get; }   //nothrow

        protected int DataCount //nothrow
        {
            get
            {
                Assert(IsConnected);

                return GetDataCount();
            }
        }


        protected virtual IList<int> DoInsert(IList<T> data)
        {
            Assert(IsConnected);
            Assert(data != null);

            var indices = new int[data.Count];

            for (int ndx = 0; ndx < indices.Length; ++ndx)
                indices[ndx] = DoInsert(data[ndx]);

            return indices;
        }

        /* Pre:
         * IsConnected
         * 
         * Post: 
         * - Result >= 0
         */
        protected abstract int GetDataCount();  //nothrow

        /* Pre:
         * - IsConnected
         * 
         * Post:
         * - DataCount == 0
         */
        protected abstract void DoClear();

        /* Pre:
         * - Reader != null
         * - Writer != null
         */
        protected abstract void Init();

        /* Pre:
         * - IsConnected
         * - datum != null
         */
        protected abstract int DoInsert(T datum);

        /* Pre:
         * - IsConnected
         * - ndx >= 0 && ndx < Count
         * 
         * Post:
         * - Result != null
         */
        protected abstract T DoGet(int ndx);

        /* Pre:
         * - IsConnected
         * - ndx >= 0 && ndx < Count
         * 
         * Post:
         * - Count = old Count - 1
         */
        protected abstract void DoDelete(int ndx);

        /* Pre:
         * - IsConnected
         * - ndx >= 0 && ndx < Count
         * - datum != null
         */
        protected abstract int DoReplace(int ndx , T datum);


        //private: 
        void Connect()
        {
            const int BUFFER_SIZE = 4096;

            if (m_dpCount == 0)
            {
                bool connected = false;

                try
                {
                    m_dataFile = new System.IO.FileStream(FilePath ,
                        System.IO.FileMode.Open ,
                        System.IO.FileAccess.ReadWrite ,
                        System.IO.FileShare.Read ,
                        BUFFER_SIZE ,
                        System.IO.FileOptions.RandomAccess);

                    Reader = new SeekableReader(m_dataFile);
                    Writer = new SeekableWriter(m_dataFile);
                    Header.Read(Reader);

                    Init();
                    connected = true;
                }
                catch (System.IO.FileNotFoundException)
                {
                    System.Diagnostics.Debug.WriteLine("Try to create the file {0}." , FilePath);

                    m_dataFile = new System.IO.FileStream(FilePath ,
                        System.IO.FileMode.CreateNew ,
                        System.IO.FileAccess.ReadWrite ,
                        System.IO.FileShare.Read ,
                        BUFFER_SIZE ,
                        System.IO.FileOptions.RandomAccess);

                    Header.Reset();
                    Header.CreationTime = DateTime.Now;

                    Writer = new SeekableWriter(m_dataFile);
                    Reader = new SeekableReader(m_dataFile);
                    Header.Write(Writer);

                    Init();
                    connected = true;
                }
                finally
                {
                    if (!connected)
                    {
                        m_dataFile?.Dispose();
                        m_dataFile = null;
                        Reader = null;
                        Writer = null;
                    }
                }
            }

            ++m_dpCount;
            Assert(IsConnected);
        }

        void Disconnect()
        {
            if (IsConnected)
            {
                Flush();

                if (--m_dpCount == 0)
                {
                    m_dataFile.Dispose();
                    m_dataFile = null;
                    Reader = null;
                    Writer = null;
                }
            }

            Assert(!IsConnected);
        }

        void Clear()
        {
            Assert(IsConnected);

            Header.Reset();
            Header.LastAccessTime = Header.LastWriteTime = DateTime.Now;
            DoClear();

            Assert(DataCount == 0);
        }

        void Delete(IList<int> indices)
        {
            Assert(IsConnected);
            Assert(indices != null);
            Assert(indices.Any(ndx => ndx < 0 || ndx >= DataCount) == false);

            if (indices.Count > 0)
            {
                for (int ndx = indices[indices.Count - 1]; ndx >= 0; --ndx)
                    DoDelete(ndx);

                Header.LastWriteTime = Header.LastAccessTime = DateTime.Now;
            }
        }

        void Delete(int ndx)
        {
            Assert(IsConnected);
            Assert(ndx >= 0 && ndx < DataCount);

            DoDelete(ndx);
            Header.LastWriteTime = Header.LastAccessTime = DateTime.Now;
        }

        void Flush()
        {
            Assert(IsConnected);

            if (Header.IsDirty)
            {
                Writer.Position = 0;
                Header.Write(Writer);
            }

            m_dataFile.Flush();
        }

        IList<T> Get(IList<int> indices)
        {
            Assert(IsConnected);
            Assert(indices != null);
            Assert(indices.Any(ndx => ndx < 0 || ndx >= DataCount) == false);

            var data = new T[indices.Count];

            foreach (int ndx in indices)
                data[ndx] = DoGet(ndx);

            Header.LastAccessTime = DateTime.Now;

            return data;
        }

        T Get(int ndx)
        {
            Assert(IsConnected);
            Assert(ndx >= 0 && ndx < DataCount);

            T datum = DoGet(ndx);
            Header.LastAccessTime = DateTime.Now;

            return datum;
        }

        uint GetNextAutoID()    //nothrow
        {
            Assert(IsConnected);

            return ++Header.AutoID;
        }

        IList<int> Insert(IList<T> data)
        {
            Assert(IsConnected);
            Assert(data != null);
            Assert(data.Any(d => d == null) == false);

            IList<int> indices = DoInsert(data);

            Header.LastAccessTime = Header.LastWriteTime = DateTime.Now;
            return indices;
        }

        int Insert(T datum)
        {
            Assert(IsConnected);
            Assert(datum != null);

            int ndx = DoInsert(datum);
            Header.LastWriteTime = Header.LastAccessTime = DateTime.Now;

            return ndx;
        }

        int Replace(int ndx , T datum)
        {
            Assert(IsConnected);
            Assert(ndx >= 0 && ndx < DataCount);
            Assert(datum != null);

            int newIndex = DoReplace(ndx , datum);
            Header.LastWriteTime = Header.LastAccessTime = DateTime.Now;

            return ndx;
        }
    }
}
