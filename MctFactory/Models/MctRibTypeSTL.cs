using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public enum MctRibTypeEnum
    {
        FLAT = 0, TSHAPE, USHAPE
    }
    public class MctRibTypeSTL
    {
        public string Name { get; set; }
        MctRibTypeEnum Type { get; set; }
        List<double> Dimensions { get; set; }
        public MctRibTypeSTL(string name, MctRibTypeEnum type, List<double> dimensions)
        {
            Name = name;
            Type = type;
            Dimensions = dimensions;
        }

        public override string ToString()
        {
            const int TOTAL = 8;
            string dimensions = "";
            for (int i = 0; i < TOTAL; ++i)
            {
                if (i < Dimensions.Count)
                    dimensions += $",{Dimensions[i]}";
                else
                    dimensions += ",";
            }
            return string.Format($",{Name},{(int)Type}{dimensions}");
        }
    }
}
