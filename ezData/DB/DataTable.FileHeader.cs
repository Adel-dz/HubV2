using easyLib.IO;
using System;
using static System.Diagnostics.Debug;


namespace easyLib.DB
{
    partial class DataTable<T>
    {

        protected abstract class FileHeader
        {
            DateTime m_tmCreation, m_tmLastWrite, m_tmLastAccess;
            long m_dataOffest;
            uint m_dataVersion;
            uint m_autoID;
            

            public bool IsDirty { get; protected set; }

            public DateTime CreationTime
            {
                get { return m_tmCreation; }
                set
                {
                    m_tmCreation = value;
                    IsDirty = true;
                }
            }

            public DateTime LastWriteTime
            {
                get { return m_tmLastWrite; }
                set
                {
                    m_tmLastWrite = value;
                    IsDirty = true;
                }
            }

            public DateTime LastAccessTime
            {
                get { return m_tmLastAccess; }
                set
                {
                    m_tmLastAccess = value;
                    IsDirty = true;
                }
            }

            public uint DataVersion
            {
                get { return m_dataVersion; }
                set
                {
                    m_dataVersion = value;
                    IsDirty = true;
                }
            }

            public long DataOffset
            {
                get { return m_dataOffest; }
                set
                {
                    m_dataOffest = value;
                    IsDirty = true;
                }
            }

            public uint AutoID
            {
                get { return m_autoID; }
                set
                {
                    m_autoID = value;
                    IsDirty = true;
                }
            }

            public void Read(ISeekableReader reader)
            {
                Assert(reader != null);

                foreach (byte b in Signature)
                    if (reader.ReadByte() != b)
                        throw new CorruptedStreamException();

                DataOffset = reader.ReadLong();
                DataVersion = reader.ReadUInt();
                DateTime creation = reader.ReadTime();
                DateTime access = reader.ReadTime();
                DateTime write = reader.ReadTime();
                uint nextId = reader.ReadUInt();

                DoRead(reader);

                LastWriteTime = write;
                LastAccessTime = access;
                CreationTime = creation;
                m_autoID = nextId;
                IsDirty = false;
            }

            public void Write(ISeekableWriter writer)
            {
                writer.Write(Signature);
                long dpPos = writer.Position;
                writer.Write(DataOffset);
                writer.Write(DataVersion);
                writer.Write(CreationTime);
                writer.Write(LastAccessTime);
                writer.Write(LastWriteTime);
                writer.Write(m_autoID);

                DoWrite(writer);

                DataOffset = writer.Position;
                writer.Position = dpPos;
                writer.Write(DataOffset);
                writer.Position = DataOffset;

                IsDirty = false;
            }

            public void Reset()
            {
                DataVersion = 0;
                AutoID = 0;
                IsDirty = false;
            }

            //protected:
            protected abstract byte[] Signature { get; }
            protected abstract void DoRead(IReader reader);
            protected abstract void DoWrite(IWriter writer);
        }

    }
}
