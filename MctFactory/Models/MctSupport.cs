﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public abstract class MctSupport
    {
        protected readonly List<MctNode> _nodes = new List<MctNode>();
    }    
}
