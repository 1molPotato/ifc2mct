using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifc2mct.MCTFactory.Model
{
    public class MCTHSection : MCTSection
    {
        private readonly double _H;
        private readonly double _B1;
        private readonly double _tw;
        private readonly double _tf1;
        private readonly double _B2;
        private readonly double _tf2;
        private readonly double _r1;
        private readonly double _r2;
        /// <summary>
        /// dimensions = { OverallDepth, TopFlangeWidth, WebThickness, TopFlangeThickness,
        /// BottomFlangeWidth, BottomFlangeThickness, TopFlangeFilletRadius, TopFlangeEdgeRadius }        
        /// </summary>
        /// <param name="iSEC"></param>
        /// <param name="name"></param>
        /// <param name="dimensions"></param>
        public MCTHSection(int iSEC, string name, List<double> dimensions)
            : this(iSEC, name, dimensions, null)
        {
            // empty
        }

        public MCTHSection(int iSEC, string name, List<double> dimensions, string offset)
            : base(iSEC, name, offset)
        {
            SHAPE = "H";
            _H = dimensions[0];
            _B1 = dimensions[1];
            _tw = dimensions[2];
            _tf1 = dimensions[3];
            _B2 = dimensions[4];
            _tf2 = dimensions[5];
            _r1 = dimensions[6];
            _r2 = dimensions[7];
        }

        public override string ToString()
        {
            return string.Format($"{_id},DBUSER,{_SNAME},{_OFFSET},{_bSD},{_bWE},{SHAPE},2," +
                $"{_H},{_B1},{_tw},{_tf1},{_B2},{_tf2},{_r1},{_r2}");
        }
    }
}
