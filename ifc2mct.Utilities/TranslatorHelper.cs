using ifc2mct.MctFactory.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xbim.Common.Geometry;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;

namespace ifc2mct.Utilities
{
    public class TranslatorHelper
    {
        const double MESHSIZE = 2000;
        const double TOLORANCE = 1e-3;
        const int PLATESNUM = 4;

        public static List<MctNode> TranslateNodes(IIfcCurve directrix, List<double> positions)
        {
            var nodes = new List<MctNode>();
            if (directrix is IIfcOffsetCurveByDistances ocbd)
            {
                var basicCurve = ocbd.BasisCurve;
                if (basicCurve is IIfcAlignmentCurve ac)
                {
                    for (int i = 0; i < positions.Count; ++i)
                    {
                        double startDist = ocbd.OffsetValues[0].DistanceAlong + positions[i];
                        double offsetLateral = ocbd.OffsetValues[0].OffsetLateral.Value;
                        double offsetVertical = ocbd.OffsetValues[0].OffsetVertical.Value;
                        var vz = new XbimVector3D(0, 0, 1);
                        double height = ac.Vertical.Segments[0].StartHeight; // assume no slope
                        var horSegs = ac.Horizontal.Segments;
                        (var pt, var vy) = Utilities.GeometryEngine.GetPointByDistAlong(horSegs, startDist);
                        var position = pt + vy * offsetLateral + vz * (offsetVertical + height);
                        nodes.Add(new MctNode(i + 1, pt.X, pt.Y, pt.Z));
                    }
                }
            }
            return nodes;
        }

