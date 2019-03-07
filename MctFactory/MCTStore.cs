using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ifc2mct.MctFactory.Model;

namespace ifc2mct.MctFactory
{
    public class MCTStore
    {
        public MCTUnit Unit { get; private set; }
        public HashSet<MCTNode> Nodes { get; private set; }
        public HashSet<MCTElement> Elements { get; private set; }
        public Dictionary<int, MCTMaterial> Materials { get; private set; }
        public Dictionary<int, MCTSection> Sections { get; private set; }
        public MCTStore()
        {
            Initialize();
        }

        private void Initialize()
        {
            Nodes = new HashSet<MCTNode>();
            Elements = new HashSet<MCTElement>();
            Materials = new Dictionary<int, MCTMaterial>();
            Sections = new Dictionary<int, MCTSection>();           
        }

        public void SetUnitSystem(string force, string length, string heat, string temper)
        {
            Unit = new MCTUnit(force, length, heat, temper);
        }

        public void AddObject(MCTRoot o)
        {
            if (o is MCTNode node) Nodes.Add(node);
            else if (o is MCTElement ele) Elements.Add(ele);
            else if (o is MCTMaterial mat) Materials[(int)mat.Id] = mat;
            else if (o is MCTSection sec) Sections[(int)sec.Id] = sec;
        }

        public void WriteMCTFile(string path)
        {
            using (var sw = new StreamWriter(path, false, Encoding.GetEncoding("GB2312")))
            {
                sw.WriteLine(Unit);
                sw.WriteLine("*NODE");
                foreach (var node in Nodes)
                    sw.WriteLine(node);
                sw.WriteLine("*ELEMENT");
                foreach (var element in Elements)
                    sw.WriteLine(element);
                sw.WriteLine("*MATERIAL");
                foreach (var mat in Materials)
                    sw.WriteLine(mat.Value);
                sw.WriteLine("*SECTION");
                foreach (var sec in Sections)
                {
                    if (sec.Value is MCTSectPSCValue)
                        continue;
                    sw.WriteLine(sec.Value);
                }
                sw.WriteLine("*SECT-PSCVALUE");
                foreach (var sec in Sections)
                {
                    if (sec.Value is MCTSectPSCValue)
                        sw.WriteLine(sec.Value);
                }
                sw.WriteLine("*ENDDATA");
            }
        }
    }
}
