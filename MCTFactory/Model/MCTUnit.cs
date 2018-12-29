using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MCTFactory.Model
{
    public class MCTUnit
    {
        private readonly string _FORCE = "N";
        private readonly string _LENGTH = "MM";
        private readonly string _HEAT = "KJ";
        private readonly string _TEMPER = "C";

        // Default constructor
        public MCTUnit()
        {
            // empty
        }

        public MCTUnit(string force, string length, string heat, string temper)
        {
            if (force != "")
                _FORCE = force;
            if (length != "")
                _LENGTH = length;
            if (heat != "")
                _HEAT = heat;
            if (temper != "")
                _TEMPER = temper;
        }

        public override string ToString()
        {
            return string.Format($"*UNIT\n{_FORCE},{_LENGTH},{_HEAT},{_TEMPER}");
        }
    }
}
