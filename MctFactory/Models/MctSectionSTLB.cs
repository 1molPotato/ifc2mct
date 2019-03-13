using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public class MctSectionSTLB : MctSectionSTL
    {
        public double B1 { get; set; }
        public double B2 { get; set; }
        public double B3 { get; set; }
        public double B4 { get; set; }
        public double B5 { get; set; }
        public double B6 { get; set; }
        public double H { get; set; }
        public double t1 { get; set; }
        public double t2 { get; set; }
        public double tw1 { get; set; }
        public double tw2 { get; set; }
        public double Top { get; set; }
        public double Bot { get; set; }


        public MctSectionSTLB() : this(null)
        {
            // empty
        }

        public MctSectionSTLB(List<double> dimensions)
        {
            Shape = "SOD-B";
            if (dimensions != null && dimensions.Count == 13)
            {
                B1 = dimensions[0];
                B2 = dimensions[1];
                B3 = dimensions[2];
                B4 = dimensions[3];
                B5 = dimensions[4];
                B6 = dimensions[5];
                H = dimensions[6];
                t1 = dimensions[7];
                t2 = dimensions[8];
                tw1 = dimensions[9];
                tw2 = dimensions[10];
                Top = dimensions[11];
                Bot = dimensions[12];
                IsSYM = false;
            }
            if (dimensions != null && dimensions.Count == 8)
            {
                B1 = dimensions[0];
                B2 = dimensions[1];
                B3 = B1;
                B4 = dimensions[2];
                B5 = dimensions[3];
                B6 = B4;
                H = dimensions[4];
                t1 = dimensions[5];
                t2 = dimensions[6];
                tw1 = dimensions[7];
                tw2 = tw1;
                Top = (B4 + B5 / 2) - (B1 + B2 / 2) > 0 ? (B4 + B5 / 2) - (B1 + B2 / 2) : 0;
                Bot = (B1 + B2 / 2) - (B4 + B5 / 2) > 0 ? (B1 + B2 / 2) - (B4 + B5 / 2) : 0;
                IsSYM = true;
            }
        }

        public override string ToString()
        {
            string bSYM = IsSYM ? "YES" : "NO";
            string dimensions = string.Format($"{bSYM},{B1},{B2},{B3},{B4},{B5},{B6},{H},{t1},{t2},{tw1},{tw2},{Top},{Bot}");
            string types = $"{_ribTypes.Count}";
            foreach (var type in _ribTypes)
                types += type.ToString();
            string layouts = $"{_ribLayouts.Count}";
            foreach (var layout in _ribLayouts)
                layouts += layout.ToString();
            return string.Format($"{base.ToString()}\n{dimensions}\n{types}\n{layouts}");
        }
    }
}
