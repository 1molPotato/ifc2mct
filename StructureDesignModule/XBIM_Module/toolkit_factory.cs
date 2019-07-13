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
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.RepresentationResource;

namespace XBIM_Module
{
    class toolkit_factory
    {
        public static IfcDirection MakeDirection(IfcStore m, double x = 0, double y = 0, double z = 0)
        {
            return m.Instances.New<IfcDirection>(d => d.SetXYZ(x, y, z));
        }

        public static IfcDirection MakeDirection(IfcStore m, XbimVector3D v)
        {
            return MakeDirection(m, v.X, v.Y, v.Z);
        }

        public static IfcSite CreateSite(IfcStore m, string name)
        {
            using (var txn = m.BeginTransaction("Create Site"))
            {
                var site = m.Instances.New<IfcSite>(s =>
                {
                    s.Name = name;
                    s.CompositionType = IfcElementCompositionEnum.ELEMENT;
                });
                var project = m.Instances.OfType<IfcProject>().FirstOrDefault();
                project.AddSite(site);
                txn.Commit();
                return site;
            }
        }

        public static void AddPrductIntoSpatial(IfcStore m, IfcSpatialStructureElement spatial, IfcProduct p, string txt)
        {
            using (var txn = m.BeginTransaction(txt))
            {
                spatial.AddElement(p);
                txn.Commit();
            }
        }
        public static IfcCurveSegment2D MakeLineSegment2D(IfcStore m, IfcCartesianPoint start, double dir, double length)
        {
            return m.Instances.New<IfcLineSegment2D>(s =>
            {
                s.StartPoint = start;
                s.StartDirection = dir;
                s.SegmentLength = length;
            });
        }

        public static IfcCircularArcSegment2D MakeCircleSeqment2D(IfcStore m,
            IfcCartesianPoint start, double dir, int length, int r, bool isccw)
        {
            return m.Instances.New<IfcCircularArcSegment2D>(cs =>
            {
                cs.StartPoint = start;
                cs.StartDirection = dir;
                cs.SegmentLength = length;
                cs.Radius = r;
                cs.IsCCW = isccw;
            });
        }

        public static IfcSectionedSolidHorizontal CreateSolidShapeBaseOnCurve(IfcStore m, IfcCurve directrix, double start, double end)
        {
            return m.Instances.New<IfcSectionedSolidHorizontal>(s =>
            {
                s.Directrix = directrix;
                var profile = MakeCircleProfile(m, 40);
                s.CrossSections.Add(profile);
                s.CrossSections.Add(profile);
                s.CrossSectionPositions.Add(MakeDistanceExpresstion(m, start));
                s.CrossSectionPositions.Add(MakeDistanceExpresstion(m, end));
            });
        }

        public static IfcCircleProfileDef MakeCircleProfile(IfcStore m, double diameter, IfcAxis2Placement2D Position = null)
        {
            return m.Instances.New<IfcCircleProfileDef>(cp =>
            {
                cp.ProfileType = IfcProfileTypeEnum.CURVE;
                if (Position != null)
                {
                    cp.Position = Position;
                }
                cp.Radius = diameter / 2;
            });
        }

        public static IfcDistanceExpression MakeDistanceExpresstion(IfcStore m, double distalong, double offlateral = 0, double offvertical = 0, bool alonghorizontal = true)
        {
            return m.Instances.New<IfcDistanceExpression>(dis =>
            {
                dis.DistanceAlong = distalong;
                dis.OffsetLateral = offlateral;
                dis.OffsetVertical = offvertical;
                dis.AlongHorizontal = alonghorizontal;
            });
        }

        public static void SetSurfaceColor(IfcStore m, IfcGeometricRepresentationItem geoitem, double red, double green, double blue, double transparency = 0)
        {
            var styleditem = m.Instances.New<IfcStyledItem>(si =>
            {
                si.Item = geoitem;
                si.Styles.Add(m.Instances.New<IfcSurfaceStyle>(ss =>
                {
                    ss.Side = IfcSurfaceSide.POSITIVE;
                    ss.Styles.Add(m.Instances.New<IfcSurfaceStyleShading>(ssha =>
                    {
                        ssha.SurfaceColour = m.Instances.New<IfcColourRgb>(c =>
                        {
                            c.Red = red;
                            c.Green = green;
                            c.Blue = blue;
                        });
                        ssha.Transparency = transparency;
                    }));
                }));
            });
        }

        public static IfcShapeRepresentation MakeShapeRepresentation(IfcStore m, int dimention, string identifier, string type, IfcRepresentationItem item)
        {
            return m.Instances.New<IfcShapeRepresentation>(sr =>
            {
                sr.ContextOfItems = m.Instances.OfType<IfcGeometricRepresentationContext>()
                .Where(c => c.CoordinateSpaceDimension == 3)
                .FirstOrDefault();
                sr.RepresentationIdentifier = identifier;
                sr.RepresentationType = type;
                sr.Items.Add(item);
            });
        }
    }
}
