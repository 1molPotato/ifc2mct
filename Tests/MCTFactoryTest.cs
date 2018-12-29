using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ifc2mct.MCTFactory;
using ifc2mct.MCTFactory.Model;

namespace ifc2mct.Tests
{
    [TestClass]
    public class MCTFactoryTest
    {
        [TestMethod]
        public void WriteMCTFileTest()
        {
            var mctStore = new MCTStore();
            string outfile = @"D:\master_thesis\midas_files\ifc2mct_test.mct";
            mctStore.WriteMCTFile(outfile);
            Assert.IsTrue(File.Exists(outfile));
        }

        [TestMethod]
        public void MCTStoreAddObjectTest()
        {
            var store = new MCTStore();
            var node = new MCTNode(0, 1.1, 2.3, 3.4);
            store.AddObject(node);
            var actual = store.Nodes.First();
            Assert.AreEqual(node, actual);
            var element = new MCTFrameElement(1, 2, 3, 4, 5);
            store.AddObject(element);
            var actual2 = store.Elements.First();
            Assert.AreEqual(element, actual2);
            var mat = new MCTMaterial(1, MatType.STEEL, "Steel");
            store.AddObject(mat);
            var actual3 = store.Materials.First();
        }

        [TestMethod]
        public void MCTWriteBoxSectionTest()
        {
            var mctStore = new MCTStore();
            string outfile = @"D:\master_thesis\midas_files\box_test.mct";

            var dimensions = new List<double>() { 1.75, 6.4, 0.05, 5.7, 2.3, 0.016, 0.016, 0.014 };
            var section = new MCTSteelBoxSection(3, "no_stiff", dimensions);
            var stiff = new MCTStiffener("U-Stiff", new List<double>() { 0.28, 0.3, 0.17, 0.008, 0.05 });
            section.Stiffen(stiff);
            var layout = new MCTStiffeningLayout(MCTStiffenedPlateType.TOP_FLANGE, MCTStiffenedLocation.CENTER);
            var stiffeners = new List<(double dist, string name)>()
            {
                (1, stiff.Name), (1, stiff.Name)
            };
            layout.Stiffen(stiffeners);
            section.Stiffen(layout);

            var stiff2 = new MCTStiffener("P-Stiff", new List<double>() { 0.18, 0.008 });
            section.Stiffen(stiff2);
            var layout2 = new MCTStiffeningLayout(MCTStiffenedPlateType.LEFT_WEB);
            layout2.Stiffen(1.2, stiff2.Name);
            section.Stiffen(layout2);

            mctStore.AddObject(section);
            mctStore.WriteMCTFile(outfile);
        }
    }
}
