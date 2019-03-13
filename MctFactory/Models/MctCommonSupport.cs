using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public class MctCommonSupport : MctSupport
    {
        readonly List<bool> _constraints = new List<bool>();

        public MctCommonSupport(List<MctNode> nodes, bool dx, bool dy, bool dz, bool rx, bool ry, bool rz)
        {
            AddNode(nodes);
            _constraints.AddRange(new List<bool>() { dx, dy, dz, rx, ry, rz });
        }

        public MctCommonSupport(List<MctNode> nodes, List<bool> constraints)
        {
            AddNode(nodes);
            if (constraints.Count != 6)
                throw new ArgumentException("Constraints must have 6 boolean values");
            for (int i = 0; i < 6; ++i)
                _constraints.Add(constraints[i]);
        }

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

        public bool IsSameBearingType(List<bool> constraints)
        {
            const int CONSTRNUM = 6;
            int count = constraints.Count;
            if (count != CONSTRNUM) return false;
            for (int i = 0; i < CONSTRNUM; ++i)
                if (_constraints[i] != constraints[i])
                    return false;
            return true;
        }

        // NODE_LIST, CONST (Dx, Dy, Dz, Rx, Ry, Rz), GROUP
        public override string ToString()
        {
            string nodeList = "";
            foreach (var node in _nodes)
                nodeList += $"{node.Id.ToString()} ";
            string constraints = "";
            foreach (var constraint in _constraints)
                constraints += $"{(constraint ? 1 : 0)}";
            string group = "";
            return $"{nodeList},{constraints},{group}";
        }
    }
}
