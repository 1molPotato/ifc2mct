using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public class MctMaterialValue : MctMaterial
    {
        public double Elast { get; set; }
        public double Poisson { get; set; }
        public double Thermal { get; set; }
        public double Density { get; set; }
        public double Mass { get; set; }


        public MctMaterialValue()
        {
            // empty
        }

        public override string ToString()
        {
            if (Data1 == null)
                Data1 = string.Format($"2,{Elast},{Poisson},{Thermal},{Density},{Mass}");
            return base.ToString();
        }
    }
}
