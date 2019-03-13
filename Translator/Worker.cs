using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ifc2mct.MctFactory;
using ifc2mct.MctFactory.Models;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;
using ifc2mct.Utilities;

namespace ifc2mct.Translator
{    
    public class Worker
    {
        private readonly IfcStore _ifcModel;
        private readonly MctStore _mctStore = new MctStore();        
        private readonly Dictionary<int, SortedList<double, bool>> _positionsTable = new Dictionary<int, SortedList<double, bool>>();

        private List<IIfcProxy> Bearings { get; set; }
        private List<IIfcElementAssembly> Bracings { get; set; }
        private int SectionCounter { get; set; }

        public Worker(string path)
        {
            if (File.Exists(path))
                _ifcModel = IfcStore.Open(path);
            else
                throw new ArgumentException("Invalid path to open ifc model");
            Initialise();
        }

        public Worker(IfcStore model)
        {
            _ifcModel = model;
            Initialise();                        
        }

        // Interfaces
        public void Run()
        {
            try
            {
                TranslateGirder();
            }
            catch (Exception)
            {

                throw;
            }
            //TranslateGirder();
        }
        
        public void WriteMctFile(string path)
        {
            if (File.Exists(path))
                Console.WriteLine($"Warning: operation will overwrite the existing file {path}");
            string dir = new FileInfo(path).Directory.FullName;
            if (!Directory.Exists(dir))
                throw new ArgumentException($"Directory {dir} doesn't exist");
            _mctStore.WriteMctFile(path);
        }

        // Utilities
        private void Initialise()
        {
            try
            {
                if (_ifcModel == null)
                    throw new InvalidOperationException("Empty model cannot be processed");
                InitialiseUnitSystem();
                if (!_ifcModel.Instances.OfType<IIfcAlignment>().Any())
                    throw new InvalidOperationException("Model without IfcAlignment cannot be processed");
                if (_ifcModel.Instances.OfType<IIfcAlignment>().FirstOrDefault().Axis == null)
                    throw new InvalidOperationException("Model without IfcAlignmentCurve cannot be processed");
                var directrices = _ifcModel.Instances.OfType<IIfcOffsetCurveByDistances>()
                    .Where(ocbd => ocbd.BasisCurve is IIfcAlignmentCurve);
                foreach (var directrix in directrices)
                    _positionsTable[directrix.EntityLabel] = new SortedList<double, bool>();
                Bearings = _ifcModel.Instances.OfType<IIfcProxy>()
                    .Where(p => p.ObjectType == "POT").ToList();
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }
        }

        private void InitialiseUnitSystem()
        {
            // Assume that the ifc model is using SI unit system
            // we might need a more robust implementation in the future
            // TODO
            var siUnits = _ifcModel.Instances.OfType<IIfcProject>().FirstOrDefault().UnitsInContext.Units.OfType<IIfcSIUnit>();
            var ifcForceUnit = siUnits.Where(u => u.UnitType == IfcUnitEnum.FORCEUNIT).FirstOrDefault();
            var forceUnit = ifcForceUnit == null ? MctForceUnitEnum.N
                : (ifcForceUnit.Prefix == IfcSIPrefix.KILO ? MctForceUnitEnum.KN : MctForceUnitEnum.N);

            var ifcLengthUnit = siUnits.Where(u => u.UnitType == IfcUnitEnum.LENGTHUNIT).FirstOrDefault();
            var lengthUnit = ifcLengthUnit == null ? MctLengthUnitEnum.MM
                : (ifcLengthUnit.Prefix == IfcSIPrefix.CENTI ? MctLengthUnitEnum.CM
                : (ifcLengthUnit.Prefix == null ? MctLengthUnitEnum.M : MctLengthUnitEnum.MM));

            // Assume temperature unit is Celsius and energy unit is Joule
            var temperUnit = MctTemperUnitEnum.C;
            var heatUnit = MctHeatUnitEnum.J;
            _mctStore.UnitSystem = new MctUnitSystem()
            {
                ForceUnit = forceUnit,
                LengthUnit = lengthUnit,
                TemperUnit = temperUnit,
                HeatUnit = heatUnit
            };
        }

