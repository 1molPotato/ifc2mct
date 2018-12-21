using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.Interfaces;
using ifc2mct.BridgeFactory;

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
            const string path = @"D:\master_thesis\ifc_files\BuildBridgeModel\test.ifc";
            var builder = new BridgeBuilder(path);
            builder.Run();
        }
    }
    [TestClass]
    public class GeometryEngineTest
    {
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
            string file = @"D:\master_thesis\ifc_files\Bridge\SteelBridgeNew.ifc";
            using (var model = IfcStore.Open(file))
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
            string file = @"D:\master_thesis\ifc_files\Bridge\SteelBridgeNew.ifc";
            using (var model = IfcStore.Open(file))
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
            string file = @"D:\master_thesis\ifc_files\Bridge\SteelBridgeNew.ifc";
            using (var model = IfcStore.Open(file))
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
            string file = @"D:\master_thesis\ifc_files\Bridge\us.ifc";
            using (var model = IfcStore.Open(file))
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
    }
}
