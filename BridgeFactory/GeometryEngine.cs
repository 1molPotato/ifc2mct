using System;   
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;

namespace ifc2mct.BridgeFactory
{
    public class GeometryEngine
    {
        private static readonly double Tolerance = 1e-9;
        public static bool isSIUnits = true;

        #region BasicGeometricUtilities
        public static XbimVector3D ToXbimVector3D(IIfcDirection d)
        {
            return new XbimVector3D(d.X, d.Y, d.Z);
        }

        /// <summary>
        /// Compute the tangent by given angle in 2D plane.
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static XbimVector3D ToVector3D(double angle)
        {
            const double Degree2Radian = Math.PI / 180;

            if (angle > 360 + Tolerance || angle < -360 - Tolerance) throw new ArgumentOutOfRangeException("angle");
            double angleRad = isSIUnits ? angle : angle * Degree2Radian;
            return new XbimVector3D(Math.Cos(angleRad), Math.Sin(angleRad), 0);
        }

        public static XbimPoint3D ToXbimPoint3D(IIfcCartesianPoint p)
        {
            if ((long)p.Dim.Value == 2) return new XbimPoint3D(p.X, p.Y, 0);
            return new XbimPoint3D(p.X, p.Y, p.Z);
        }

        public static XbimMatrix3D ToMatrix3D(IIfcAxis2Placement3D ax)
        {
            var origin = ToXbimPoint3D(ax.Location);
            var zAxis = ax.Axis == null ? new XbimVector3D(0, 0, 1) : ToXbimVector3D(ax.Axis).Normalized();
            var xAxis = ax.RefDirection == null ? new XbimVector3D(1, 0, 0) : ToXbimVector3D(ax.RefDirection).Normalized();
            var yAxis = zAxis.CrossProduct(xAxis);
            return new XbimMatrix3D(xAxis.X, xAxis.Y, xAxis.Z, 0, yAxis.X, yAxis.Y, yAxis.Z, 0,
                zAxis.X, zAxis.Y, zAxis.Z, 0, origin.X, origin.Y, origin.Z, 1);
        }

        public static XbimMatrix3D ToMatrix3D(IIfcObjectPlacement op)
        {
            // op must be an IfcLocalPlacement
            var lp = (IIfcLocalPlacement)op;
            var matrix = ToMatrix3D(lp.RelativePlacement as IIfcAxis2Placement3D);
            // If the attribute PlacementRelTo is null, then the local placement is given to the WCS.
            if (lp.PlacementRelTo == null) return matrix;
            // Recursively deal with the transformation
            return matrix * ToMatrix3D(lp.PlacementRelTo);
        }
        #endregion

        #region GetPointOnCurve
        // 已知曲线(直线、圆曲线)和其上一点距曲线起点的沿线距离, 
        // 计算该点的坐标, 以及该点切向的垂直方向向量lateral
        public static (XbimPoint3D pt, XbimVector3D vec) GetPointOnCurve(IIfcCurveSegment2D c, double dist)
        {
            if (c is IIfcLineSegment2D line)
                return GetPointOnCurve(line, dist);
            if (c is IIfcCircularArcSegment2D arc)
                return GetPointOnCurve(arc, dist);
            throw new NotImplementedException("Transition curve not supported for now");
        }

        // 已知直线和其上一点距直线起点的沿线距离
        public static (XbimPoint3D pt, XbimVector3D vec) GetPointOnCurve(IIfcLineSegment2D l, double dist)
        {
            var length = l.SegmentLength;
            if (dist > length + Tolerance) throw new ArgumentOutOfRangeException("dist");
            var start = new XbimPoint3D(l.StartPoint.X, l.StartPoint.Y, 0);
            var startDir = ToVector3D((double)l.StartDirection.Value);
            var zAxis = new XbimVector3D(0, 0, 1);
            var lateral = zAxis.CrossProduct(startDir);
            return (start + dist * startDir, lateral);
        }

        // 已知圆曲线和其上一点距圆曲线起点的沿线距离
        public static (XbimPoint3D pt, XbimVector3D vec) GetPointOnCurve(IIfcCircularArcSegment2D arc, double dist)
        {
            var length = (double)arc.SegmentLength.Value;
            if (dist > length + Tolerance) throw new ArgumentOutOfRangeException("dist");
            var start = new XbimPoint3D(arc.StartPoint.X, arc.StartPoint.Y, 0);
            var radius = (double)arc.Radius.Value;
            var isCCW = (bool)arc.IsCCW.Value;

            // 计算起点切线方向向量
            var startDir = ToVector3D((double)arc.StartDirection.Value);
            return GetPointOnCurve(start, startDir, radius, isCCW, dist);
        }

