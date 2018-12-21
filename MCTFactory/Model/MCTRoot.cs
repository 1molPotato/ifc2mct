using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MCTFactory.Model
{
    public abstract class MCTRoot
    {
        protected readonly long _id;
        //public abstract override string ToString();
        public MCTRoot(long id)
        {
            _id = id;
        }
        public long Id { get { return _id; } }
    }
}
