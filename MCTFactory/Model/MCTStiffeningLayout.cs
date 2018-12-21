using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MCTFactory.Model
{
    public enum MCTStiffenedPlateType { TOP_FLANGE, LEFT_WEB, RIGHT_WEB, BOTTOM_FLANGE }
    public enum MCTStiffenedLocation { LEFT, CENTER, RIGHT, WEB }
    public class MCTStiffeningLayout
    {
        public MCTStiffenedPlateType StiffenedPlate { get; }
        public MCTStiffenedLocation Location { get; }
        public List<(double dist, string stiffenerName)> Stiffeners { get; }
        public MCTStiffeningLayout(MCTStiffenedPlateType stiffenedPlate, MCTStiffenedLocation location)
        {
            StiffenedPlate = stiffenedPlate;
            Location = location;
            Stiffeners = new List<(double dist, string stiffenerName)>();
        }

        public MCTStiffeningLayout(MCTStiffenedPlateType stiffenedPlate) : this(stiffenedPlate, MCTStiffenedLocation.WEB)
        {
            if ((int)stiffenedPlate == 0 || (int)stiffenedPlate == 3)
                throw new NotImplementedException("Flange layout should use 2 arguments constructor.");
        }

        /// <summary>
        /// Add one stiffner by givien the distance to its previous stiffner or reference point
        /// and the name of it which has been set when intialising stiffeners
        /// </summary>
        /// <param name="dist"></param>
        /// <param name="stiffenerName"></param>
        public void Stiffen(double dist, string stiffenerName)
        {
            Stiffeners.Add((dist, stiffenerName));
        }

        /// <summary>
        /// Add a list of stiffeners by given distances and names
        /// </summary>
        /// <param name="stiffeners"></param>
        public void Stiffen(List<(double dist, string stiffenerName)> stiffeners)
        {
            Stiffeners.AddRange(stiffeners);
        }

        public override string ToString()
        {
            int stiffenedLocation = (int)Location, stiffenerLocation = 0;
            string locationName = "", stiffnerPrefix = "";
            switch (StiffenedPlate)
            {
                case MCTStiffenedPlateType.TOP_FLANGE: locationName = "上"; stiffnerPrefix = "T"; stiffenerLocation = 1; break;
                case MCTStiffenedPlateType.BOTTOM_FLANGE: locationName = "下"; stiffnerPrefix = "B"; stiffenerLocation = 0; break;
                case MCTStiffenedPlateType.LEFT_WEB: locationName = "腹板"; stiffnerPrefix = "LW"; stiffenerLocation = 1; break;
                default: locationName = "腹板"; stiffnerPrefix = "RW"; stiffenerLocation = 0; break;
            }

            // 
            if (stiffenedLocation == 3)
                stiffenedLocation = 0;
            else
            {
                switch (stiffenedLocation)
                {
                    case 0: locationName += "-左"; stiffnerPrefix += "L"; break;
                    case 1: locationName += "-中"; stiffnerPrefix += "C"; break;
                    default: locationName += "-右"; stiffnerPrefix += "R"; break;
                }
            }
            int stiffenerNum = Stiffeners.Count();
            string output = string.Format($"{(int)StiffenedPlate},{stiffenedLocation},{locationName},0,{stiffenerNum},{stiffenerNum}");
            for (int i = 0; i < stiffenerNum; ++i)
            {
                output += string.Format($",YES,{Stiffeners[i].dist},{Stiffeners[i].stiffenerName},{stiffenerLocation},{stiffnerPrefix}{i + 1}");
            }

            return output;
        }
    }
}
