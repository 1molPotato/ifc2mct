using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MCTFactory.Model
{
    public class MCTSteelBoxSection : MCTSection
    {
        private readonly bool IsSymmetrical = true;
        public List<double> SectDimensions { get; }
        public HashSet<MCTStiffener> Stiffeners { get; }
        public HashSet<MCTStiffeningLayout> StiffeningLayouts { get; }

        /// <summary>
        /// dimensions = { B1, B2, B4, B5, H, t1, t2, tw1 }
        /// </summary>
        /// <param name="iSEC"></param>
        /// <param name="name"></param>
        /// <param name="dimensions"></param>
        public MCTSteelBoxSection(int iSEC, string name, List<double> dimensions)
            : base(iSEC, name)
        {
            SHAPE = "SOD-B";
            SectDimensions = dimensions;
            Stiffeners = new HashSet<MCTStiffener>();
            StiffeningLayouts = new HashSet<MCTStiffeningLayout>();
            SetSectDimensions();
        }

        private void SetSectDimensions()
        {
            if (IsSymmetrical)
            {
                double B3 = SectDimensions[0]; // B3 = B1
                double B6 = SectDimensions[2]; // B6 = B4
                double tw2 = SectDimensions[7]; // tw2 = tw1
                double topDist = 0;
                double bottomDist = SectDimensions[0] + SectDimensions[1] / 2
                    - SectDimensions[2] - SectDimensions[3] / 2;
                SectDimensions.Add(tw2);
                SectDimensions.Add(topDist);
                SectDimensions.Add(bottomDist);
                SectDimensions.Insert(4, B6);
                SectDimensions.Insert(2, B3);
            }
            else
            {
                throw new NotImplementedException("Asymmetrical box section not supported yet.");
            }
        }

        // Interface to add stiffeners and layouts
        public void Stiffen(MCTStiffener stiffener)
        {
            Stiffeners.Add(stiffener);
        }

        public void Stiffen(MCTStiffeningLayout layout)
        {
            StiffeningLayouts.Add(layout);
        }

        public override string ToString()
        {
            string output = "";
            output += string.Format($"{_id},SOD,{_SNAME},{_OFFSET},{_bSD},{_bWE},{SHAPE}\n");
            output += IsSymmetrical ? "YES" : "NO";
            for (int i = 0; i < SectDimensions.Count(); ++i)
            {
                output += string.Format($",{SectDimensions[i]}");
            }
            if (!Stiffeners.Any() && !StiffeningLayouts.Any())
                output += "\n0\n0";
            else
            {
                output += string.Format($"\n{Stiffeners.Count()}");
                foreach (var stiffenerType in Stiffeners)
                    output += string.Format($",{stiffenerType}");
                output += string.Format($"\n{StiffeningLayouts.Count()}");
                foreach (var layout in StiffeningLayouts)
                    output += string.Format($",{layout}");
            }
            return output;
        }
    }
}
