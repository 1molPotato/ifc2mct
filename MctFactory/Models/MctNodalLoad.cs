using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public class MctNodalLoad : MctStaticLoad
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

        public MctNodalLoad(List<MctNode> nodes, List<double> forces)
        {
            AddNode(nodes);
            Fx = forces[0];
            Fy = forces[1];
            Fz = forces[2];
            Mx = forces[3];
            My = forces[4];
            Mz = forces[5];
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
        public bool IsSameLoadType(List<double> forces)
        {
            const int FORCENUM = 6;
            int count = forces.Count;
            if (count != FORCENUM) return false;
            if (forces[0] != Fx) return false;
            if (forces[1] != Fy) return false;
            if (forces[2] != Fz) return false;
            if (forces[3] != Mx) return false;
            if (forces[4] != My) return false;
            if (forces[5] != Mz) return false;
            return true;
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
}
