using ifc2mct.BridgeFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace ifc2mct.Tests
{
    /// <summary>
    /// Summary description for BridgeFactoryTest
    /// </summary>
    [TestClass]
    public class BridgeBuilderTest
    {
        public BridgeBuilderTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void BuildModelTest()
        {
            const string path = "../../TestFiles/test.ifc";
            var builder = new BridgeBuilder(path);
            builder.Run();
        }

        [TestMethod]
        public void BuildBridgeTest()
        {
            const string PATH = "../../TestFiles/bridgeTest.ifc";
            // bridge data
            // overall
            const int START = 20000, END = 92302, VEROFFSET = -200, LATOFFSET = 0, STARTGAP = 40, ENDGAP = 40;
            var secDimensions = new List<double>() { 1750, 6400, 50, 5550, 1968 };
            var thicknessLists = new List<List<(double, double)>>()
            {
                new List<(double, double)>() { (28000, 16), (44000, 20), (72262, 16) }, 
                new List<(double, double)>() { (6000, 16), (28000, 14), (33000, 16), (39000, 20), (44000, 16), (66302, 14), (72262, 16) },
                new List<(double, double)>() { (28000, 16), (33000, 20), (39000, 25), (44000, 20), (72262, 16)}
            };
            // stiffeners
            var stiffenerTypeTable = new Dictionary<int, List<double>>()
            {
                { 1, new List<double>(){ 160, 14 } }, // flat stiffener
                { 2, new List<double>(){ 190, 16 } }, // flat stiffener
                { 3, new List<double>(){ 240, 20 } }, // flat stiffener
                { 4, new List<double>(){ 400, 16 } }, // flat stiffener
                { 5, new List<double>(){ 280, 300, 170, 8, 40 } }, // U-shape stiffener
                { 6, new List<double>(){ 400, 16 } } // edge plate
            };
            var stiffenerLayoutTable = new Dictionary<int, List<(int num, double gap)>>()
            {
                { 1, new List<(int, double)>() { (2, 250), (1, 8900), (1, 250) } },
                { 2, new List<(int, double)>() { (1, 870), (1, 530), (1, 850), (9, 600), (1, 850), (1, 530) } },
                { 3, new List<(int, double)>() { (/*1, 450), (2*/3, 400), (7, 450), (2, 400) } },
                { 4, new List<(int, double)>() { (1, 400), (1, 1212) } },
                { 5, new List<(int, double)>() { (1, 8), (1, 9884)} }
            };
            var stiffenerLists = new List<(int parentId, List<(double distanceAlong, int typeId, int layoutId)> stiffList)>()
            {
                { (0, new List<(double, int, int)>() { (72262, 2, 1) }) }, // stiffeners on top flange
                { (0, new List<(double, int, int)>() { (72262, 5, 2) }) }, // stiffeners on top flange
                { (1, new List<(double, int, int)>() { (33000, 1, 4), (39000, 2, 4), (72262, 1, 4) }) }, // stiffeners on left web
                { (2, new List<(double, int, int)>() { (33000, 1, 4), (39000, 2, 4), (72262, 1, 4) }) }, // stiffeners on right web
                { (3, new List<(double, int, int)>() { (23000, 2, 3), (44000, 3, 3), (72262, 2, 3) }) }, // stiffeners on bottom flange
                { (0, new List<(double, int, int)>() { (72262, 6, 5) }) } // edge plate on top flange
            };
            // bearings
            // TODO
            var bearingTypeTable = new Dictionary<int, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)>()
            {
                {1, (true, true, false) }, // dual-fixed (non-guided)
                {2, (false, true, true) }, // longitudinal-fixed (tranversely guided)
                {3, (true, false, true) }, // lateral-fixed (longitudinally guided), pull-resistant
                {4, (false, false, true) } // non-fixed (dual-guided), pull-resistant
            };
            var bearingList = new List<(double distanceAlong, double offsetLateral, int bearingTypeId)>()
            {
                (590, 1300, 3), (590, -1700, 4), (36000, 1500, 1), (36000, -1500, 2), (71752, 1400, 3), (71752, -1900, 4)
            };

            // cross frames
            // TODO
            var diaphragmTypeTable = new Dictionary<int, double>()
            {
                {1, 20}, {2, 12}, {3, 20}, {4, 25}
            };
            var diaphragmList = new List<(int typeId, int num, double gap)>()
            {
                (1, 1, 550), (2, 1, 1410), (2, 10, 3000), (3, 1, 3000), (4, 1, 1000), (3, 1, 1000), (2, 10, 3000), (2, 1, 1500), (2, 1, 1606), (1, 1, 1606)
            };
            
            // build bridge components
            var builder = new BridgeBuilder(PATH);
            builder.SetBridgeAlignment(START, END, VEROFFSET, LATOFFSET);
            builder.SetGaps(STARTGAP, ENDGAP);
            builder.SetOverallSection(secDimensions);
            builder.SetPlateThicknesses(thicknessLists);
            foreach (var stiffenerType in stiffenerTypeTable)
                builder.AddStiffenerType(stiffenerType.Key, stiffenerType.Value);
            foreach (var stiffenerLayout in stiffenerLayoutTable)
                builder.AddStiffenerLayout(stiffenerLayout.Key, stiffenerLayout.Value);
            foreach (var (parentId, stiffList) in stiffenerLists)
                builder.AddStiffeners(parentId, stiffList);
            foreach (var bearingType in bearingTypeTable)
                builder.AddBearingType(bearingType.Key, bearingType.Value);
            foreach (var bearing in bearingList)
                builder.AddBearing(bearing);
            foreach (var diaphragmType in diaphragmTypeTable)
                builder.AddDiaphragmType(diaphragmType.Key, diaphragmType.Value);
            builder.AddDiaphragm(diaphragmList);
            builder.Run2();
        }

        [TestMethod]
        public void BuildSectionedSolidTest()
        {
            const string PATH = "../../TestFiles/testSectionedSolid.ifc";
            var builder = new BridgeBuilder(PATH);
            builder.Run3();
        }
    }
    [TestClass]
    public class GeometryEngineTest
    {
        private readonly string _testFilePath1 = "../../TestFiles/SteelBridgeNew.ifc";
        private readonly string _testFilePath2 = "../../TestFiles/us.ifc";
        [TestMethod]
        public void GetArcEndByDistAlongTest()
        {
            var start = new XbimPoint3D(0, 0, 0);
            var dir = new XbimVector3D(1, 0, 0);
            double r = 1;
            bool isCCW = true;
            double dist = 0.5 * Math.PI;
            var expected = new XbimPoint3D(1, 1, 0);
            var ret = GeometryEngine.GetPointOnCurve(start, dir, r, isCCW, dist);
            var actual = ret.pt;
            Assert.AreEqual(expected, actual);
            var expected2 = new XbimVector3D(-1, 0, 0);
            var actual2 = ret.vec;
            Assert.AreEqual(expected2, actual2);

            dir = new XbimVector3D(1, 1, 0).Normalized();
            expected = new XbimPoint3D(0, Math.Sqrt(2), 0);
            ret = GeometryEngine.GetPointOnCurve(start, dir, r, isCCW, dist);
            actual = ret.pt;
            Assert.AreEqual(expected, actual);
            expected2 = new XbimVector3D(-1, -1, 0).Normalized();
            actual2 = ret.vec;
            Assert.AreEqual(expected2, actual2);

            isCCW = false;
            expected = new XbimPoint3D(Math.Sqrt(2), 0, 0);
            ret = GeometryEngine.GetPointOnCurve(start, dir, r, isCCW, dist);
            actual = ret.pt;
            Assert.AreEqual(expected, actual);
            expected2 = expected2.Negated();
            actual2 = ret.vec;
            Assert.AreEqual(expected2, actual2);

            dist = Math.PI;
            expected = new XbimPoint3D(Math.Sqrt(2), -Math.Sqrt(2), 0);
            ret = GeometryEngine.GetPointOnCurve(start, dir, r, isCCW, dist);
            actual = ret.pt;
            Assert.AreEqual(expected, actual);
            expected2 = new XbimVector3D(1, -1, 0).Normalized();
            actual2 = ret.vec;
            Assert.AreEqual(expected2, actual2);

            dir = new XbimVector3D(-1, -1, 0).Normalized();
            expected = new XbimPoint3D(-Math.Sqrt(2), Math.Sqrt(2), 0);
            ret = GeometryEngine.GetPointOnCurve(start, dir, r, isCCW, dist);
            actual = ret.pt;
            Assert.AreEqual(expected, actual);
            expected2 = expected2.Negated();
            actual2 = ret.vec;
            Assert.AreEqual(expected2, actual2);

            dist = Math.PI / 4;
            expected = new XbimPoint3D(-Math.Sqrt(2) / 2, -Math.Tan(Math.PI / 8) * Math.Sqrt(2) / 2, 0);
            ret = GeometryEngine.GetPointOnCurve(start, dir, r, isCCW, dist);
            actual = ret.pt;
            Assert.AreEqual(expected, actual);
            expected2 = new XbimVector3D(0, -1, 0);
            actual2 = ret.vec;
            Assert.AreEqual(expected2, actual2);

            dir = GeometryEngine.ToVector3D(13.35833333);
            r = 9279;
            expected = new XbimPoint3D(2945.13464253837, 216.386895560319, 0);

        }

        [TestMethod]
        public void GetMatrixFromAxisPlacement3DTest()
        {
            using (var model = IfcStore.Open(_testFilePath1))
            {
                var ax = (IIfcAxis2Placement3D)model.Instances[3795];
                var actual = GeometryEngine.ToMatrix3D(ax);
                var zAxis = new XbimVector3D(-0.06, 0, 1).Normalized();
                var xAxis = new XbimVector3D(-0.221311411869369, 0.975203188559383, 0).Normalized();
                var yAxis = zAxis.CrossProduct(xAxis);
                var expected = new XbimMatrix3D()
                {
                    M11 = xAxis.X,
                    M12 = xAxis.Y,
                    M13 = xAxis.Z,
                    M14 = 0,
                    M21 = yAxis.X,
                    M22 = yAxis.Y,
                    M23 = yAxis.Z,
                    M24 = 0,
                    M31 = zAxis.X,
                    M32 = zAxis.Y,
                    M33 = zAxis.Z,
                    M34 = 0,
                    OffsetX = -24.7868781293693,
                    OffsetY = 109.222757118651,
                    OffsetZ = 144,
                    M44 = 1
                };
                Assert.AreEqual(expected, actual);

                ax = (IIfcAxis2Placement3D)model.Instances[13887];
                actual = GeometryEngine.ToMatrix3D(ax);
                zAxis = new XbimVector3D(0, 0, 1).Normalized();
                xAxis = new XbimVector3D(1, 0, 0).Normalized();
                yAxis = zAxis.CrossProduct(xAxis);
                expected = new XbimMatrix3D()
                {
                    M11 = xAxis.X,
                    M12 = xAxis.Y,
                    M13 = xAxis.Z,
                    M14 = 0,
                    M21 = yAxis.X,
                    M22 = yAxis.Y,
                    M23 = yAxis.Z,
                    M24 = 0,
                    M31 = zAxis.X,
                    M32 = zAxis.Y,
                    M33 = zAxis.Z,
                    M34 = 0,
                    OffsetX = -170.25,
                    OffsetY = -24,
                    OffsetZ = -120,
                    M44 = 1
                };
                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void GetMatrixFromLocalPlacementTest()
        {
            using (var model = IfcStore.Open(_testFilePath1))
            {
                // PlacementRelTo is null.
                var lp = (IIfcLocalPlacement)model.Instances[3791];
                var actual = GeometryEngine.ToMatrix3D(lp);
                var zAxis = new XbimVector3D(-0.06, 0, 1).Normalized();
                var xAxis = new XbimVector3D(-0.221311411869369, 0.975203188559383, 0).Normalized();
                var yAxis = zAxis.CrossProduct(xAxis);
                var expected = new XbimMatrix3D()
                {
                    M11 = xAxis.X,
                    M12 = xAxis.Y,
                    M13 = xAxis.Z,
                    M14 = 0,
                    M21 = yAxis.X,
                    M22 = yAxis.Y,
                    M23 = yAxis.Z,
                    M24 = 0,
                    M31 = zAxis.X,
                    M32 = zAxis.Y,
                    M33 = zAxis.Z,
                    M34 = 0,
                    OffsetX = -24.7868781293693,
                    OffsetY = 109.222757118651,
                    OffsetZ = 144,
                    M44 = 1
                };
                Assert.AreEqual(expected, actual);

                // PlacementRelTo is not null.
                lp = (IIfcLocalPlacement)model.Instances[13883];
                actual = GeometryEngine.ToMatrix3D(lp);
                var ax = (IIfcAxis2Placement3D)model.Instances[13887];
                expected = GeometryEngine.ToMatrix3D(ax);
                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void TransformationMatrixTest()
        {
            using (var model = IfcStore.Open(_testFilePath1))
            {
                var lp = (IIfcLocalPlacement)model.Instances[42015];
                var ax = (IIfcAxis2Placement3D)model.Instances[42093];
                var mat1 = GeometryEngine.ToMatrix3D(lp);
                var mat2 = GeometryEngine.ToMatrix3D(ax);
                var mat = mat2 * mat1;
                var p = mat.Transform(new XbimPoint3D(2945.13464253837, 216.386895560319, 0));
                Assert.IsNotNull(p);
            }
        }

        [TestMethod]
        public void ToCoordListTest()
        {
            using (var model = IfcStore.Open(_testFilePath2))
            {
                var indPoly = (IIfcIndexedPolyCurve)model.Instances[836];
                var expected = new List<double>() { -162.25, -9.52, -144, -11.52, 180, 14.4, 204, 14.88, 252, 13.92, 270.25, 15.92, 272.25, 13.92, 272.25, 4.67, 271.5, 3.92, 236, 2.4, 214, 2.4, 214, 5.9, 121, 0.380000000000001, 121, -3.12, 101, -3.12, 101, 0.380000000000001, 7, -8.74, 7, -12.24, -13, -12.24, -13, -8.74, -107, -17.86, -107, -21.36, -127, -21.36, -163.5, -21.52, -164.25, -20.77, -164.25, -11.52 };
                var actual = GeometryEngine.ToCoordList(indPoly);
                int expectedSize = expected.Count;
                int actualSize = actual.Count;
                Assert.AreEqual(expectedSize, actualSize);
                for (int i = 0; i < actualSize; i++)
                    Assert.AreEqual(expected[i], actual[i]);
            }
        }

        [TestMethod]
        public void GetPointByDistAlongTest()
        {
            using (var model = IfcStore.Open(_testFilePath2))
            {
                GeometryEngine.isSIUnits = false;
                var start = new XbimPoint3D(0, 0, 0);
                var dir = GeometryEngine.ToVector3D(13.35833333);
                double r = 9279;
                double dist = 2965.68;
                var v1 = GeometryEngine.ToVector3D(-4.95408690777971);
                var zAx = new XbimVector3D(0, 0, 1);
                var v2 = zAx.CrossProduct(v1);
                var arcEnd = new XbimPoint3D(2945.13464253837, 216.386895560319, 0);
                var expected = (arcEnd, v2);
                var actual = GeometryEngine.GetPointOnCurve(start, dir, r, false, dist);
                Assert.AreEqual(expected, actual);

                var hor = (IIfcAlignment2DHorizontal)model.Instances[102];
                var horsegs = hor.Segments;
                actual = GeometryEngine.GetPointByDistAlong(horsegs, dist);
                Assert.AreEqual(expected, actual);

                dist = 3500;
                expected = (arcEnd + (dist - 2965.68) * v1, v2);
                actual = GeometryEngine.GetPointByDistAlong(horsegs, dist);
                Assert.AreEqual(expected, actual);
            }
        }
    }
}
