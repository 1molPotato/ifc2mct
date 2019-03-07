using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Model
{
    public class MCTNode : MCTRoot
    {

        private readonly double _X;

        private readonly double _Y;

        private readonly double _Z;

        public MCTNode(long iNO, double x, double y, double z) : base(iNO)
        {
            _X = x;
            _Y = y;
            _Z = z;
        }

        public override string ToString()
        {
            return string.Format($"{_id},{_X},{_Y},{_Z}");
        }
    }
}