        /// <summary>
        /// Get point on arc by given start point, start direction, arc radius, 
        /// is counter-clockwise and distance along.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="dir"></param>
        /// <param name="radius"></param>
        /// <param name="isCCW"></param>
        /// <param name="dist"></param>
        /// <returns></returns>
        public static (XbimPoint3D pt, XbimVector3D vec) GetPointOnCurve(XbimPoint3D start, XbimVector3D dir,
            double radius, bool isCCW, double dist)
        {
            // Compute the location of arc center.
            var zAxis = new XbimVector3D(0, 0, 1);
            var start2center = isCCW ? zAxis.CrossProduct(dir) : dir.CrossProduct(zAxis);
            var center = start + radius * start2center;

            // Compute the location of arc end point
            var theta = isCCW ? dist / radius : -dist / radius;
            var center2start = start2center.Negated();
            var mat = new XbimMatrix3D();
            mat.RotateAroundZAxis(theta);
            var center2end = mat.Transform(center2start);
            var lateral = isCCW ? center2end.Negated() : center2end;
            return (center + radius * center2end, lateral);
        }

        public static (XbimPoint3D pt, XbimVector3D vec) GetPointByDistAlong(IItemSet<IIfcAlignment2DHorizontalSegment> horSegs, double dist)
        {
            int i = 0;
            IIfcCurveSegment2D cur = null;
            for (; i < horSegs.Count; i++)
            {
                cur = horSegs[i].CurveGeometry;
                if (dist > cur.SegmentLength + Tolerance)
                    dist -= cur.SegmentLength;
                else break;
            }
            if (cur == null) throw new ArgumentNullException("horSegs");
            return GetPointOnCurve(cur, dist);
        }

        #endregion

        #region GetElevation
        public static double GetElevOnCurve(IIfcAlignment2DVerticalSegment verSeg, double dist)
        {
            if (verSeg is IIfcAlignment2DVerSegLine line)
                return GetElevOnCurve(line, dist);
            throw new NotImplementedException();
        }

        public static double GetElevOnCurve(IIfcAlignment2DVerSegLine verSeg, double dist)
        {
            var start = new XbimPoint3D(0, 0, verSeg.StartHeight);
            double gradient = verSeg.StartGradient;
            var dir = new XbimVector3D(Math.Cos(gradient), 0, Math.Sin(gradient));
            return (start + dir * dist).Z;
            throw new NotImplementedException();
        }

        public static double GetElevByDistAlong(IItemSet<IIfcAlignment2DVerticalSegment> verSegs, double dist)
        {
            int i = 0;
            IIfcAlignment2DVerticalSegment seg = null;
            for (; i < verSegs.Count; i++)
            {
                seg = verSegs[i];
                if (i == 0) dist -= seg.StartDistAlong;
                if (dist > seg.HorizontalLength + Tolerance)
                    dist -= seg.HorizontalLength;
                else break;
            }
            return GetElevOnCurve(seg, dist);
        }

        #endregion

        #region ToCoordList
        public static List<double> ToCoordList(IIfcPolyline pl)
        {
            throw new NotImplementedException();
        }
        public static List<double> ToCoordList(IIfcIndexedPolyCurve ipc)
        {
            var ret = new List<double>();
            var index = (List<IfcPositiveInteger>)((IfcLineIndex)ipc.Segments.FirstOrDefault()).Value;
            var points2D = ToCoordList((IIfcCartesianPointList2D)ipc.Points);
            int len = index.Count;
            for (int i = 0; i < len; i++)
            {
                if (i != len - 1 || (i == len - 1 && index[0] != index[i]))
                    ret.AddRange(new List<double>() { points2D[(int)index[i] - 1].x, points2D[(int)index[i] - 1].y });
            }
            return ret;            
        }

        public static List<(double x, double y)> ToCoordList(IIfcCartesianPointList2D pList2D)
        {
            var ret = new List<(double x, double y)>();
            foreach (var pt in pList2D.CoordList)
                ret.Add((x: pt[0], y: pt[1]));
            return ret;
        }

        #endregion
    }
}
