using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MCTFactory.Model
{
    public enum MCTStiffenerType { FLAT_STIFF, T_STIFF, U_STIFF }
    public class MCTStiffener
    {
        public string Name { get; }
        public MCTStiffenerType StiffenerType { get; }
        public List<double> Dimensions { get; }
        /// <summary>
        /// If type is FLAT_STIFF, dimensions = { H, B }
        /// else if type is T_STIFF, dimensions = { H, B, tw, tf }
        /// else type is U_STIFF, dimensions = { H, B1, B2, t, R }
        /// </summary>
        /// <param name="name"></param>
        /// <param name="dimensions"></param>
        public MCTStiffener(string name, List<double> dimensions)
        {
            switch (dimensions.Count)
            {
                case 2: Name = string.Format($"{name}-Flat-Stiff");  StiffenerType = MCTStiffenerType.FLAT_STIFF; break;
                case 4: Name = string.Format($"{name}-T-Stiff"); StiffenerType = MCTStiffenerType.T_STIFF; break;
                case 5: Name = string.Format($"{name}-U-Stiff"); StiffenerType = MCTStiffenerType.U_STIFF; break;
                default: throw new ArgumentException("Stiffener dimensions not provided correctlly.");
            }
            Dimensions = dimensions;
        }

        public bool IsSameStiffener(List<double> dimensions)
        {
            if (dimensions.Count != Dimensions.Count)
                return false;
            for (int i = 0; i < dimensions.Count; ++i)
                if (dimensions[i] != Dimensions[i])
                    return false;
            return true;
        }
        public override string ToString()
        {
            string output = string.Format($"{Name},{(int)StiffenerType}");
            int dimensionCount = StiffenerType == MCTStiffenerType.FLAT_STIFF ? 2
                : (StiffenerType == MCTStiffenerType.T_STIFF ? 4 : 5);
            for (int i = 0; i < dimensionCount; ++i)
                output += string.Format($",{Dimensions[i]}");
            for (int i = 8 - dimensionCount; i > 0; --i)
                output += ",0";
            return output;
        }
    }
}
