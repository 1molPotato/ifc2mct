using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MCTFactory.Model
{
    public abstract class MCTSection : MCTRoot
    {
        // default parameters
        protected readonly string _OFFSET = "CC,0,0,0,0,0,0"; // default value is not okay
        protected readonly string _bSD = "YES"; // take shear deflection into consideration?
        protected readonly string _bWE = "NO"; // take wrapping effect into consideration?

        protected readonly string _SNAME;
        protected string SHAPE { get; set; }

        public MCTSection(int id, string name) : this(id, name, null)
        {
            // empty
        }
        public MCTSection(int id, string name, string offset) : base(id)
        {
            _SNAME = name;
            if (offset != null)
                _OFFSET = offset;
        }
    }
}
