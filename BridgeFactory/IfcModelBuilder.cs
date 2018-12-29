using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PropertyResource;

namespace ifc2mct.BridgeFactory
{
    public class IfcModelBuilder
    {
        public static IfcCartesianPoint MakeCartesianPoint(IfcStore m, double x, double y)
        {
            return m.Instances.New<IfcCartesianPoint>(p => p.SetXY(x, y));
        }

        public static IfcCartesianPoint MakeCartesianPoint(IfcStore m, double x, double y, double z)
        {
            return m.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(x, y, z));
        }

        public static IfcDirection MakeDirection(IfcStore m, double x, double y)
        {
            return m.Instances.New<IfcDirection>(d => d.SetXY(x, y));
        }

        public static IfcDirection MakeDirection(IfcStore m, double x, double y, double z)
        {
            return m.Instances.New<IfcDirection>(d => d.SetXYZ(x, y, z));
        }

        public static IfcPolyline MakePolyline(IfcStore m, IfcCartesianPoint start, IfcCartesianPoint end)
        {
            return MakePolyline(m, new List<IfcCartesianPoint>() { start, end });
        }

        public static IfcPolyline MakePolyline(IfcStore m, List<IfcCartesianPoint> points)
        {
            return m.Instances.New<IfcPolyline>(p =>
            {
                foreach (var point in points)
                    p.Points.Add(point);
            });
        }

        public static IfcCenterLineProfileDef MakeCenterLineProfileDef(IfcStore m, IfcBoundedCurve curve, double thickness)
        {
            return m.Instances.New<IfcCenterLineProfileDef>(c =>
            {
                c.Thickness = thickness;
                c.Curve = curve;
            });
        }

        public static IfcExtrudedAreaSolid MakeExtrudedAreaSolid(IfcStore m, IfcProfileDef area, IfcAxis2Placement3D pos, IfcDirection dir, double depth)
        {
            return m.Instances.New<IfcExtrudedAreaSolid>(s =>
            {
                s.SweptArea = area;
                s.Position = pos;
                s.ExtrudedDirection = dir;
                s.Depth = depth;
            });
        }

        public static IfcCompositeCurve MakeCompositeCurve(IfcStore m, List<IfcCurve> curves)
        {
            return m.Instances.New<IfcCompositeCurve>(c =>
            {
                foreach (var curve in curves)
                {
                    var seg = m.Instances.New<IfcCompositeCurveSegment>(s => s.ParentCurve = curve);
                    c.Segments.Add(seg);
                }
            });
        }

        public static IfcTrimmedCurve MakeSemiCircle(IfcStore m, double radius)
        {
            var circle = m.Instances.New<IfcCircle>(c =>
            {
                c.Position = MakeAxis2Placement2D(m);
                c.Radius = radius;
            });
            var semiCircle = m.Instances.New<IfcTrimmedCurve>();
            IfcParameterValue t1 = 0.0;
            IfcParameterValue t2 = Math.PI;
            semiCircle.Trim1.Add(t1);
            semiCircle.Trim2.Add(t2);
            semiCircle.MasterRepresentation = IfcTrimmingPreference.PARAMETER;
            semiCircle.BasisCurve = circle;
            return semiCircle;
        }

        public static IfcCircularArcSegment2D MakeCircularArcSegment2D(IfcStore m, 
            IfcCartesianPoint start, double dir, double length, double r, bool isCCW)
        {
            return m.Instances.New<IfcCircularArcSegment2D>(s =>
            {
                s.StartPoint = start;
                s.StartDirection = dir;
                s.SegmentLength = length;
                s.Radius = r;
                s.IsCCW = isCCW;
            });
        }

        public static IfcAxis2Placement2D MakeAxis2Placement2D(IfcStore m)
        {
            return m.Instances.New<IfcAxis2Placement2D>(ap =>
            {
                ap.Location = MakeCartesianPoint(m, 0, 0);
                ap.RefDirection = MakeDirection(m, 1, 0);
            });
        }

        public static IfcAxis2Placement3D MakeAxis2Placement3D(IfcStore m)
        {
            return m.Instances.New<IfcAxis2Placement3D>(ap =>
            {
                ap.Location = MakeCartesianPoint(m, 0, 0, 0);
                ap.Axis = MakeDirection(m, 0, 0, 1);
                ap.RefDirection = MakeDirection(m, 1, 0, 0);
            });
        }

        public static IfcAxis2Placement3D MakeAxis2Placement3D(IfcStore m, IfcCartesianPoint origin, IfcDirection localZ, IfcDirection localX)
        {
            return m.Instances.New<IfcAxis2Placement3D>(a =>
            {
                a.Location = origin;
                a.Axis = localZ;
                a.RefDirection = localX;
            });
        }

        public static IfcDistanceExpression MakeDistanceExpression(IfcStore m, double distanceAlong, double offsetLateral, double offsetVertical)
        {
            return m.Instances.New<IfcDistanceExpression>(d =>
            {
                d.DistanceAlong = distanceAlong;
                d.OffsetLateral = offsetLateral;
                d.OffsetVertical = offsetVertical;
                d.OffsetLongitudinal = 0;
                d.AlongHorizontal = true;
            });
        }

        public static IfcPropertySet MakePropertySet(IfcStore m, string name, Dictionary<string, double> properties)
        {
            var propertySet = m.Instances.New<IfcPropertySet>(ps => ps.Name = name);
            foreach (var property in properties)
            {
                var prop = m.Instances.New<IfcPropertySingleValue>(p =>
                {
                    p.Name = property.Key;
                    p.NominalValue = new IfcLengthMeasure(property.Value);
                });
                propertySet.HasProperties.Add(prop);
            }
            return propertySet;
        }
    }
}

