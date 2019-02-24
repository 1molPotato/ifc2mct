using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.RepresentationResource;

namespace ifc2mct.BridgeFactory
{
    public class IfcModelBuilder
    {

        public static IfcCartesianPoint MakeCartesianPoint(IfcStore m, double x = 0, double y = 0, double z = 0)
        {
            return m.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(x, y, z));
        }

        public static IfcCartesianPoint MakeCartesianPoint(IfcStore m, XbimPoint3D pt)
        {
            return MakeCartesianPoint(m, pt.X, pt.Y, pt.Z);
        }

        public static IfcDirection MakeDirection(IfcStore m, double x = 0, double y = 0, double z = 0)
        {
            return m.Instances.New<IfcDirection>(d => d.SetXYZ(x, y, z));
        }

        public static IfcDirection MakeDirection(IfcStore m, XbimVector3D v)
        {
            return MakeDirection(m, v.X, v.Y, v.Z);
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

        public static IfcPolyline MakePolyline(IfcStore m, XbimPoint3D start, XbimPoint3D end)
        {
            return MakePolyline(m, new List<XbimPoint3D>() { start, end });
        }

        public static IfcPolyline MakePolyline(IfcStore m, List<XbimPoint3D> points)
        {
            return MakePolyline(m, points.Select(p => MakeCartesianPoint(m, p)).ToList());
        }

        public static IfcCircle MakeCircle(IfcStore m, IfcAxis2Placement position, double r)
        {
            return m.Instances.New<IfcCircle>(c =>
            {
                c.Position = position ?? MakeAxis2Placement2D(m);
                c.Radius = r;
            });
        }

        public static IfcCenterLineProfileDef MakeCenterLineProfile(IfcStore m, IfcBoundedCurve curve, double thickness)
        {
            return m.Instances.New<IfcCenterLineProfileDef>(c =>
            {
                c.Thickness = thickness;
                c.Curve = curve;
            });
        }

        public static IfcArbitraryProfileDefWithVoids MakeArbProfileWithVoids(IfcStore m, IfcCurve outerCurve, List<IfcCurve> innerCurves)
        {
            return m.Instances.New<IfcArbitraryProfileDefWithVoids>(p =>
            {
                p.ProfileType = IfcProfileTypeEnum.AREA;
                p.OuterCurve = outerCurve;
                p.InnerCurves.AddRange(innerCurves);
            });
        }

        public static IfcArbitraryClosedProfileDef MakeArbClosedProfile(IfcStore m, IfcCurve outerCurve)
        {
            return m.Instances.New<IfcArbitraryClosedProfileDef>(p =>
            {
                p.ProfileType = IfcProfileTypeEnum.AREA;
                p.OuterCurve = outerCurve;
            });
        }
        
        public static IfcRectangleProfileDef MakeRectangleProfile(IfcStore m, double xDim, double yDim)
        {
            return m.Instances.New<IfcRectangleProfileDef>(r =>
            {
                r.XDim = xDim;
                r.YDim = yDim;
                r.ProfileType = IfcProfileTypeEnum.AREA;
            });
        }

        public static IfcCircleProfileDef MakeCircleProfile(IfcStore m, double r, IfcAxis2Placement2D position = null)
        {
            return m.Instances.New<IfcCircleProfileDef>(cp =>
            {
                if (position != null)
                    cp.Position = position;
                cp.Radius = r;
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
                    var seg = MakeCompositeCurveSegment(m, curve);
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

        public static IfcLineSegment2D MakeLineSegment2D(IfcStore m, IfcCartesianPoint start, double dir, int length)
        {
            return m.Instances.New<IfcLineSegment2D>(s =>
            {
                s.StartPoint = start;
                s.StartDirection = dir;
                s.SegmentLength = length;
            });
        }

        public static IfcCircularArcSegment2D MakeCircularArcSegment2D(IfcStore m, 
            IfcCartesianPoint start, double dir, int length, int r, bool isCCW)
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

        public static IfcTrimmedCurve MakeTrimmedCurve(IfcStore m, IfcCurve basis, double p1, double p2, bool sense = false)
        {
            return m.Instances.New<IfcTrimmedCurve>(tc =>
            {
                tc.BasisCurve = basis;
                tc.MasterRepresentation = IfcTrimmingPreference.PARAMETER;
                tc.SenseAgreement = sense;
                tc.Trim1.Add(new IfcParameterValue(p1));
                tc.Trim2.Add(new IfcParameterValue(p2));
            });
        }

        public static IfcTrimmedCurve MakeTrimmedCurve(IfcStore m, IfcCurve basis, IfcCartesianPoint p1, IfcCartesianPoint p2, bool sense = false)
        {
            return m.Instances.New<IfcTrimmedCurve>(tc =>
            {
                tc.BasisCurve = basis;
                tc.MasterRepresentation = IfcTrimmingPreference.CARTESIAN;
                tc.SenseAgreement = sense;
                tc.Trim1.Add(p1);
                tc.Trim2.Add(p2);
            });
        }

        public static IfcTrimmedCurve MakeTrimmedCurve(IfcStore m, IfcCurve basis, XbimPoint3D p1, XbimPoint3D p2, bool sense = false)
        {
            return m.Instances.New<IfcTrimmedCurve>(tc =>
            {
                tc.BasisCurve = basis;
                tc.MasterRepresentation = IfcTrimmingPreference.CARTESIAN;
                tc.SenseAgreement = sense;
                tc.Trim1.Add(MakeCartesianPoint(m, p1));
                tc.Trim2.Add(MakeCartesianPoint(m, p2));
            });
        }

        public static IfcCompositeCurveSegment MakeCompositeCurveSegment(IfcStore m, IfcCurve parent, 
            IfcTransitionCode code = IfcTransitionCode.CONTINUOUS, bool sameSense = false)
        {
            return m.Instances.New<IfcCompositeCurveSegment>(ccs =>
            {
                ccs.ParentCurve = parent;
                ccs.Transition = code;
                ccs.SameSense = sameSense;
            });
        }

        public static IfcAxis2Placement2D MakeAxis2Placement2D(IfcStore m, IfcCartesianPoint origin = null, IfcDirection localX = null)
        {
            return m.Instances.New<IfcAxis2Placement2D>(ap =>
            {
                ap.Location = origin ?? MakeCartesianPoint(m, 0, 0);
                ap.RefDirection = localX ?? MakeDirection(m, 1, 0);
            });
        }

        public static IfcAxis2Placement2D MakeAxis2Placement2D(IfcStore m, XbimPoint3D origin, XbimVector3D localX)
        {
            return MakeAxis2Placement2D(m, MakeCartesianPoint(m, origin), MakeDirection(m, localX));
        }

        public static IfcAxis2Placement2D MakeAxis2Placement2D(IfcStore m, XbimPoint3D origin)
        {
            return MakeAxis2Placement2D(m, MakeCartesianPoint(m, origin));
        }

        public static IfcAxis2Placement3D MakeAxis2Placement3D(IfcStore m, IfcCartesianPoint origin = null, IfcDirection localZ = null, IfcDirection localX = null)
        {            
            return m.Instances.New<IfcAxis2Placement3D>(a =>
            {
                a.Location = origin ?? MakeCartesianPoint(m, 0, 0, 0);
                a.Axis = localZ/* ?? MakeDirection(m, 0, 0, 1)*/;
                a.RefDirection = localX/* ?? MakeDirection(m, 1, 0, 0)*/; 
            });
        }

        public static IfcShapeRepresentation MakeShapeRepresentation(IfcStore m, int dimension, string identifier, string type)
        {
            return m.Instances.New<IfcShapeRepresentation>(sr =>
            {
                sr.ContextOfItems = m.Instances.OfType<IfcGeometricRepresentationContext>()
                    .Where(c => c.CoordinateSpaceDimension == dimension)
                    .FirstOrDefault();
                sr.RepresentationIdentifier = identifier;
                sr.RepresentationType = type;
            });
        }

        //public static IfcPropertySet MakePropertySet(IfcStore m, string name)
        //{
        //    return m.Instances.New<IfcPropertySet>(ps => ps.Name = name);
        //}

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

        public static IfcDistanceExpression MakeDistanceExpression(IfcStore m, double distanceAlong, double offsetLateral = 0, double offsetVertical = 0, bool alongHorizontal = true)
        {
            return m.Instances.New<IfcDistanceExpression>(d =>
            {
                d.DistanceAlong = distanceAlong;
                d.OffsetLateral = offsetLateral;
                d.OffsetVertical = offsetVertical;
                d.AlongHorizontal = alongHorizontal;
            });
        }

        public static IfcLocalPlacement MakeLocalPlacement(IfcStore m, IfcAxis2Placement ax = null, IfcObjectPlacement op = null)
        {
            // not implemented
            return m.Instances.New<IfcLocalPlacement>(lp =>
            {
                lp.PlacementRelTo = op;
                lp.RelativePlacement = ax ?? MakeAxis2Placement3D(m);
            });
        }

        public static IfcLinearPlacement MakeLinearPlacement(IfcStore m, IfcCurve curve, IfcDistanceExpression distance, IfcOrientationExpression orientation = null)
        {
            var lp = m.Instances.New<IfcLinearPlacement>(p =>
            {
                p.PlacementRelTo = curve;
                p.Distance = distance;
                p.Orientation = orientation ?? m.Instances.New<IfcOrientationExpression>();
            });
            lp.CartesianPosition = ToAx3D(m, lp);
            return lp;
        }

        private static IfcAxis2Placement3D ToAx3D(IfcStore m, IfcLinearPlacement lp)
        {
            var origin = MakeCartesianPoint(m);
            var localZ = MakeDirection(m, 0, 0, 1);
            var localX = MakeDirection(m, 1, 0, 0);
            // not implemented
            var curve = lp.PlacementRelTo;
            if (curve is IIfcOffsetCurveByDistances offsetCurve)
            {
                var basicCurve = offsetCurve.BasisCurve;
                double startDist = offsetCurve.OffsetValues[0].DistanceAlong + lp.Distance.DistanceAlong;
                double offsetLateral = offsetCurve.OffsetValues[0].OffsetLateral.Value + lp.Distance.OffsetLateral.Value;
                double offsetVertical = offsetCurve.OffsetValues[0].OffsetVertical.Value + lp.Distance.OffsetVertical.Value;
                if (basicCurve is IIfcAlignmentCurve ac)
                {
                    var vz = new XbimVector3D(0, 0, 1);
                    double height = ac.Vertical.Segments[0].StartHeight; // assume no slope
                    var horSegs = ac.Horizontal.Segments;
                    (var pt, var vy) = GeometryEngine.GetPointByDistAlong(horSegs, startDist);                    
                    var position = pt + vy * offsetLateral + vz * (offsetVertical + height);
                    var vx = vy.CrossProduct(vz);
                    origin = MakeCartesianPoint(m, position.X, position.Y, position.Z);
                    localX = MakeDirection(m, vx.X, vx.Y, vx.Z);
                }
            }            
            return MakeAxis2Placement3D(m, origin, localZ, localX);
        }                         
    }
}

