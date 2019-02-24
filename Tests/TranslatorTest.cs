using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ifc2mct.Translator;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace ifc2mct.Tests
{
    [TestClass]
    public class TranslatorTest
    {
        [TestMethod]
        public void ProcessProfileTest()
        {
            string file = "../../TestFiles/SteelBridgeNew.ifc";
            using (var model = IfcStore.Open(file))
            {
                var profile = (IIfcProfileDef)model.Instances[42089];
                string expected = "42089,DBUSER,HSEC-42089,CC,0,0,0,0,0,0,YES,NO,H,2,66,16,0.688,0.875,16,1.375,1,0";
                var actual = TranslatorUtils.ProcessProfile(profile).ToString();
                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void TranslateSteelBoxGirderTest()
        {
            string ifcfile = "../../TestFiles/test.ifc";
            string mctfile = "../../TestFiles/test.mct";
            using (var model = IfcStore.Open(ifcfile))
            {
                var worker = new Worker(model);
                worker.TranslateSteelBoxGirder();
                worker.WriteMCTFile(mctfile);
            }
        }
    }
}
