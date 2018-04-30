using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace easyLib.DB
{
    /*
     * Version: 1
     */
    public partial class FuzzyTable<T>: DataTable<T>
        where T : IDatum, IO.IStorable
    {
        public FuzzyTable(uint id , string filePath) : 
            base(id , filePath)
        {
        }

        protected override FileHeader Header
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        protected override void DoClear()
        {
            throw new NotImplementedException();
        }

        protected override void DoDelete(int ndx)
        {
            throw new NotImplementedException();
        }

        protected override T DoGet(int ndx)
        {
            throw new NotImplementedException();
        }

        protected override int DoInsert(T datum)
        {
            throw new NotImplementedException();
        }

        protected override int DoReplace(int ndx , T datum)
        {
            throw new NotImplementedException();
        }

        protected override int GetDataCount()
        {
            throw new NotImplementedException();
        }

        protected override void Init()
        {
            throw new NotImplementedException();
        }
    }
}
