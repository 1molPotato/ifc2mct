using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MaterialResource;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.SharedBldgElements;
using Xbim.Ifc4.SharedComponentElements;
using Xbim.IO;

namespace ifc2mct.BridgeFactory
{
    public class BridgeBuilder : IDisposable
    {
        private readonly string _outputPath = "";
        //private readonly string _projectName = "xx大道立交SW1-SW5匝道";
        private readonly string _bridgeName = "SW3-SW5钢结构连续箱梁";
        private readonly IfcStore _model;


        private bool IsAlignmentStraight { get; set; }
        private IfcCurve BridgeAlignmentCurve { get; set; }

        /// <summary>
        /// bridge alignment starts at BridgeStart of the main alignment
        /// </summary>
        private double BridgeStart { get; set; }

        /// <summary>
        /// bridge alignment ends at BridgeEnd of the main alignment
        /// </summary>
        private double BridgeEnd { get; set; }

        /// <summary>
        /// the vertical offset of bridge alignment to the main alignment
        /// </summary>
        private double BridgeAlignmentVerOff { get; set; }

        /// <summary>
        /// the lateral offset of bridge alignment to the main alignment
        /// </summary>
        private double BridgeAlignmentLatOff { get; set; }

        /// <summary>
        /// girder starts at StartGap of the bridge alignment
        /// </summary>
        private double StartGap { get; set; }

        /// <summary>
        /// girder ends at (BridgeEnd - BridgeStart - EndGap) of the bridge alignment
        /// </summary>
        private double EndGap { get; set; }

        /// <summary>
        /// overall section is constant with dimensions
        /// B1, B2, B4, B5, H
        /// </summary>
        private List<double> SectionDimensions { get; set; }

        /// <summary>
        /// the 1st, 2nd, 3rd table are respectively the table of top flange, web and bottom flange
        /// each table contains a list of (distanceAlong, thickness) pairs which indicates
        /// the thickness of the plate between last distanceAlong (begin with 0) and this distanceAlong
        /// </summary>
        private Dictionary<int, List<(double distanceAlong, double thickness)>> PlateThicknessLists { get; set; }

        /// <summary>
        /// (parentId, [(distanceAlong, typeId, layoutId)]) pairs indicate
        /// the sittener information of the parent plate with parentId
        /// where, parentId = 0, 1, 2, 3 is respectively for top flange, left web, right web and bottom flange
        /// (distanceAlong, typeId, layoutId) pairs indicate
        /// the typeId and layoutId of the stiffener between last distanceAlong and this distanceAlong
        /// </summary>
        private Dictionary<int, List<List<(double distanceAlong, int typeId, int layoutId)>>> StiffenerLists { get; set; }

        /// <summary>
        /// (typeId, dimensions) pair indicates
        /// the dimensions of the stiffener with typeId
        /// dimensions could be {H, B}, {H, B, tw, tf} and {H, B1, B2, t, R} respectively for
        /// flat stiffener, T-shape stiffener and U-shape stiffener
        /// </summary>
        private Dictionary<int, List<double>> StiffenerTypeTable { get; set; }

        /// <summary>
        /// (layoutId, gaps) pair indicates
        /// the gaps of the stiffener layout with layoutId
        /// gaps is a list of tuple (num, gap) which represents
        /// the number of stiffener and the gap distance between every 2 stiffener
        /// </summary>
        private Dictionary<int, List<(int num, double gap)>> StiffenerLayoutTable { get; set; }

        private Dictionary<int, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)> BearingTypeTable { get; set; }

        private List<(double distanceAlong, double offsetLateral, int bearingTypeId)> BearingList { get; set; }

        private Dictionary<int, double> DiaphragmTypeTable { get; set; }

        private List<(int typeId, int num, double gap)> DiaphragmList { get; set; }

        private IfcCartesianPoint Origin3D { get; set; }
        private IfcDirection AxisX3D { get; set; }
        private IfcDirection AxisY3D { get; set; }
        private IfcDirection AxisZ3D { get; set; }
        private IfcAxis2Placement3D WCS { get; set; }
        private IfcCartesianPoint Origin2D { get; set; }
        private IfcDirection AxisX2D { get; set; }
        private IfcDirection AxisY2D { get; set; }
        private IfcAxis2Placement2D WCS2D { get; set; }

        // Constructors
        public BridgeBuilder() : this("alignment.ifc", "test.ifc")
        {
            // empty
        }

