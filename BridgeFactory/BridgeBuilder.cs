using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common;
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
            const double LENGTH = 30000;
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
            double B1 = dimensions[0], B2 = dimensions[1], B4 = dimensions[2], B5 = dimensions[3],
                H = dimensions[4], t1 = dimensions[5], t2 = dimensions[6], tw1 = dimensions[7];
            
            using (var txn = m.BeginTransaction("Create an assembly to hold all sub-elements"))
            {
                var girder = m.Instances.New<IfcElementAssembly>(g =>
                {
                    g.Name = name;
                    g.PredefinedType = IfcElementAssemblyTypeEnum.GIRDER;
                });

                // Add top flange, bottom flange and webs to girder
                // TODO
                // Create the top flange
                var solid = m.Instances.New<IfcSectionedSolidHorizontal>(s =>
                {
                    s.Directrix = axis;
                    var p1 = IfcModelBuilder.MakeCartesianPoint(m, -B1 - B2 / 2, t1 / 2);
                    var p2 = IfcModelBuilder.MakeCartesianPoint(m, B1 + B2 / 2, t1 / 2);
                    var line = IfcModelBuilder.MakePolyline(m, new List<IfcCartesianPoint>() { p1, p2 });
                    var profile = IfcModelBuilder.MakeCenterLineProfileDef(m, line, t1);                    
                    s.CrossSections.AddRange(new List<IfcProfileDef>() { profile, profile });
                    var pos1 = IfcModelBuilder.MakeDistanceExpression(m, start, 0, 0);
                    var pos2 = IfcModelBuilder.MakeDistanceExpression(m, start + length, 0, 0);
                    s.CrossSectionPositions.AddRange(new List<IfcDistanceExpression>() { pos1, pos2 });
                    // s.FixedAxisVertical = ?
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
                var topFlange = m.Instances.New<IfcPlate>(tf =>
                {
                    tf.Name = string.Format($"{name}-TopFlange");
                    tf.ObjectPlacement = m.Instances.New<IfcLinearPlacement>(lp =>
                    {
                        lp.PlacementRelTo = axis;
                        lp.Distance = m.Instances.New<IfcDistanceExpression>(d => d.DistanceAlong = start);
                    });
                    tf.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                    //tf.PredefinedType = IfcPlateTypeEnum.FLANGE_PLATE;
                });

                // Add stiffeners to be connected with flanges and webs
                // TODO

                // The plate shape stiffener has dimensions H = 190mm, B = 16mm
                var plateStiff = new List<double>() { 190, 16 };
                // There are 2 stiffeners, the 1st has a distance of 250mm to reference point
                // and the 2nd has a distance of 250mm to the 1st
                var gaps = new List<double>() { 250, 250 };
                var topLeftStiffners = AddStiffeners(m, topFlange, plateStiff, gaps, B1 + B2 / 2);

                // Aggregate flanges and webs into the girder assembly
                var relAggregates = m.Instances.New<IfcRelAggregates>(r =>
                {
                    r.RelatingObject = girder;
                    r.RelatedObjects.AddRange(new List<IfcElement>() { topFlange, topLeftStiffners });
                });

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

                // Add property set which holds properties 'StartDistanceAlong' and 'SegmentLength'
                var startDistanceAlong = m.Instances.New<IfcPropertySingleValue>(s =>
                {
                    s.Name = "StartDistanceAlong";
                    s.NominalValue = new IfcLengthMeasure(start);                    
                });
                var segmentLength = m.Instances.New<IfcPropertySingleValue>(s =>
                {
                    s.Name = "SegmentLength";
                    s.NominalValue = new IfcLengthMeasure(length);
                });
                var propertySet = m.Instances.New<IfcPropertySet>(ps =>
                {
                    ps.Name = "GirderCommonProperties";
                    ps.HasProperties.Add(startDistanceAlong);
                    ps.HasProperties.Add(segmentLength);
                });
                // Create the relationship
                m.Instances.New<IfcRelDefinesByProperties>(rdbp =>
                {
                    rdbp.RelatedObjects.Add(girder);
                    rdbp.RelatingPropertyDefinition = propertySet;
                });

                txn.Commit();
                return girder;
            }
        }

        // Add palte shape stiffeners to left top flange
        private IfcMember AddStiffeners(IfcStore m, IfcPlate parent, List<double> stiffDimensions, List<double> gaps, double refPos)
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
                    var p1 = IfcModelBuilder.MakeCartesianPoint(m, 0, 0);
                    var p2 = IfcModelBuilder.MakeCartesianPoint(m, 0, stiffDimensions[0]);
                    var line = IfcModelBuilder.MakePolyline(m, new List<IfcCartesianPoint>() { p1, p2 });
                    var profile = IfcModelBuilder.MakeCenterLineProfileDef(m, line, stiffDimensions[1]);
                    s.CrossSections.AddRange(new List<IfcProfileDef>() { profile, profile });
                    var pos1 = IfcModelBuilder.MakeDistanceExpression(m, start, refPos - gaps[i], 0);
                    var pos2 = IfcModelBuilder.MakeDistanceExpression(m, end, refPos - gaps[i], 0);
                    s.CrossSectionPositions.AddRange(new List<IfcDistanceExpression>() { pos1, pos2 });
                });
                stiffShape.Items.Add(stiff);
            }
            var stiffenerGroup = m.Instances.New<IfcMember>(me =>
            {
                me.Name = "TopFlange-Left";
                me.Description = "STIFFENING_RIB";
                //me.PredefinedType = IfcMemberTypeEnum.STIFFENING_RIB;
                me.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(stiffShape));
            });
            // Connect top flange and the stiffeners on it
            m.Instances.New<IfcRelConnectsElements>(rce =>
            {
                rce.RelatedElement = parent;
                rce.RelatingElement = stiffenerGroup;
            });

            return stiffenerGroup;
        }
    }
}