        public static List<bool> TranslateBearing(List<IIfcProxy> bearingPair)
        {
            // box girder normally has 2 bearings at the same longitudinal location
            var values1 = bearingPair[0].IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet ps && ps.Name == "Pset_BearingCommon")
                .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                .OfType<IIfcPropertyListValue>()
                .SelectMany(lv => lv.ListValues).ToList();
            var values2 = bearingPair[1].IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet ps && ps.Name == "Pset_BearingCommon")
                .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                .OfType<IIfcPropertyListValue>()
                .SelectMany(lv => lv.ListValues).ToList();
            var accommodates1 = new List<bool>();
            foreach (var value in values1)
                if (value is IfcBoolean val)
                    accommodates1.Add(val);
            var accommodates2 = new List<bool>();
            foreach (var value in values2)
                if (value is IfcBoolean val)
                    accommodates2.Add(val);
            var constraints = new List<bool>()
            {
                !(accommodates1[0] && accommodates2[0]), // Dx = !(Ax1 && Ax2)
                !(accommodates1[1] && accommodates2[1]), // Dy = !(Ay1 && Ay2)
                true, // Dz = true
                !(accommodates1[2] || accommodates2[2]), // Rx = !(Az1 || Az2)
                false, // Ry = false
                !(accommodates1[0] || accommodates2[0]) // Rz = !(Ax1 || Ax2)
            };
            return constraints;
        }

        public static double TranslateBracing(IIfcElementAssembly bracing, MctMaterial mat)
        {
            double massDensity = 0, volume = 0;
            if (mat is MctMaterialValue m)
                massDensity = m.Mass;
            var geoItems = bracing.IsDecomposedBy.FirstOrDefault().RelatedObjects
                .Where(o => o is IIfcProduct p && p.Representation != null)
                .SelectMany(o => ((IIfcProduct)o).Representation.Representations[0].Items);
            foreach (var geom in geoItems)
                volume += Utilities.GeometryEngine.Volume(geom);
            // not implemented, return a constant value
            return massDensity * volume;
        }

        public static MctMaterial TranslateMaterial(IIfcMaterial m)
        {
            var mp = new Dictionary<string, double>();
            foreach (var p_set in m.HasProperties)
            {
                foreach (var p in p_set.Properties)
                {
                    if (p is IIfcPropertySingleValue pv && pv.NominalValue != null)
                        mp[pv.Name.ToString().ToUpper()] = (double)pv.NominalValue.Value;
                }
            }

            var mat = new MctMaterialValue()
            {
                Id = m.EntityLabel,
                Name = m.Name,
                Type = m.Category.ToString().ToUpper() == "STEEL" ? MctMaterialTypeEnum.STEEL
                : (m.Category.ToString().ToUpper() == "CONCRETE" ? MctMaterialTypeEnum.CONC : MctMaterialTypeEnum.USER),
                UseMass = true,
                Elast = mp.ContainsKey("YOUNGMODULUS") ? mp["YOUNGMODULUS"] : 0,
                Poisson = mp.ContainsKey("POISSONRATIO") ? mp["POISSONRATIO"] : 0,
                Thermal = mp.ContainsKey("MASSDENSITY") ? mp["MASSDENSITY"] : 0,
                Mass = mp.ContainsKey("MASSDENSITY") ? mp["MASSDENSITY"] : 0
            };
            return mat;
        }

        public static MctSectionSTLB TranslateSectionSTLB(IIfcElementAssembly girder, List<double> dims, double distanceAlong, int sectionId)
        {
            var dimensions = new List<double>(dims);
            var plateSolids = ParseBoxPlateSolids(girder);
            for (int i = 0; i < PLATESNUM; ++i)
            {
                if (i == 2) continue;
                for (int j = 0; j < plateSolids[i].Count; ++j)
                {
                    if (plateSolids[i][j].CrossSectionPositions[1].DistanceAlong >= distanceAlong)
                    {
                        if (plateSolids[i][j].CrossSections[0] is IIfcCenterLineProfileDef clp)
                            dimensions.Add(clp.Thickness);
                        break;
                    }
                }
            }
            var sec = new MctSectionSTLB(dimensions)
            {
                Id = sectionId,
                Name = $"STLB-{sectionId}"
            };
            // TODO
            // add stiffeners
            var plateAssemblies = girder.IsDecomposedBy.FirstOrDefault().RelatedObjects
                .Where(o => o is IIfcElementAssembly ea && (ea.ObjectType == "FLANGE_ASSEMBLY" || ea.ObjectType == "WEB_ASSEMBLY"))
                .Select(o => (IIfcElementAssembly)o).ToList();
            for (int i = 0; i < PLATESNUM; ++i)
                ParseRibs(i, distanceAlong, dims, plateAssemblies[i], sec);

            return sec;
        }

        private static void ParseRibs(int plateCode, double distanceAlong, List<double> dims, IIfcElementAssembly plateAssembly, MctSectionSTLB sec)
        {
            double B1 = dims[0], B2 = dims[1], B4 = dims[2], B5 = dims[3], H = dims[4]; // overall section
            var layouts = new List<MctRibLayoutSTL>();
            MctRibDirectionEnum direction = 0;
            MctStiffenedPlateTypeEnum stiffenedPlate = 0;
            var stiffenerLocations = new List<MctRibLocationEnum>();
            var locationNames = new List<string>();
            var refPoints = new List<double>();
            if (plateCode == 0)
            {
                // stiffeners on top flange
                direction = MctRibDirectionEnum.DOWNWARD;
                stiffenedPlate = MctStiffenedPlateTypeEnum.TOP_FLANGE;
                stiffenerLocations.Add(MctRibLocationEnum.LEFT);
                stiffenerLocations.Add(MctRibLocationEnum.CENTER);
                stiffenerLocations.Add(MctRibLocationEnum.RIGHT);
                locationNames.Add("上-左");
                locationNames.Add("上-中");
                locationNames.Add("上-右");
                refPoints.AddRange(new List<double>() { B1 + B2 / 2, B2 / 2, -B2 / 2 });
            }
            else if (plateCode == 1)
            {
                // stiffeners on left web
                direction = MctRibDirectionEnum.RIGHTWARD;
                stiffenedPlate = MctStiffenedPlateTypeEnum.LEFT_WEB;
                stiffenerLocations.Add(MctRibLocationEnum.WEB);
                locationNames.Add("腹板");
                refPoints.Add(0);
            }
            else if (plateCode == 2)
            {
                // stiffeners on right web
                direction = MctRibDirectionEnum.LEFTWARD;
                stiffenedPlate = MctStiffenedPlateTypeEnum.RIGHT_WEB;
                stiffenerLocations.Add(MctRibLocationEnum.WEB);
                locationNames.Add("腹板");
                refPoints.Add(0);
            }
            else
            {
                // stiffeners on bottom flange
                direction = MctRibDirectionEnum.UPWARD;
                stiffenedPlate = MctStiffenedPlateTypeEnum.BOT_FLANGE;
                stiffenerLocations.Add(MctRibLocationEnum.CENTER);
                locationNames.Add("下-中");
                refPoints.Add(B5 / 2);
            }

            var groups = new List<List<(double pos, string name)>>();
            for (int j = 0; j < locationNames.Count; ++j)
            {
                layouts.Add(new MctRibLayoutSTL()
                {
                    StiffenedPlate = stiffenedPlate,
                    StiffenedLocation = stiffenerLocations[j],
                    LocationName = locationNames[j],
                    RefPoint = MctRibRefPointEnum.TOP_LEFT
                });
                groups.Add(new List<(double pos, string name)>());
            }

            var ribs = plateAssembly.IsDecomposedBy.SelectMany(rg => rg.RelatedObjects)
                    .Where(o => o is IIfcElementAssembly ea && ea.ObjectType == "RIB_ASSEMBLY")
                    .Select(o => (IIfcElementAssembly)o).ToList();
            foreach (var rib in ribs)
            {
                List<double> ribDimensions = null;
                MctRibTypeEnum type = 0;
                string name = rib.Tag;
                if (name.ToUpper() == "FLAT")
                {
                    ribDimensions = ParseRibDimensions(rib, distanceAlong, 2);
                    type = MctRibTypeEnum.FLAT;
                }
                else if (name.ToUpper() == "U-SHAPE")
                {
                    ribDimensions = ParseRibDimensions(rib, distanceAlong, 5);
                    type = MctRibTypeEnum.USHAPE;
                }
                else
                {
                    ribDimensions = ParseRibDimensions(rib, distanceAlong, 4);
                    type = MctRibTypeEnum.TSHAPE;
                }
                name = $"Rib-{name}-{rib.EntityLabel}";
                sec.AddRibType(name, type, ribDimensions);
                var ribSolids = rib.IsDecomposedBy.FirstOrDefault().RelatedObjects
                    .Where(o => o is IIfcMember m && m.ObjectType == "STIFFENING_RIB")
                    .Select(o => (IIfcMember)o)
                    .Select(m => m.Representation.Representations[0].Items[0])
                    .Where(geo => geo is IIfcSectionedSolidHorizontal)
                    .Select(geo => (IIfcSectionedSolidHorizontal)geo).ToList();
                foreach (var ribSolid in ribSolids)
                {
                    if (ribSolid.CrossSectionPositions[0].DistanceAlong > distanceAlong || ribSolid.CrossSectionPositions[1].DistanceAlong < distanceAlong)
                        continue;

                    if (plateCode == 0)
                    {
                        double pos = ribSolid.CrossSectionPositions[0].OffsetLateral.Value;
                        if (pos > B2 / 2)
                            groups[0].Add((pos, name));
                        else if (pos < -B2 / 2)
                            groups[2].Add((pos, name));
                        else
                            groups[1].Add((pos, name));
                    }
                    else if (plateCode == 1 || plateCode == 2)
                    {
                        double pos = ribSolid.CrossSectionPositions[0].OffsetVertical.Value;
                        groups[0].Add((pos, name));
                    }
                    else
                    {
                        double pos = ribSolid.CrossSectionPositions[0].OffsetLateral.Value;
                        groups[0].Add((pos, name));
                    }
                }
            }
            for (int j = 0; j < groups.Count; ++j)
            {
                groups[j] = groups[j].OrderByDescending(s => s.pos).ToList();
                for (int k = 0; k < groups[j].Count; ++k)
                {
                    double gap = refPoints[j] - groups[j][k].pos;
                    layouts[j].AddRib(gap, sec.StiffenerTypeByName(groups[j][k].name), direction);
                    refPoints[j] = groups[j][k].pos;
                }
            }
            foreach (var layout in layouts)
                if (!layout.IsEmpty())
                    sec.AddStiffenerLayout(layout);
        }

        private static List<double> ParseRibDimensions(IIfcElementAssembly ribAssembly, double dist, int parameterNum)
        {
            var dimensions = new List<double>();
            var tableValues = ribAssembly.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet ps && ps.Name == "Dimensions")
                .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                .OfType<IIfcPropertyTableValue>().ToList();
            int count = tableValues[0].DefiningValues.Count;
            int i = 1;
            for (; i < count; ++i)
                if ((double)tableValues[0].DefiningValues[i].Value > dist)
                    break;
            for (int j = 0; j < parameterNum; ++j)
                dimensions.Add((double)tableValues[j].DefinedValues[i - 1].Value);
            return dimensions;
        }

        public static List<double> ParseSectionDimensions(IIfcElementAssembly girder)
        {
            var plateSolids = ParseBoxPlateSolids(girder);

            var solids = new List<IIfcSectionedSolidHorizontal>();
            for (int i = 0; i < PLATESNUM; ++i)
                solids.Add(plateSolids[i].FirstOrDefault());
            double w1 = 0, w2 = 0, w3 = 0, w4 = 0, h = 0;
            if (solids[0].CrossSections[0] is IIfcCenterLineProfileDef clp && clp.Curve is IIfcPolyline pl)
                w1 = Math.Abs(pl.Points[0].X - pl.Points[1].X);
            if (solids[3].CrossSections[0] is IIfcCenterLineProfileDef clp2 && clp2.Curve is IIfcPolyline pl2)
                w3 = Math.Abs(pl2.Points[0].X - pl2.Points[1].X);
            if (solids[1].CrossSections[0] is IIfcCenterLineProfileDef clp3 && clp3.Curve is IIfcPolyline pl3
                && solids[2].CrossSections[0] is IIfcCenterLineProfileDef clp4 && clp4.Curve is IIfcPolyline pl4)
            {
                double leftOrigin = solids[1].CrossSectionPositions[0].OffsetLateral.Value;
                double rightOrigin = solids[2].CrossSectionPositions[0].OffsetLateral.Value;
                w2 = leftOrigin + pl3.Points[0].X - (rightOrigin + pl4.Points[0].X);
                w4 = leftOrigin + pl3.Points[1].X - (rightOrigin + pl4.Points[1].X);
            }
            h = Math.Abs(solids[3].CrossSectionPositions[0].OffsetVertical.Value);
            return new List<double>()
            {
                (w1 - w2) / 2, // B1
                w2, // B2
                (w3 - w4) / 2, // B4
                w4, // B5
                h // H
            };
        }

        public static List<List<IIfcSectionedSolidHorizontal>> ParseBoxPlateSolids(IIfcElementAssembly girder)
        {
            var plates = girder.IsDecomposedBy.FirstOrDefault().RelatedObjects
                .Where(o => o is IIfcElementAssembly ea && (ea.ObjectType == "FLANGE_ASSEMBLY" || ea.ObjectType == "WEB_ASSEMBLY"))
                .SelectMany(o => ((IIfcElementAssembly)o).IsDecomposedBy.FirstOrDefault().RelatedObjects)
                .Where(o => o is IIfcPlate p && (p.ObjectType == "FLANGE_PLATE" || p.ObjectType == "WEB_PLATE"))
                .Select(o => (IIfcPlate)o).ToList();
            var plateSolids = new List<List<IIfcSectionedSolidHorizontal>>();
            for (int i = 0; i < PLATESNUM; ++i)
                plateSolids.Add(new List<IIfcSectionedSolidHorizontal>());
            foreach (var plate in plates)
            {
                var item = plate.Representation.Representations[0].Items[0];
                if (item is IIfcSectionedSolidHorizontal ssh)
                {
                    if (ssh.CrossSectionPositions[0].OffsetLateral.Value > 0)
                        plateSolids[1].Add(ssh); // solids of left web
                    else if (ssh.CrossSectionPositions[0].OffsetLateral.Value < 0)
                        plateSolids[2].Add(ssh); // solids of right web
                    else if (ssh.CrossSectionPositions[0].OffsetVertical.Value >= 0)
                        plateSolids[0].Add(ssh); // solids of top flange
                    else/* if (ssh.CrossSectionPositions[0].OffsetVertical.Value < 0)*/
                        plateSolids[3].Add(ssh); // solids of bottom flange                       
                }
            }
            return plateSolids;
        }
    }
}
