using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ifc2mct.MctFactory;
using ifc2mct.MctFactory.Model;
using ifc2mct.MctFactory.Models;

namespace ifc2mct.Tests
{
    [TestClass]
    public class MctFactoryTest
    {
        [TestMethod]
        public void WriteMCTFileTest()
        {
            var mctStore = new MCTStore();
            string outfile = "../../TestFiles/empty_mct_test.mct";
            mctStore.WriteMCTFile(outfile);
            Assert.IsTrue(File.Exists(outfile));
        }

        [TestMethod]
        public void MctWriteFileTest()
        {
            string PATH = "../../TestFiles/mct_test.mct";
            var mct = new MctStore()
            {
                UnitSystem = new MctUnitSystem() { LengthUnit = MctLengthUnitEnum.MM }
            };
            // Add nodes
            var nodes = new List<MctNode>()
            {
                new MctNode(1, 0, 0, 0),
                new MctNode(2, 5000, 0, 0)
            };
            mct.AddNode(nodes);

            // Add material
            var mat = new MctMaterialDB()
            {
                Id = 1, Name = "q345", Db = "GB12(S)", Code = "Q345", Type = MctMaterialTypeEnum.STEEL
            };
            mct.AddMateral(mat);

            // Add section
            var dimensions = new List<double>() { 1750, 6400, 50, 6000, 2000, 16, 16, 14 };
            var sec = new MctSectionSTLB(dimensions)
            {
                Id = 39,
                Name = "SW3-SW4",
                IsSD = true
            };            
            sec.AddStiffenerType("flat1", MctStiffenerTypeEnum.FLAT, new List<double>() { 190, 16 });
            sec.AddStiffenerType("u2", MctStiffenerTypeEnum.USHAPE, new List<double>() { 280, 300, 170, 8, 40 });
            var layout = new MctStiffenerLayoutSTL()
            {
                StiffenedPlate = MctStiffenedPlateTypeEnum.TOP_FLANGE,
                StiffenedLocation = MctStiffenedLocationEnum.CENTER,
                LocationName = "上-中",
                RefPoint = MctStiffenedRefPointEnum.TOP_LEFT
            };
            var gapGroups = new List<(int, double)>()
            {
                (1, 500), (2, 600)
            };
            layout.AddStiffener(gapGroups, sec.StiffenerTypeByName("u2"), MctStiffenerDirectionEnum.DOWNWARD);
            sec.AddStiffenerLayout(layout);
            mct.AddSection(sec);

            // Add element
            var element = new MctFrameElement()
            {
                Type = MctElementTypeEnum.BEAM, Id = 1, Node1 = nodes[0], Node2 = nodes[1], Mat = mat, Sec = sec
            };
            mct.AddElement(element);

            // Add support
            var support = new MctCommonSupport(new List<MctNode>() { nodes[0] }, new List<bool>() { false, true, true, true, false, true });
            mct.AddSupport(support);

            // Add load
            var load = new MctNodalLoad(new List<MctNode>() { nodes[1] })
            {
                Fz = -100
            };
            var self = new MctSelfWeight()
            {
                Z = -1.1
            };
            var loadCase = new MctStaticLoadCase() { LoadCaseType = MctStiticLoadCaseTypeEnum.CS, Name = "case1" };
            loadCase.AddStaticLoad(load);
            loadCase.AddStaticLoad(self);
            mct.AddLoadCase(loadCase);

            mct.WriteMctFile(PATH);
            Assert.IsTrue(File.Exists(PATH));
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
            string outfile = "../../TestFiles/box_test.mct";

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

        [TestMethod]
        public void Test()
        {
            
        }

        [TestMethod]
        public void MctStaticLoadCaseTest()
        {
            var load = new MctNodalLoad(new List<MctNode>() { new MctNode(1, 0, 0, 0) })
            {
                Fz = -100
            };
            var loadCase = new MctStaticLoadCase() { LoadCaseType = MctStiticLoadCaseTypeEnum.CS, Name = "case1" };
            loadCase.AddStaticLoad(load);
            string expected = "\n*USE-STLD,case1\n" +
                "*CONLOAD    ; Nodal Loads\n" +
                "; NODE_LIST, FX, FY, FZ, MX, MY, MZ, GROUP\n1 ,0,0,-100,0,0,0,";
            string actual = loadCase.ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MctCommonSupportTest()
        {
            var nodes = new List<MctNode>()
            {
                new MctNode(1, 2.5, 3, 1),
                new MctNode(3, 5, 1, 20)
            };
            var support = new MctCommonSupport(nodes, new List<bool>() { false, true, true, true, false, true });
            string expected = "1 3 ,011101,";
            string actual = support.ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MctMaterialDBTest()
        {
            var mat = new MctMaterialDB()
            {
                Id = 1,
                Name = "q345",
                Db = "GB12(S)",
                Code = "Q345",
                Type = MctMaterialTypeEnum.STEEL
            };
            string expected = "1,STEEL,q345,0,0,,C,NO,0.02,1,GB12(S),,Q345,NO,0";
            string actual = mat.ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MctNodalLoadTest()
        {
            var nodes = new List<MctNode>()
            {
                new MctNode(1, 2.5, 3, 1),
                new MctNode(3, 5, 1, 20)
            };
            var load = new MctNodalLoad(nodes)
            {
                Fz = -10
            };
            string expected = "1 3 ,0,0,-10,0,0,0,";
            string actual = load.ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MctSelfWeightTest()
        {
            var selfWeight = new MctSelfWeight()
            {
                Z = -1.1
            };
            string expected = "; *SELFWEIGHT, X, Y, Z, GROUP\n*SELFWEIGHT,0,0,-1.1,";
            string actual = selfWeight.ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MctSectionSTLBTest()
        {
            var dimensions = new List<double>() { 1750, 6400, 50, 6000, 2000, 16, 16, 14};
            var sec = new MctSectionSTLB(dimensions)
            {
                Id = 39, Name = "SW3-SW4", IsSD = true
            };
            // Add stiffener types
            sec.AddStiffenerType("flat1", MctStiffenerTypeEnum.FLAT, new List<double>() { 190, 16 });
            string expected = "39,SOD,SW3-SW4,CC,0,0,0,0,0,0,YES,NO,SOD-B\n" +
                "YES,1750,6400,1750,50,6000,50,2000,16,16,14,14,0,1900\n" +
                "1,flat1,0,190,16,,,,,,\n" +
                "0";
            string actual = sec.ToString();
            Assert.AreEqual(expected, actual);
            sec.AddStiffenerType("u2", MctStiffenerTypeEnum.USHAPE, new List<double>() { 280, 300, 170, 8, 40 });
            expected = "39,SOD,SW3-SW4,CC,0,0,0,0,0,0,YES,NO,SOD-B\n" +
                "YES,1750,6400,1750,50,6000,50,2000,16,16,14,14,0,1900\n" +
                "2,flat1,0,190,16,,,,,,,u2,2,280,300,170,8,40,,,\n" +
                "0";
            actual = sec.ToString();
            Assert.AreEqual(expected, actual);

            // Add stiffener layout
            var layout = new MctStiffenerLayoutSTL()
            {
                StiffenedPlate = MctStiffenedPlateTypeEnum.TOP_FLANGE,
                StiffenedLocation = MctStiffenedLocationEnum.CENTER,
                LocationName = "上-中",
                RefPoint = MctStiffenedRefPointEnum.TOP_LEFT                
            };
            var gapGroups = new List<(int, double)>()
            {
                (1, 500), (2, 600)
            };
            layout.AddStiffener(gapGroups, sec.StiffenerTypeByName("u2"), MctStiffenerDirectionEnum.DOWNWARD);
            sec.AddStiffenerLayout(layout);
            expected = "39,SOD,SW3-SW4,CC,0,0,0,0,0,0,YES,NO,SOD-B\n" +
                "YES,1750,6400,1750,50,6000,50,2000,16,16,14,14,0,1900\n" +
                "2,flat1,0,190,16,,,,,,,u2,2,280,300,170,8,40,,,\n" +
                "1,0,1,上-中,0,3,3,YES,500,u2,1,TC1,YES,600,u2,1,TC2,YES,600,u2,1,TC3";
            actual = sec.ToString();
            Assert.AreEqual(expected, actual);

            layout = new MctStiffenerLayoutSTL()
            {
                StiffenedPlate = MctStiffenedPlateTypeEnum.LEFT_WEB,
                StiffenedLocation = MctStiffenedLocationEnum.WEB,
                LocationName = "腹板",
                RefPoint = MctStiffenedRefPointEnum.TOP_LEFT
            };
            var gaps = new List<double>()
            {
                600, 1000
            };
            layout.AddStiffener(gaps, sec.StiffenerTypeByName("flat1"), MctStiffenerDirectionEnum.RIGHTWARD);
            sec.AddStiffenerLayout(layout);
            expected = "39,SOD,SW3-SW4,CC,0,0,0,0,0,0,YES,NO,SOD-B\n" +
                "YES,1750,6400,1750,50,6000,50,2000,16,16,14,14,0,1900\n" +
                "2,flat1,0,190,16,,,,,,,u2,2,280,300,170,8,40,,,\n" +
                "2,0,1,上-中,0,3,3,YES,500,u2,1,TC1,YES,600,u2,1,TC2,YES,600,u2,1,TC3,1,0,腹板,0,2,2,YES,600,flat1,1,LW1,YES,1000,flat1,1,LW2";
            actual = sec.ToString();
            Assert.AreEqual(expected, actual);
        }
    }
}
