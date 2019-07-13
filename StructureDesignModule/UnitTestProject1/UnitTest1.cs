using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StructureDesignModule;
using XBIM_Module;
using System.IO;

namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        //[TestMethod]
        //public void TestMethod1()
        //{
        //    var t1 = new Technical_Demand();
        //    t1.Write_Info2TXT();
        //    var t2 = new CrossSection();
        //    t2.calculate_girder_parament(ref t1);
        //    t2.Write_Info2TXT();
        //    var t3 = new longitudinal_lateral_connection();
        //    t3.get_parameters(ref t1, ref t3, ref t2);
        //    t3.Write_Info2TXT();
        //}
        [TestMethod]
        public void TestMethod2()
        {
            const string PATH = "../../TestFiles/alignment.ifc";
            var align = new Create_Alignment(PATH) { IsStraight = true };
            align.Create();
        }

        [TestMethod]
        public void TestMethod3()
        {
            const string PATH = "../../TestFiles/alignment-arc.ifc";
            var align = new Create_Alignment(PATH) { IsStraight = false };
            align.Create();
        }

        [TestMethod]
        public void BuildBridge_Construction_Test()
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
