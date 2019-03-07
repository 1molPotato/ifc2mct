using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public abstract class MctLoad
    {
        // empty
    }

    public class MctNodalLoad : MctLoad
    {
        private readonly List<MctNode> _nodes = new List<MctNode>();
        public double Fx { get; set; }
        public double Fy { get; set; }
        public double Fz { get; set; }
        public double Mx { get; set; }
        public double My { get; set; }
        public double Mz { get; set; }

        public MctNodalLoad(List<MctNode> nodes)
        {
            AddNode(nodes);
        }

        // Interfaces to add nodes
        public void AddNode(MctNode node)
        {
            if (!_nodes.Where(n => n.Id == node.Id).Any())
                _nodes.Add(node);
        }
        public void AddNode(List<MctNode> nodes)
        {
            foreach (var node in nodes)
                AddNode(node);
        }

        // NODE_LIST, FX, FY, FZ, MX, MY, MZ, GROUP
        public override string ToString()
        {
            string nodeList = "";
            foreach (var node in _nodes)
                nodeList += $"{node.Id.ToString()} ";
            string group = "";
            return $"{nodeList},{Fx},{Fy},{Fz},{Mx},{My},{Mz},{group}";
        }
    }
    
    public class MctSelfWeight : MctLoad
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        // *SELFWEIGHT, X, Y, Z, GROUP
        public override string ToString()
        {
            string comment = "; *SELFWEIGHT, X, Y, Z, GROUP";
            string group = "";
            return $"{comment}\n*SELFWEIGHT,{X},{Y},{Z},{group}";
        }
    }
}
