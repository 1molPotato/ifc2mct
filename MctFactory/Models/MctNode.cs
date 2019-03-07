using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public class MctNode
    {
        public long Id { get; set; }
        double X { get; set; }
        double Y { get; set; }
        double Z { get; set; }

        public MctNode(long id, double x, double y, double z)
        {
            Id = id;
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return string.Format($"{Id},{X},{Y},{Z}");
        }
    }
}
