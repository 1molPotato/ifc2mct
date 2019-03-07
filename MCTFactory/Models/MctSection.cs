using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{

    public abstract class MctSection
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Offset { get; set; }
        public bool IsSD { get; set; }
        public bool IsWE { get; set; }
        public string Shape { get; set; }

        public MctSection()
        {
            if (Offset == null)
                Offset = "CC,0,0,0,0,0,0";
        }

        public override string ToString()
        {
            string bSD = IsSD ? "YES" : "NO";
            string bWE = IsWE ? "YES" : "NO";            
            return string.Format($"{Id},{Type},{Name},{Offset},{bSD},{bWE},{Shape}");
        }
    }

    public class MctDbUserSection : MctSection
    {
        // not implemented 
    }    

}
