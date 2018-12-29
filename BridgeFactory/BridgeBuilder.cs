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
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.SharedBldgElements;
using Xbim.IO;

namespace ifc2mct.BridgeFactory
{
    public class BridgeBuilder
    {
        private readonly string OutputPath = "";

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
            OutputPath = path;
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
                            model.SaveAs(OutputPath, StorageType.Ifc);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Failed to save {0}", OutputPath);
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

        /// <summary>
        /// Create an instance of IfcAlignment to hold the alignment curve
        /// with some default geometrical properties
        /// </summary>
        /// <param name="m"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private IfcAlignment CreateAlignment(IfcStore m, string name)
        {
            const double RADIUS = 235687; // the radius of the arc is 235.687m
            const double LENGTH = 100000; // the segment length of the arc is 100m
            const double DIRECTION = Math.PI / 6; // the start direction of  the arc
            const bool ISCCW = false; // the orientation of the arc is clockwise
            const double DISTANCE = 10000; // the vertical segment starts at 10m along horizontal curve
            const double LENGTH2 = 80000; // the length of vertical segement is 80m along horizontal
            const double HEIGHT = 15000; // the vertical segment starts at 15m high
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
                });

                txn.Commit();
                return align;
            }
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
                var profile = IfcModelBuilder.MakeCenterLineProfileDef(m, line, t);
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
            return IfcModelBuilder.MakeCenterLineProfileDef(m, line, stiffDimensions[1]);            
        }

        private IfcCenterLineProfileDef CreateTShapeStiffenerProfile(IfcStore m, List<double> stiffDimensions, XbimVector3D vec)
        {
            throw new NotImplementedException("TShape stiffener not supported for now.");
        }

        private IfcCenterLineProfileDef CreateUShapeStiffenerProfile(IfcStore m, List<double> stiffDimensions, XbimVector3D vec)
        {
            // For now, it's a fake U-shape profile using a trapezoid as replacement
            var p1 = IfcModelBuilder.MakeCartesianPoint(m, stiffDimensions[1] / 2, 0);
            var p2 = IfcModelBuilder.MakeCartesianPoint(m, stiffDimensions[2] / 2, -stiffDimensions[0]);
            var p3 = IfcModelBuilder.MakeCartesianPoint(m, -stiffDimensions[2] / 2, -stiffDimensions[0]);
            var p4 = IfcModelBuilder.MakeCartesianPoint(m, -stiffDimensions[1] / 2, 0);
            var line = IfcModelBuilder.MakePolyline(m, new List<IfcCartesianPoint>() { p1, p2, p3, p4 });
            return IfcModelBuilder.MakeCenterLineProfileDef(m, line, stiffDimensions[3]);            
        }

    }
}