        public BridgeBuilder(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath))
            {
                var builder = new AlignmentBuilder(inputPath);
                builder.Run();
            }
            _model = IfcStore.Open(inputPath);

            _outputPath = outputPath;
            PlateThicknessLists = new Dictionary<int, List<(double distanceAlong, double thickness)>>();
            StiffenerLists = new Dictionary<int, List<List<(double distanceAlong, int typeId, int layoutId)>>>();
            StiffenerTypeTable = new Dictionary<int, List<double>>();
            StiffenerLayoutTable = new Dictionary<int, List<(int num, double gap)>>();
            BearingTypeTable = new Dictionary<int, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)>();
            BearingList = new List<(double distanceAlong, double offsetLateral, int bearingTypeId)>();
            DiaphragmList = new List<(int typeId, int num, double gap)>();
            DiaphragmTypeTable = new Dictionary<int, double>();
        }

        // Interfaces  
        /// <summary>
        /// Set bridge's start position, end position, vertical offset and lateral offset to the road alignment
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="verOffset"></param>
        /// <param name="latOffset"></param>
        public void SetBridgeAlignment(double start, double end, double verOffset, double latOffset)
        {
            BridgeStart = start;
            BridgeEnd = end;
            BridgeAlignmentVerOff = verOffset;
            BridgeAlignmentLatOff = latOffset;
        }

        /// <summary>
        /// Set the girder's start gap and end gap to the bridge's start position and end position respectively
        /// </summary>
        /// <param name="startGap"></param>
        /// <param name="endGap"></param>
        public void SetGaps(double startGap, double endGap)
        {
            StartGap = startGap;
            EndGap = endGap;
        }

        /// <summary>
        /// Set box-girder section dimensions = {B1, B2, B4, B5, H}, where
        /// B1 is the external length of top flange
        /// B2 is the internal length of top flange
        /// B4 is the external length of bottom flange
        /// B5 is the internal length of bottom flange
        /// H is the height of the box
        /// thickness of plates is not included
        /// </summary>
        /// <param name="dimensions"></param>
        public void SetOverallSection(List<double> dimensions)
        {
            if (dimensions.Count == 5)
                SectionDimensions = dimensions;
            else
                throw new ArgumentException("The overall section dimensions should be {B1, B2, B4, B5, H}");
        }


        public void SetThicknesses(List<List<(double, double)>> thicknessLists)
        {
            if (thicknessLists.Count == 3)
            {
                SetThicknesses(0, thicknessLists[0]);
                SetThicknesses(1, thicknessLists[1]);
                SetThicknesses(2, thicknessLists[1]);
                SetThicknesses(3, thicknessLists[2]);
            }
            else
                throw new ArgumentException("You have to provide thickness tables for top flange, web and bottom flange");
        }

        /// <summary>
        /// Set the thickness of flange and web, considering its change along bridge alignment
        /// plateCode must be either of 0, 1, 2, 3 to represent 
        /// top flange, left web, right web and bottom flange respectively
        /// </summary>
        /// <param name="plateCode"></param>
        /// <param name="thicknessList"></param>
        public void SetThicknesses(int plateCode, List<(double dist, double thickness)> thicknessList)
        {
            if (plateCode > 3 || plateCode < 0)
                throw new ArgumentException("plateCode must be either of 0, 1, 2, 3");
            PlateThicknessLists[plateCode] = thicknessList;
        }

        public void SetThickness(int plateCode, double distanceAlong, double thickness)
        {
            // not implemented
            if (plateCode > 3 || plateCode < 0)
                throw new ArgumentException("plateCode must be either of 0, 1, 2, 3");
        }

        public void AddStiffenerType(int id, List<double> dimensions)
        {
            if (!StiffenerTypeTable.ContainsKey(id))
                StiffenerTypeTable[id] = dimensions;
            else
                throw new ArgumentException($"Stiffener type with id {id} has been defined");
        }

        public void AddStiffenerLayout(int id, List<(int num, double gap)> layout)
        {
            if (!StiffenerLayoutTable.ContainsKey(id))
                StiffenerLayoutTable[id] = layout;
            else
                throw new ArgumentException($"Stiffener layout with id {id} has been defined");
        }

        public void AddStiffeners(int parentId, List<(double distanceAlong, int typeId, int layoutId)> stiffenerList)
        {
            if (parentId >= 0 && parentId <= 3)
            {
                foreach (var (distanceAlong, typeId, layoutId) in stiffenerList)
                {
                    if (!StiffenerTypeTable.ContainsKey(typeId))
                        throw new ArgumentException($"Stiffener type with id {typeId} has not been defined");
                    if (!StiffenerLayoutTable.ContainsKey(layoutId))
                        throw new ArgumentException($"Stiffener layout with id {layoutId} has not been defined");
                }
                if (!StiffenerLists.ContainsKey(parentId))
                    StiffenerLists[parentId] = new List<List<(double distanceAlong, int typeId, int layoutId)>>();
                StiffenerLists[parentId].Add(stiffenerList);
            }
            else
                throw new ArgumentException("ParentId should only be 0(top flange) or 1(web) or 3(bottom flange)");
        }

        public void AddBearingType(int typeId, bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)
        {
            if (!BearingTypeTable.ContainsKey(typeId))
                BearingTypeTable[typeId] = (fixedLateral, fixedLongitudinal, fixedVertical);
            else
                throw new ArgumentException($"Bearing type with id {typeId} has been defined");
        }

        public void AddBearingType(int typeId, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical) isFixed)
        {
            AddBearingType(typeId, isFixed.fixedLateral, isFixed.fixedLongitudinal, isFixed.fixedVertical);
        }

        public void AddBearing(double distanceAlong, double offsetLateral, int typeId)
        {
            if (!BearingTypeTable.ContainsKey(typeId))
                throw new ArgumentException($"Bearing type with id {typeId} has not been defined");
            else
                BearingList.Add((distanceAlong, offsetLateral, typeId));
        }

        public void AddBearing((double distanceAlong, double offsetLateral, int typeId) bearing)
        {
            AddBearing(bearing.distanceAlong, bearing.offsetLateral, bearing.typeId);
        }

        public void AddDiaphragmType(int typeId, double thickness)
        {
            if (DiaphragmTypeTable.ContainsKey(typeId))
                throw new ArgumentException($"Diaphragm type with id {typeId} has been defined");
            DiaphragmTypeTable[typeId] = thickness;
        }

        public void AddDiaphragm(List<(int typeId, int num, double gap)> list)
        {
            foreach (var (typeId, num, gap) in list)            
                if (!DiaphragmTypeTable.ContainsKey(typeId))
                    throw new ArgumentException($"Diaphragm type with id {typeId} has not been defined");
            DiaphragmList = list;
        }        

        public void Build()
        {
            InitWCS();
            var site = _model.Instances.OfType<IfcSite>().FirstOrDefault();
            if (site == null)
                throw new NotSupportedException("Input IFC file must include an instance of IfcSite");
            var alignment = _model.Instances.OfType<IfcAlignment>().FirstOrDefault();
            if (alignment == null)
                throw new NotSupportedException("Input IFC file must include an instance of IfcAlignment");
            BridgeAlignmentCurve = AddBridgeAlignment(alignment);

            // Build the superstructure of a steel-box-girder bridge
            var girder = CreateSteelBoxGirder(BridgeAlignmentCurve);
            IfcModelBuilder.AddProductIntoSpatial(_model, site, girder, "Add the girder to site");

            // Build the bearings
            var bearings = CreateBearings(girder);
            foreach (var bearing in bearings)
            {
                //_model.Instances.New<IfcRelConnectsElements>(rce =>
                //{
                //    rce.RelatedElement = girder;
                //    rce.RelatingElement = bearing;
                //}); // IFC4x2 features
                IfcModelBuilder.AddProductIntoSpatial(_model, site, bearing, "Add the bearing to site");
            }
                

            try
            {
                _model.SaveAs(_outputPath, StorageType.Ifc);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to save {0}", _outputPath);
                Console.WriteLine(e.Message);
            }
        }

        // Utilities        
        private void InitWCS()
        {
            using (var txn = this._model.BeginTransaction("Initialise WCS"))
            {
                var context3D = this._model.Instances.OfType<IfcGeometricRepresentationContext>()
                .Where(c => c.CoordinateSpaceDimension == 3)
                .FirstOrDefault();
                if (context3D.WorldCoordinateSystem is IfcAxis2Placement3D wcs)
                {
                    WCS = wcs;
                    Origin3D = wcs.Location;
                    AxisZ3D = IfcModelBuilder.MakeDirection(_model, 0, 0, 1);
                    wcs.Axis = AxisZ3D;
                    AxisX3D = IfcModelBuilder.MakeDirection(_model, 1, 0, 0);
                    wcs.RefDirection = AxisX3D;
                    AxisY3D = IfcModelBuilder.MakeDirection(_model, 0, 1, 0);
                }

                var context2D = this._model.Instances.OfType<IfcGeometricRepresentationContext>()
                    .Where(c => c.CoordinateSpaceDimension == 2)
                    .FirstOrDefault();
                if (context2D.WorldCoordinateSystem is IfcAxis2Placement2D wcs2d)
                {
                    WCS2D = wcs2d;
                    Origin2D = wcs2d.Location;
                    AxisX2D = IfcModelBuilder.MakeDirection(_model, 1, 0);
                    wcs2d.RefDirection = AxisX2D;
                    AxisY2D = IfcModelBuilder.MakeDirection(_model, 0, 1);
                }

                txn.Commit();
            }
        }                             

        /// <summary>
        /// Create an offset alignment curve based on the main alignment curve 
        /// and add it to the main alignment's shape representation
        /// </summary>
        /// <param name="mainAlignment"></param>
        /// <returns></returns>
        private IfcCurve AddBridgeAlignment(IfcAlignment mainAlignment)
        {
            using (var txn = this._model.BeginTransaction("Add offset curve"))
            {
                var offsetCurve = this._model.Instances.New<IfcOffsetCurveByDistances>(c =>
                {
                    c.BasisCurve = mainAlignment.Axis;
                    c.OffsetValues.Add(IfcModelBuilder.MakeDistanceExpression(_model, BridgeStart, BridgeAlignmentLatOff, BridgeAlignmentVerOff));
                    c.OffsetValues.Add(IfcModelBuilder.MakeDistanceExpression(_model, BridgeEnd, BridgeAlignmentLatOff, BridgeAlignmentVerOff));
                    c.Tag = "Bridge alignment curve";
                });
                var offsetCurveSolid = IfcModelBuilder.CreateSolidShapeForCurve(_model, offsetCurve, 0, BridgeEnd - BridgeStart);
                IfcModelBuilder.SetSurfaceStyle(_model, offsetCurveSolid, 0, 0, 1);
                mainAlignment.Representation.Representations.FirstOrDefault(r => r.RepresentationIdentifier == "Body").Items.Add(offsetCurveSolid);

                var shape = IfcModelBuilder.MakeShapeRepresentation(_model, 3, "StringLines", "OffsetCurves");
                shape.Items.Add(offsetCurve);                
                mainAlignment.Representation.Representations.Add(shape);
                
                txn.Commit();
                return offsetCurve;
            }                
        }

        private IfcElementAssembly CreateSteelBoxGirder(IfcCurve directrix)
        {
            using (var txn = this._model.BeginTransaction("Create steel-box-girder"))
            {
                var girder = this._model.Instances.New<IfcElementAssembly>(g =>
                {
                    g.Name = _bridgeName;
                    g.Description = "Steel-box-girder";
                    g.PredefinedType = IfcElementAssemblyTypeEnum.GIRDER;
                });

                // Set material for this girder
                CreateMaterialForGirder(girder);

                // 0 for top flange, 1 for left web, 2 for right web, 3 for bottom flange
                var plateCodes = new List<int>() { 0, 1, 2, 3 };
                var plates = plateCodes.Select(c => CreateBoxPlate(directrix, c)).ToList();
                foreach (var stiffenerLists in StiffenerLists)
                {
                    var plateCode = stiffenerLists.Key;
                    foreach (var stiffenerList in stiffenerLists.Value)
                    {
                        var stiffenerGroup = CreateStiffeners(plateCode, stiffenerList);
                        var rel = this._model.Instances.New<IfcRelAggregates>(ra =>
                        {
                            ra.RelatingObject = plates[plateCode];
                            ra.RelatedObjects.Add(stiffenerGroup);
                        });
                    }
                }

                var diaphragms = CreateDiaphragms();

                // Aggregate flanges and webs into the girder assembly
                var relAggregates = this._model.Instances.New<IfcRelAggregates>(r =>
                {
                    r.RelatingObject = girder;
                    r.RelatedObjects.AddRange(plates);
                    foreach (var diaphragm in diaphragms)
                        r.RelatedObjects.Add(diaphragm);
                });

                txn.Commit();
                return girder;
            }
        }

        private IfcElementAssembly CreateBoxPlate(IfcCurve directrix, int plateCode)
        {            
            var plateAssembly = this._model.Instances.New<IfcElementAssembly>(ea => 
            {
                ea.Name = plateCode == 0 ? "顶板" : (plateCode == 1 ? "左腹板" : (plateCode == 2 ? "右腹板" : "底板"));
                ea.ObjectType = (plateCode == 0 || plateCode == 3) ? "FLANGE_ASSEMBLY" : "WEB_ASSEMBLY";
                ea.PredefinedType = IfcElementAssemblyTypeEnum.USERDEFINED;
            });
            var relAggregates = this._model.Instances.New<IfcRelAggregates>(rg => rg.RelatingObject = plateAssembly);
            
            double x1 = 0, y1 = 0, x2 = 0, y2 = 0, t = 0, offsetLateral = 0, offsetVertical = 0;            
            var thicknessList = PlateThicknessLists[plateCode];
            var lastDistanceAlong = StartGap;            
            
            for (int i = 0; i < thicknessList.Count; ++i)
            {
                var start = lastDistanceAlong;
                var end = thicknessList[i].distanceAlong;
                var dimensionNames = new List<string>() { "B1", "B2", "B4", "B5", "H" };
                var dimensions = new Dictionary<string, double>();
                for (int j = 0; j < SectionDimensions.Count; ++j)
                    dimensions[dimensionNames[j]] = SectionDimensions[j];
                switch (plateCode)
                {
                    case 0:
                        x1 = dimensions["B1"] + dimensions["B2"] / 2;
                        y1 = thicknessList[i].thickness / 2;
                        x2 = -x1; y2 = y1; t = thicknessList[i].thickness;
                        break;                    
                    case 1:
                        x1 = 0; y1 = 0;
                        x2 = (dimensions["B5"] - dimensions["B2"]) / 2; y2 = -dimensions["H"];
                        t = thicknessList[i].thickness; offsetLateral = dimensions["B2"] / 2;
                        break;
                    case 2:
                        x1 = 0; y1 = 0;
                        x2 = (dimensions["B2"] - dimensions["B5"]) / 2; y2 = -dimensions["H"];
                        t = thicknessList[i].thickness; offsetLateral = -dimensions["B2"] / 2;
                        break;
                    case 3:
                        x1 = dimensions["B4"] + dimensions["B5"] / 2;
                        y1 = -thicknessList[i].thickness / 2;
                        x2 = -x1; y2 = y1; t = thicknessList[i].thickness;
                        offsetVertical = -dimensions["H"];
                        break;
                    default: break;
                }
                var solid = this._model.Instances.New<IfcSectionedSolidHorizontal>(s =>
                {
                    s.Directrix = directrix;
                    var p1 = IfcModelBuilder.MakeCartesianPoint(_model, x1, y1);
                    var p2 = IfcModelBuilder.MakeCartesianPoint(_model, x2, y2);
                    var line = IfcModelBuilder.MakePolyline(_model, new List<IfcCartesianPoint>() { p1, p2 });
                    var profile = IfcModelBuilder.MakeCenterLineProfile(_model, line, t);
                    s.CrossSections.Add(profile);
                    s.CrossSections.Add(profile);
                    var pos1 = IfcModelBuilder.MakeDistanceExpression(_model, start, offsetLateral, offsetVertical);
                    var pos2 = IfcModelBuilder.MakeDistanceExpression(_model, end, offsetLateral, offsetVertical);
                    s.CrossSectionPositions.Add(pos1);
                    s.CrossSectionPositions.Add(pos2);
                });
                IfcModelBuilder.SetSurfaceStyle(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = IfcModelBuilder.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid");
                shape.Items.Add(solid);
                lastDistanceAlong = end;

                var plate = this._model.Instances.New<IfcPlate>(p =>
                {
                    p.Name = $"{plateAssembly.Name}-0{i + 1}";
                    p.ObjectType = (plateCode == 0 || plateCode == 3) ? "FLANGE_PLATE" : "WEB_PLATE";
                    p.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                    p.PredefinedType = IfcPlateTypeEnum.USERDEFINED;
                    //p.PredefinedType = (part == 0 || part == 1) ? IfcPlateTypeEnum.FLANGE_PLATE : IfcPlateTypeEnum.WEB_PLATE; // IFC4x2 feature
                });
                relAggregates.RelatedObjects.Add(plate);
            }
                                               
            return plateAssembly;
        }

        private IfcElementAssembly CreateStiffeners(int plateCode, List<(double distanceAlong, int typeId, int layoutId)> stiffenerList)
        {
            int dimensionNum = 0;
            var stiffenerAssembly = this._model.Instances.New<IfcElementAssembly>(ea => 
            {
                string assemblyName = plateCode == 0 ? "顶板纵肋" : (plateCode == 3 ? "底板纵肋" : "腹板纵肋");
                ea.Name = assemblyName;
                ea.ObjectType = "RIB_ASSEMBLY";
                if (stiffenerList.Any())
                {
                    dimensionNum = StiffenerTypeTable[stiffenerList[0].typeId].Count;
                    ea.Tag = dimensionNum == 5 ? "U-shape" : (dimensionNum == 2 ? "Flat" : "T-shape");
                }
            });
            var relAggregates = this._model.Instances.New<IfcRelAggregates>(rg => rg.RelatingObject = stiffenerAssembly);
            var lastDistanceAlong = StartGap;

            int stiffenerCounter = 1;
            foreach (var (distanceAlong, typeId, layoutId) in stiffenerList)
            {
                var start = lastDistanceAlong;
                var end = distanceAlong;
                var dimensions = StiffenerTypeTable[typeId];
                string name = dimensionNum == 5 ? "U形肋" : (dimensionNum == 2 ? "板肋" : "T形肋");
                var profile = CreateStiffenerProfile(dimensions, GetStiffenerDirection(plateCode));
                var refPoint = GetRefPoint(plateCode);
                var offsetDirection = GetOffsetDirection(plateCode);
                foreach (var (num, gap) in StiffenerLayoutTable[layoutId])
                {
                    for (int i = 0; i < num; ++i)
                    {
                        var stiffShape = IfcModelBuilder.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid");
                        var stiffenerSolid = this._model.Instances.New<IfcSectionedSolidHorizontal>(s =>
                        {
                            s.Directrix = BridgeAlignmentCurve;
                            s.CrossSections.Add(profile);
                            s.CrossSections.Add(profile);
                            refPoint = refPoint + gap * offsetDirection;
                            var pos1 = IfcModelBuilder.MakeDistanceExpression(_model, start, refPoint.X, refPoint.Y);
                            var pos2 = IfcModelBuilder.MakeDistanceExpression(_model, end, refPoint.X, refPoint.Y);
                            s.CrossSectionPositions.Add(pos1);
                            s.CrossSectionPositions.Add(pos2);
                        });
                        //IfcModelBuilder.SetSurfaceStyle(m, stiff, 0.752941176470588, 0.313725490196078, 0.301960784313725);
                        stiffShape.Items.Add(stiffenerSolid);
                        var stiffener = this._model.Instances.New<IfcMember>(sg =>
                        {
                            sg.Name = $"{name}-{stiffenerCounter++}";
                            sg.ObjectType = "STIFFENING_RIB";
                            sg.PredefinedType = IfcMemberTypeEnum.USERDEFINED;
                            //sg.PredefinedType = IfcMemberTypeEnum.STIFFENING_RIB; // IFC4x2 features
                            sg.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(stiffShape));
                        });
                        relAggregates.RelatedObjects.Add(stiffener);
                    }
                }
                lastDistanceAlong = distanceAlong;
            }
            
            var dimensionsTable = CreateStiffDimsPropSet(stiffenerList);
            this._model.Instances.New<IfcRelDefinesByProperties>(rdbp =>
            {
                rdbp.RelatedObjects.Add(stiffenerAssembly);
                rdbp.RelatingPropertyDefinition = dimensionsTable;
            });
            return stiffenerAssembly;
        }

        private IfcPropertySet CreateStiffDimsPropSet(List<(double distanceAlong, int typeId, int layoutId)> stiffenerList)
        {
            var propSet = _model.Instances.New<IfcPropertySet>(ps => ps.Name = "Dimensions");
            double start = StartGap;
            var dimensions = StiffenerTypeTable[stiffenerList.FirstOrDefault().typeId];
            List<string> fieldNames = null;
            if (dimensions.Count == 2)
                // flat stiffener
                fieldNames = new List<string>() { "H", "B" };
            else if (dimensions.Count == 5)
                // U-shape stiffener
                fieldNames = new List<string>() { "H", "B1", "B2", "t", "R" };
            else
                // T-shape stiffener
                fieldNames = new List<string>() { "H", "B", "tw", "tf" };
            foreach (var name in fieldNames)
                propSet.HasProperties.Add(IfcModelBuilder.MakePropertyTableValue(_model, name));
            foreach (var (distanceAlong, typeId, layoutId) in stiffenerList)
            {
                dimensions = StiffenerTypeTable[typeId];
                for (int i = 0; i < fieldNames.Count; ++i)
                {
                    var val = propSet.HasProperties.OfType<IfcPropertyTableValue>()
                        .Where(p => p.Name == fieldNames[i]).FirstOrDefault();
                    val.DefiningValues.Add(new IfcLengthMeasure(start));
                    val.DefinedValues.Add(new IfcLengthMeasure(dimensions[i]));
                }
                start = distanceAlong;
            }

            return propSet;
        }

        private List<IfcProxy> CreateBearings(IfcElement girder)
        {
            var bearings = new List<IfcProxy>();
            foreach (var (distanceAlong, offsetLateral, bearingTypeId) in BearingList)
                bearings.Add(CreateBearing(distanceAlong, offsetLateral, bearingTypeId, girder));
                //AddElementToSpatial(m, site, bearing, "Add the bearing to site");
            return bearings;
        }

        private IfcProxy CreateBearing(double dist, double offsetLateral, int typeId, IfcElement girder)
        {
            using (var txn = this._model.BeginTransaction("Create bearings"))
            {                
                var distance = IfcModelBuilder.MakeDistanceExpression(_model, dist, offsetLateral, -SectionDimensions[4] - 100);
                string name = "支座";                

                // define bearing properties
                var (fixedLateral, fixedLongitudinal, fixedVertical) = BearingTypeTable[typeId];
                if (fixedVertical)
                    name = "抗拉" + name;
                if (fixedLateral && fixedLongitudinal)
                    name = "固定" + name;
                else if (fixedLateral || fixedLongitudinal)
                    name = "单向滑动" + name;
                else
                    name = "双向滑动" + name;                
                var pset_BearingCommon = _model.Instances.New<IfcPropertySet>(ps =>
                {
                    ps.Name = "Pset_BearingCommon";
                    var displacementAccomodated = _model.Instances.New<IfcPropertyListValue>(l =>
                    {
                        l.Name = "DisplacementAccomodated";
                        l.ListValues.Add(new IfcBoolean(!fixedLongitudinal));
                        l.ListValues.Add(new IfcBoolean(!fixedLateral));
                        l.ListValues.Add(new IfcBoolean(!fixedVertical));
                    });
                    var rotationAccomodated = this._model.Instances.New<IfcPropertyListValue>(l =>
                    {
                        l.Name = "RotationAccomodated";
                        l.ListValues.Add(new IfcBoolean(true));
                        l.ListValues.Add(new IfcBoolean(true));
                        l.ListValues.Add(new IfcBoolean(true));
                    });
                    ps.HasProperties.Add(displacementAccomodated);
                    ps.HasProperties.Add(rotationAccomodated);
                });

                var shape = CreateBearingShape();
                var bearing = this._model.Instances.New<IfcProxy>(b =>
                {
                    b.ObjectPlacement = IfcModelBuilder.MakeLinearPlacement(_model, BridgeAlignmentCurve, distance);
                    b.Name = name;
                    b.Description = "IfcBearing";
                    b.ProxyType = IfcObjectTypeEnum.PRODUCT;
                    b.ObjectType = "POT";
                    b.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                });

                this._model.Instances.New<IfcRelDefinesByProperties>(r =>
                {
                    r.RelatingPropertyDefinition = pset_BearingCommon;
                    r.RelatedObjects.Add(bearing);
                });

                // Connect bearing to girder
                //m.Instances.New<IfcRelConnectsElements>(rce =>
                //{
                //    rce.RelatedElement = girder;
                //    rce.RelatingElement = bearing;
                //});

                txn.Commit();
                return bearing;
            }                
        }

        private IfcShapeRepresentation CreateBearingShape()
        {
            var shape = IfcModelBuilder.MakeShapeRepresentation(_model, 3, "Body", "CSG");
            var rectangle = IfcModelBuilder.MakeRectangleProfile(_model, 940, 920);
            var vzNegated = IfcModelBuilder.MakeDirection(_model, 0, 0, -1);
            var pos1 = IfcModelBuilder.MakeAxis2Placement3D(_model);            
            shape.Items.Add(IfcModelBuilder.MakeExtrudedAreaSolid(_model, rectangle, pos1, vzNegated, 50));

            var rectangle2 = IfcModelBuilder.MakeRectangleProfile(_model, 80, 920);
            var pos4 = IfcModelBuilder.MakeAxis2Placement3D(_model, IfcModelBuilder.MakeCartesianPoint(_model, -410, 0, -50));            
            shape.Items.Add(IfcModelBuilder.MakeExtrudedAreaSolid(_model, rectangle2, pos4, vzNegated, 80));

            var pos5 = IfcModelBuilder.MakeAxis2Placement3D(_model, IfcModelBuilder.MakeCartesianPoint(_model, 410, 0, -50));
            shape.Items.Add(IfcModelBuilder.MakeExtrudedAreaSolid(_model, rectangle2, pos5, vzNegated, 80));

            var rectangle3 = IfcModelBuilder.MakeRectangleProfile(_model, 740, 850);
            var pos6 = IfcModelBuilder.MakeAxis2Placement3D(_model, IfcModelBuilder.MakeCartesianPoint(_model, 0, 0, -50));
            var solid = IfcModelBuilder.MakeExtrudedAreaSolid(_model, rectangle3, pos6, vzNegated, 70);
            IfcModelBuilder.SetSurfaceStyle(_model, solid, 1, 0, 0);
            shape.Items.Add(solid);

            var circle = IfcModelBuilder.MakeCircleProfile(_model, 300);
            var pos2 = IfcModelBuilder.MakeAxis2Placement3D(_model, IfcModelBuilder.MakeCartesianPoint(_model, 0, 0, -120));            
            shape.Items.Add(IfcModelBuilder.MakeExtrudedAreaSolid(_model, circle, pos2, vzNegated, 65));

            var rectangle4 = IfcModelBuilder.MakeRectangleProfile(_model, 800, 1200);
            var pos3 = IfcModelBuilder.MakeAxis2Placement3D(_model, IfcModelBuilder.MakeCartesianPoint(_model, 0, 0, -185));            
            shape.Items.Add(IfcModelBuilder.MakeExtrudedAreaSolid(_model, rectangle4, pos3, vzNegated, 75));
            
            return shape;
        }

        private List<IfcElementAssembly> CreateDiaphragms()
        {
            var diaphragms = new List<IfcElementAssembly>();
            double lastDist = StartGap;
            foreach (var (typeId, num, gap) in DiaphragmList)
            {
                for (int i = 0; i < num; ++i)
                {
                    lastDist += gap;
                    diaphragms.Add(CreateDiaphragm(typeId, lastDist));                    
                }
            }                
            return diaphragms;
        }

        private IfcElementAssembly CreateDiaphragm(int typeId, double distanceAlong)
        {
            var distance = IfcModelBuilder.MakeDistanceExpression(_model, distanceAlong);
            var diaphragm = _model.Instances.New<IfcElementAssembly>(ea =>
            {
                ea.Name = "横向支撑";
                ea.ObjectPlacement = IfcModelBuilder.MakeLinearPlacement(_model, BridgeAlignmentCurve, distance);
                ea.ObjectType = "CROSS_BRACING";
                ea.PredefinedType = IfcElementAssemblyTypeEnum.USERDEFINED;
            });
            
            var plate = _model.Instances.New<IfcPlate>(p =>
            {
                p.Name = "横隔板";
                p.ObjectPlacement = IfcModelBuilder.MakeLocalPlacement(_model, null, diaphragm.ObjectPlacement);
                var solid = _model.Instances.New<IfcExtrudedAreaSolid>(s =>
                {
                    s.Position = IfcModelBuilder.MakeAxis2Placement3D(_model, Origin3D, AxisX3D, AxisY3D);
                    var outerCurve = CreateDiaphragmOuterCurve(distanceAlong);                    
                    var innerCurve = CreateDiaphragmInnerCurve();
                    s.SweptArea = IfcModelBuilder.MakeArbProfileWithVoids(_model, outerCurve/*outerCurve*/, new List<IfcCurve>() { innerCurve });
                    s.ExtrudedDirection = IfcModelBuilder.MakeDirection(_model, 0, 0, 1);
                    s.Depth = DiaphragmTypeTable[typeId];
                });
                IfcModelBuilder.SetSurfaceStyle(_model, solid, 1, 0.9333, 0, 0.15);
                var shape = IfcModelBuilder.MakeShapeRepresentation(_model, 3, "Body", "CSG");
                shape.Items.Add(solid);
                p.Representation = _model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
            });

            // Cantilever web
            var plate2 = _model.Instances.New<IfcPlate>(p =>
            {
                p.Name = "悬挑腹板";
                p.ObjectPlacement = IfcModelBuilder.MakeLocalPlacement(_model, null, diaphragm.ObjectPlacement);
                var solid = this._model.Instances.New<IfcExtrudedAreaSolid>(s =>
                {
                    s.Position = IfcModelBuilder.MakeAxis2Placement3D(_model, Origin3D, AxisX3D, AxisY3D);
                    var outerCurve = CreateCantileverWebOuterCurve(distanceAlong, true);
                    
                    s.SweptArea = IfcModelBuilder.MakeArbClosedProfile(_model, outerCurve);
                    s.ExtrudedDirection = IfcModelBuilder.MakeDirection(_model, 0, 0, 1);
                    s.Depth = DiaphragmTypeTable[typeId];
                });
                IfcModelBuilder.SetSurfaceStyle(_model, solid, 1, 0.9333, 0, 0.15);
                var solid2 = _model.Instances.New<IfcExtrudedAreaSolid>(s =>
                {
                    s.Position = IfcModelBuilder.MakeAxis2Placement3D(_model, Origin3D, AxisX3D, AxisY3D);
                    var outerCurve = CreateCantileverWebOuterCurve(distanceAlong, false);                    
                    s.SweptArea = IfcModelBuilder.MakeArbProfileWithVoids(_model, outerCurve, new List<IfcCurve>() { });
                    s.ExtrudedDirection = IfcModelBuilder.MakeDirection(_model, 0, 0, 1);
                    s.Depth = DiaphragmTypeTable[typeId];
                });
                IfcModelBuilder.SetSurfaceStyle(_model, solid2, 1, 0.9333, 0, 0.15);

                var shape = IfcModelBuilder.MakeShapeRepresentation(_model, 3, "Body", "CSG");
                shape.Items.Add(solid);
                shape.Items.Add(solid2);
                p.Representation = _model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
            });

            // Aggregate plates and stiffeners
            var relAggregates = _model.Instances.New<IfcRelAggregates>(r =>
            {
                r.RelatingObject = diaphragm;
                r.RelatedObjects.Add(plate);
                r.RelatedObjects.Add(plate2);
            });
            return diaphragm;
        }

        private XbimPoint3D GetRefPoint(int plateCode)
        {
            if (plateCode == 0) // top flange, return (B1+B2/2, 0, 0)
                return new XbimPoint3D(SectionDimensions[0] + SectionDimensions[1] / 2, 0, 0);
            else if (plateCode == 1) // left web, return (B2/2, 0, 0)
                return new XbimPoint3D(SectionDimensions[1] / 2, 0, 0);
            else if (plateCode == 2) // right web, return (-B2/2, 0, 0)
                return new XbimPoint3D(-SectionDimensions[1] / 2, 0, 0);
            else // bottom flange, return (B5/2, -H, 0)
                return new XbimPoint3D(SectionDimensions[3] / 2, -SectionDimensions[4], 0);
        }

        private XbimVector3D GetOffsetDirection(int plateCode)
        {
            if (plateCode == 1)
                return new XbimVector3D(0, 0, 1).CrossProduct(GetStiffenerDirection(plateCode)).Normalized();
            else if (plateCode == 2)
                return GetStiffenerDirection(plateCode).CrossProduct(new XbimVector3D(0, 0, 1)).Normalized();
            else
                return new XbimVector3D(-1, 0, 0);
        }

        /// <summary>
        /// Compute the direction of stiffener based on which plate the stiffener belongs to
        /// </summary>
        /// <param name="plateCode"></param>
        /// <returns></returns>
        private XbimVector3D GetStiffenerDirection(int plateCode)
        {
            if (plateCode == 0)
                return new XbimVector3D(0, -1, 0);
            else if (plateCode == 1) // left web, return (-2H, B2-B5, 0).Normalized()
                return new XbimVector3D(-2 * SectionDimensions[4], SectionDimensions[1] - SectionDimensions[3], 0).Normalized();
            else if (plateCode == 2) // right web, return (2H, B2-B5, 0).Normalized()
                return new XbimVector3D(2 * SectionDimensions[4], SectionDimensions[1] - SectionDimensions[3], 0).Normalized();
            else
                return new XbimVector3D(0, 1, 0);
        }        

        // assign defalt material to girder
        private void CreateMaterialForGirder(IfcElementAssembly girder)
        {
            // Create material and associates it with the girder
            var material = _model.Instances.New<IfcMaterial>(mat =>
            {
                mat.Name = "Q345";
                mat.Category = "Steel";
            });
            _model.Instances.New<IfcRelAssociatesMaterial>(ram =>
            {
                ram.RelatingMaterial = material;
                ram.RelatedObjects.Add(girder);
            });
            // Create P_set to hold material properties
            var pset_MaterialCommon = _model.Instances.New<IfcMaterialProperties>(mp =>
            {
                mp.Name = "Pset_MaterialCommon";
                mp.Material = material;
                var massDensity = _model.Instances.New<IfcPropertySingleValue>(p =>
                {
                    p.Name = "MassDensity";
                    p.NominalValue = new IfcMassDensityMeasure(7.85e-9);
                });
                mp.Properties.Add(massDensity);
            });
            var pset_MaterialMechanical = _model.Instances.New<IfcMaterialProperties>(mp =>
            {
                mp.Name = "Pset_MaterialMechanical";
                mp.Material = material;
                // Add YoungModulus, PoissonRatio, ThermalExpansionCoefficient
                // TODO
                var youngModulus = _model.Instances.New<IfcPropertySingleValue>(p =>
                {
                    p.Name = "YoungModulus";
                    p.NominalValue = new IfcModulusOfElasticityMeasure(2.06e5);
                });
                var poissonRatio = _model.Instances.New<IfcPropertySingleValue>(p =>
                {
                    p.Name = "PoissonRatio";
                    p.NominalValue = new IfcPositiveRatioMeasure(0.3);
                });
                var thermalExpansionCoefficient = _model.Instances.New<IfcPropertySingleValue>(p =>
                {
                    p.Name = "ThermalExpansionCoefficient";
                    p.NominalValue = new IfcThermalExpansionCoefficientMeasure(1.2e-5);
                });
                mp.Properties.AddRange(new List<IfcPropertySingleValue>() { youngModulus, poissonRatio, thermalExpansionCoefficient });
            });
        }        

        private IfcCenterLineProfileDef CreateStiffenerProfile(List<double> stiffDimensions, XbimVector3D vec)
        {
            switch (stiffDimensions.Count)
            {
                case 2: return CreateFlatStiffenerProfile(stiffDimensions, vec); 
                case 4: return CreateTShapeStiffenerProfile(stiffDimensions, vec); 
                case 5: return CreateUShapeStiffenerProfile(stiffDimensions, vec);
                default: throw new NotImplementedException("Other stiffener types not supported for now.");
            }
        }

        private IfcCenterLineProfileDef CreateFlatStiffenerProfile(List<double> stiffDimensions, XbimVector3D vec)
        {
            vec = vec.Normalized();
            var p1 = IfcModelBuilder.MakeCartesianPoint(_model, 0, 0);
            var p2 = IfcModelBuilder.MakeCartesianPoint(_model, stiffDimensions[0] * vec.X, stiffDimensions[0] * vec.Y);
            var line = IfcModelBuilder.MakePolyline(_model, new List<IfcCartesianPoint>() { p1, p2 });
            return IfcModelBuilder.MakeCenterLineProfile(_model, line, stiffDimensions[1]);            
        }

        private IfcCenterLineProfileDef CreateTShapeStiffenerProfile(List<double> stiffDimensions, XbimVector3D vec)
        {
            throw new NotImplementedException("T-shape stiffener not supported for now.");
        }

        private IfcCenterLineProfileDef CreateUShapeStiffenerProfile(List<double> stiffDimensions, XbimVector3D vec)
        {
            double H = stiffDimensions[0], B1 = stiffDimensions[1], B2 = stiffDimensions[2], 
                t = stiffDimensions[3], R = stiffDimensions[4];

            var compositeCurve = _model.Instances.New<IfcCompositeCurve>(cc =>
            {
                // Preparation
                var p1 = new XbimPoint3D(B1 / 2, 0, 0);
                var p2 = new XbimPoint3D(B2 / 2, -H + t, 0);
                var p3 = new XbimPoint3D(-B2 / 2, -H + t, 0);
                var p4 = new XbimPoint3D(-B1 / 2, 0, 0);
                var p2p1 = (p1 - p2).Normalized();
                var p2p3 = (p3 - p2).Normalized();
                var p2p01 = (p2p1 + p2p3).Normalized();
                var theta = p2p1.Angle(p2p3) / 2;
                var p01 = p2 + p2p01 * (R / Math.Sin(theta));
                var p02 = new XbimPoint3D(-p01.X, p01.Y, p01.Z);
                var p21 = p2 + p2p1 * (R / Math.Tan(theta));
                var p22 = p2 + p2p3 * (R / Math.Tan(theta));
                var p31 = new XbimPoint3D(-p21.X, p21.Y, p21.Z);
                var p32 = new XbimPoint3D(-p22.X, p22.Y, p22.Z);

                // Draw U-shape stiffener centerline
                var line = IfcModelBuilder.MakePolyline(_model, p1, p21);
                var seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, line);
                cc.Segments.Add(seg);

                var center = IfcModelBuilder.MakeAxis2Placement2D(_model, p01);
                var circle = IfcModelBuilder.MakeCircle(_model, center, R);
                var arc = IfcModelBuilder.MakeTrimmedCurve(_model, circle, p21, p22);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, arc);
                cc.Segments.Add(seg);

                line = IfcModelBuilder.MakePolyline(_model, p22, p32);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, line);
                cc.Segments.Add(seg);

                center = IfcModelBuilder.MakeAxis2Placement2D(_model, p02);
                circle = IfcModelBuilder.MakeCircle(_model, center, R);
                arc = IfcModelBuilder.MakeTrimmedCurve(_model, circle, p32, p31);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, arc);
                cc.Segments.Add(seg);

                line = IfcModelBuilder.MakePolyline(_model, p31, p4);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, line, IfcTransitionCode.DISCONTINUOUS);
                cc.Segments.Add(seg);
            });
            return IfcModelBuilder.MakeCenterLineProfile(_model, compositeCurve, t);
        }   

        private IfcCompositeCurve CreateDiaphragmInnerCurve()
        {
            var compCurve = _model.Instances.New<IfcCompositeCurve>(cc =>
            {
                var center = IfcModelBuilder.MakeCartesianPoint(_model, 0, -950, 0);
                var pos = IfcModelBuilder.MakeAxis2Placement2D(_model, center);
                var circle = IfcModelBuilder.MakeCircle(_model, pos, 300);
                var halfCircle = IfcModelBuilder.MakeTrimmedCurve(_model, circle, Math.PI, 0);
                var seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, halfCircle);
                cc.Segments.Add(seg);
                var pts = new List<IfcCartesianPoint>()
                {
                    IfcModelBuilder.MakeCartesianPoint(_model, 300, -950),
                    IfcModelBuilder.MakeCartesianPoint(_model, 300, -1200)
                };
                var poly = IfcModelBuilder.MakePolyline(_model, pts);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                cc.Segments.Add(seg);

                pts = new List<IfcCartesianPoint>()
                {
                    IfcModelBuilder.MakeCartesianPoint(_model, -300, -950),
                    IfcModelBuilder.MakeCartesianPoint(_model, -300, -1200)
                };
                poly = IfcModelBuilder.MakePolyline(_model, pts);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                cc.Segments.Add(seg);

                center = IfcModelBuilder.MakeCartesianPoint(_model, 0, -1200, 0);
                pos = IfcModelBuilder.MakeAxis2Placement2D(_model, center);
                circle = IfcModelBuilder.MakeCircle(_model, pos, 300);
                halfCircle = IfcModelBuilder.MakeTrimmedCurve(_model, circle, 0, Math.PI);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, halfCircle);
                cc.Segments.Add(seg);
            });            

            return compCurve;
        }

        private IfcCompositeCurve CreateDiaphragmOuterCurve(double distanceAlong)
        {
            double B1 = SectionDimensions[0], B2 = SectionDimensions[1], B3 = SectionDimensions[2],
                B4 = SectionDimensions[3], H = SectionDimensions[4];
            return _model.Instances.New<IfcCompositeCurve>(cc =>
            {
                var pts = new List<IfcCartesianPoint>()
                {
                    IfcModelBuilder.MakeCartesianPoint(_model, -B4 / 2, -H),
                    IfcModelBuilder.MakeCartesianPoint(_model, -B2 / 2, 0)
                };
                var poly = IfcModelBuilder.MakePolyline(_model, pts);
                var seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                cc.Segments.Add(seg);
                                
                var list = StiffenerLists[0].Where((List<(double distanceAlong, int typeId, int layoutId)> l) =>
                    StiffenerTypeTable[l[0].typeId].Count == 5)
                    .FirstOrDefault(); // Top flange U-shape stiffener list
                var pt = new XbimPoint3D(-B2 / 2, 0, 0);
                var dir = new XbimVector3D(1, 0, 0);
                var vz = new XbimVector3D(0, 0, 1);

                if (list.Any())
                {
                    bool isFirst = true;
                    foreach (var (dist, typeId, layoutId) in list)
                    {
                        //const int R1 = 27, R2 = 35;
                        var dimensions = StiffenerTypeTable[typeId]; // {H, B1, B2, t, R}
                        double h = dimensions[0], b1 = dimensions[1], b2 = dimensions[2], t = dimensions[3], r = dimensions[4];
                        double right = 0;
                        foreach (var (num, gap) in StiffenerLayoutTable[layoutId])
                        {
                            for (int i = 0; i < num; ++i)
                            {
                                right += gap;
                                if (right <= B1)
                                    continue;
                                if (right >= (B1 + B2))
                                    continue;

                                pts = new List<IfcCartesianPoint>()
                                {
                                    IfcModelBuilder.MakeCartesianPoint(_model, pt)
                                };
                                if (isFirst)
                                {
                                    pt = pt + dir * (right - B1 - b1 / 2);
                                    isFirst = false;
                                }
                                else
                                {
                                    pt = pt + dir * (gap - b1);
                                }
                                pts.Add(IfcModelBuilder.MakeCartesianPoint(_model, pt));
                                poly = IfcModelBuilder.MakePolyline(_model, pts);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                                cc.Segments.Add(seg);
                                AddUShapeHole(ref cc, ref pt, h, b1, b2);                                
                            }
                        }
                        break;
                    }
                    pts = new List<IfcCartesianPoint>()
                    {
                        IfcModelBuilder.MakeCartesianPoint(_model, pt),
                        IfcModelBuilder.MakeCartesianPoint(_model, B2 / 2, 0)
                    };
                    poly = IfcModelBuilder.MakePolyline(_model, pts);
                    seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                }
                else
                {
                    pts = new List<IfcCartesianPoint>()
                    {
                        IfcModelBuilder.MakeCartesianPoint(_model, -B2 / 2, 0),
                        IfcModelBuilder.MakeCartesianPoint(_model, B2 / 2, 0)
                    };
                    poly = IfcModelBuilder.MakePolyline(_model, pts);
                    seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                }
                cc.Segments.Add(seg);

                pts = new List<IfcCartesianPoint>()
                {
                    IfcModelBuilder.MakeCartesianPoint(_model, B2 / 2, 0),
                    IfcModelBuilder.MakeCartesianPoint(_model, B4 / 2, -H)
                };
                poly = IfcModelBuilder.MakePolyline(_model, pts);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                cc.Segments.Add(seg);

                pt = new XbimPoint3D(B4 / 2, -H, 0);
                dir = new XbimVector3D(-1, 0, 0);
                list = StiffenerLists[3].FirstOrDefault(); // bottom flange stiffener list
                foreach (var (dist, typeId, layoutId) in list)
                {
                    if (distanceAlong > dist)
                        continue;
                    else
                    {
                        const int R = 35; // welded hole radius
                        var dimensions = StiffenerTypeTable[typeId]; // {H, B}
                        double h = dimensions[0], b = dimensions[1];
                        bool isFirst = true;
                        foreach (var (num, gap) in StiffenerLayoutTable[layoutId])
                        {                            
                            for (int i = 0; i < num; ++i)
                            {                                                                
                                pts = new List<IfcCartesianPoint>
                                {
                                    IfcModelBuilder.MakeCartesianPoint(_model, pt)
                                };
                                if (isFirst)
                                {
                                    pt = pt + dir * (gap - b / 2 - R);
                                    isFirst = false;
                                }                                        
                                else
                                    pt = pt + dir * (gap - 2 * R);
                                pts.Add(IfcModelBuilder.MakeCartesianPoint(_model, pt));
                                poly = IfcModelBuilder.MakePolyline(_model, pts);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                                cc.Segments.Add(seg);
                                
                                pt = pt + dir * R;
                                var pos = IfcModelBuilder.MakeAxis2Placement2D(_model, pt);
                                var circle = IfcModelBuilder.MakeCircle(_model, pos, R);
                                var quater = IfcModelBuilder.MakeTrimmedCurve(_model, circle, Math.PI / 2, 0);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, quater);
                                cc.Segments.Add(seg);
                                                               
                                dir = new XbimVector3D(0, 1, 0);
                                pt = pt + dir * R;
                                pts = new List<IfcCartesianPoint>
                                {
                                    IfcModelBuilder.MakeCartesianPoint(_model, pt)
                                };
                                pt = pt + dir * (h - 2 * R);
                                pts.Add(IfcModelBuilder.MakeCartesianPoint(_model, pt));
                                poly = IfcModelBuilder.MakePolyline(_model, pts);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                                cc.Segments.Add(seg);
                                
                                pt = pt + dir * R;
                                pos = IfcModelBuilder.MakeAxis2Placement2D(_model, pt);
                                circle = IfcModelBuilder.MakeCircle(_model, pos, R);
                                quater = IfcModelBuilder.MakeTrimmedCurve(_model, circle, Math.PI, Math.PI * 1.5);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, quater);
                                cc.Segments.Add(seg);
                                
                                dir = new XbimVector3D(-1, 0, 0);
                                pt = pt + dir * R;
                                pts = new List<IfcCartesianPoint>
                                {
                                    IfcModelBuilder.MakeCartesianPoint(_model, pt)
                                };
                                dir = new XbimVector3D(0, -1, 0);
                                pt = pt + dir * h;
                                pts.Add(IfcModelBuilder.MakeCartesianPoint(_model, pt));
                                poly = IfcModelBuilder.MakePolyline(_model, pts);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                                cc.Segments.Add(seg);
                                dir = new XbimVector3D(-1, 0, 0);
                            }
                        }
                        break;
                    }
                }
                pts = new List<IfcCartesianPoint>()
                {
                    IfcModelBuilder.MakeCartesianPoint(_model, pt),
                    IfcModelBuilder.MakeCartesianPoint(_model, -2775, -1968)
                };
                poly = IfcModelBuilder.MakePolyline(_model, pts);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
                cc.Segments.Add(seg);
            });
        }

        private IfcCompositeCurve CreateCantileverWebOuterCurve(double distanceAlong, bool isLeft)
        {
            var cc = this._model.Instances.New<IfcCompositeCurve>();
            // Preparation
            double B1 = SectionDimensions[0], B2 = SectionDimensions[1], B3 = SectionDimensions[2],
                B4 = SectionDimensions[3], H = SectionDimensions[4];            
            var p1 = new XbimPoint3D((isLeft ? 1 : -1) * (B1 + B2 / 2 - 16), 0, 0);
            var p2 = new XbimPoint3D((isLeft ? 1 : -1) * (B2 / 2 + 14), 0, 0);
            var p2p3 = new XbimVector3D((isLeft ? 1 : -1) * (B4 - B2) / 2, -H, 0).Normalized();
            var p3 = p2 + p2p3 * 610;
            var p4 = new XbimPoint3D((isLeft ? 1 : -1) * (B1 + B2 / 2 - 16), -360, 0);
            var p2p1 = new XbimVector3D((isLeft ? 1 : -1), 0, 0);
            var p51 = p2 + p2p1 * 186;
            var p52 = p51 + p2p1 * 300;
            var p61 = p52 + p2p1 * 230;
            var p62 = p61 + p2p1 * 300;
            var p71 = p62 + p2p1 * 177;
            var p72 = p71 + p2p1 * 86;
            var p81 = p72 + p2p1 * (250 - 86);
            var p82 = p81 + p2p1 * 86;

            // Draw shape
            var poly = IfcModelBuilder.MakePolyline(_model, new List<XbimPoint3D>() { p82, p1, p4, p3, p2, p51 });
            var seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
            cc.Segments.Add(seg);
            poly = IfcModelBuilder.MakePolyline(_model, p52, p61);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
            cc.Segments.Add(seg);
            poly = IfcModelBuilder.MakePolyline(_model, p62, p71);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
            cc.Segments.Add(seg);
            poly = IfcModelBuilder.MakePolyline(_model, p72, p81);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, poly);
            cc.Segments.Add(seg);
            if (isLeft)
            {
                AddFlatStiffenerHole(ref cc, p71);
                AddFlatStiffenerHole(ref cc, p81);
                AddUShapeHole(ref cc, ref p51, 280, 300, 170);
                AddUShapeHole(ref cc, ref p61, 280, 300, 170);
            }
            else
            {
                AddFlatStiffenerHole(ref cc, p72);
                AddFlatStiffenerHole(ref cc, p82);
                AddUShapeHole(ref cc, ref p52, 280, 300, 170);
                AddUShapeHole(ref cc, ref p62, 280, 300, 170);
            }

            return cc;
        }

        // Add Flat stiffener hole
        private void AddFlatStiffenerHole(ref IfcCompositeCurve usingCurve, XbimPoint3D start)
        {
            // Preparation
            const double R = 35, H = 190, t = 16;
            var p1 = start;
            var p1p8 = new XbimVector3D(1, 0, 0);
            var p8 = p1 + p1p8 * R;
            var p8p2 = new XbimVector3D(0, -1, 0);
            var p2 = p8 + p8p2 * R;
            var p3 = p8 + p8p2 * (H - Math.Sqrt(R * R - t * t / 4));
            var p4 = p1 + p1p8 * (R + t / 2) + p8p2 * H;
            var p5 = p3 + p1p8 * t;
            var p6 = p2 + p1p8 * t;
            var p9 = p8 + p1p8 * t;
            var p7 = p1 + p1p8 * (t + 2 * R);

            // Draw
            var center = IfcModelBuilder.MakeAxis2Placement2D(_model, p8);
            var circle = IfcModelBuilder.MakeCircle(_model, center, R);
            var arc = IfcModelBuilder.MakeTrimmedCurve(_model, circle, p2, p1);
            var seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, arc);
            usingCurve.Segments.Add(seg);

            var line = IfcModelBuilder.MakePolyline(_model, p2, p3);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, line);
            usingCurve.Segments.Add(seg);

            center = IfcModelBuilder.MakeAxis2Placement2D(_model, p4);
            circle = IfcModelBuilder.MakeCircle(_model, center, R);
            arc = IfcModelBuilder.MakeTrimmedCurve(_model, circle, p5, p3);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, arc);
            usingCurve.Segments.Add(seg);

            line = IfcModelBuilder.MakePolyline(_model, p5, p6);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, line);
            usingCurve.Segments.Add(seg);

            center = IfcModelBuilder.MakeAxis2Placement2D(_model, p9);
            circle = IfcModelBuilder.MakeCircle(_model, center, R);
            arc = IfcModelBuilder.MakeTrimmedCurve(_model, circle, p7, p6);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(_model, arc);
            usingCurve.Segments.Add(seg);
        }

        // Add U-shape hole which is composed by a groupt of composite curve segments
        private void AddUShapeHole(ref IfcCompositeCurve usingCurve, ref XbimPoint3D ptOut, double h, double b1, double b2)
        {
            // Preparation
            var p1 = ptOut;
            var dir = new XbimVector3D(1, 0, 0);
            var vz = new XbimVector3D(0, 0, 1);
            var mid = p1 + dir * (b1 / 2);
            const int R1 = 27, R2 = 35;
            var p1p2 = new XbimVector3D((b1 - b2) / 2, -h, 0).Normalized();
            double theta1 = dir.Angle(p1p2);
            var p2 = p1 + p1p2 * ((h - 100) / Math.Sin(theta1));
            var p2p3 = p1p2.CrossProduct(vz);
            var p3 = p2 + p2p3 * 6;
            var p4 = p3 + p2p3 * R1 * 2;
            var p34 = p3 + p2p3 * R1;
            var p5 = p4 + p1p2 * ((130 - ((p3 - p2).Length + (p4 - p3).Length) * Math.Cos(theta1)) / Math.Sin(theta1));
            var p2p1 = p1p2.Negated();
            double theta2 = dir.Angle(p2p1) / 2;
            var p5p01 = (dir + p2p1).Normalized();
            var p01 = p5 + p5p01 * (R2 / Math.Sin(theta2));
            var p51 = p5 + p2p1 * (R2 / Math.Tan(theta2));
            var p52 = p5 + dir * (R2 / Math.Tan(theta2));
            var p62 = new XbimPoint3D(2 * mid.X - p52.X, p52.Y, p52.Z);
            var p6 = new XbimPoint3D(2 * mid.X - p5.X, p5.Y, p5.Z);
            var p61 = new XbimPoint3D(2 * mid.X - p51.X, p51.Y, p51.Z);
            var p02 = new XbimPoint3D(2 * mid.X - p01.X, p01.Y, p01.Z);
            var p7 = new XbimPoint3D(2 * mid.X - p4.X, p4.Y, p4.Z);
            var p78 = new XbimPoint3D(2 * mid.X - p34.X, p34.Y, p34.Z);
            var p8 = new XbimPoint3D(2 * mid.X - p3.X, p3.Y, p3.Z);
            var p9 = new XbimPoint3D(2 * mid.X - p2.X, p2.Y, p2.Z);
            var p10 = p1 + dir * b1;

            // Draw U-shape hole
            var poly = IfcModelBuilder.MakePolyline(_model, new List<XbimPoint3D>() { p1, p2, p3 });
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(_model, poly));

            var center = IfcModelBuilder.MakeAxis2Placement2D(_model, p34);
            var circle = IfcModelBuilder.MakeCircle(_model, center, R1);
            var arc = IfcModelBuilder.MakeTrimmedCurve(_model, circle, p4, p3);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(_model, arc));

            poly = IfcModelBuilder.MakePolyline(_model, p4, p51);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(_model, poly));

            center = IfcModelBuilder.MakeAxis2Placement2D(_model, p01);
            circle = IfcModelBuilder.MakeCircle(_model, center, R2);
            arc = IfcModelBuilder.MakeTrimmedCurve(_model, circle, p52, p51);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(_model, arc));

            poly = IfcModelBuilder.MakePolyline(_model, p52, p62);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(_model, poly));

            center = IfcModelBuilder.MakeAxis2Placement2D(_model, p02);
            circle = IfcModelBuilder.MakeCircle(_model, center, R2);
            arc = IfcModelBuilder.MakeTrimmedCurve(_model, circle, p61, p62);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(_model, arc));

            poly = IfcModelBuilder.MakePolyline(_model, p61, p7);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(_model, poly));

            center = IfcModelBuilder.MakeAxis2Placement2D(_model, p78);
            circle = IfcModelBuilder.MakeCircle(_model, center, R1);
            arc = IfcModelBuilder.MakeTrimmedCurve(_model, circle, p8, p7);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(_model, arc));

            poly = IfcModelBuilder.MakePolyline(_model, new List<XbimPoint3D>() { p8, p9, p10 });
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(_model, poly));

            // Set ptOut
            ptOut = p10;
        }

        public void Dispose()
        {
            _model.Dispose();            
        }
    }
}
