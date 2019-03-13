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
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.RepresentationResource;
using Xbim.IO;

namespace ifc2mct.BridgeFactory
{
    public class AlignmentBuilder
    {
        private readonly string _outputPath = "";
        private readonly string _projectName = "xx大道立交SW1-SW5匝道";

        private IfcCartesianPoint Origin3D { get; set; }
        private IfcDirection AxisX3D { get; set; }
        private IfcDirection AxisY3D { get; set; }
        private IfcDirection AxisZ3D { get; set; }
        private IfcAxis2Placement3D WCS { get; set; }
        private IfcCartesianPoint Origin2D { get; set; }
        private IfcDirection AxisX2D { get; set; }
        private IfcDirection AxisY2D { get; set; }
        private IfcAxis2Placement2D WCS2D { get; set; }
        public bool IsStraight { get; set; }

        public AlignmentBuilder(string path)
        {
            _outputPath = path;
        }

        public void Run()
        {
            using (var model = CreateAndInitModel(_projectName))
            {
                if (model != null)
                {
                    InitWCS(model);
                    string info = "";
                    if (BuildAlignment(model, ref info))
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
                ApplicationFullName = "IFC Model Builder for Bridge",
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

        private bool BuildAlignment(IfcStore m, ref string info)
        {
            // Create spatial element to hold the created alignment
            var site = IfcModelBuilder.CreateSite(m, "场地");
            IfcAlignment alignment = null;
            if (IsStraight)
                alignment = CreateStraightAlignment(m, "主线高架道路设计中心线");
            else
                alignment = CreateAlignment(m, "SW匝道道路设计中心线");
            //var alignment = CreateStraightAlignment(m, "SW匝道道路设计中心线");
            if (alignment == null)
            {
                info = "failed to create alignment";
                return false;
            }
            IfcModelBuilder.AddProductIntoSpatial(m, site, alignment, "Add the alignment to site");            

            info = "successfully created alignment model";
            return true;
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
                    ac.Tag = "Alignment curve of the road";
                });
                var align = m.Instances.New<IfcAlignment>(a =>
                {
                    a.Name = name;
                    a.ObjectPlacement = m.Instances.New<IfcLocalPlacement>(p => p.RelativePlacement = WCS);
                    a.Axis = alignCurve;
                    var offsetCurveSolid = IfcModelBuilder.CreateSolidShapeForCurve(m, alignCurve, 0, LENGTH);
                    IfcModelBuilder.SetSurfaceStyle(m, offsetCurveSolid, 1, 0, 0);
                    var bodyShape = IfcModelBuilder.MakeShapeRepresentation(m, 3, "Body", "AdvancedSweptSolid");
                    bodyShape.Items.Add(offsetCurveSolid);
                    a.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(bodyShape));
                });

                txn.Commit();
                return align;
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
                    var offsetCurveSolid = IfcModelBuilder.CreateSolidShapeForCurve(m, alignCurve, 0, LENGTH);
                    IfcModelBuilder.SetSurfaceStyle(m, offsetCurveSolid, 1, 0, 0);
                    var bodyShape = IfcModelBuilder.MakeShapeRepresentation(m, 3, "Body", "AdvancedSweptSolid");
                    bodyShape.Items.Add(offsetCurveSolid);
                    a.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(bodyShape));
                });

                txn.Commit();
                return align;
            }
        }
    }
}
