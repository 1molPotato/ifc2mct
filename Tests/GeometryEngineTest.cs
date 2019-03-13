using System;
using ifc2mct.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace ifc2mct.Tests
{
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
