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

    public class MctMaterialDB : MctMaterial
    {
        public string Db { get; set; }
        public string DbName { get; set; }
        public string Code { get; set; }
        public bool UseElast { get; set; }
        public double Elast { get; set; }

        public override string ToString()
        {
            string useElast = UseElast ? "YES" : "NO";
            if (Data1 == null)
                Data1 = string.Format($"1,{Db},{DbName},{Code},{useElast},{Elast}");
            return base.ToString();
        }
    }

    public class MctMateriaValue : MctMaterial
    {
        public double Elast { get; set; }
        public double Poisson { get; set; }
        public double Thermal { get; set; }
        public double Density { get; set; }
        public double Mass { get; set; }


        public MctMateriaValue()
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

    class MctMaterialSRC : MctMaterial
    {
        // not implemented
    }
}
