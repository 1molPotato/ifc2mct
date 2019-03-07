using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ifc2mct.BridgeFactory;
using ifc2mct.MctFactory;
using ifc2mct.MctFactory.Model;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace ifc2mct.Translator
{
    public class Worker
    {

        #region Fields
        private readonly IfcStore _ifcModel;
        private readonly MCTStore _mctStore;
        private readonly HashSet<MCTStiffener> _stiffeners;
        #endregion

        // Constructor
        public Worker(IfcStore model)
        {
            _ifcModel = model;
            _mctStore = new MCTStore();
            _stiffeners = new HashSet<MCTStiffener>();
            InitialiseUnit();
        }

        private void InitialiseUnit()
        {
            var siUnits = _ifcModel.Instances.OfType<IIfcProject>().FirstOrDefault().UnitsInContext.Units.OfType<IIfcSIUnit>();
            string force = "", length = "", heat = "", temper = "";
            var forceUnit = siUnits
                .Where(u => u.UnitType == IfcUnitEnum.FORCEUNIT).FirstOrDefault();
            force = forceUnit == null ? force : (forceUnit.Prefix == IfcSIPrefix.KILO ? "KN" : "N");
            var lengthUnit = siUnits
                .Where(u => u.UnitType == IfcUnitEnum.LENGTHUNIT).FirstOrDefault();
            length = lengthUnit == null ? length : (lengthUnit.Prefix == IfcSIPrefix.KILO ? "KM" : (lengthUnit.Prefix == IfcSIPrefix.MILLI ? "MM" : "M"));
            var heatUnit = siUnits
                .Where(u => u.UnitType == IfcUnitEnum.ENERGYUNIT).FirstOrDefault();
            heat = heatUnit == null ? heat : (heatUnit.Prefix == IfcSIPrefix.KILO ? "KJ" : "J");
            var temperUnit = siUnits
                .Where(u => u.UnitType == IfcUnitEnum.THERMODYNAMICTEMPERATUREUNIT).FirstOrDefault();
            temper = temperUnit == null ? temper : "C";
            _mctStore.SetUnitSystem(force, length, heat, temper);
        }

        public void TranslateSteelBoxGirder()
        {            
            var girder = _ifcModel.Instances.OfType<IIfcElementAssembly>()
                .Where(a => a.PredefinedType == IfcElementAssemblyTypeEnum.GIRDER).FirstOrDefault();
            // Get properties in property set "GirderCommon" and "SteelBoxSectionDimensions"
            var properties = girder.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet) 
                //&& ((IIfcPropertySet)r.RelatingPropertyDefinition).Name == "GirderCommon" 
                //&& ((IIfcPropertySet)r.RelatingPropertyDefinition).Name == "SteelBoxSectionDimensions")                                
                .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                .OfType<IIfcPropertySingleValue>();
            var propertiesMap = new Dictionary<string, double>();
            foreach (var property in properties)
                propertiesMap[property.Name] = (double)property.NominalValue.Value;

            // Find the axis of this girder from its top flange's geometry representation
            var axis = girder.IsDecomposedBy.FirstOrDefault().RelatedObjects
                .Where(o => o is IIfcPlate)
                .Select(o => ((IIfcPlate)o).Representation.Representations.FirstOrDefault().Items.FirstOrDefault())
                .Select(s => ((IIfcSectionedSolidHorizontal)s).Directrix).FirstOrDefault();
            // Find the alignment this alignment curve belongs to,
            // if its coordinate system is not WCS, do coordinate system transformation
            var al = _ifcModel.Instances.OfType<IIfcAlignment>()
                .Where(a => a.Axis == axis)
                .FirstOrDefault();
            if (al == null) throw new ArgumentNullException("Corresponding Alignment not found.");
            var matrix = GeometryEngine.ToMatrix3D(al.ObjectPlacement);
            // Create element nodes from axis
            var elementNodes = TranslatorUtils.CreateElementNodes(axis, propertiesMap["StartDistanceAlong"], propertiesMap["SegmentLength"]);
            if (!matrix.IsIdentity)
                for (int i = 0; i < elementNodes.Count; ++i)
                    matrix.Transform(elementNodes[i]);

            // Process steel box section dimensions from the girder
            var overalDimensions = new List<double>()
            {
                propertiesMap["B1"], propertiesMap["B2"], propertiesMap["B4"], propertiesMap["B5"],
                propertiesMap["H"], propertiesMap["t1"], propertiesMap["t2"], propertiesMap["tw1"]
            };           
            TranslateSteelBoxSection(girder, overalDimensions);

            // Process material from the girder
            var mat = (IIfcMaterial)girder.Material;
            AddMCTObjects(TranslatorUtils.ProcessMaterial(mat));

            // Create elements and nodes for mct
            for (int i = 0; i < elementNodes.Count; ++i)
            {
                var node = elementNodes[i];
                AddMCTObjects(new MCTNode(i + 1, node.X, node.Y, node.Z));
                if (i == 0) continue;
                AddMCTObjects(new MCTFrameElement(i, mat.EntityLabel, girder.EntityLabel, i, i + 1));
            }
        }

        private void TranslateSteelBoxSection(IIfcElementAssembly girder, List<double> overalDimensions)
        {
            var section = new MCTSteelBoxSection(girder.EntityLabel, girder.Name, overalDimensions);

            // Process flanges and webs and their stiffeners
            var plates = girder.IsDecomposedBy.FirstOrDefault().RelatedObjects
                .Where(o => o is IIfcPlate).Select(o => o as IIfcPlate)
                .Where(p => p.Description == "FLANGE_PLATE" || p.Description == "WEB_PLATE");
            //.Where(p => p.PredefinedType == IfcPlateTypeEnum.FLANGE_PLATE || p.PredefinedType == IfcPlateTypeEnum.WEB_PLATE) // IFC4x2 feature
            int plateCode = -1, locationCode = -1;
            foreach (var plate in plates)
            {
                var plateGeometry = plate.Representation.Representations.FirstOrDefault().Items.FirstOrDefault() as IIfcSectionedSolidHorizontal;
                if (plate.Description == "FLANGE_PLATE" && plateGeometry.CrossSectionPositions.FirstOrDefault().OffsetVertical == 0)                
                    plateCode = 0; // top flange                
                else if (plate.Description == "FLANGE_PLATE")
                {
                    plateCode = 3; // bottom flange                
                    locationCode = 1; // stiffeners at center
                }                                   
                else if (plateGeometry.CrossSectionPositions.FirstOrDefault().OffsetLateral > 0)
                {
                    plateCode = 1; // left web
                    locationCode = 3;
                }
                else
                {
                    plateCode = 2; // right web
                    locationCode = 3;
                }                    
                var stiffenerGroups = plate.ConnectedFrom
                    .Select(c => c.RelatingElement as IIfcMember)
                    .Where(m => m.Description == "STIFFENING_RIB");
                foreach (var stiffenerGroup in stiffenerGroups)
                {
                    // Determine location
                    var stiffenerShapes = stiffenerGroup.Representation.Representations.FirstOrDefault().Items;
                    if (plateCode == 0)
                    {
                        double firstPos = stiffenerShapes.Select(s => s as IIfcSectionedSolidHorizontal).FirstOrDefault().CrossSectionPositions.FirstOrDefault().OffsetLateral.Value;
                        if (firstPos > overalDimensions[1] / 2)
                            locationCode = 0; // top left
                        else if (firstPos > -overalDimensions[1] / 2)
                            locationCode = 1; // top center
                        else
                            locationCode = 2; // top right
                    }

                    // Get stiffener dimensions
                    var stiffDimensions = stiffenerGroup.IsDefinedBy
                        .Where(r => r.RelatingPropertyDefinition is IIfcPropertySet)
                        .SelectMany(r => ((IIfcPropertySet)r.RelatingPropertyDefinition).HasProperties)
                        .OfType<IIfcPropertySingleValue>()
                        .Select(p => (double)p.NominalValue.Value).ToList();
                    MCTStiffener stiff = null;
                    foreach (var stiffener in _stiffeners)
                    {
                        if (stiffener.IsSameStiffener(stiffDimensions))
                        {
                            stiff = stiffener;
                            break;
                        }
                    }
                    if (stiff == null)
                    {
                        stiff = new MCTStiffener(stiffenerGroup.Name, stiffDimensions);
                        _stiffeners.Add(stiff);
                    }
                    section.Stiffen(stiff);
                    var layout = new MCTStiffeningLayout((MCTStiffenedPlateType)plateCode, (MCTStiffenedLocation)locationCode);
                    double refPos = 0;
                    if (plateCode == 3)
                        refPos = overalDimensions[3] / 2; // B5 / 2
                    else if (plateCode == 1 || plateCode == 2)
                        refPos = 0; // for web
                    else if (locationCode == 0)
                        refPos = overalDimensions[0] + overalDimensions[1] / 2; // B1 + B2 / 2
                    else if (locationCode == 1)
                        refPos = overalDimensions[1] / 2; // B2 / 2
                    else
                        refPos = -overalDimensions[1] / 2; // -B2 / 2
                    foreach (var stiffener in stiffenerShapes)
                    {
                        double dist = refPos - ((IIfcSectionedSolidHorizontal)stiffener).CrossSectionPositions.FirstOrDefault().OffsetLateral.Value;
                        layout.Stiffen(dist, stiff.Name);
                        refPos -= dist;
                    }
                    section.Stiffen(layout);
                }
            }
            AddMCTObjects(section);
        }

        public void AddMCTObjects(MCTRoot obj)
        {
            _mctStore.AddObject(obj);
        }

        public void WriteMCTFile(string path)
        {
            _mctStore.WriteMCTFile(path);
        }
    }

    public class TranslatorUtils
    {
        const double MESHSIZE = 2000;
        const double TOLORANCE = 1e-3;

        public static List<XbimPoint3D> CreateElementNodes(IIfcCurve c, double start, double length)
        {
            var ret = new List<XbimPoint3D>();
            if (c is IIfcAlignmentCurve curve)
            {
                var zAxis = new XbimVector3D(0, 0, 1);
                var hor = curve.Horizontal.Segments;
                var ver = curve.Vertical.Segments;
                int num = (int)(length / MESHSIZE);
                double rest = length - num * MESHSIZE;
                if (rest >= MESHSIZE / 2 + TOLORANCE)
                    ++num;
                for (int i = 0; i <= num; ++i)
                {
                    double dist = start + (i < num ? MESHSIZE * i : length);
                    var pt = GeometryEngine.GetPointByDistAlong(hor, start + dist).pt;
                    double elev = GeometryEngine.GetElevByDistAlong(ver, dist);
                    ret.Add(pt + zAxis * elev);
                }
                return ret;
            }
            throw new NotImplementedException("Other curve types as alignment not supported for now.");
        }

        public static MCTSection ProcessProfile(IIfcProfileDef p)
        {
            if (p is IIfcAsymmetricIShapeProfileDef iShape)
                return ProcessProfile(iShape);
            if (p is IIfcArbitraryClosedProfileDef aP)
                return ProcessProfile(aP);
            throw new NotImplementedException("Other profile types not supported for now.");
        }

        private static MCTHSection ProcessProfile(IIfcAsymmetricIShapeProfileDef p)
        {
            int label = p.EntityLabel;
            var topFiR = p.TopFlangeFilletRadius;
            var topEdR = p.TopFlangeEdgeRadius;
            double r1 = topFiR == null ? 0 : topFiR.Value;
            double r2 = topEdR == null ? 0 : topEdR.Value;
            var IProfileParams = new List<double>()
            {
                (double)p.OverallDepth.Value,
                (double)p.TopFlangeWidth.Value,
                (double)p.WebThickness.Value,
                (double)p.TopFlangeThickness.Value,
                (double)p.BottomFlangeWidth.Value,
                (double)p.BottomFlangeThickness.Value,
                r1,
                r2
            };
            return new MCTHSection(label, string.Format("HSEC-{0}", label), IProfileParams);
        }

        public static MCTMaterial ProcessMaterial(IIfcMaterial mat)
        {
            var mp = new Dictionary<string, double>();
            foreach (var p_set in mat.HasProperties)
            {
                foreach (var p in p_set.Properties)
                {
                    if (p is IIfcPropertySingleValue pv && pv.NominalValue != null)
                        mp[pv.Name.ToString()] = (double)pv.NominalValue.Value;
                }
            }
            int matId = mat.EntityLabel;
            var matType = mat.Category == "Steel" ? MatType.STEEL : MatType.CONC;
            var matName = string.Format("{0}-{1}", mat.Name, matId);
            // If no pset_properties provided, default material will be set.
            if (!mp.Any()) return new MCTMaterial(matId, matType, matName);

            if (!mp.ContainsKey("PoissonRatio") && mp.ContainsKey("YoungModulus")
                && mp.ContainsKey("ShearModulus"))
                mp["PoissonRatio"] = mp["YoungModulus"] / (2 * mp["ShearModulus"]) - 1;
            else if (!mp.ContainsKey("PoissonRatio"))
                mp["PoissonRatio"] = 0;
            var DATA1 = new List<double>()
            {
                mp["YoungModulus"],
                mp["PoissonRatio"],
                mp["ThermalExpansionCoefficient"],
                mp["MassDensity"]
            };
            return new MCTMaterial(matId, matType, matName, DATA1);
        }
    }
}
