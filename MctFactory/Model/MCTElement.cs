using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MctFactory.Model
{
    public abstract class MCTElement : MCTRoot
    {
        protected readonly int _iMAT; // 单元对应材料号
        protected readonly int _iPRO; // 单元对应截面号
        protected readonly long _iN1;  // 单元i节点节点号
        protected readonly long _iN2;  // 单元j节点节点号
        public MCTElement(long iEL, int iMAT, int iPRO, long iN1, long iN2) : base(iEL)
        {
            _iMAT = iMAT;
            _iPRO = iPRO;
            _iN1 = iN1;
            _iN2 = iN2;
        }
    }
}
