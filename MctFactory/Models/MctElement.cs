﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public enum MctElementTypeEnum
    {
        TRUSS = 0, BEAM, TENSTR, COMPTR, PLATE, PLSTRS, PLSTRN, AXISYM, SOLID
    }

    public abstract class MctElement
    {
        public long Id { get; set; }
        public MctElementTypeEnum Type { get; set; }
        public MctMaterial Mat { get; set; }
        public MctSection Sec { get; set; }
        public MctNode Node1 { get; set; }
        public MctNode Node2 { get; set; }

        public override string ToString()
        {
            // not implemented for TENSTR and COMPTR  
            if (Mat == null || Sec == null || Node1 == null || Node2 == null)
                throw new InvalidOperationException("In-completed MctElement can't write to text");
            return string.Format($"{Id},{Type},{Mat.Id},{Sec.Id},{Node1.Id},{Node2.Id}");
        }
    }           
}
