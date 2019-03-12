using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public enum MctStiticLoadCaseTypeEnum
    {
        USER = 0, D, CS
    }
    public class MctStaticLoadCase
    {
        private readonly List<MctLoad> _staticLoads = new List<MctLoad>();
        public string Name { get; set; }
        public MctStiticLoadCaseTypeEnum LoadCaseType { get; set; }
        public string Description { get; set; }

        public void AddStaticLoad(MctLoad load)
        {
            _staticLoads.Add(load);
        }

        public void AddNodalLoad(MctNode node, List<double> forces)
        {
            foreach (var load in _staticLoads)
            {
                if (load is MctNodalLoad nl && nl.IsSameLoadType(forces))
                {
                    nl.AddNode(node);
                    return;
                }                    
            }
            AddStaticLoad(new MctNodalLoad(new List<MctNode>() { node }, forces));
        }

        public override string ToString()
        {
            string ret = $"\n*USE-STLD,{Name}";

            var self = _staticLoads.OfType<MctSelfWeight>().FirstOrDefault();
            if (self != null)                            
                ret += $"\n\n{self}";            

            var nodalLoads = _staticLoads.OfType<MctNodalLoad>().ToList();
            if (nodalLoads.Any())
            {
                ret += "\n\n*CONLOAD    ; Nodal Loads\n; NODE_LIST, FX, FY, FZ, MX, MY, MZ, GROUP";
                foreach (var load in nodalLoads)
                    ret += $"\n{load}";
            }

            return ret;
        }
    }
}
