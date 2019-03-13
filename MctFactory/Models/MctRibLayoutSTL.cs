using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Models
{
    public enum MctStiffenedPlateTypeEnum
    {
        TOP_FLANGE = 0, LEFT_WEB, RIGHT_WEB, BOT_FLANGE
    }
    public enum MctRibLocationEnum
    {
        LEFT = 0, CENTER, RIGHT, WEB
    }
    public enum MctRibRefPointEnum
    {
        TOP_LEFT = 0, BOT_RIGHT
    }
    public class MctRibLayoutSTL
    {
        public MctStiffenedPlateTypeEnum StiffenedPlate { get; set; }
        public MctRibLocationEnum StiffenedLocation { get; set; }
        public MctRibRefPointEnum RefPoint { get; set; }
        public string LocationName { get; set; }
        protected readonly List<MctRibSTL> _stiffeners = new List<MctRibSTL>();

        public override string ToString()
        {
            int stiffenedLocation = StiffenedLocation == MctRibLocationEnum.WEB ? 0 : (int)StiffenedLocation;
            string ret = string.Format($",{(int)StiffenedPlate},{stiffenedLocation},{LocationName}" +
                $",{(int)RefPoint},{_stiffeners.Count},{_stiffeners.Count}");
            foreach (var stiffener in _stiffeners)
                ret += stiffener.ToString();
            return ret;
        }

        // Interfaces to add stiffener
        public void AddRib(double gap, MctRibTypeSTL type, MctRibDirectionEnum direction, bool isCountedForProp = true, string name = "")
        {
            if (name == "")
            {
                name = GenerateRibNamePrefix();
                name += (_stiffeners.Count + 1).ToString();
            }
            var stiffener = new MctRibSTL()
            {
                Gap = gap,
                Type = type,
                Direction = direction,
                IsForPropCalc = isCountedForProp,
                Name = name
            };
            _stiffeners.Add(stiffener);
        }

        public void AddRib(List<double> gaps, MctRibTypeSTL type, MctRibDirectionEnum direction, bool isCountedForProp = true)
        {
            string name = GenerateRibNamePrefix();
            foreach (var gap in gaps)
                AddRib(gap, type, direction, isCountedForProp, string.Format($"{name}{_stiffeners.Count + 1}"));
        }

        public void AddRib(List<(int num, double gap)> gapGroups, MctRibTypeSTL type, MctRibDirectionEnum direction, bool isCountedForProp = true)
        {
            //string name = GenerateStiffenerNamePrefix();
            var gaps = new List<double>();
            foreach (var (num, gap) in gapGroups)
                for (int i = 0; i < num; ++i)
                    gaps.Add(gap);
            AddRib(gaps, type, direction, isCountedForProp);
        }

        public bool IsEmpty()
        {
            return !_stiffeners.Any();
        }

        private string GenerateRibNamePrefix()
        {
            string name = StiffenedPlate == MctStiffenedPlateTypeEnum.TOP_FLANGE ? "T"
                    : (StiffenedPlate == MctStiffenedPlateTypeEnum.BOT_FLANGE ? "B"
                    : (StiffenedPlate == MctStiffenedPlateTypeEnum.LEFT_WEB ? "LW" : "RW"));
            name += StiffenedLocation == MctRibLocationEnum.LEFT ? "L"
                : (StiffenedLocation == MctRibLocationEnum.CENTER ? "C"
                : (StiffenedLocation == MctRibLocationEnum.RIGHT ? "R" : ""));
            return name;
        }

    }
}
