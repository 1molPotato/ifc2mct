using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MCTFactory.Model
{ 
    public enum MatType
    {
        STEEL = 0, CONC, SRC, USER
    }

    public class MCTMaterial : MCTRoot
    {
        private readonly MatType _matType;
        private readonly string _MNAME;
        private readonly double _SPHEAT = 0;
        private readonly double _HEATCO = 0;
        private readonly string _TUNIT = "C";
        private readonly string _bMASS = "YES";
        private readonly double _DAMPRATION;
        private readonly double _ELAST;
        private readonly double _POISN;
        private readonly double _THERMAL;
        private readonly double _DEN = 0;
        private readonly double _MASS;

        // Constructor for the condition where only material type is provided.
        public MCTMaterial(int iMAT, MatType type, string name) : base(iMAT)
        {
            _MNAME = name;
            switch (type)
            {
                case MatType.STEEL: // Q345 JTG D64-2015(S)
                    _ELAST = 2.1006e+7;
                    _POISN = 0.31;
                    _THERMAL = 1.2e-5;
                    _MASS = 0.8163;
                    _DAMPRATION = 0.02;
                    break;
                case MatType.CONC: // C40 JTG04(RC)
                    _ELAST = 3.3141e+6;
                    _POISN = 0.2;
                    _THERMAL = 1e-5;
                    _MASS = 0.26;
                    _DAMPRATION = 0.05;
                    break;
                default:
                    break;
            }
        }
        /// <summary>
        /// Constructor for the condition where material properties are provided, 
        /// DATA1 = {_ELAST, _POISN, _THERMAL, _MASS }
        /// </summary>
        /// <param name="iMAT"></param>
        /// <param name="DATA1"></param>
        public MCTMaterial(int iMAT, MatType type, string name, List<double> DATA1) : base(iMAT)
        {
            _matType = type;
            _MNAME = name;
            _ELAST = DATA1[0];
            _POISN = DATA1[1] == 0 ? (type == MatType.STEEL ? 0.3 : 0.2) : DATA1[1];
            _THERMAL = DATA1[2];
            _MASS = DATA1[3];
            _DAMPRATION = type == MatType.STEEL ? 0.02 : 0.05;
        }

        public override string ToString()
        {
            return string.Format($"{_id},{_matType},{_MNAME},{_SPHEAT},{_HEATCO}, ,{_TUNIT},{_bMASS}," +
                $"{_DAMPRATION},2,{_ELAST},{_POISN},{_THERMAL},{_DEN},{_MASS}");
        }
    }
}
