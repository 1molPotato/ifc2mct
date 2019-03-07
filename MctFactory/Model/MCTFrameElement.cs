using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Model
{
    public enum MCTFrameElementType
    {
        TRUSS = 0, BEAM, TENSTR, COMPTR
    }

    public class MCTFrameElement : MCTElement
    {
        private readonly MCTFrameElementType _TYPE = MCTFrameElementType.BEAM;
        private readonly double _ANGLE = 0;
        private readonly double _iSUB = 0;
        public MCTFrameElement(long iEL, int iMAT, int iPRO, long iN1, long iN2)
            : base(iEL, iMAT, iPRO, iN1, iN2)
        {
            // empty
        }
        public override string ToString()
        {
            return string.Format($"{_id},{_TYPE},{_iMAT},{_iPRO},{_iN1},{_iN2},{_ANGLE},{_iSUB}");
        }
    }
}
