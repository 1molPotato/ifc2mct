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
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.RepresentationResource;
using Xbim.IO;

namespace XBIM_Module
{
    public class Create_Alignment
    {
        private readonly string _projectName = "xx道路中心线";
        private readonly string _outputpath = "";
        public void Create()
        {
            using (var model = CreateAndInitModel(_projectName))
            {
                if (model != null)
                {
                    InitWCS(model);
                    string info = "";
                    if (CreateAlignment(model, ref info))
                    {
                        try
                        {
                            Console.WriteLine(info);
                            model.SaveAs(_outputpath, StorageType.Ifc);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Failed to save {0}", _outputpath);
                            Console.WriteLine(e.Message);
                        }
                    }
                    else
                        Console.WriteLine("Failed to build bridge model,because {0}", info);
                }
                else
                    Console.WriteLine("Failed to initialise the model");
            }
        }

        public Create_Alignment(string path)
        {
            _outputpath = path;
        }

        /// <summary>
        /// Sets up the basic parameters any model must provide, units, ownership etc
        /// </summary>
        /// <param name="projectName"></param>
        /// <returns></returns>
        private IfcStore CreateAndInitModel(string projectname)
        {
            //first we need register essential information for the project
            var credentials = new XbimEditorCredentials
            {
                ApplicationDevelopersName = "fzm",
                ApplicationFullName = "IFC Model_Alignment for Bridge",
                ApplicationIdentifier = "",
                ApplicationVersion = "1.0",
                EditorsFamilyName = "FU",
                EditorsGivenName = "Zhongmin",
                EditorsOrganisationName = "TJU"
            };
            //create model by using method in IfcStore class,using memory mode,and IFC4x1 format
            var model = IfcStore.Create(credentials, XbimSchemaVersion.Ifc4x1, XbimStoreType.InMemoryModel);

            //begin a transition when do any change in a model
            using (var txn = model.BeginTransaction("Initialise Model"))
            {
                //add new IfcProject item to a certain container
                var project = model.Instances.New<IfcProject>
                    (p =>
                    {
                        //Set the units to SI (mm and metres)
                        p.Initialize(ProjectUnits.SIUnitsUK);
                        p.Name = projectname;
                    });
                // Now commit the changes, else they will be rolled back 
                // at the end of the scope of the using statement
                txn.Commit();
            }
            return model;
        }

        /// <summary>
        /// Initate the coordinate system

        private IfcAxis2Placement3D WCS { get; set; }
        private IfcCartesianPoint Origin3D { get; set; }
        private IfcDirection AxisX3D { get; set; }
        private IfcDirection AxisY3D { get; set; }
        private IfcDirection AxisZ3D { get; set; }
        private IfcAxis2Placement2D WCS2D { get; set; }
        private IfcCartesianPoint Origin2D { get; set; }
        private IfcDirection AxisX2D { get; set; }
        private IfcDirection AxisY2D { get; set; }

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
                    AxisZ3D = toolkit_factory.MakeDirection(m, 0, 0, 1);
                    wcs.Axis = AxisZ3D;
                    AxisX3D = toolkit_factory.MakeDirection(m, 1, 0, 0);
                    wcs.RefDirection = AxisX3D;
                    AxisY3D = toolkit_factory.MakeDirection(m, 0, 1, 0);
                }

                var context2D = m.Instances.OfType<IfcGeometricRepresentationContext>()
                    .Where(c => c.CoordinateSpaceDimension == 2)
                    .FirstOrDefault();
                if (context2D.WorldCoordinateSystem is IfcAxis2Placement2D wcs2d)
                {
                    WCS2D = wcs2d;
                    Origin2D = wcs2d.Location;
                    AxisX2D = toolkit_factory.MakeDirection(m, 1, 0);
                    wcs2d.RefDirection = AxisX2D;
                    AxisY2D = toolkit_factory.MakeDirection(m, 0, 1, 0);
                }

