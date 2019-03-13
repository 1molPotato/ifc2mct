using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public abstract class MctSectionSTL : MctSection
    {
        public bool IsSYM { get; set; }
        protected readonly List<MctRibTypeSTL> _ribTypes = new List<MctRibTypeSTL>();
        protected readonly List<MctRibLayoutSTL> _ribLayouts = new List<MctRibLayoutSTL>();
        public MctSectionSTL()
        {
            Type = "SOD";
        }

        // Interfaces to add stiffener
        public void AddRibType(string name, MctRibTypeEnum type, List<double> dimensions)
        {
            if (_ribTypes.Where(t => t.Name == name).Any())
                throw new InvalidOperationException($"Stiffener type with the name {name} has been defined");
            if (type == MctRibTypeEnum.FLAT && dimensions.Count != 2)
                throw new ArgumentException("Flat stiffener must have 2 dimensions");
            if (type == MctRibTypeEnum.TSHAPE && dimensions.Count != 4)
                throw new ArgumentException("T-shape stiffener must have 4 dimensions");
            if (type == MctRibTypeEnum.USHAPE && dimensions.Count != 5)
                throw new ArgumentException("U-shape stiffener must have 5 dimensions");
            _ribTypes.Add(new MctRibTypeSTL(name, type, dimensions));
        }

        public void AddStiffenerLayout(MctRibLayoutSTL layout)
        {
            _ribLayouts.Add(layout);
        }

        public MctRibTypeSTL StiffenerTypeByName(string name)
        {
            return _ribTypes.Where(t => t.Name == name).FirstOrDefault();
        }
    }    

    public class MctSectionSTLI : MctSectionSTL
    {
        // not implemented
    }        
}
