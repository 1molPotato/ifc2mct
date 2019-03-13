using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public enum MctRibDirectionEnum
    {
        UPWARD = 0, DOWNWARD, LEFTWARD = 0, RIGHTWARD, BOTH
    }
    public class MctRibSTL
    {
        public bool IsForPropCalc { get; set; }
        public double Gap { get; set; }
        public MctRibTypeSTL Type { get; set; }
        public MctRibDirectionEnum Direction { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            string C = IsForPropCalc ? "YES" : "NO";
            return string.Format($",{C},{Gap},{Type.Name},{(int)Direction},{Name}");
        }
    }
}
