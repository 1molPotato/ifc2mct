using System;
using System.Collections.Generic;
using System.Linq;
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
    public class BridgeBuilder
    {
        private readonly string _outputPath = "";
        private readonly string _projectName = "xx大道立交SW1-SW5匝道";
        private readonly string _bridgeName = "SW3-SW5钢结构连续箱梁";

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
        private List<List<(double distanceAlong, double thickness)>> PlateThicknessLists { get; set; }

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

        public BridgeBuilder() : this("test.ifc")
        {
            // empty
        }

        public BridgeBuilder(string path)
        {
            _outputPath = path;
            StiffenerLists = new Dictionary<int, List<List<(double distanceAlong, int typeId, int layoutId)>>>();
            StiffenerTypeTable = new Dictionary<int, List<double>>();
            StiffenerLayoutTable = new Dictionary<int, List<(int num, double gap)>>();
            BearingTypeTable = new Dictionary<int, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)>();
            BearingList = new List<(double distanceAlong, double offsetLateral, int bearingTypeId)>();
            DiaphragmList = new List<(int typeId, int num, double gap)>();
            DiaphragmTypeTable = new Dictionary<int, double>();
        }

        // Interfaces        
        public void SetBridgeAlignment(double start, double end, double verOffset, double latOffset)
        {
            BridgeStart = start;
            BridgeEnd = end;
            BridgeAlignmentVerOff = verOffset;
            BridgeAlignmentLatOff = latOffset;
        }

        public void SetGaps(double start, double end)
        {
            StartGap = start;
            EndGap = end;
        }

        public void SetOverallSection(List<double> dimensions)
        {
            if (dimensions.Count == 5)
                SectionDimensions = dimensions;
            else
                throw new ArgumentException("The overall section dimensions should be {B1, B2, B4, B5, H}");
        }

        public void SetPlateThicknesses(List<List<(double, double)>> thicknessLists)
        {
            if (thicknessLists.Count == 3)
            {
                PlateThicknessLists = new List<List<(double, double)>>
                {
                    thicknessLists[0],
                    thicknessLists[1],
                    thicknessLists[1],
                    thicknessLists[2]
                };
            }
            else
                throw new ArgumentException("You have to provide thickness tables for top flange, web and bottom flange");
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

        public void Run()
        {
            using (var model = CreateAndInitModel("SteelBoxBridge"))
            {
                if (model != null)
                {
                    InitWCS(model);
                    string info = "";
                    if (BuildBridge(model, ref info))
                    {
                        try
                        {
                            Console.WriteLine(info);
                            model.SaveAs(_outputPath, StorageType.Ifc);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Failed to save {0}", _outputPath);
                            Console.WriteLine(e.Message);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to build bridge model because {0}", info);
                    }
                }
                else
                {
                    Console.WriteLine("Failed to initialise the model");
                }
            }
        }

        public void Run2()
        {
            using (var model = CreateAndInitModel(_projectName))
            {
                if (model != null)
                {
                    InitWCS(model);
                    string info = "";
                    if (BuildBridge2(model, ref info))
                    {
                        try
                        {
                            Console.WriteLine(info);
                            model.SaveAs(_outputPath, StorageType.Ifc);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Failed to save {0}", _outputPath);
                            Console.WriteLine(e.Message);
                        }
                    }
                    else
                        Console.WriteLine("Failed to build bridge model because {0}", info);                    
                }
                else
                    Console.WriteLine("Failed to initialise the model");
            }
        }

        public void Run3()
        {
            using (var model = CreateAndInitModel(_projectName))
            {
                if (model != null)
                {
                    InitWCS(model);
                    if (BuildSectionedSolid(model))
                    {
                        try
                        {
                            model.SaveAs(_outputPath, StorageType.Ifc);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Failed to save {0}", _outputPath);
                            Console.WriteLine(e.Message);
                        }
                    }
                    else
                        Console.WriteLine("Failed to build bridge model");
                }
                else
                    Console.WriteLine("Failed to initialise the model");
            }
        }

        // Utilities

        /// <summary>
        /// Sets up the basic parameters any model must provide, units, ownership etc
        /// </summary>
        /// <param name="projectName"></param>
        /// <returns></returns>
        private IfcStore CreateAndInitModel(string projectName)
        {
            // First we need to set up some credentials for ownership of data in the new model
            var credentials = new XbimEditorCredentials
            {
                ApplicationDevelopersName = "lky",
                ApplicationFullName = "Steel Box Girder Builder",
                ApplicationIdentifier = "",
                ApplicationVersion = "1.0",
                EditorsFamilyName = "lyu",
                EditorsGivenName = "kaiyuan",
                EditorsOrganisationName = "TJU"
            };

            // Now we can create an IfcStore, it is in IFC4x1 format and will be held in memory
            var model = IfcStore.Create(credentials, XbimSchemaVersion.Ifc4x1, XbimStoreType.InMemoryModel);

            // Begin a transaction as all changes to a model are ACID
            using (var txn = model.BeginTransaction("Initialise Model"))
            {
                // Create a project
                var project = model.Instances.New<IfcProject>(p =>
                {
                    // Set the units to SI (mm and metres)
                    p.Initialize(ProjectUnits.SIUnitsUK);
                    p.Name = projectName;
                });
                // Now commit the changes, else they will be rolled back 
                // at the end of the scope of the using statement
                txn.Commit();
            }
            return model;
        }

        private void InitWCS(IfcStore m)
        {
            using (var txn = m.BeginTransaction("Initialise WCS"))
            {
                var context3D = m.Instances.OfType<IfcGeometricRepresentationContext>()
                .Where(c => c.CoordinateSpaceDimension == 3)
                .FirstOrDefault();
                if (context3D.WorldCoordinateSystem is IfcAxis2Placement3D wcs)
                {
                    WCS = wcs;
                    Origin3D = wcs.Location;
                    AxisZ3D = IfcModelBuilder.MakeDirection(m, 0, 0, 1);
                    wcs.Axis = AxisZ3D;
                    AxisX3D = IfcModelBuilder.MakeDirection(m, 1, 0, 0);
                    wcs.RefDirection = AxisX3D;
                    AxisY3D = IfcModelBuilder.MakeDirection(m, 0, 1, 0);
                }

                var context2D = m.Instances.OfType<IfcGeometricRepresentationContext>()
                    .Where(c => c.CoordinateSpaceDimension == 2)
                    .FirstOrDefault();
                if (context2D.WorldCoordinateSystem is IfcAxis2Placement2D wcs2d)
                {
                    WCS2D = wcs2d;
                    Origin2D = wcs2d.Location;
                    AxisX2D = IfcModelBuilder.MakeDirection(m, 1, 0);
                    wcs2d.RefDirection = AxisX2D;
                    AxisY2D = IfcModelBuilder.MakeDirection(m, 0, 1);
                }

                txn.Commit();
            }
        }

        private bool BuildBridge(IfcStore m, ref string info)
        {
            var site = CreateSite(m, "Site #1");
            if (site == null)
            {
                info = "failed to create site"; return false;              
            }

            // Create an instance of IfcAlignment and add it to the instance of spatial structure element IfcSite
            var alignment = CreateAlignment(m, "Center Alignment");
            if (alignment == null)
            {
                info = "failed to create alignment"; return false;                
            }            
            AddElementToSpatial(m, site, alignment, "Add an alignment instance to site");

            // Build a steel-box-girder as superstructure based on the alignment curve created before,
            // using instances of IfcElementAssembly to group all sub-elements
            const double START = 10000;
            const double LENGTH = 36000;
            var DIMENSIONS = new List<double>() { 1750, 6400, 50, 6000, 2000, 16, 16, 14 };
            var girder = CreateSteelBoxGirder(m, "SW3-SW4", alignment.Axis, START, LENGTH, DIMENSIONS);  
            if (girder == null)
            {
                info = "failed to create girder"; return false;
            }
            AddElementToSpatial(m, site, girder, "Add a girder to site");

            info = "Successfully created ifc model";
            return true;
        }

        private bool BuildBridge2(IfcStore m, ref string info)
        {
            // Create spatial element to hold the created alignment
            var site = CreateSite(m, "site #1");
            //var alignment = CreateAlignment(m, "Road Alignment");
            var alignment = CreateAlignment(m, "SW匝道道路设计中心线");
            if (alignment == null)
            {
                info = "failed to create alignment";
                return false;
            }
            AddElementToSpatial(m, site, alignment, "Add the alignment to site");
            BridgeAlignmentCurve = AddBridgeAlignment(m, alignment);
            // Build the superstructure of a steel-box-girder bridge
            var girder = CreateSteelBoxGirder2(m, BridgeAlignmentCurve);
            AddElementToSpatial(m, site, girder, "Add the girder to site");

            // Build the bearings
            var bearings = CreateBearings(m);
            foreach (var bearing in bearings)
                AddElementToSpatial(m, site, bearing, "Add the bearing to site");

            info = "successfully created bridge model";
            return true;
        }

        private bool BuildSectionedSolid(IfcStore m)
        {
            // Create spatial element to hold the created alignment
            var site = CreateSite(m, "site #1");
            //var alignment = CreateAlignment(m, "Road Alignment");
            var alignment = CreateAlignment(m, "Road alignment");
            if (alignment == null)
                return false;
            AddElementToSpatial(m, site, alignment, "Add the alignment to site");

            BridgeStart = 20000;
            BridgeEnd = 50000;
            BridgeAlignmentCurve = AddBridgeAlignment(m, alignment);
            IfcPlate plate = null;
            using (var txn = m.BeginTransaction())
            {
                var solid = m.Instances.New<IfcSectionedSolidHorizontal>(s =>
                {
                    s.Directrix = BridgeAlignmentCurve;
                    //var profile = CreateStiffenerProfile(m, new List<double>() { 280, 300, 170, 8, 50 }, new XbimVector3D(0, -1, 0));
                    var profile = CreateStiffenerProfile(m, new List<double>() { 500, 16}, new XbimVector3D(1, 0, 0));
                    s.CrossSections.AddRange(new List<IfcProfileDef>() { profile, profile });
                    var pos1 = IfcModelBuilder.MakeDistanceExpression(m, 40, -200, 1000);
                    var pos2 = IfcModelBuilder.MakeDistanceExpression(m, BridgeEnd - BridgeStart - 40, -200, 1000);
                    s.CrossSectionPositions.AddRange(new List<IfcDistanceExpression>() { pos1, pos2 });
                });
                var solid2 = m.Instances.New<IfcSectionedSolidHorizontal>(s =>
                {
                    s.Directrix = BridgeAlignmentCurve;
                    var profile = CreateStiffenerProfile(m, new List<double>() { 280, 300, 170, 8, 50 }, new XbimVector3D(0, -1, 0));                    
                    s.CrossSections.AddRange(new List<IfcProfileDef>() { profile, profile });
                    var pos1 = IfcModelBuilder.MakeDistanceExpression(m, 40, 0, 1000);
                    var pos2 = IfcModelBuilder.MakeDistanceExpression(m, BridgeEnd - BridgeStart - 40, 0, 1000);
                    s.CrossSectionPositions.AddRange(new List<IfcDistanceExpression>() { pos1, pos2 });
                });
                var shape = m.Instances.New<IfcShapeRepresentation>(s =>
                {
                    s.ContextOfItems = m.Instances.OfType<IfcGeometricRepresentationContext>()
                        .Where(c => c.CoordinateSpaceDimension == 3)
                        .FirstOrDefault();
                    s.RepresentationIdentifier = "Body";
                    s.RepresentationType = "AdvancedSweptSolid";
                    s.Items.Add(solid);
                    s.Items.Add(solid2);
                });
                plate = m.Instances.New<IfcPlate>(p =>
                {
                    p.ObjectPlacement = m.Instances.New<IfcLinearPlacement>(lp =>
                    {
                        lp.PlacementRelTo = BridgeAlignmentCurve;
                        lp.Distance = m.Instances.New<IfcDistanceExpression>(d =>
                        {
                            d.DistanceAlong = BridgeStart;
                        });
                    });
                    p.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                    //p.PredefinedType = (part == 0 || part == 1) ? IfcPlateTypeEnum.FLANGE_PLATE : IfcPlateTypeEnum.WEB_PLATE; // IFC4x2 feature
                });
                txn.Commit();
            }                
            AddElementToSpatial(m, site, plate, "Add plate to site");

            return true;
        }

        /// <summary>
        /// Add a physical product into a spatial structure element in the scope of a transaction
        /// </summary>
        /// <param name="m"></param>
        /// <param name="spatial"></param>
        /// <param name="e"></param>
        /// <param name="info"></param>
        private void AddElementToSpatial(IfcStore m, IfcSpatialStructureElement spatial, IfcProduct e, string info)
        {
            using (var txn = m.BeginTransaction(info))
            {
                spatial.AddElement(e);
                txn.Commit();
            }
        }

        /// <summary>
        /// Create site by given name, set its CompositionType to ELEMENT,
        /// then add it to the already existed project.
        /// </summary>
        /// <param name="m"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private IfcSite CreateSite(IfcStore m, string name)
        {
            using (var txn = m.BeginTransaction("Create Site"))
            {
                var site = m.Instances.New<IfcSite>(s =>
                {
                    s.Name = name;
                    s.CompositionType = IfcElementCompositionEnum.ELEMENT;
                });
                // Get the only one project and add site to it
                var project = m.Instances.OfType<IfcProject>().FirstOrDefault();
                project?.AddSite(site);

                txn.Commit();
                return site;
            }
        }

        private IfcAlignment CreateStraightAlignment(IfcStore m, string name)
        {
            const int LENGTH = 100000; // the segment length of the arc is 100m
            const double DIRECTION = Math.PI / 6; // the start direction of  the arc
            const int DISTANCE = 0; // the vertical segment starts at 10m along horizontal curve
            const int LENGTH2 = 100000; // the length of vertical segement is 80m along horizontal
            const int HEIGHT = 15000; // the vertical segment starts at 15m high
            const double GRADIENT = 0.0; // the gradient of the line vertical segment
            using (var txn = m.BeginTransaction("Create Alignment"))
            {
                // Create an alignment curve holding only one arc segment as horizontal
                // and only one line segment as vertical                
                var alignCurve = m.Instances.New<IfcAlignmentCurve>(ac =>
                {
                    var lineSeg = m.Instances.New<IfcAlignment2DHorizontalSegment>(s =>
                    {
                        s.CurveGeometry = IfcModelBuilder.MakeLineSegment2D(m, Origin2D, DIRECTION, LENGTH);
                        s.TangentialContinuity = true;
                    });
                    ac.Horizontal = m.Instances.New<IfcAlignment2DHorizontal>(h => h.Segments.Add(lineSeg));
                    var segLine = m.Instances.New<IfcAlignment2DVerSegLine>(sl =>
                    {
                        sl.StartDistAlong = DISTANCE;
                        sl.StartHeight = HEIGHT;
                        sl.HorizontalLength = LENGTH2;
                        sl.StartGradient = GRADIENT;
                        sl.TangentialContinuity = true;
                    });
                    ac.Vertical = m.Instances.New<IfcAlignment2DVertical>(v => v.Segments.Add(segLine));
                    ac.Tag = "Road alignment curve";
                });
                var align = m.Instances.New<IfcAlignment>(a =>
                {
                    a.Name = name;
                    a.ObjectPlacement = m.Instances.New<IfcLocalPlacement>(p => p.RelativePlacement = WCS);
                    a.Axis = alignCurve;
                });

                txn.Commit();
                return align;
            }
        }

        /// <summary>
        /// Create an instance of IfcAlignment to hold the alignment curve
        /// with some default geometric parameters
        /// </summary>
        /// <param name="m"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private IfcAlignment CreateAlignment(IfcStore m, string name)
        {
            const int RADIUS = 150000; // the radius of the arc is 150m
            const int LENGTH = 150000; // the segment length of the arc is 150m
            const double DIRECTION = 0/*Math.PI / 6*/; // the start direction of the arc
            const bool ISCCW = true; // the orientation of the arc is clockwise
            const int DISTANCE = 0; // the vertical segment starts at 0m
            const int LENGTH2 = 150000; // the length of vertical segement is 150m along horizontal
            const int HEIGHT = 15000; // the vertical segment starts at 15m high
            const double GRADIENT = 0.02; // the gradient of the line vertical segment
            using (var txn = m.BeginTransaction("Create Alignment"))
            {
                // Create an alignment curve holding only one arc segment as horizontal
                // and only one line segment as vertical                
                var alignCurve = m.Instances.New<IfcAlignmentCurve>(ac =>
                {
                    var arcSeg = m.Instances.New<IfcAlignment2DHorizontalSegment>(s =>
                    {
                        s.CurveGeometry = IfcModelBuilder.MakeCircularArcSegment2D(m, Origin2D, DIRECTION, LENGTH, RADIUS, ISCCW);
                        s.TangentialContinuity = true;
                    });
                    ac.Horizontal = m.Instances.New<IfcAlignment2DHorizontal>(h => h.Segments.Add(arcSeg));
                    var segLine = m.Instances.New<IfcAlignment2DVerSegLine>(sl =>
                    {
                        sl.StartDistAlong = DISTANCE;
                        sl.StartHeight = HEIGHT;
                        sl.HorizontalLength = LENGTH2;
                        sl.StartGradient = GRADIENT;
                        sl.TangentialContinuity = true;
                    });
                    ac.Vertical = m.Instances.New<IfcAlignment2DVertical>(v => v.Segments.Add(segLine));                    
                    ac.Tag = "Center curve of the road";
                });
                var align = m.Instances.New<IfcAlignment>(a =>
                {
                    a.Name = name;
                    a.ObjectPlacement = m.Instances.New<IfcLocalPlacement>(p => p.RelativePlacement = WCS);
                    a.Axis = alignCurve;
                    var offsetCurveSolid = CreateSolidShapeForCurve(m, alignCurve, 0, LENGTH);
                    SetSurfaceStyle(m, offsetCurveSolid, 1, 0, 0);
                    var bodyShape = IfcModelBuilder.MakeShapeRepresentation(m, 3, "Body", "AdvancedSweptSolid");
                    bodyShape.Items.Add(offsetCurveSolid);
                    a.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(bodyShape));
                });

                txn.Commit();
                return align;
            }
        }

        private IfcSectionedSolidHorizontal CreateSolidShapeForCurve(IfcStore m, IfcCurve directrix, double start, double end)
        {
            return m.Instances.New<IfcSectionedSolidHorizontal>(ss =>
            {
                ss.Directrix = directrix;
                var profile = IfcModelBuilder.MakeCircleProfile(m, 40);
                ss.CrossSections.AddRange(new List<IfcProfileDef>() { profile, profile });
                ss.CrossSectionPositions.Add(IfcModelBuilder.MakeDistanceExpression(m, start));
                ss.CrossSectionPositions.Add(IfcModelBuilder.MakeDistanceExpression(m, end));
            });
        }

        /// <summary>
        /// Create an offset alignment curve based on the main alignment curve 
        /// and add it to the main alignment's shape representation
        /// </summary>
        /// <param name="m"></param>
        /// <param name="mainAlignment"></param>
        /// <returns></returns>
        private IfcCurve AddBridgeAlignment(IfcStore m, IfcAlignment mainAlignment)
        {
            using (var txn = m.BeginTransaction("Add offset curve"))
            {
                var offsetCurve = m.Instances.New<IfcOffsetCurveByDistances>(c =>
                {
                    c.BasisCurve = mainAlignment.Axis;
                    c.OffsetValues.Add(IfcModelBuilder.MakeDistanceExpression(m, BridgeStart, BridgeAlignmentLatOff, BridgeAlignmentVerOff));
                    c.OffsetValues.Add(IfcModelBuilder.MakeDistanceExpression(m, BridgeEnd, BridgeAlignmentLatOff, BridgeAlignmentVerOff));
                    c.Tag = "Bridge alignment curve";
                });
                var offsetCurveSolid = CreateSolidShapeForCurve(m, offsetCurve, 0, BridgeEnd - BridgeStart);
                SetSurfaceStyle(m, offsetCurveSolid, 0, 0, 1);
                mainAlignment.Representation.Representations.FirstOrDefault(r => r.RepresentationIdentifier == "Body").Items.Add(offsetCurveSolid);

                var shape = IfcModelBuilder.MakeShapeRepresentation(m, 3, "StringLines", "OffsetCurves");
                shape.Items.Add(offsetCurve);                
                mainAlignment.Representation.Representations.Add(shape);
                
                txn.Commit();
                return offsetCurve;
            }                
        }

        private IfcElementAssembly CreateSteelBoxGirder2(IfcStore m, IfcCurve directrix)
        {
            using (var txn = m.BeginTransaction("Create steel-box-girder"))
            {
                var girder = m.Instances.New<IfcElementAssembly>(g =>
                {
                    g.Name = _bridgeName;
                    g.Description = "Steel-box-girder";
                    g.PredefinedType = IfcElementAssemblyTypeEnum.GIRDER;
                });

                // Set material for this girder
                CreateMaterialForGirder(m, girder);

                // 0 for top flange, 1 for left web, 2 for right web, 3 for bottom flange
                var plateCodes = new List<int>() { 0, 1, 2, 3 };
                var plates = plateCodes.Select(c => CreateSingleBoxPlate2(m, directrix, c)).ToList();
                foreach (var stiffenerLists in StiffenerLists)
                {
                    var plateCode = stiffenerLists.Key;
                    foreach (var stiffenerList in stiffenerLists.Value)
                    {
                        var stiffenerGroup = CreateStiffeners(m, plateCode, stiffenerList);
                        m.Instances.New<IfcRelConnectsElements>(rce =>
                        {
                            rce.RelatedElement = plates[plateCode];
                            rce.RelatingElement = stiffenerGroup;
                        });
                    }
                }
                
                var diaphragms = CreateDiaphragms(m);

                // Aggregate flanges and webs into the girder assembly
                var relAggregates = m.Instances.New<IfcRelAggregates>(r =>
                {
                    r.RelatingObject = girder;
                    r.RelatedObjects.AddRange(plates);
                    foreach (var plate in plates)
                        foreach (var connect in plate.ConnectedFrom)
                            r.RelatedObjects.Add(connect.RelatingElement);
                    foreach (var diaphragm in diaphragms)
                        r.RelatedObjects.Add(diaphragm);
                });

                txn.Commit();
                return girder;
            }
        }

        private IfcPlate CreateSingleBoxPlate2(IfcStore m, IfcCurve directrix, int plateCode)
        {
            double x1 = 0, y1 = 0, x2 = 0, y2 = 0, t = 0, offsetLateral = 0, offsetVertical = 0;
            string name = "";
            var thicknessList = PlateThicknessLists[plateCode];
            var lastDistanceAlong = StartGap;
            var shape = IfcModelBuilder.MakeShapeRepresentation(m, 3, "Body", "AdvancedSweptSolid");

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
                        name = "顶板"; break;                    
                    case 1:
                        x1 = 0; y1 = 0;
                        x2 = (dimensions["B5"] - dimensions["B2"]) / 2; y2 = -dimensions["H"];
                        t = thicknessList[i].thickness; offsetLateral = dimensions["B2"] / 2;
                        name = "腹板"; break;
                    case 2:
                        x1 = 0; y1 = 0;
                        x2 = (dimensions["B2"] - dimensions["B5"]) / 2; y2 = -dimensions["H"];
                        t = thicknessList[i].thickness; offsetLateral = -dimensions["B2"] / 2;
                        name = "腹板"; break;
                    case 3:
                        x1 = dimensions["B4"] + dimensions["B5"] / 2;
                        y1 = -thicknessList[i].thickness / 2;
                        x2 = -x1; y2 = y1; t = thicknessList[i].thickness;
                        offsetVertical = -dimensions["H"];
                        name = "底板"; break;
                    default: break;
                }
                var solid = m.Instances.New<IfcSectionedSolidHorizontal>(s =>
                {
                    s.Directrix = directrix;
                    var p1 = IfcModelBuilder.MakeCartesianPoint(m, x1, y1);
                    var p2 = IfcModelBuilder.MakeCartesianPoint(m, x2, y2);
                    var line = IfcModelBuilder.MakePolyline(m, new List<IfcCartesianPoint>() { p1, p2 });
                    var profile = IfcModelBuilder.MakeCenterLineProfile(m, line, t);
                    s.CrossSections.AddRange(new List<IfcProfileDef>() { profile, profile });
                    var pos1 = IfcModelBuilder.MakeDistanceExpression(m, start, offsetLateral, offsetVertical);
                    var pos2 = IfcModelBuilder.MakeDistanceExpression(m, end, offsetLateral, offsetVertical);
                    s.CrossSectionPositions.AddRange(new List<IfcDistanceExpression>() { pos1, pos2 });
                });
                SetSurfaceStyle(m, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                shape.Items.Add(solid);
                lastDistanceAlong = end;
            }
                                   
            var plate = m.Instances.New<IfcPlate>(p =>
            {
                p.Name = name;
                p.ObjectType = (plateCode == 0 || plateCode == 3) ? "FLANGE_PLATE" : "WEB_PLATE";
                p.ObjectPlacement = m.Instances.New<IfcLinearPlacement>(lp =>
                {
                    lp.PlacementRelTo = directrix;
                    lp.Distance = m.Instances.New<IfcDistanceExpression>(d =>
                    {
                        d.DistanceAlong = StartGap;
                        d.OffsetVertical = offsetVertical;
                        d.OffsetLateral = offsetLateral;
                    });
                });
                p.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                p.PredefinedType = IfcPlateTypeEnum.USERDEFINED;
                //p.PredefinedType = (part == 0 || part == 1) ? IfcPlateTypeEnum.FLANGE_PLATE : IfcPlateTypeEnum.WEB_PLATE; // IFC4x2 feature
            });
            return plate;
        }

        private IfcMember CreateStiffeners(IfcStore m, int plateCode, List<(double distanceAlong, int typeId, int layoutId)> stiffenerList)
        {
            string tag = "";
            var stiffShape = IfcModelBuilder.MakeShapeRepresentation(m, 3, "Body", "AdvancedSweptSolid");
            var lastDistanceAlong = StartGap;
            var propertySet = m.Instances.New<IfcPropertySet>(ps => ps.Name = "Dimensions");
            var dimensionTable = m.Instances.New<IfcPropertyTableValue>();
            foreach (var (distanceAlong, typeId, layoutId) in stiffenerList)
            {
                var start = lastDistanceAlong;
                var end = distanceAlong;
                var dimensions = StiffenerTypeTable[typeId];
                if (tag == "")
                    tag = dimensions.Count == 5 ? "U形肋" : (dimensions.Count == 2 ? "板肋" : "T形肋");
                var stiffenerProfile = CreateStiffenerProfile(m, dimensions, GetStiffenerDirection(plateCode));
                var refPoint = GetRefPoint(plateCode);
                var offsetDirection = GetOffsetDirection(plateCode);
                foreach (var (num, gap) in StiffenerLayoutTable[layoutId])
                {
                    for (int i = 0; i < num; ++i)
                    {
                        var stiff = m.Instances.New<IfcSectionedSolidHorizontal>(s =>
                        {
                            s.Directrix = BridgeAlignmentCurve;
                            s.CrossSections.AddRange(new List<IfcProfileDef>() { stiffenerProfile, stiffenerProfile });
                            refPoint = refPoint + gap * offsetDirection;
                            var pos1 = IfcModelBuilder.MakeDistanceExpression(m, start, refPoint.X, refPoint.Y);
                            var pos2 = IfcModelBuilder.MakeDistanceExpression(m, end, refPoint.X, refPoint.Y);
                            s.CrossSectionPositions.AddRange(new List<IfcDistanceExpression>() { pos1, pos2 });
                        });
                        //SetSurfaceStyle(m, stiff, 0.752941176470588, 0.313725490196078, 0.301960784313725);
                        stiffShape.Items.Add(stiff);
                    }
                }

                lastDistanceAlong = distanceAlong;
            }

            var stiffenerGroup = m.Instances.New<IfcMember>(sg =>
            {
                sg.Name = plateCode == 0 ? "顶板纵肋" : (plateCode == 3 ? "底板纵肋" : "腹板纵肋");
                //sg.Description = "STIFFENING_RIB";
                sg.ObjectType = "STIFFENING_RIB";
                sg.Tag = tag;
                sg.PredefinedType = IfcMemberTypeEnum.USERDEFINED;
                //sg.PredefinedType = IfcMemberTypeEnum.STIFFENING_RIB; // IFC4x2 features
                sg.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(stiffShape));
            });
            return stiffenerGroup;
        }

        private List<IfcBuildingElementProxy> CreateBearings(IfcStore m)
        {
            var bearings = new List<IfcBuildingElementProxy>();
            foreach (var (distanceAlong, offsetLateral, bearingTypeId) in BearingList)
                bearings.Add(CreateBearing(m, distanceAlong, offsetLateral, bearingTypeId));
                //AddElementToSpatial(m, site, bearing, "Add the bearing to site");
            return bearings;
        }

        private IfcBuildingElementProxy CreateBearing(IfcStore m, double dist, double offsetLateral, int typeId)
        {
            using (var txn = m.BeginTransaction("Create bearings"))
            {                
                var distance = IfcModelBuilder.MakeDistanceExpression(m, dist, offsetLateral, -SectionDimensions[4] - 100);

                var shape = CreateBearingShape(m);
                var bearing = m.Instances.New<IfcBuildingElementProxy>(b =>
                {
                    b.ObjectPlacement = IfcModelBuilder.MakeLinearPlacement(m, BridgeAlignmentCurve, distance);
                    b.Name = "支座";
                    b.ObjectType = "IFCBEARING";
                    b.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                });

                // define bearing properties
                var (fixedLateral, fixedLongitudinal, fixedVertical) = BearingTypeTable[typeId];
                var pset_BearingCommon = m.Instances.New<IfcPropertySet>(ps =>
                {
                    ps.Name = "Pset_BearingCommon";
                    var displacementAccomodated = m.Instances.New<IfcPropertyListValue>(l =>
                    {
                        l.Name = "DisplacementAccomodated";
                        l.ListValues.Add(new IfcBoolean(!fixedLongitudinal));
                        l.ListValues.Add(new IfcBoolean(!fixedLateral));
                        l.ListValues.Add(new IfcBoolean(!fixedVertical));
                    });
                    ps.HasProperties.Add(displacementAccomodated);
                });
                m.Instances.New<IfcRelDefinesByProperties>(r =>
                {
                    r.RelatingPropertyDefinition = pset_BearingCommon;
                    r.RelatedObjects.Add(bearing);
                });

                txn.Commit();
                return bearing;
            }                
        }

        private IfcShapeRepresentation CreateBearingShape(IfcStore m)
        {
            var shape = IfcModelBuilder.MakeShapeRepresentation(m, 3, "Body", "CSG");
            var rectangle = IfcModelBuilder.MakeRectangleProfile(m, 940, 920);
            var vzNegated = IfcModelBuilder.MakeDirection(m, 0, 0, -1);
            var pos1 = IfcModelBuilder.MakeAxis2Placement3D(m);            
            shape.Items.Add(IfcModelBuilder.MakeExtrudedAreaSolid(m, rectangle, pos1, vzNegated, 50));

            var rectangle2 = IfcModelBuilder.MakeRectangleProfile(m, 80, 920);
            var pos4 = IfcModelBuilder.MakeAxis2Placement3D(m, IfcModelBuilder.MakeCartesianPoint(m, -410, 0, -50));            
            shape.Items.Add(IfcModelBuilder.MakeExtrudedAreaSolid(m, rectangle2, pos4, vzNegated, 80));

            var pos5 = IfcModelBuilder.MakeAxis2Placement3D(m, IfcModelBuilder.MakeCartesianPoint(m, 410, 0, -50));
            shape.Items.Add(IfcModelBuilder.MakeExtrudedAreaSolid(m, rectangle2, pos5, vzNegated, 80));

            var rectangle3 = IfcModelBuilder.MakeRectangleProfile(m, 740, 850);
            var pos6 = IfcModelBuilder.MakeAxis2Placement3D(m, IfcModelBuilder.MakeCartesianPoint(m, 0, 0, -50));
            var solid = IfcModelBuilder.MakeExtrudedAreaSolid(m, rectangle3, pos6, vzNegated, 70);
            SetSurfaceStyle(m, solid, 1, 0, 0);
            shape.Items.Add(solid);

            var circle = IfcModelBuilder.MakeCircleProfile(m, 300);
            var pos2 = IfcModelBuilder.MakeAxis2Placement3D(m, IfcModelBuilder.MakeCartesianPoint(m, 0, 0, -120));            
            shape.Items.Add(IfcModelBuilder.MakeExtrudedAreaSolid(m, circle, pos2, vzNegated, 65));

            var rectangle4 = IfcModelBuilder.MakeRectangleProfile(m, 800, 1200);
            var pos3 = IfcModelBuilder.MakeAxis2Placement3D(m, IfcModelBuilder.MakeCartesianPoint(m, 0, 0, -185));            
            shape.Items.Add(IfcModelBuilder.MakeExtrudedAreaSolid(m, rectangle4, pos3, vzNegated, 75));
            
            return shape;
        }

        private List<IfcElementAssembly> CreateDiaphragms(IfcStore m)
        {
            var diaphragms = new List<IfcElementAssembly>();
            double lastDist = StartGap;
            foreach (var (typeId, num, gap) in DiaphragmList)
            {
                for (int i = 0; i < num; ++i)
                {
                    lastDist += gap;
                    diaphragms.Add(CreateDiaphragm(m, typeId, lastDist));                    
                }
            }                
            return diaphragms;
        }

        private IfcElementAssembly CreateDiaphragm(IfcStore m, int typeId, double distanceAlong)
        {
            var diaphragm = m.Instances.New<IfcElementAssembly>(ea =>
            {
                ea.Name = "横向支撑";
                ea.ObjectType = "DIAPHRAGM";
                ea.PredefinedType = IfcElementAssemblyTypeEnum.USERDEFINED;
            });
            var distance = IfcModelBuilder.MakeDistanceExpression(m, distanceAlong);
            var plate = m.Instances.New<IfcPlate>(p =>
            {
                p.Name = "横隔板";
                p.ObjectPlacement = IfcModelBuilder.MakeLinearPlacement(m, BridgeAlignmentCurve, distance);
                var solid = m.Instances.New<IfcExtrudedAreaSolid>(s =>
                {
                    s.Position = IfcModelBuilder.MakeAxis2Placement3D(m, Origin3D, AxisX3D, AxisY3D);
                    var outerCurve = CreateDiaphragmOuterCurve(m, distanceAlong);                    
                    var innerCurve = CreateDiaphragmInnerCurve(m);
                    s.SweptArea = IfcModelBuilder.MakeArbProfileWithVoids(m, outerCurve/*outerCurve*/, new List<IfcCurve>() { innerCurve });
                    s.ExtrudedDirection = IfcModelBuilder.MakeDirection(m, 0, 0, 1);
                    s.Depth = DiaphragmTypeTable[typeId];
                });
                SetSurfaceStyle(m, solid, 1, 0.9333, 0, 0.15);
                var shape = IfcModelBuilder.MakeShapeRepresentation(m, 3, "Body", "CSG");
                shape.Items.Add(solid);
                p.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
            });

            // Cantilever web
            var plate2 = m.Instances.New<IfcPlate>(p =>
            {
                p.Name = "悬挑腹板";
                p.ObjectPlacement = IfcModelBuilder.MakeLinearPlacement(m, BridgeAlignmentCurve, distance);
                var solid = m.Instances.New<IfcExtrudedAreaSolid>(s =>
                {
                    s.Position = IfcModelBuilder.MakeAxis2Placement3D(m, Origin3D, AxisX3D, AxisY3D);
                    //var outerCurvePoints = new List<IfcCartesianPoint>()
                    //{
                    //    IfcModelBuilder.MakeCartesianPoint(m, -4934, 0),
                    //    IfcModelBuilder.MakeCartesianPoint(m, -4934, -360),
                    //    IfcModelBuilder.MakeCartesianPoint(m, -3100, -600),
                    //    IfcModelBuilder.MakeCartesianPoint(m, -3220, 0),
                    //    IfcModelBuilder.MakeCartesianPoint(m, -4934, 0)
                    //};
                    var outerCurve = CreateCantileverWebOuterCurve(m, distanceAlong, true);
                    
                    s.SweptArea = IfcModelBuilder.MakeArbClosedProfile(m, outerCurve);
                    s.ExtrudedDirection = IfcModelBuilder.MakeDirection(m, 0, 0, 1);
                    s.Depth = DiaphragmTypeTable[typeId];
                });
                SetSurfaceStyle(m, solid, 1, 0.9333, 0, 0.15);
                var solid2 = m.Instances.New<IfcExtrudedAreaSolid>(s =>
                {
                    s.Position = IfcModelBuilder.MakeAxis2Placement3D(m, Origin3D, AxisX3D, AxisY3D);
                    //var outerCurvePoints = new List<IfcCartesianPoint>()
                    //{
                    //    IfcModelBuilder.MakeCartesianPoint(m, 4934, 0),
                    //    IfcModelBuilder.MakeCartesianPoint(m, 4934, -360),
                    //    IfcModelBuilder.MakeCartesianPoint(m, 3100, -600),
                    //    IfcModelBuilder.MakeCartesianPoint(m, 3220, 0),
                    //    IfcModelBuilder.MakeCartesianPoint(m, 4934, 0)
                    //};
                    var outerCurve = CreateCantileverWebOuterCurve(m, distanceAlong, false);
                    //var innerCurve = IfcModelBuilder.MakeCircle(m, IfcModelBuilder.MakeAxis2Placement3D(m, IfcModelBuilder.MakeCartesianPoint(m, 0, -950, 0)), 300);
                    s.SweptArea = IfcModelBuilder.MakeArbProfileWithVoids(m, outerCurve, new List<IfcCurve>() { });
                    s.ExtrudedDirection = IfcModelBuilder.MakeDirection(m, 0, 0, 1);
                    s.Depth = DiaphragmTypeTable[typeId];
                });
                SetSurfaceStyle(m, solid2, 1, 0.9333, 0, 0.15);

                var shape = IfcModelBuilder.MakeShapeRepresentation(m, 3, "Body", "CSG");
                shape.Items.Add(solid);
                shape.Items.Add(solid2);
                p.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
            });

            // Aggregate plates and stiffeners
            var relAggregates = m.Instances.New<IfcRelAggregates>(r =>
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

        /// <summary>
        /// dimensions = { B1, B2, B4, B5, H, t1, t2, tw1 }
        /// </summary>
        /// <param name="m"></param>
        /// <param name="axis"></param>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <param name="dimensions"></param>
        /// <returns></returns>
        private IfcElementAssembly CreateSteelBoxGirder(IfcStore m, string name, IfcCurve axis, double start, double length, List<double> dimensions)
        {
            var dimensionNames = new List<string>() { "B1", "B2", "B4", "B5", "H", "t1", "t2", "tw1" };
            var dimensionsMap = new Dictionary<string, double>();
            for (int i = 0; i < dimensions.Count; ++i)
                dimensionsMap[dimensionNames[i]] = dimensions[i];
            
            using (var txn = m.BeginTransaction("Create an assembly to hold all sub-elements"))
            {
                var girder = m.Instances.New<IfcElementAssembly>(g =>
                {
                    g.Name = name;
                    g.Description = "Steel Box Girder";
                    g.PredefinedType = IfcElementAssemblyTypeEnum.GIRDER;
                });

                // Add property set which holds section properties
                m.Instances.New<IfcRelDefinesByProperties>(rdbp =>
                {
                    rdbp.RelatedObjects.Add(girder);
                    rdbp.RelatingPropertyDefinition = IfcModelBuilder.MakePropertySet(m, "SteelBoxSectionDimensions", dimensionsMap);
                });

                // Add top flange, bottom flange and webs to girder
                // TODO                
                // 0 for top flange, 3 for bottom flange, 1 for left web, 2 for right web
                var plateCodes = new List<int>() { 0, 3, 1, 2 };
                var plates = plateCodes.Select(c => CreateSingleBoxPlate(m, axis, start, length, dimensionsMap, c)).ToList();

                // Add stiffeners to be connected with flanges and webs
                // TODO
                // The flat stiffener has dimensions H = 190mm, B = 16mm
                var flatStiffDims = new List<double>() { 190, 16 };
                var topFlatStiffProfile = CreateStiffenerProfile(m, flatStiffDims, new XbimVector3D(0, -1, 0));
                var gaps = new List<double>() { 250, 250, 250, 250, 250, 250 };           
                var topLeftStiffners = AddStiffeners(m, plates[0], topFlatStiffProfile, gaps, dimensionsMap["B1"] + dimensionsMap["B2"] / 2);
                topLeftStiffners.Name = "TopLeftStiffeners";
                var topRightStiffners = AddStiffeners(m, plates[0], topFlatStiffProfile, gaps, -dimensionsMap["B2"] / 2);
                topRightStiffners.Name = "TopRightStiffeners";

                var bottomFlatStiffProfile = CreateStiffenerProfile(m, flatStiffDims, new XbimVector3D(0, 1, 0));
                gaps = new List<double>() { 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400, 400 };                
                var bottomCenterStiffeners = AddStiffeners(m, plates[1], bottomFlatStiffProfile, gaps, dimensionsMap["B5"] / 2);
                bottomCenterStiffeners.Name = "BottomCenterStiffeners";

                var leftStiffProfile = CreateStiffenerProfile(m, flatStiffDims, 
                    new XbimVector3D(-2 * dimensionsMap["H"], dimensionsMap["B2"] - dimensionsMap["B5"], 0).Normalized());
                gaps = new List<double>() { 600, 1000 };
                var leftWebStiffeners = AddStiffeners(m, plates[2], leftStiffProfile, gaps, 0);
                leftWebStiffeners.Name = "LeftWebStiffeners";

                var rightStiffProfile = CreateStiffenerProfile(m, flatStiffDims,
                    new XbimVector3D(2 * dimensionsMap["H"], dimensionsMap["B2"] - dimensionsMap["B5"], 0).Normalized());
                var rightWebStiffeners = AddStiffeners(m, plates[3], rightStiffProfile, gaps, 0);
                rightWebStiffeners.Name = "RightWebStiffeners";

                // Add property set to hold flat stiffener dimensions
                var flatStiffDimMap = new Dictionary<string, double>() { { "H", flatStiffDims[0] }, { "B", flatStiffDims[1] } };
                m.Instances.New<IfcRelDefinesByProperties>(rdbp =>
                {
                    rdbp.RelatedObjects.AddRange(new List<IfcMember>() { topLeftStiffners, topRightStiffners,
                        bottomCenterStiffeners, leftWebStiffeners, rightWebStiffeners });
                    rdbp.RelatingPropertyDefinition = IfcModelBuilder.MakePropertySet(m, "StiffenerDimensions", flatStiffDimMap);
                });

                // The U-shape stiffener has dimensions H = 280, B1 = 300, B2 = 170, t = 8, R = 40
                var ushapeStiffDims = new List<double>() { 280, 300, 170, 8, 40 };
                var topUshapeStiffProfile = CreateStiffenerProfile(m, ushapeStiffDims, new XbimVector3D(0, -1, 0));
                gaps = new List<double>() { 500, 600, 600, 600, 600, 600, 600, 600, 600, 600 };
                var topCenterStiffeners = AddStiffeners(m, plates[0], topUshapeStiffProfile, gaps, dimensionsMap["B2"] / 2);
                topCenterStiffeners.Name = "TopCenterStiffeners";
                var ushapeStiffDimNames = new List<string>() { "H", "B1", "B2", "t", "R" };
                var ushapeStiffDimMap = new Dictionary<string, double>();
                for (int i = 0; i < ushapeStiffDims.Count; ++i)
                    ushapeStiffDimMap[ushapeStiffDimNames[i]] = ushapeStiffDims[i];
 
                // Add property set to hold U-shape stiffener dimensions
                m.Instances.New<IfcRelDefinesByProperties>(rdbp =>
                {
                    rdbp.RelatedObjects.AddRange(new List<IfcMember>() { topCenterStiffeners });
                    rdbp.RelatingPropertyDefinition = IfcModelBuilder.MakePropertySet(m, "StiffenerDimensions", ushapeStiffDimMap);
                });

                // Aggregate flanges and webs into the girder assembly
                var relAggregates = m.Instances.New<IfcRelAggregates>(r =>
                {
                    r.RelatingObject = girder;
                    r.RelatedObjects.AddRange(plates);
                    foreach (var plate in plates)                    
                        foreach (var connect in plate.ConnectedFrom)
                            r.RelatedObjects.Add(connect.RelatingElement);                    
                });                

                // Add property set which holds properties 'StartDistanceAlong' and 'SegmentLength'
                var commonPropertiesMap = new Dictionary<string, double>()
                {
                    {"StartDistanceAlong", start }, {"SegmentLength", length}
                };
                m.Instances.New<IfcRelDefinesByProperties>(rdbp =>
                {
                    rdbp.RelatedObjects.Add(girder);
                    rdbp.RelatingPropertyDefinition = IfcModelBuilder.MakePropertySet(m, "GirderCommon", commonPropertiesMap);
                });

                // Set material for this girder
                CreateMaterialForGirder(m, girder);

                txn.Commit();
                return girder;
            }
        }

        private void CreateMaterialForGirder(IfcStore m, IfcElementAssembly girder)
        {
            // Create material and associates it with the girder
            var material = m.Instances.New<IfcMaterial>(mat =>
            {
                mat.Name = "Q345";
                mat.Category = "Steel";
            });
            m.Instances.New<IfcRelAssociatesMaterial>(ram =>
            {
                ram.RelatingMaterial = material;
                ram.RelatedObjects.Add(girder);
            });
            // Create P_set to hold material properties
            var pset_MaterialCommon = m.Instances.New<IfcMaterialProperties>(mp =>
            {
                mp.Name = "Pset_MaterialCommon";
                mp.Material = material;
                var massDensity = m.Instances.New<IfcPropertySingleValue>(p =>
                {
                    p.Name = "MassDensity";
                    p.NominalValue = new IfcMassDensityMeasure(7.85e-9);
                });
                mp.Properties.Add(massDensity);
            });
            var pset_MaterialMechanical = m.Instances.New<IfcMaterialProperties>(mp =>
            {
                mp.Name = "Pset_MaterialMechanical";
                mp.Material = material;
                // Add YoungModulus, PoissonRatio, ThermalExpansionCoefficient
                // TODO
                var youngModulus = m.Instances.New<IfcPropertySingleValue>(p =>
                {
                    p.Name = "YoungModulus";
                    p.NominalValue = new IfcModulusOfElasticityMeasure(2.06e5);
                });
                var poissonRatio = m.Instances.New<IfcPropertySingleValue>(p =>
                {
                    p.Name = "PoissonRatio";
                    p.NominalValue = new IfcPositiveRatioMeasure(0.3);
                });
                var thermalExpansionCoefficient = m.Instances.New<IfcPropertySingleValue>(p =>
                {
                    p.Name = "ThermalExpansionCoefficient";
                    p.NominalValue = new IfcThermalExpansionCoefficientMeasure(1.2e-5);
                });
                mp.Properties.AddRange(new List<IfcPropertySingleValue>() { youngModulus, poissonRatio, thermalExpansionCoefficient });
            });
        }

        // Symmetric box section
        // part = 0 => top flange, part = 1 => left web, part = 2 => right web, part = 3 => bottom flange
        private IfcPlate CreateSingleBoxPlate(IfcStore m, IfcCurve axis, double start, double length, 
            Dictionary<string, double> dimensions, int plateCode)
        {            
            double x1 = 0, y1 = 0, x2 = 0, y2 = 0, t = 0, offsetLateral = 0, offsetVertical = 0;
            string name = "";
            switch (plateCode)
            {
                case 0:
                    x1 = dimensions["B1"] + dimensions["B2"] / 2;
                    y1 = dimensions["t1"] / 2;
                    x2 = -x1; y2 = y1; t = dimensions["t1"];
                    name = "TopFlange"; break;
                case 3:
                    x1 = dimensions["B4"] + dimensions["B5"] / 2;
                    y1 = -dimensions["t2"] / 2;
                    x2 = -x1; y2 = y1; t = dimensions["t2"];
                    offsetVertical = -dimensions["H"];
                    name = "BottomFlange"; break;
                case 1:
                    x1 = 0; y1 = 0; 
                    x2 = (dimensions["B5"] - dimensions["B2"]) / 2; y2 = -dimensions["H"];
                    t = dimensions["tw1"]; offsetLateral = dimensions["B2"] / 2;
                    name = "LeftWeb"; break;
                case 2:
                    x1 = 0; y1 = 0;
                    x2 = (dimensions["B2"] - dimensions["B5"]) / 2; y2 = -dimensions["H"];
                    t = dimensions["tw1"]; offsetLateral = -dimensions["B5"] / 2;
                    name = "RightWeb"; break;
                default: break;
            }
            var solid = m.Instances.New<IfcSectionedSolidHorizontal>(s =>
            {
                s.Directrix = axis;                
                var p1 = IfcModelBuilder.MakeCartesianPoint(m, x1, y1);
                var p2 = IfcModelBuilder.MakeCartesianPoint(m, x2, y2);
                var line = IfcModelBuilder.MakePolyline(m, new List<IfcCartesianPoint>() { p1, p2 });
                var profile = IfcModelBuilder.MakeCenterLineProfile(m, line, t);
                s.CrossSections.AddRange(new List<IfcProfileDef>() { profile, profile });
                var pos1 = IfcModelBuilder.MakeDistanceExpression(m, start, offsetLateral, offsetVertical);
                var pos2 = IfcModelBuilder.MakeDistanceExpression(m, start + length, offsetLateral, offsetVertical);
                s.CrossSectionPositions.AddRange(new List<IfcDistanceExpression>() { pos1, pos2 });
            });
            var shape = m.Instances.New<IfcShapeRepresentation>(s =>
            {
                s.ContextOfItems = m.Instances.OfType<IfcGeometricRepresentationContext>()
                    .Where(c => c.CoordinateSpaceDimension == 3)
                    .FirstOrDefault();
                s.RepresentationIdentifier = "Body";
                s.RepresentationType = "AdvancedSweptSolid";
                s.Items.Add(solid);
            });
            var plate = m.Instances.New<IfcPlate>(p =>
            {
                p.Name = name;
                p.Description = (plateCode == 0 || plateCode == 3) ? "FLANGE_PLATE" : "WEB_PLATE";
                p.ObjectPlacement = m.Instances.New<IfcLinearPlacement>(lp =>
                {
                    lp.PlacementRelTo = axis;
                    lp.Distance = m.Instances.New<IfcDistanceExpression>(d =>
                    {
                        d.DistanceAlong = start;
                        d.OffsetVertical = offsetVertical;
                        d.OffsetLateral = offsetLateral;
                    });
                });
                p.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                //p.PredefinedType = (part == 0 || part == 1) ? IfcPlateTypeEnum.FLANGE_PLATE : IfcPlateTypeEnum.WEB_PLATE; // IFC4x2 feature
            });
            return plate;
        }        

        // Add stiffeners to flanges and webs
        private IfcMember AddStiffeners(IfcStore m, IfcPlate parent, IfcProfileDef stiffProfile, List<double> gaps, double refPos)
        {
            int stiffNum = gaps.Count;
            IfcSectionedSolidHorizontal parentGeometry = (IfcSectionedSolidHorizontal)parent.Representation.Representations.FirstOrDefault().Items.FirstOrDefault();
            double start = parentGeometry.CrossSectionPositions.FirstOrDefault().DistanceAlong;
            double end = parentGeometry.CrossSectionPositions.LastOrDefault().DistanceAlong;
            var stiffShape = m.Instances.New<IfcShapeRepresentation>(s =>
            {
                s.ContextOfItems = m.Instances.OfType<IfcGeometricRepresentationContext>()
                    .Where(c => c.CoordinateSpaceDimension == 3)
                    .FirstOrDefault();
                s.RepresentationIdentifier = "Body";
                s.RepresentationType = "AdvancedSweptSolid";
            });
            for (int i = 0; i < stiffNum; ++i)
            {
                var stiff = m.Instances.New<IfcSectionedSolidHorizontal>(s =>
                {
                    s.Directrix = parentGeometry.Directrix;                  
                    s.CrossSections.AddRange(new List<IfcProfileDef>() { stiffProfile, stiffProfile });
                    refPos -= gaps[i];
                    var pos1 = IfcModelBuilder.MakeDistanceExpression(m, start, refPos, 0);
                    var pos2 = IfcModelBuilder.MakeDistanceExpression(m, end, refPos, 0);
                    s.CrossSectionPositions.AddRange(new List<IfcDistanceExpression>() { pos1, pos2 });
                });
                stiffShape.Items.Add(stiff);
            }
            var stiffenerGroup = m.Instances.New<IfcMember>(sg =>
            {                
                sg.Description = "STIFFENING_RIB";
                //me.PredefinedType = IfcMemberTypeEnum.STIFFENING_RIB; // IFC4x2 features
                sg.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(stiffShape));
            });
            // Connect top flange and the stiffeners on it
            m.Instances.New<IfcRelConnectsElements>(rce =>
            {
                rce.RelatedElement = parent;
                rce.RelatingElement = stiffenerGroup;
            });

            return stiffenerGroup;
        }

        private IfcCenterLineProfileDef CreateStiffenerProfile(IfcStore m, List<double> stiffDimensions, XbimVector3D vec)
        {
            switch (stiffDimensions.Count)
            {
                case 2: return CreateFlatStiffenerProfile(m, stiffDimensions, vec); 
                case 4: return CreateTShapeStiffenerProfile(m, stiffDimensions, vec); 
                case 5: return CreateUShapeStiffenerProfile(m, stiffDimensions, vec);
                default: throw new NotImplementedException("Other stiffener types not supported for now.");
            }
        }

        private IfcCenterLineProfileDef CreateFlatStiffenerProfile(IfcStore m, List<double> stiffDimensions, XbimVector3D vec)
        {
            vec = vec.Normalized();
            var p1 = IfcModelBuilder.MakeCartesianPoint(m, 0, 0);
            var p2 = IfcModelBuilder.MakeCartesianPoint(m, stiffDimensions[0] * vec.X, stiffDimensions[0] * vec.Y);
            var line = IfcModelBuilder.MakePolyline(m, new List<IfcCartesianPoint>() { p1, p2 });
            return IfcModelBuilder.MakeCenterLineProfile(m, line, stiffDimensions[1]);            
        }

        private IfcCenterLineProfileDef CreateTShapeStiffenerProfile(IfcStore m, List<double> stiffDimensions, XbimVector3D vec)
        {
            throw new NotImplementedException("T-shape stiffener not supported for now.");
        }

        private IfcCenterLineProfileDef CreateUShapeStiffenerProfile(IfcStore m, List<double> stiffDimensions, XbimVector3D vec)
        {
            double H = stiffDimensions[0], B1 = stiffDimensions[1], B2 = stiffDimensions[2], 
                t = stiffDimensions[3], R = stiffDimensions[4];

            var compositeCurve = m.Instances.New<IfcCompositeCurve>(cc =>
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
                var line = IfcModelBuilder.MakePolyline(m, p1, p21);
                var seg = IfcModelBuilder.MakeCompositeCurveSegment(m, line);
                cc.Segments.Add(seg);

                var center = IfcModelBuilder.MakeAxis2Placement2D(m, p01);
                var circle = IfcModelBuilder.MakeCircle(m, center, R);
                var arc = IfcModelBuilder.MakeTrimmedCurve(m, circle, p21, p22);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, arc);
                cc.Segments.Add(seg);

                line = IfcModelBuilder.MakePolyline(m, p22, p32);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, line);
                cc.Segments.Add(seg);

                center = IfcModelBuilder.MakeAxis2Placement2D(m, p02);
                circle = IfcModelBuilder.MakeCircle(m, center, R);
                arc = IfcModelBuilder.MakeTrimmedCurve(m, circle, p32, p31);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, arc);
                cc.Segments.Add(seg);

                line = IfcModelBuilder.MakePolyline(m, p31, p4);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, line, IfcTransitionCode.DISCONTINUOUS);
                cc.Segments.Add(seg);
            });
            return IfcModelBuilder.MakeCenterLineProfile(m, compositeCurve, t);
        }

        // set color for geometry item
        private void SetSurfaceStyle(IfcStore m, IfcGeometricRepresentationItem geomItem, double red, double green, double blue, double transparency = 0)
        {
            var styledItem = m.Instances.New<IfcStyledItem>(i =>
            {
                i.Item = geomItem;
                i.Styles.Add(m.Instances.New<IfcSurfaceStyle>(s =>
                {
                    s.Side = IfcSurfaceSide.POSITIVE;
                    s.Styles.Add(m.Instances.New<IfcSurfaceStyleRendering>(r =>
                    {
                        r.SurfaceColour = m.Instances.New<IfcColourRgb>(c =>
                        {
                            c.Red = red;
                            c.Green = green;
                            c.Blue = blue;
                        });
                        r.Transparency = transparency;
                    }));
                }));
            });
        }      

        private IfcCompositeCurve CreateDiaphragmInnerCurve(IfcStore m)
        {
            var compCurve = m.Instances.New<IfcCompositeCurve>(cc =>
            {
                var center = IfcModelBuilder.MakeCartesianPoint(m, 0, -950, 0);
                var pos = IfcModelBuilder.MakeAxis2Placement2D(m, center);
                var circle = IfcModelBuilder.MakeCircle(m, pos, 300);
                var halfCircle = IfcModelBuilder.MakeTrimmedCurve(m, circle, Math.PI, 0);
                var seg = IfcModelBuilder.MakeCompositeCurveSegment(m, halfCircle);
                cc.Segments.Add(seg);
                var pts = new List<IfcCartesianPoint>()
                {
                    IfcModelBuilder.MakeCartesianPoint(m, 300, -950),
                    IfcModelBuilder.MakeCartesianPoint(m, 300, -1200)
                };
                var poly = IfcModelBuilder.MakePolyline(m, pts);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
                cc.Segments.Add(seg);

                pts = new List<IfcCartesianPoint>()
                {
                    IfcModelBuilder.MakeCartesianPoint(m, -300, -950),
                    IfcModelBuilder.MakeCartesianPoint(m, -300, -1200)
                };
                poly = IfcModelBuilder.MakePolyline(m, pts);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
                cc.Segments.Add(seg);

                center = IfcModelBuilder.MakeCartesianPoint(m, 0, -1200, 0);
                pos = IfcModelBuilder.MakeAxis2Placement2D(m, center);
                circle = IfcModelBuilder.MakeCircle(m, pos, 300);
                halfCircle = IfcModelBuilder.MakeTrimmedCurve(m, circle, 0, Math.PI);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, halfCircle);
                cc.Segments.Add(seg);
            });            

            return compCurve;
        }

        private IfcCompositeCurve CreateDiaphragmOuterCurve(IfcStore m, double distanceAlong)
        {
            double B1 = SectionDimensions[0], B2 = SectionDimensions[1], B3 = SectionDimensions[2],
                B4 = SectionDimensions[3], H = SectionDimensions[4];
            return m.Instances.New<IfcCompositeCurve>(cc =>
            {
                var pts = new List<IfcCartesianPoint>()
                {
                    IfcModelBuilder.MakeCartesianPoint(m, -B4 / 2, -H),
                    IfcModelBuilder.MakeCartesianPoint(m, -B2 / 2, 0)
                };
                var poly = IfcModelBuilder.MakePolyline(m, pts);
                var seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
                cc.Segments.Add(seg);
                                
                var list = StiffenerLists[0].Where(l => 
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
                                    IfcModelBuilder.MakeCartesianPoint(m, pt)
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
                                pts.Add(IfcModelBuilder.MakeCartesianPoint(m, pt));
                                poly = IfcModelBuilder.MakePolyline(m, pts);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
                                cc.Segments.Add(seg);
                                AddUShapeHole(m, ref cc, ref pt, h, b1, b2);                                
                            }
                        }
                        break;
                    }
                    pts = new List<IfcCartesianPoint>()
                    {
                        IfcModelBuilder.MakeCartesianPoint(m, pt),
                        IfcModelBuilder.MakeCartesianPoint(m, B2 / 2, 0)
                    };
                    poly = IfcModelBuilder.MakePolyline(m, pts);
                    seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
                }
                else
                {
                    pts = new List<IfcCartesianPoint>()
                    {
                        IfcModelBuilder.MakeCartesianPoint(m, -B2 / 2, 0),
                        IfcModelBuilder.MakeCartesianPoint(m, B2 / 2, 0)
                    };
                    poly = IfcModelBuilder.MakePolyline(m, pts);
                    seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
                }
                cc.Segments.Add(seg);

                pts = new List<IfcCartesianPoint>()
                {
                    IfcModelBuilder.MakeCartesianPoint(m, B2 / 2, 0),
                    IfcModelBuilder.MakeCartesianPoint(m, B4 / 2, -H)
                };
                poly = IfcModelBuilder.MakePolyline(m, pts);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
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
                                    IfcModelBuilder.MakeCartesianPoint(m, pt)
                                };
                                if (isFirst)
                                {
                                    pt = pt + dir * (gap - b / 2 - R);
                                    isFirst = false;
                                }                                        
                                else
                                    pt = pt + dir * (gap - 2 * R);
                                pts.Add(IfcModelBuilder.MakeCartesianPoint(m, pt));
                                poly = IfcModelBuilder.MakePolyline(m, pts);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
                                cc.Segments.Add(seg);
                                
                                pt = pt + dir * R;
                                var pos = IfcModelBuilder.MakeAxis2Placement2D(m, pt);
                                var circle = IfcModelBuilder.MakeCircle(m, pos, R);
                                var quater = IfcModelBuilder.MakeTrimmedCurve(m, circle, Math.PI / 2, 0);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, quater);
                                cc.Segments.Add(seg);
                                                               
                                dir = new XbimVector3D(0, 1, 0);
                                pt = pt + dir * R;
                                pts = new List<IfcCartesianPoint>
                                {
                                    IfcModelBuilder.MakeCartesianPoint(m, pt)
                                };
                                pt = pt + dir * (h - 2 * R);
                                pts.Add(IfcModelBuilder.MakeCartesianPoint(m, pt));
                                poly = IfcModelBuilder.MakePolyline(m, pts);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
                                cc.Segments.Add(seg);
                                
                                pt = pt + dir * R;
                                pos = IfcModelBuilder.MakeAxis2Placement2D(m, pt);
                                circle = IfcModelBuilder.MakeCircle(m, pos, R);
                                quater = IfcModelBuilder.MakeTrimmedCurve(m, circle, Math.PI, Math.PI * 1.5);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, quater);
                                cc.Segments.Add(seg);
                                
                                dir = new XbimVector3D(-1, 0, 0);
                                pt = pt + dir * R;
                                pts = new List<IfcCartesianPoint>
                                {
                                    IfcModelBuilder.MakeCartesianPoint(m, pt)
                                };
                                dir = new XbimVector3D(0, -1, 0);
                                pt = pt + dir * h;
                                pts.Add(IfcModelBuilder.MakeCartesianPoint(m, pt));
                                poly = IfcModelBuilder.MakePolyline(m, pts);
                                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
                                cc.Segments.Add(seg);
                                dir = new XbimVector3D(-1, 0, 0);
                            }
                        }
                        break;
                    }
                }
                pts = new List<IfcCartesianPoint>()
                {
                    IfcModelBuilder.MakeCartesianPoint(m, pt),
                    IfcModelBuilder.MakeCartesianPoint(m, -2775, -1968)
                };
                poly = IfcModelBuilder.MakePolyline(m, pts);
                seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
                cc.Segments.Add(seg);
            });
        }

        private IfcCompositeCurve CreateCantileverWebOuterCurve(IfcStore m, double distanceAlong, bool isLeft)
        {
            var cc = m.Instances.New<IfcCompositeCurve>();
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
            var poly = IfcModelBuilder.MakePolyline(m, new List<XbimPoint3D>() { p82, p1, p4, p3, p2, p51 });
            var seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
            cc.Segments.Add(seg);
            poly = IfcModelBuilder.MakePolyline(m, p52, p61);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
            cc.Segments.Add(seg);
            poly = IfcModelBuilder.MakePolyline(m, p62, p71);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
            cc.Segments.Add(seg);
            poly = IfcModelBuilder.MakePolyline(m, p72, p81);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(m, poly);
            cc.Segments.Add(seg);
            if (isLeft)
            {
                AddFlatStiffenerHole(m, ref cc, p71);
                AddFlatStiffenerHole(m, ref cc, p81);
                AddUShapeHole(m, ref cc, ref p51, 280, 300, 170);
                AddUShapeHole(m, ref cc, ref p61, 280, 300, 170);
            }
            else
            {
                AddFlatStiffenerHole(m, ref cc, p72);
                AddFlatStiffenerHole(m, ref cc, p82);
                AddUShapeHole(m, ref cc, ref p52, 280, 300, 170);
                AddUShapeHole(m, ref cc, ref p62, 280, 300, 170);
            }

            return cc;
        }

        // Add Flat stiffener hole
        private void AddFlatStiffenerHole(IfcStore m, ref IfcCompositeCurve usingCurve, XbimPoint3D start)
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
            var center = IfcModelBuilder.MakeAxis2Placement2D(m, p8);
            var circle = IfcModelBuilder.MakeCircle(m, center, R);
            var arc = IfcModelBuilder.MakeTrimmedCurve(m, circle, p2, p1);
            var seg = IfcModelBuilder.MakeCompositeCurveSegment(m, arc);
            usingCurve.Segments.Add(seg);

            var line = IfcModelBuilder.MakePolyline(m, p2, p3);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(m, line);
            usingCurve.Segments.Add(seg);

            center = IfcModelBuilder.MakeAxis2Placement2D(m, p4);
            circle = IfcModelBuilder.MakeCircle(m, center, R);
            arc = IfcModelBuilder.MakeTrimmedCurve(m, circle, p5, p3);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(m, arc);
            usingCurve.Segments.Add(seg);

            line = IfcModelBuilder.MakePolyline(m, p5, p6);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(m, line);
            usingCurve.Segments.Add(seg);

            center = IfcModelBuilder.MakeAxis2Placement2D(m, p9);
            circle = IfcModelBuilder.MakeCircle(m, center, R);
            arc = IfcModelBuilder.MakeTrimmedCurve(m, circle, p7, p6);
            seg = IfcModelBuilder.MakeCompositeCurveSegment(m, arc);
            usingCurve.Segments.Add(seg);
        }

        // Add U-shape hole which is composed by a groupt of composite curve segments
        private void AddUShapeHole(IfcStore m, ref IfcCompositeCurve usingCurve, ref XbimPoint3D ptOut, double h, double b1, double b2)
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
            var poly = IfcModelBuilder.MakePolyline(m, new List<XbimPoint3D>() { p1, p2, p3 });
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(m, poly));

            var center = IfcModelBuilder.MakeAxis2Placement2D(m, p34);
            var circle = IfcModelBuilder.MakeCircle(m, center, R1);
            var arc = IfcModelBuilder.MakeTrimmedCurve(m, circle, p4, p3);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(m, arc));

            poly = IfcModelBuilder.MakePolyline(m, p4, p51);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(m, poly));

            center = IfcModelBuilder.MakeAxis2Placement2D(m, p01);
            circle = IfcModelBuilder.MakeCircle(m, center, R2);
            arc = IfcModelBuilder.MakeTrimmedCurve(m, circle, p52, p51);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(m, arc));

            poly = IfcModelBuilder.MakePolyline(m, p52, p62);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(m, poly));

            center = IfcModelBuilder.MakeAxis2Placement2D(m, p02);
            circle = IfcModelBuilder.MakeCircle(m, center, R2);
            arc = IfcModelBuilder.MakeTrimmedCurve(m, circle, p61, p62);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(m, arc));

            poly = IfcModelBuilder.MakePolyline(m, p61, p7);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(m, poly));

            center = IfcModelBuilder.MakeAxis2Placement2D(m, p78);
            circle = IfcModelBuilder.MakeCircle(m, center, R1);
            arc = IfcModelBuilder.MakeTrimmedCurve(m, circle, p8, p7);
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(m, arc));

            poly = IfcModelBuilder.MakePolyline(m, new List<XbimPoint3D>() { p8, p9, p10 });
            usingCurve.Segments.Add(IfcModelBuilder.MakeCompositeCurveSegment(m, poly));

            // Set ptOut
            ptOut = p10;
        }
    }
}
