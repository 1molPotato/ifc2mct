using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public class MctMaterialDb : MctMaterial
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
}
