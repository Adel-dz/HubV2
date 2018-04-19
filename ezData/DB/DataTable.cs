using easyLib.IO;
using System;
using System.Linq;
using System.Threading;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    public interface IDataTable<T>: IDataSource<T>
    {
        string FilePath { get; }
        bool IsOpen { get; }

        void Open();
        void Close();
        void Create();
        IDisposable Lock();
        IDisposable TryLock();
        uint GetNextAutoID();
        void Flush();
        void Insert(T datum);
        void Insert(T[] data);
        void Replace(int ndx , T datum);
        void Delete(int ndx);
        void Delete(int[] indices);
        void Clear();
        T Get(int ndx);
        T[] Get(int[] indices);
    }


    public interface IDataTable: IDataTable<IDatum>
    { }


    public abstract partial class DataTable<T>: IDataTable<T>
        where T : IDatum, IO.IStorable
    {
        readonly object m_rwLock = new object();
        System.IO.FileStream m_dataFile;

        public DataTable(uint id , string filePath)
        {
            Assert(!string.IsNullOrWhiteSpace(filePath));

            FilePath = filePath;
            ID = id;
        }

        public IDatumProvider DataProvider => new DatumProvider(this);
        public string FilePath { get; }
        public uint ID { get; }
        public bool IsOpen { get; private set; }
        public bool IsDisposed { get; private set; }

        public DateTime CreationTime
        {
            get
            {
                Assert(IsOpen);

                Monitor.Enter(m_rwLock);
                DateTime dt = Header.CreationTime;
                Monitor.Exit(m_rwLock);

                return dt;
            }
        }

        public int DataCount
        {
            get
            {
                Monitor.Enter(m_rwLock);
                int count = GetDataCount();
                Monitor.Exit(m_rwLock);

                return count;
            }
        }

        public DateTime LastAccessTime
        {
            get
            {
                Assert(IsOpen);

                Monitor.Enter(m_rwLock);
                DateTime dt = Header.LastAccessTime;
                Monitor.Exit(m_rwLock);

                return dt;
            }
        }

        public DateTime LastWriteTime
        {
            get
            {
                Assert(IsOpen);

                Monitor.Enter(m_rwLock);
                DateTime dt = Header.LastWriteTime;
                Monitor.Exit(m_rwLock);

                return dt;

            }
        }

        public uint Version
        {
            get
            {
                Assert(IsOpen);

                Monitor.Enter(m_rwLock);
                uint ver = Header.DataVersion;
                Monitor.Exit(m_rwLock);

                return ver;
            }

            set
            {
                Assert(IsOpen);

                Monitor.Enter(m_rwLock);
                Header.DataVersion = value;
                Monitor.Exit(m_rwLock);
            }
        }

        public void Clear()
        {
            Assert(IsOpen);

            Monitor.Enter(m_rwLock);
            Header.Reset();
            Header.LastAccessTime = Header.LastWriteTime = DateTime.Now;

            DoClear();

            Monitor.Exit(m_rwLock);
        }

        public void Open()
        {
            Assert(!IsOpen);

            const int BUFFER_SIZE = 4096;

            System.Diagnostics.Debug.WriteLine("Ouverture du fichier {0}." , FilePath);

            lock (m_rwLock)
            {
                m_dataFile = new System.IO.FileStream(FilePath ,
                    System.IO.FileMode.Open ,
                    System.IO.FileAccess.ReadWrite ,
                    System.IO.FileShare.Read ,
                    BUFFER_SIZE ,
                    System.IO.FileOptions.RandomAccess);



                try
                {
                    Reader = new SeekableReader(m_dataFile);
                    Header.Read(Reader);
                }
                catch (Exception ex)
                {
                    m_dataFile.Dispose();
                    m_dataFile = null;
                    Reader = null;

                    throw new CorruptedFileException(FilePath , innerException: ex);
                }

                Writer = new SeekableWriter(m_dataFile);
                Init();
            }
        }

        public void Create()
        {
            Assert(IsOpen == false);

            const int BUFFER_SIZE = 4096;

            System.Diagnostics.Debug.WriteLine("Création de fichier {0}." , FilePath);

            try
            {
                Monitor.Enter(m_rwLock);

                m_dataFile = new System.IO.FileStream(FilePath ,
                    System.IO.FileMode.CreateNew ,
                 System.IO.FileAccess.ReadWrite ,
                 System.IO.FileShare.Read ,
                 BUFFER_SIZE ,
                 System.IO.FileOptions.RandomAccess);

                Header.Reset();
                Header.CreationTime = DateTime.Now;

                Writer = new SeekableWriter(m_dataFile);
                Header.Write(Writer);
                Reader = new SeekableReader(m_dataFile);

                Init();
            }
            finally
            {
                if (m_dataFile != null)
                {
                    m_dataFile.Dispose();
                    Reader = null;
                    Writer = null;

                    Monitor.Exit(m_rwLock);
                }
            }
        }

        public void Delete(int[] indices)
        {
            Assert(IsOpen);
            Assert(indices != null);
            Assert(indices.Any(ndx => ndx < 0 || ndx >= DataCount) == false);

            if (indices.Length > 0)
                lock (m_rwLock)
                {
                    DataDeleting?.Invoke(indices);

                    for (int ndx = indices[indices.Length - 1]; ndx >= 0; --ndx)
                        DoDelete(ndx);

                    Header.LastWriteTime = Header.LastAccessTime = DateTime.Now;
                    DataDeleted?.Invoke(indices);
                }
        }

        public void Delete(int ndx)
        {
            Assert(IsOpen);
            Assert(ndx >= 0 && ndx < DataCount);

            lock (m_rwLock)
            {
                DatumDeleting?.Invoke(ndx);
                DoDelete(ndx);
                Header.LastWriteTime = Header.LastAccessTime = DateTime.Now;
                DatumDeleted?.Invoke(ndx);
            }
        }

        public void Close()
        {
            lock (m_rwLock)
                if (IsOpen)
                {
                    Flush();

                    DoDispose();
                    IsOpen = false;

                    DatumInserted = DatumReplaced = DatumReplacing = null;
                    DatumDeleting = DatumDeleted = null;
                    DataInserted = null;
                    DataDeleted = DataDeleting = null;

                    m_dataFile.Dispose();
                    m_dataFile = null;
                    Reader = null;
                    Writer = null;
                    IsDisposed = true;
                }
        }

        public void Dispose() => Close();

        public void Flush()
        {
            Assert(IsOpen);

            lock(m_rwLock)
            {
                if(Header.IsDirty)
                {
                    Writer.Position = 0;
                    Header.Write(Writer);
                }

                m_dataFile.Flush();
            }
        }

        public T[] Get(int[] indices)
        {
            Assert(IsOpen);
            Assert(indices != null);
            Assert(indices.Any(ndx => ndx < 0 || ndx >= DataCount) == false);

            lock(m_rwLock)
            {
                var data = new T[indices.Length];

                foreach (int ndx in indices)
                    data[ndx] = DoGet(ndx);

                Header.LastAccessTime = DateTime.Now;

                return data;
            }
        }

        public T Get(int ndx)
        {
            Assert(IsOpen);
            Assert(ndx >= 0 && ndx < DataCount);

            lock(m_rwLock)
            {
                Header.LastAccessTime = DateTime.Now;
                return DoGet(ndx);
            }
        }

        public uint GetNextAutoID()
        {
            Assert(IsOpen);

            Monitor.Enter(m_rwLock);
            uint id = ++Header.AutoID;
            Monitor.Exit(m_rwLock);

            return id;
        }

        public void Insert(T[] data)
        {
            Assert(IsOpen);
            Assert(data != null);
            Assert(data.Any(d => d == null) == false);

            lock(m_rwLock)
            {
                var indices = new int[data.Length];

                for (int ndx = 0; ndx < indices.Length; ++ndx)
                    indices[ndx] = DoInsert(data[ndx]);

                Header.LastAccessTime = Header.LastWriteTime = DateTime.Now;
                DataInserted?.Invoke(indices, data);
            }
        }

        public void Insert(T datum)
        {
            Assert(IsOpen);
            Assert(datum != null);

            lock(m_rwLock)
            {
                int ndx = DoInsert(datum);
                Header.LastWriteTime = Header.LastAccessTime = DateTime.Now;

                DatumInserted?.Invoke(ndx , datum);
            }
        }

        public void Replace(int ndx , T datum)
        {
            Assert(IsOpen);
            Assert(ndx >= 0 && ndx < DataCount);
            Assert(datum != null);

            lock(m_rwLock)
            {
                T curDatum = DoGet(ndx);
                DatumReplacing?.Invoke(ndx , curDatum);

                int newIndex = DoReplace(ndx , datum);
                Header.LastWriteTime = Header.LastAccessTime = DateTime.Now;

                DatumReplaced?.Invoke(newIndex , datum);
            }
        }

        public IDisposable Lock()
        {
            Monitor.Enter(m_rwLock);

            return new AutoReleaser(Unlock);
        }

        public IDisposable TryLock()
        {
            if (Monitor.TryEnter(m_rwLock))
                return new AutoReleaser(Unlock);

            return null;
        }


        //protected:
        protected SeekableReader Reader { get; private set; }
        protected SeekableWriter Writer { get; private set; }

        protected abstract FileHeader Header { get; }
        protected abstract int GetDataCount();
        protected abstract void DoClear();
        protected abstract void Init();
        protected abstract int DoInsert(T datum);
        protected abstract T DoGet(int ndx);
        protected abstract void DoDelete(int ndx);
        protected abstract int DoReplace(int ndx , T datum);
        protected abstract void DoDispose();
        //protected abstract IDataColumn[] GetColumns();

        
        event Action<int> DatumDeleting;
        event Action<int> DatumDeleted;
        event Action<int[]> DataDeleting;
        event Action<int[]> DataDeleted;
        event Action<int , T> DatumInserted;
        event Action<int[] , T[]> DataInserted;
        event Action<int , T> DatumReplacing;
        event Action<int , T> DatumReplaced;
        event Action ProvidersInvalidated;

        void Unlock() => Monitor.Exit(m_rwLock);
    }
}
