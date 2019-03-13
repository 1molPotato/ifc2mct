using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public enum MctMaterialTypeEnum
    {
        STEEL = 0, CONC, USER, SRC
    }
    public abstract class MctMaterial
    {
        public int Id { get; set; }
        public MctMaterialTypeEnum Type { get; set; }
        public string Name { get; set; }
        public double Spheat { get; set; }
        public double Heatco { get; set; }
        public bool UseMass { get; set; }
        public double DampRatio { get; set; }
        public string Data1 { get; set; }
        public string Data2 { get; set; }

        public MctMaterial()
        {
            if (DampRatio == 0)
                DampRatio = Type == MctMaterialTypeEnum.STEEL ? 0.02 : (Type == MctMaterialTypeEnum.USER ? 0 : 0.05);
        }

        public override string ToString()
        {
            string bMASS = UseMass ? "YES" : "NO";
            return string.Format($"{Id},{Type},{Name},{Spheat},{Heatco},,C,{bMASS},{DampRatio},{Data1}");
        }
    }            
}
