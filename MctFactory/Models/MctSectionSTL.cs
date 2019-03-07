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
        protected readonly List<MctStiffenerTypeSTL> _stiffenerTypes = new List<MctStiffenerTypeSTL>();
        protected readonly List<MctStiffenerLayoutSTL> _stiffenerLayouts = new List<MctStiffenerLayoutSTL>();
        public MctSectionSTL()
        {
            Type = "SOD";
        }

        // Interfaces to add stiffener
        public void AddStiffenerType(string name, MctStiffenerTypeEnum type, List<double> dimensions)
        {
            if (_stiffenerTypes.Where(t => t.Name == name).Any())
                throw new InvalidOperationException($"Stiffener type with the name {name} has been defined");
            if (type == MctStiffenerTypeEnum.FLAT && dimensions.Count != 2)
                throw new ArgumentException("Flat stiffener must have 2 dimensions");
            if (type == MctStiffenerTypeEnum.TSHAPE && dimensions.Count != 4)
                throw new ArgumentException("T-shape stiffener must have 4 dimensions");
            if (type == MctStiffenerTypeEnum.USHAPE && dimensions.Count != 5)
                throw new ArgumentException("U-shape stiffener must have 5 dimensions");
            _stiffenerTypes.Add(new MctStiffenerTypeSTL(name, type, dimensions));
        }

        public void AddStiffenerLayout(MctStiffenerLayoutSTL layout)
        {
            _stiffenerLayouts.Add(layout);
        }

        public MctStiffenerTypeSTL StiffenerTypeByName(string name)
        {
            return _stiffenerTypes.Where(t => t.Name == name).FirstOrDefault();
        }
    }

    public class MctSectionSTLB : MctSectionSTL
    {
        public double B1 { get; set; }
        public double B2 { get; set; }
        public double B3 { get; set; }
        public double B4 { get; set; }
        public double B5 { get; set; }
        public double B6 { get; set; }
        public double H { get; set; }
        public double t1 { get; set; }
        public double t2 { get; set; }
        public double tw1 { get; set; }
        public double tw2 { get; set; }
        public double Top { get; set; }
        public double Bot { get; set; }        
        

        public MctSectionSTLB() : this(null)
        {
            // empty
        }

        public MctSectionSTLB(List<double> dimensions)
        {
            Shape = "SOD-B";
            if (dimensions != null && dimensions.Count == 13)
            {
                B1 = dimensions[0];
                B2 = dimensions[1];
                B3 = dimensions[2];
                B4 = dimensions[3];
                B5 = dimensions[4];
                B6 = dimensions[5];
                H = dimensions[6];
                t1 = dimensions[7];
                t2 = dimensions[8];
                tw1 = dimensions[9];
                tw2 = dimensions[10];
                Top = dimensions[11];
                Bot = dimensions[12];
                IsSYM = false;
            }
            if (dimensions != null && dimensions.Count == 8)
            {
                B1 = dimensions[0];
                B2 = dimensions[1];
                B3 = B1;
                B4 = dimensions[2];
                B5 = dimensions[3];
                B6 = B4;
                H = dimensions[4];
                t1 = dimensions[5];
                t2 = dimensions[6];
                tw1 = dimensions[7];
                tw2 = tw1;
                Top = (B4 + B5 / 2) - (B1 + B2 / 2) > 0 ? (B4 + B5 / 2) - (B1 + B2 / 2) : 0;
                Bot = (B1 + B2 / 2) - (B4 + B5 / 2) > 0 ? (B1 + B2 / 2) - (B4 + B5 / 2) : 0;
                IsSYM = true;
            }
        }

        

        public override string ToString()
        {
            string bSYM = IsSYM ? "YES" : "NO";
            string dimensions = string.Format($"{bSYM},{B1},{B2},{B3},{B4},{B5},{B6},{H},{t1},{t2},{tw1},{tw2},{Top},{Bot}");
            string types = $"{_stiffenerTypes.Count}";
            foreach (var type in _stiffenerTypes)
                types += type.ToString();
            string layouts = $"{_stiffenerLayouts.Count}";
            foreach (var layout in _stiffenerLayouts)
                layouts += layout.ToString();
            return string.Format($"{base.ToString()}\n{dimensions}\n{types}\n{layouts}");
        }
    }

    public class MctSectionSTLI : MctSectionSTL
    {
        // not implemented
    }

    public enum MctStiffenerTypeEnum
    {
        FLAT = 0, TSHAPE, USHAPE
    }
    public class MctStiffenerTypeSTL
    {
        public string Name { get; set; }
        MctStiffenerTypeEnum Type { get; set; }
        List<double> Dimensions { get; set; }
        public MctStiffenerTypeSTL(string name, MctStiffenerTypeEnum type, List<double> dimensions)
        {
            Name = name;
            Type = type;
            Dimensions = dimensions;
        }

        public override string ToString()
        {
            const int TOTAL = 8;
            string dimensions = "";
            for (int i = 0; i < TOTAL; ++i)
            {
                if (i < Dimensions.Count)
                    dimensions += $",{Dimensions[i]}";
                else
                    dimensions += ",";
            }
            return string.Format($",{Name},{(int)Type}{dimensions}");
        }
    }


    public enum MctStiffenedPlateTypeEnum
    {
        TOP_FLANGE = 0, LEFT_WEB, RIGHT_WEB, BOT_FLANGE
    }
    public enum MctStiffenedLocationEnum
    {
        LEFT = 0, CENTER, RIGHT, WEB
    }
    public enum MctStiffenedRefPointEnum
    {
        TOP_LEFT = 0, BOT_RIGHT
    }
    public class MctStiffenerLayoutSTL
    {
        public MctStiffenedPlateTypeEnum StiffenedPlate { get; set; }
        public MctStiffenedLocationEnum StiffenedLocation { get; set; }
        public MctStiffenedRefPointEnum RefPoint { get; set; }
        public string LocationName { get; set; }
        protected readonly List<MctStiffenerSTL> _stiffeners = new List<MctStiffenerSTL>();

        public override string ToString()
        {
            int stiffenedLocation = StiffenedLocation == MctStiffenedLocationEnum.WEB ? 0 : (int)StiffenedLocation;
            string ret = string.Format($",{(int)StiffenedPlate},{stiffenedLocation},{LocationName}" +
                $",{(int)RefPoint},{_stiffeners.Count},{_stiffeners.Count}");
            foreach (var stiffener in _stiffeners)
                ret += stiffener.ToString();
            return ret;
        }

        // Interfaces to add stiffener
        public void AddStiffener(double gap, MctStiffenerTypeSTL type, MctStiffenerDirectionEnum direction, bool isCountedForProp = true, string name = "")
        {
            if (name == "")
            {
                name = GenerateStiffenerNamePrefix();
                name += (_stiffeners.Count + 1).ToString();
            }
            var stiffener = new MctStiffenerSTL()
            {
                Gap = gap, Type = type, Direction = direction, IsForPropCalc = isCountedForProp, Name = name
            };
            _stiffeners.Add(stiffener);
        }

        public void AddStiffener(List<double> gaps, MctStiffenerTypeSTL type, MctStiffenerDirectionEnum direction, bool isCountedForProp = true)
        {
            string name = GenerateStiffenerNamePrefix();
            foreach (var gap in gaps)
                AddStiffener(gap, type, direction, isCountedForProp, string.Format($"{name}{_stiffeners.Count + 1}"));
        }

        public void AddStiffener(List<(int num, double gap)> gapGroups, MctStiffenerTypeSTL type, MctStiffenerDirectionEnum direction, bool isCountedForProp = true)
        {
            string name = GenerateStiffenerNamePrefix();
            var gaps = new List<double>();
            foreach (var (num, gap) in gapGroups)
                for (int i = 0; i < num; ++i)
                    gaps.Add(gap);
            AddStiffener(gaps, type, direction, isCountedForProp);
        }

        private string GenerateStiffenerNamePrefix()
        {
            string name = StiffenedPlate == MctStiffenedPlateTypeEnum.TOP_FLANGE ? "T"
                    : (StiffenedPlate == MctStiffenedPlateTypeEnum.BOT_FLANGE ? "B"
                    : (StiffenedPlate == MctStiffenedPlateTypeEnum.LEFT_WEB ? "LW" : "RW"));
            name += StiffenedLocation == MctStiffenedLocationEnum.LEFT ? "L"
                : (StiffenedLocation == MctStiffenedLocationEnum.CENTER ? "C"
                : (StiffenedLocation == MctStiffenedLocationEnum.RIGHT ? "R" : ""));
            return name;
        }
        
    }

    public enum MctStiffenerDirectionEnum
    {
        UPWARD = 0, DOWNWARD, LEFTWARD = 0, RIGHTWARD, BOTH
    }
    public class MctStiffenerSTL
    {
        public bool IsForPropCalc { get; set; }
        public double Gap { get; set; }
        public MctStiffenerTypeSTL Type { get; set; }
        public MctStiffenerDirectionEnum Direction { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            string C = IsForPropCalc ? "YES" : "NO";
            return string.Format($",{C},{Gap},{Type.Name},{(int)Direction},{Name}");
        }
    }
}
