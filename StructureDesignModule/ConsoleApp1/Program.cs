using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XBIM_Module;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            BuildBridge_Construction_Test();
        }
        static void BuildBridge_Construction_Test()
        {
            const string INPATH = "../../TestFiles/alignment.ifc";
            const string OUTPATH = "../../TestFiles/aligment&construction.ifc";
            //generate the parameter of offset distance

            //var t1 = new Technical_Demand();
            //var t2 = new CrossSection();
            //t2.calculate_girder_parament(ref t1);
            //var t3 = new longitudinal_lateral_connection();
            //t3.get_parameters(ref t1, ref t3, ref t2);

            using (var bridgeconstruction = new Bridge_Construction(INPATH, OUTPATH))
            {
                bridgeconstruction.build();
            }
        }
    }
}
