using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public class MctSelfWeight : MctStaticLoad
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        // *SELFWEIGHT, X, Y, Z, GROUP
        public override string ToString()
        {
            string comment = "; *SELFWEIGHT, X, Y, Z, GROUP";
            string group = "";
            return $"{comment}\n*SELFWEIGHT,{X},{Y},{Z},{group}";
        }
    }
}
