using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ifc2mct.MctFactory.Models;

namespace ifc2mct.MctFactory
{
    public class MctStore
    {
        private readonly Dictionary<long, MctNode> _nodes = new Dictionary<long, MctNode>();
        private readonly Dictionary<long, MctElement> _elements = new Dictionary<long, MctElement>();
        private readonly Dictionary<int, MctMaterial> _materials = new Dictionary<int, MctMaterial>();
        private readonly Dictionary<int, MctSection> _sections = new Dictionary<int, MctSection>();
        private readonly HashSet<MctSupport> _supports = new HashSet<MctSupport>();
        //private readonly HashSet<MctLoad> _loads = new HashSet<MctLoad>();
        private readonly HashSet<MctStaticLoadCase> _loadCases = new HashSet<MctStaticLoadCase>();

        public MctUnitSystem UnitSystem { get; set; }

        // Interfaces
        public void AddNode(MctNode node)
        {
            if (!_nodes.ContainsKey(node.Id))
                _nodes[node.Id] = (node);
        }

        public void AddNode(List<MctNode> nodes)
        {
            foreach (var node in nodes)
                AddNode(node);
        }

        public void AddElement(MctElement element)
        {
            if (!_elements.ContainsKey(element.Id))
                _elements[element.Id] = element;
        }

        public void AddElement(List<MctElement> elements)
        {
            foreach (var element in elements)
                AddElement(element);
        }

        public void AddMateral(MctMaterial mat)
        {
            if (!_materials.ContainsKey(mat.Id))
                _materials[mat.Id] = mat;
        }

        public void AddSection(MctSection sec)
        {
            if (!_sections.ContainsKey(sec.Id))
                _sections[sec.Id] = sec;
        }

        public void AddSupport(MctSupport support)
        {
            _supports.Add(support);
        }

        public void AddSupport(MctNode node, List<bool> constraints)
        {
            foreach (var support in _supports)
            {
                if (support is MctCommonSupport cs && cs.IsSameBearingType(constraints))
                {
                    cs.AddNode(node);
                    return;
                }                    
            }
            AddSupport(new MctCommonSupport(new List<MctNode>() { node }, constraints));
        }

        //public void AddLoad(MctLoad load)
        //{
        //    _loads.Add(load);
        //}

        public void AddLoadCase(MctStaticLoadCase loadCase)
        {
            _loadCases.Add(loadCase);
        }

        // Access
        public MctMaterial RetrieveMaterial(int id)
        {
            if (_materials.ContainsKey(id))
                return _materials[id];
            throw new ArgumentException($"Material with id {id} doesn't exist");
        }

        public void WriteMctFile(string path)
        {
            using (var sw = new StreamWriter(path, false, Encoding.GetEncoding("GB2312")))
            {
                string head = ";--------------------------------------\n" +
                    ";\tMIDAS/Civil Text(MCT) File\n" +
                    $";\tDate/Time : {DateTime.Now}\n" +
                    ";\tProduced by : ifc2mct.MctFactory\n" +
                    ";\tAuthor : lyu kaiyuan\n" +
                    ";--------------------------------------";
                sw.WriteLine(head);
                sw.WriteLine(UnitSystem);

                head = "\n*NODE\t; Nodes\n; iNO, X, Y, Z";
                sw.WriteLine(head);
                foreach (var node in _nodes)
                    sw.WriteLine(node.Value);

                head = "\n*ELEMENT\t; Elements";
                sw.WriteLine(head);
                foreach (var element in _elements)
                    sw.WriteLine(element.Value);

                head = "\n*MATERIAL\t; Materials";
                sw.WriteLine(head);
                foreach (var mat in _materials)
                    sw.WriteLine(mat.Value);

                head = "\n*SECTION\t; Sections";
                sw.WriteLine(head);
                foreach (var sec in _sections)
                    sw.WriteLine(sec.Value);

                var commonSupports = _supports.OfType<MctCommonSupport>().ToList();
                head = "\n*CONSTRAINT\t; Supports";
                sw.WriteLine(head);                
                foreach (var support in commonSupports)
                    sw.WriteLine(support);

                head = "\n*STLDCASE\t; Static Load Cases\n; LCNAME, LCTYPE, DESC";
                sw.WriteLine(head);
                foreach (var loadCase in _loadCases)
                    sw.WriteLine($"{loadCase.Name},{loadCase.LoadCaseType},{loadCase.Description}");
                foreach (var loadCase in _loadCases)
                    sw.WriteLine(loadCase);

                sw.WriteLine("\n*ENDDATA");
            }
        }
    }
}