        private void TranslateGirder()
        {
            var girder = _ifcModel.Instances.OfType<IIfcElementAssembly>()
                .Where(ea => ea.PredefinedType == IfcElementAssemblyTypeEnum.GIRDER).FirstOrDefault();
            if (girder == null)
                throw new InvalidOperationException("A bridge without superstructure cannot be processed");
            // parse material
            var material = girder.Material;
            var mat = TranslatorHelper.TranslateMaterial((IIfcMaterial)material);
            _mctStore.AddMateral(mat);
            // add load case            
            var loadcase = new MctStaticLoadCase()
            {
                Name = "施工阶段荷载",
                LoadCaseType = MctStiticLoadCaseTypeEnum.CS
            };
            _mctStore.AddLoadCase(loadcase);
            var selfWeight = new MctSelfWeight() { Z = -1 };
            loadcase.AddStaticLoad(selfWeight);
            // parse directrix
            PreProcessDirectrix(girder);
            // parse sections
            var dimensions = TranslatorHelper.ParseSectionDimensions(girder);
            int sectionCounter = 1;
            foreach (var positions in _positionsTable)
            {
                var coordinates = positions.Value.Select(p => p.Key).ToList();
                var nodes = TranslatorHelper.TranslateNodes((IIfcCurve)_ifcModel.Instances[positions.Key], coordinates);
                _mctStore.AddNode(nodes);                
                int nodeNum = positions.Value.Keys.Count;
                MctSection sec = null;
                for (int i = 0; i < nodeNum - 1; ++i)
                {
                    double currentPos = positions.Value.Keys[i], nextPos = positions.Value.Keys[i + 1];
                    bool isSectionChanged = positions.Value.Values[i];
                    if (isSectionChanged)
                    {
                        sec = TranslatorHelper.TranslateSectionSTLB(girder, dimensions, currentPos + (nextPos - currentPos) / 2, sectionCounter);
                        _mctStore.AddSection(sec);
                        sectionCounter++;
                    }
                    
                    // parse bearings
                    var bearingPair = Bearings.Where(b => b.ObjectPlacement is IIfcLinearPlacement lp && lp.Distance.DistanceAlong == currentPos).ToList();
                    if (bearingPair.Count == 2)
                    {
                        var constraints = TranslatorHelper.TranslateBearing(bearingPair);
                        _mctStore.AddSupport(nodes[i], constraints);
                    }

                    // parse bracings
                    var bracing = Bracings.Where(b => b.ObjectPlacement is IIfcLinearPlacement lp && lp.Distance.DistanceAlong == currentPos).FirstOrDefault();
                    if (bracing != null)
                    {
                        var bracingLoadZ = TranslatorHelper.TranslateBracing(bracing, mat);
                        loadcase.AddNodalLoad(nodes[i], new List<double>() { 0, 0, -bracingLoadZ, 0, 0, 0 });
                    }
                    
                    // create elements 
                    var element = new MctFrameElement()
                    {
                        Id = i + 1,
                        Mat = mat,
                        Sec = sec,
                        Type = MctElementTypeEnum.BEAM,
                        Node1 = nodes[i],
                        Node2 = nodes[i + 1]
                    };
                    _mctStore.AddElement(element);
                }
            }                
        }              

        private void PreProcessDirectrix(IIfcElementAssembly girder)
        {
            const int MESHSIZE = 3000;
            var plateAssemblies = girder.IsDecomposedBy.FirstOrDefault().RelatedObjects
                .Where(o => o is IIfcElementAssembly ea && (ea.ObjectType == "FLANGE_ASSEMBLY" || ea.ObjectType == "WEB_ASSEMBLY"))
                .Select(o => (IIfcElementAssembly)o);
            foreach (var plateAssembly in plateAssemblies)
            {
                var plates = plateAssembly.IsDecomposedBy.FirstOrDefault().RelatedObjects
                    .Where(o => o is IIfcPlate)
                    .Select(o => (IIfcPlate)o);
                foreach (var plate in plates)
                    PreProcessDirectrix(plate);
                var stiffenerAssemblies = plateAssembly.IsDecomposedBy.FirstOrDefault().RelatedObjects
                    .Where(o => o is IIfcElementAssembly ea && ea.ObjectType == "STIFFENER_ASSEMBLY");
                foreach (var stiffenerAssembly in stiffenerAssemblies)
                {
                    var stiffeners = stiffenerAssembly.IsDecomposedBy.FirstOrDefault().RelatedObjects
                        .Where(o => o is IIfcMember m && m.ObjectType == "STIFFENING_RIB")
                        .Select(o => (IIfcMember)o);
                    foreach (var stiffener in stiffeners)
                        PreProcessDirectrix(stiffener);
                }
            }

            Bracings = girder.IsDecomposedBy.FirstOrDefault().RelatedObjects
                .Where(o => o is IIfcElementAssembly ea && ea.ObjectType == "CROSS_BRACING")
                .Select(o => (IIfcElementAssembly)o).ToList();
            foreach (var bracing in Bracings)
                PreProcessDirectrix((IIfcLinearPlacement)bracing.ObjectPlacement);
            
            foreach (var bearing in Bearings)
                PreProcessDirectrix((IIfcLinearPlacement)bearing.ObjectPlacement);

            // TODO
            // Adjust the mesh
        }

        // Add positions at where section changes
        private void PreProcessDirectrix(IIfcBuildingElement linearBuildingElement)
        {
            var sectionedSolid = linearBuildingElement.Representation.Representations[0].Items
                .Where(i => i is IIfcSectionedSolidHorizontal)
                .Select(i => (IIfcSectionedSolidHorizontal)i).FirstOrDefault();
            int id = sectionedSolid.Directrix.EntityLabel;
            double d = sectionedSolid.CrossSectionPositions[0].DistanceAlong;
            PushPosition(id, d, true);
            d= sectionedSolid.CrossSectionPositions[1].DistanceAlong;
            PushPosition(id, d, true);
        }

        // Add positions where load or constraint are imposed
        private void PreProcessDirectrix(IIfcLinearPlacement lp)
        {
            if (lp == null) return;
            int id = lp.PlacementRelTo.EntityLabel;
            double d = lp.Distance.DistanceAlong;
            PushPosition(id, d, false);
        }

        private void PushPosition(int id, double d, bool isSectionChanged)
        {
            if (!_positionsTable[id].ContainsKey(d))
                _positionsTable[id].Add(d, isSectionChanged);
        }                     
    }
    
}
