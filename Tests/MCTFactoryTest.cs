using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ifc2mct.MctFactory;
using ifc2mct.MctFactory.Models;

namespace ifc2mct.Tests
{
    [TestClass]
    public class MctFactoryTest
    {
        [TestMethod]
        public void MctWriteFileTest()
        {
            string PATH = "../../TestFiles/mct-test.mct";
            var mct = new MctStore()
            {
                UnitSystem = new MctUnitSystem() { LengthUnit = MctLengthUnitEnum.MM }
            };
            // Add nodes
            var nodes = new List<MctNode>()
            {
                new MctNode(1, 0, 0, 0),
                new MctNode(2, 1500, 0, 0),
                new MctNode(3, 3000, 0, 0)
            };
            mct.AddNode(nodes);

            // Add material
            var mat = new MctMaterialDb()
            {
                Id = 1, Name = "q345", Db = "GB12(S)", Code = "Q345", Type = MctMaterialTypeEnum.STEEL
            };
            mct.AddMateral(mat);

            // Add section
            var sec = new MctSectionDbUser(MctSectionDbUserTypeEnum.H)
            {
                DbName = "GB-YB05",
                Name = "I-shape",
                Id = 1,
                SectionName = "HW 300x305x15/15",
                IsDb = true
            };
            mct.AddSection(sec);

            // Add element
            var element = new MctFrameElement()
            {
                Type = MctElementTypeEnum.BEAM,
                Id = 1,
                Node1 = nodes[0],
                Node2 = nodes[1],
                Mat = mat,
                Sec = sec
            };
            var element2 = new MctFrameElement()
            {
                Type = MctElementTypeEnum.BEAM,
                Id = 2,
                Node1 = nodes[1],
                Node2 = nodes[2],
                Mat = mat,
                Sec = sec
            };
            mct.AddElement(element);
            mct.AddElement(element2);

            // Add support
            var support = new MctCommonSupport(new List<MctNode>() { nodes[0] }, new List<bool>() { false, true, true, true, false, true });
            var support2 = new MctCommonSupport(new List<MctNode>() { nodes[2] }, new List<bool>() { true, true, true, true, false, true });
            mct.AddSupport(support);
            mct.AddSupport(support2);

            // Add load
            var load = new MctNodalLoad(new List<MctNode>() { nodes[1] })
            {
                Fz = -1000
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
        public void MctSectionDbUserTest()
        {
            var sec = new MctSectionDbUser(MctSectionDbUserTypeEnum.H)
            {
                DbName = "GB-YB05",
                Name = "I-shape",
                Id = 1,
                SectionName = "HW 300x305x15/15",
                IsDb = true
            };
            string expected = "1,DBUSER,I-shape,CT,0,0,0,0,0,0,NO,NO,H,1,GB-YB05,HW 300x305x15/15";
            string actual = sec.ToString();
            Assert.AreEqual(expected, actual);
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
            string expected = "\n*USE-STLD,case1\n\n" +
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
            var mat = new MctMaterialDb()
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
            sec.AddRibType("flat1", MctRibTypeEnum.FLAT, new List<double>() { 190, 16 });
            string expected = "39,SOD,SW3-SW4,CT,0,0,0,0,0,0,YES,NO,SOD-B\n" +
                "YES,1750,6400,1750,50,6000,50,2000,16,16,14,14,0,1900\n" +
                "1,flat1,0,190,16,,,,,,\n" +
                "0";
            string actual = sec.ToString();
            Assert.AreEqual(expected, actual);
            sec.AddRibType("u2", MctRibTypeEnum.USHAPE, new List<double>() { 280, 300, 170, 8, 40 });
            expected = "39,SOD,SW3-SW4,CT,0,0,0,0,0,0,YES,NO,SOD-B\n" +
                "YES,1750,6400,1750,50,6000,50,2000,16,16,14,14,0,1900\n" +
                "2,flat1,0,190,16,,,,,,,u2,2,280,300,170,8,40,,,\n" +
                "0";
            actual = sec.ToString();
            Assert.AreEqual(expected, actual);

            // Add stiffener layout
            var layout = new MctRibLayoutSTL()
            {
                StiffenedPlate = MctStiffenedPlateTypeEnum.TOP_FLANGE,
                StiffenedLocation = MctRibLocationEnum.CENTER,
                LocationName = "上-中",
                RefPoint = MctRibRefPointEnum.TOP_LEFT                
            };
            var gapGroups = new List<(int, double)>()
            {
                (1, 500), (2, 600)
            };
            layout.AddRib(gapGroups, sec.StiffenerTypeByName("u2"), MctRibDirectionEnum.DOWNWARD);
            sec.AddStiffenerLayout(layout);
            expected = "39,SOD,SW3-SW4,CT,0,0,0,0,0,0,YES,NO,SOD-B\n" +
                "YES,1750,6400,1750,50,6000,50,2000,16,16,14,14,0,1900\n" +
                "2,flat1,0,190,16,,,,,,,u2,2,280,300,170,8,40,,,\n" +
                "1,0,1,上-中,0,3,3,YES,500,u2,1,TC1,YES,600,u2,1,TC2,YES,600,u2,1,TC3";
            actual = sec.ToString();
            Assert.AreEqual(expected, actual);

            layout = new MctRibLayoutSTL()
            {
                StiffenedPlate = MctStiffenedPlateTypeEnum.LEFT_WEB,
                StiffenedLocation = MctRibLocationEnum.WEB,
                LocationName = "腹板",
                RefPoint = MctRibRefPointEnum.TOP_LEFT
            };
            var gaps = new List<double>()
            {
                600, 1000
            };
            layout.AddRib(gaps, sec.StiffenerTypeByName("flat1"), MctRibDirectionEnum.RIGHTWARD);
            sec.AddStiffenerLayout(layout);
            expected = "39,SOD,SW3-SW4,CT,0,0,0,0,0,0,YES,NO,SOD-B\n" +
                "YES,1750,6400,1750,50,6000,50,2000,16,16,14,14,0,1900\n" +
                "2,flat1,0,190,16,,,,,,,u2,2,280,300,170,8,40,,,\n" +
                "2,0,1,上-中,0,3,3,YES,500,u2,1,TC1,YES,600,u2,1,TC2,YES,600,u2,1,TC3,1,0,腹板,0,2,2,YES,600,flat1,1,LW1,YES,1000,flat1,1,LW2";
            actual = sec.ToString();
            Assert.AreEqual(expected, actual);
        }
    }
}
