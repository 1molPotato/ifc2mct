using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public class MctFrameElement : MctElement
    {
        public double Angle { get; set; }
        public int SubType { get; set; }
        public string Exval { get; set; }
        public override string ToString()
        {
            if (Type != MctElementTypeEnum.BEAM && Type != MctElementTypeEnum.TRUSS)
                throw new ArgumentException("Incorrect element type");
            return string.Format($"{base.ToString()},{Angle},{SubType}");
        }
    }
}
