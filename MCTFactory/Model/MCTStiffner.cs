using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MCTFactory.Model
{
    public enum MCTStiffenerType { PLATE_STIFF, T_STIFF, U_STIFF }
    public class MCTStiffener
    {
        public string Name { get; }
        public MCTStiffenerType StiffenerType { get; }
        public List<double> Dimensions { get; }
        /// <summary>
        /// If type is PLATE_STIFF, dimensions = { H, B }
        /// else if type is T_STIFF, dimensions = { H, B, tw, tf }
        /// else type is U_STIFF, dimensions = { H, B1, B2, t, R }
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="dimensions"></param>
        public MCTStiffener(string name, MCTStiffenerType type, List<double> dimensions)
        {
            Name = name;
            StiffenerType = type;
            Dimensions = dimensions;
        }
        public override string ToString()
        {
            string output = string.Format($"{Name},{(int)StiffenerType}");
            int dimensionCount = StiffenerType == MCTStiffenerType.PLATE_STIFF ? 2
                : (StiffenerType == MCTStiffenerType.T_STIFF ? 4 : 5);
            for (int i = 0; i < dimensionCount; ++i)
                output += string.Format($",{Dimensions[i]}");
            for (int i = 8 - dimensionCount; i > 0; --i)
                output += ",0";
            return output;
        }
    }
}