                txn.Commit();
            }
        }

        public bool IsStraight { get; set; }
        private bool CreateAlignment(IfcStore model, ref string info)
        {
            var site = toolkit_factory.CreateSite(model, "site");
            IfcAlignment alignment = null;
            if (IsStraight)
            {
                alignment = CreateStraightAlignment(model, "xx道路设计中心线");
            }
            else
            {
                alignment = CreateArcAlignment(model, "xx道路设计重心线（曲线）");
            }
            if (alignment == null)
            {
                info = "failed to create alignment";
                return false;
            }
            toolkit_factory.AddPrductIntoSpatial(model, site, alignment, "add positionplacement into site");

            info = "create alignment successfully";
            return true;
        }

        private IfcAlignment CreateStraightAlignment(IfcStore m, string txt)
        {
            const int LENGTH = 80000;//the total length of horizontal lineSeq 2D
            const double DIRECTION = Math.PI / 6;//the direction of horizontal lineSeq
            const int DISTANCE = 0;//the start length of verticalSeq along horizontal line
            const int LENGTH2 = 60000;//the end along horizontal
            const int HEIGHT = 15000;//the height of verticalSeq
            const double GRADIENT = 0.0;//gradient of start in verticalSeq
            using (var txn = m.BeginTransaction("Create straight Alignment"))
            {
                var alignCurve = m.Instances.New<IfcAlignmentCurve>(ac =>
                {
                    var lineSeg = m.Instances.New<IfcAlignment2DHorizontalSegment>(s =>
                    {
                        s.CurveGeometry = toolkit_factory.MakeLineSegment2D(m, Origin2D, DIRECTION, LENGTH);
                        s.TangentialContinuity = true;
                    });
                    ac.Horizontal = m.Instances.New<IfcAlignment2DHorizontal>(h => h.Segments.Add(lineSeg));
                    var versegLine = m.Instances.New<IfcAlignment2DVerSegLine>(sl =>
                    {
                        sl.StartDistAlong = DISTANCE;
                        sl.HorizontalLength = LENGTH2;
                        sl.StartHeight = HEIGHT;
                        sl.StartGradient = GRADIENT;
                        sl.TangentialContinuity = true;
                    });
                    ac.Vertical = m.Instances.New<IfcAlignment2DVertical>(v => v.Segments.Add(versegLine));
                    ac.Tag = "Road alignment curve";
                });
                var alignment = m.Instances.New<IfcAlignment>(a =>
                {
                    a.Name = txt;
                    a.Axis = alignCurve;
                    a.ObjectPlacement = m.Instances.New<IfcLocalPlacement>(p => p.RelativePlacement = WCS);
                    var sweepCurveSolid = toolkit_factory.CreateSolidShapeBaseOnCurve(m, alignCurve, 0, LENGTH);
                    toolkit_factory.SetSurfaceColor(m, sweepCurveSolid, 1, 0, 0);
                    var bodyshape = toolkit_factory.MakeShapeRepresentation(m, 3, "Body", "AdvancedSweptSolid", sweepCurveSolid);
                    a.Representation = m.Instances.New<IfcProductDefinitionShape>(pd =>
                    {
                        pd.Representations.Add(bodyshape);
                    });
                });

                txn.Commit();
                return alignment;
            }
        }

        private IfcAlignment CreateArcAlignment(IfcStore m, string txt)
        {
            const int LENGTH = 150000;
            const double DIRECTION = 0;
            const int RADIUS = 150000;
            bool ISCCW = true;

            const int DISTANCE = 0;
            const double LENGTH2 = 150000;
            const double HEIGHT = 15000;
            const double GRADIENT = 0.02;

            using (var txn = m.BeginTransaction("CreateArcAlignment"))
            {
                var alignCurve = m.Instances.New<IfcAlignmentCurve>(ac =>
                {
                    var alignment2Dhorizontal = m.Instances.New<IfcAlignment2DHorizontal>(az =>
                    {
                        var horizontalSeq = m.Instances.New<IfcAlignment2DHorizontalSegment>(hs =>
                        {
                            hs.TangentialContinuity = true;
                            hs.CurveGeometry = toolkit_factory.MakeCircleSeqment2D(m, Origin2D, DIRECTION, LENGTH, RADIUS, ISCCW);
                        });
                        az.Segments.Add(horizontalSeq);
                    });
                    ac.Horizontal = alignment2Dhorizontal;

                    var alignment2Dvertical = m.Instances.New<IfcAlignment2DVertical>(av =>
                    {
                        var alignment2DverSeqLine = m.Instances.New<IfcAlignment2DVerSegLine>(sl =>
                        {
                            sl.TangentialContinuity = true;
                            sl.StartDistAlong = DISTANCE;
                            sl.HorizontalLength = LENGTH2;
                            sl.StartHeight = HEIGHT;
                            sl.StartGradient = GRADIENT;
                        });
                        av.Segments.Add(alignment2DverSeqLine);
                    });
                    ac.Vertical = alignment2Dvertical;
                });
                var align = m.Instances.New<IfcAlignment>(a =>
                {
                    a.Axis = alignCurve;
                    a.Name = txt;
                    a.ObjectPlacement = m.Instances.New<IfcLocalPlacement>(lp => lp.RelativePlacement = WCS);

                    var shaperepresentation = m.Instances.New<IfcShapeRepresentation>();
                    shaperepresentation.ContextOfItems = m.Instances.OfType<IfcGeometricRepresentationContext>().
                    Where(c => c.CoordinateSpaceDimension == 3).FirstOrDefault();
                    shaperepresentation.RepresentationIdentifier = "Body";
                    shaperepresentation.RepresentationType = "AdvancedSweptSolid";

                    //the items of shaperepresentation ouught to be IfcGeometricalRepresentationItem
                    //so create IfcGeometricalRepresentationItem

                    var sectionsolidhorizontal = m.Instances.New<IfcSectionedSolidHorizontal>();
                    sectionsolidhorizontal.Directrix = alignCurve;
                    var crosssection = toolkit_factory.MakeCircleProfile(m, 40);
                    sectionsolidhorizontal.CrossSections.Add(crosssection);
                    sectionsolidhorizontal.CrossSections.Add(crosssection);
                    sectionsolidhorizontal.CrossSectionPositions.Add(toolkit_factory.MakeDistanceExpresstion(m, DISTANCE));
                    sectionsolidhorizontal.CrossSectionPositions.Add(toolkit_factory.MakeDistanceExpresstion(m, LENGTH));
                    sectionsolidhorizontal.FixedAxisVertical = true;

                    var styleitem = m.Instances.New<IfcStyledItem>(si =>
                    {
                        si.Item = sectionsolidhorizontal;
                        var surfacestyle = m.Instances.New<IfcSurfaceStyle>(ss =>
                        {
                            var styleshading = m.Instances.New<IfcSurfaceStyleRendering>(ssr =>
                            {
                                var surfacecolor = m.Instances.New<IfcColourRgb>(cr =>
                                {
                                    cr.Red = 0;
                                    cr.Green = 1;
                                    cr.Blue = 0;
                                });
                                ssr.SurfaceColour = surfacecolor;
                                ssr.Transparency = 0;
                            });
                            ss.Side = IfcSurfaceSide.POSITIVE;
                            ss.Styles.Add(styleshading);
                        });
                        si.Styles.Add(surfacestyle);
                    });
                    shaperepresentation.Items.Add(sectionsolidhorizontal);

                    a.Representation = m.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shaperepresentation));

                    /*IfcProductRepresentation PR = null;
                    IfcShapeRepresentation rep = null;
                    IfcSectionedSolidHorizontal ssh = null;
                    ssh=toolkit_factory.CreateSolidShapeBaseOnCurve(m, alignCurve, 0, LENGTH);
                    toolkit_factory.SetSurfaceColor(m, ssh, 0, 1, 0);
                    rep.Items.Add(ssh);
                    IfcGeometricRepresentationContext geocontext = null;
                    geocontext.CoordinateSpaceDimension = 3;
                    rep.ContextOfItems = geocontext;
                    rep.RepresentationIdentifier = "Body";
                    rep.RepresentationType = "AdvancedSweptSolid";
                    PR.Representations.Add(rep);
                    var PR = m.Instances.New<IfcShapeRepresentation>();
                    m.Instances.Where(c => c is IfcProductRepresentation).FirstOrDefault();
                    m.Instances.Where(c => c is IfcShapeRepresentation).FirstOrDefault();
                    a.Representation = PR;*/
                });
                txn.Commit();
                return align;
            }
        }
    }
}
