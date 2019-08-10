using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
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
using StructureDesignModule;


namespace XBIM_Module
{
    public class Bridge_Construction:IDisposable
    {
        private readonly string _outputPath = "";
        //       private readonly string _projectName = "xx工程";
        private readonly string _bridgeName = "xx钢板梁桥";
        private readonly IfcStore _model;//using external Alignment model as refenrence

        //Constructors
        Bridge_Construction() : this("../../TestFiles/alignment.ifc", "../../TestFiles/aligment&construction.ifc")
        {
            //empty
        }

        public Bridge_Construction(string inputPath,string outputPath)
        {
            if (!File.Exists(inputPath))
            {
                var bridgeconstruction = new Create_Alignment(inputPath) { IsStraight = true };
                bridgeconstruction.Create();
            }
            //use readonly IfcStore _model(created or existed)
            _model = IfcStore.Open(inputPath);

            _outputPath = outputPath;
        }
#region get offset curve
        //using the following arguments as AddBridgeAlinment function
        private double BridgeStart { get; set; }
        private double BridgeEnd { get; set; }
        private double VerticalOffsetValue { get; set; }
        private double LateralOffsetValue { get; set; }

        public void Set_Bridge_Construction_Parameter(double start,double end,double verOffset,double latOffset)
        {
            BridgeStart = start;
            BridgeEnd = end;
            VerticalOffsetValue = verOffset;
            LateralOffsetValue = latOffset;
        }

        public IfcOffsetCurveByDistances AddBridgeAlignment(IfcAlignment mainAlignment)
        {
            using (var txn = this._model.BeginTransaction("Add Offset Curve"))
            {
                var offsetCurve = this._model.Instances.New<IfcOffsetCurveByDistances>(cbd =>
                {
                    cbd.BasisCurve = mainAlignment.Axis;
                    cbd.OffsetValues.Add(toolkit_factory.MakeDistanceExpresstion(_model, BridgeStart, LateralOffsetValue, VerticalOffsetValue));
                    cbd.OffsetValues.Add(toolkit_factory.MakeDistanceExpresstion(_model, BridgeEnd, LateralOffsetValue, VerticalOffsetValue));
                    cbd.Tag = "sturcture position curve";
                });
                var offsetCurveSolid = toolkit_factory.CreateSolidShapeBaseOnCurve(_model, offsetCurve, 0, BridgeEnd - BridgeStart);
                toolkit_factory.SetSurfaceColor(_model, offsetCurveSolid, 0, 1, 0);
                mainAlignment.Representation.Representations.FirstOrDefault(r => r.RepresentationIdentifier == "Body").Items.Add(offsetCurveSolid);

                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "StructureLine", "OffsetCurves", offsetCurveSolid);
                mainAlignment.Representation.Representations.Add(shape);

                txn.Commit();
                return offsetCurve;
            }
        }
        #endregion

        #region initiate the document's coordinate

        private IfcCartesianPoint Origin3D { get; set; }
        private IfcDirection AxisX3D { get; set; }
        private IfcDirection AxisY3D { get; set; }
        private IfcDirection AxisZ3D { get; set; }
        private IfcAxis2Placement3D WCS { get; set; }
        private IfcCartesianPoint Origin2D { get; set; }
        private IfcDirection AxisX2D { get; set; }
        private IfcDirection AxisY2D { get; set; }
        private IfcAxis2Placement2D WCS2D { get; set; }
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
                    AxisZ3D = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    wcs.Axis = AxisZ3D;
                    AxisX3D = toolkit_factory.MakeDirection(_model, 1, 0, 0);
                    wcs.RefDirection = AxisX3D;
                    AxisY3D = toolkit_factory.MakeDirection(_model, 0, 1, 0);
                }

                var context2D = this._model.Instances.OfType<IfcGeometricRepresentationContext>()
                    .Where(c => c.CoordinateSpaceDimension == 2)
                    .FirstOrDefault();
                if (context2D.WorldCoordinateSystem is IfcAxis2Placement2D wcs2d)
                {
                    WCS2D = wcs2d;
                    Origin2D = wcs2d.Location;
                    AxisX2D = toolkit_factory.MakeDirection(_model, 1, 0);
                    wcs2d.RefDirection = AxisX2D;
                    AxisY2D = toolkit_factory.MakeDirection(_model, 0, 1);
                }

                txn.Commit();
            }
        }
        #endregion
        #region generate SteelGirder

        //the dictionary refer to the platecode 
        //consider the thickness may change along the bridge construction curve
        //using nuget tuplegroup
        private Dictionary<int, List<(double distanceAlong, double thickness)>> PlateThicknessLists { get; set; }

        //using var bridgeStart as start position  defined in get offset curve
        //define SectionParams for user use
        private List<double> SectionParams { get; set; }

        public void SetOverallSection(List<double> parameters)
        {
            if (parameters.Count == 3)
                SectionParams = parameters;
            else
            {
                throw new ArgumentException("The overall section dimensions should be {B1, B2, H}");
            }
        }

        public Dictionary<int,List<(double distanceAlong,double thickness)>> CreatePlateThicknessAlongDistance(int distanceAlongStart,
            int distanceAlongEnd,List<double> thicknessLists)
        {
            //Create PlateListsThickness also 0 for upper,1for web,2 for lower
            var PlateListsThickness = new Dictionary<int, List<(double distanceAlong, double thickness)>>();
            for(int i=0;i<3;i++)
            {
                var PairItem = new List<(double distanceAlong, double thickness)>();
                PairItem.Add((distanceAlongStart, thicknessLists[i]));
                PairItem.Add((distanceAlongEnd, thicknessLists[i]));
                PlateListsThickness[i]=PairItem;
            }
            return PlateListsThickness;
        }

        public void SetPlateThicknessLists(Dictionary<int, List<(double distanceAlong, double thickness)>> plateThicknessLists)
        {
            PlateThicknessLists = plateThicknessLists;
        }

        private IfcElementAssembly CreateSteelGirder(IfcCurve directrix)
        {
            using (var txn = this._model.BeginTransaction("Create SteelGirder"))
            {
                var girder = this._model.Instances.New<IfcElementAssembly>(elem =>
                  {
                      elem.Name = _bridgeName;
                      elem.Description = "SteelGirder";
                      elem.PredefinedType = IfcElementAssemblyTypeEnum.GIRDER;
                  });

                //暂时不添加材料特性
                // 0 for upperflange,1 for web, 2 for lower flange
                var platesCodes = new List<int>() { 0, 1, 2 };
                var plates = platesCodes.Select(c => CreateIPlate(directrix, c)).ToList();

                //加劲和横隔板暂时不添加

                //Aggregate flanges and webs into girder assembly
                var relAggregates = this._model.Instances.New<IfcRelAggregates>(r =>
                {
                    r.RelatingObject = girder;
                    r.RelatedObjects.AddRange(plates);
                });
                txn.Commit();
                return girder;
            }
        }

        private IfcElementAssembly CreateIPlate(IfcCurve diretrix, int plateCode)
        {
            var plateAssembly = this._model.Instances.New<IfcElementAssembly>(IEA =>
              {
                  IEA.Name = plateCode == 0 ? "顶板" : (plateCode == 1 ? "腹板" : "底板");
                  IEA.ObjectType = (plateCode == 0 || plateCode == 2) ? "FLANGE-ASSEMBLY" : "WEB-ASSEMBLY";
                  IEA.PredefinedType = IfcElementAssemblyTypeEnum.USERDEFINED;
              });
            //define a relAggregates to represente the plate assembly
            var relAggregate = this._model.Instances.New<IfcRelAggregates>(rg => rg.RelatingObject = plateAssembly);

            //information for location
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0, t = 0, offLateral = 0, offVertical = 0;

            var thicknessList = PlateThicknessLists[plateCode];
            var StartDistanceAlong = BridgeStart;

            for(int i=0;i<thicknessList.Count-1;i++)
            {
                var start = thicknessList[i].distanceAlong;
                var end = thicknessList[i+1].distanceAlong;
                var parametersNames = new List<string>() { "B1","B2","H" };
                var parameters = new Dictionary<string, double>();
                for(int j=0;j<SectionParams.Count;j++)
                {
                    parameters[parametersNames[j]] = SectionParams[j];
                }

                switch(plateCode)
                {
                    case 0:
                        x1 = -parameters["B1"] / 2;
                        y1 = -thicknessList[i].thickness / 2;
                        x2 = -x1;
                        y2 = y1;
                        t = thicknessList[i].thickness;
                        break;
                    case 1:
                        x1 = 0;
                        y1 = 0;
                        x2 = -parameters["H"];
                        y2 = 0;
                        t = thicknessList[i].thickness;
                        break;
                    case 2:
                        x1 = parameters["B2"] / 2;
                        y1 = -parameters["H"];
                        x2 = -x1;
                        y2 = y1;
                        t = thicknessList[i].thickness;
                        break;
                    default:
                        break;
                }
                var solid = this._model.Instances.New<IfcSectionedSolidHorizontal>(ssh =>
                  {
                      ssh.Directrix = diretrix;
                      var p1 = toolkit_factory.MakeCartesianPoint(_model, x1, y1);
                      var p2 = toolkit_factory.MakeCartesianPoint(_model, x2, y2);
                      var profile = toolkit_factory.MakeCenterLineProfile(_model, p1, p2, t);
                      ssh.CrossSections.Add(profile);
                      ssh.CrossSections.Add(profile);
                      var posStart = toolkit_factory.MakeDistanceExpresstion(_model, start, offLateral, offVertical);
                      var posEnd = toolkit_factory.MakeDistanceExpresstion(_model, end, offLateral, offVertical);
                      ssh.CrossSectionPositions.Add(posStart);
                      ssh.CrossSectionPositions.Add(posEnd);
                  });
                //now the geometry data already
                //using this data to rendering
                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweepSolid",solid);
                //update the startDistanceAlong
                //StartDistanceAlong = BridgeEnd;

                var plate = this._model.Instances.New<IfcPlate>(p =>
                  {
                      p.Name = $"{plateAssembly.Name}-0{i + 1}";
                      p.ObjectType = (plateCode == 0 || plateCode == 2) ? "FLANGE-PLATE" : "WEB-PLATE";
                      p.Representation = this._model.Instances.New<IfcProductDefinitionShape>(
                          pd => pd.Representations.Add(shape));
                      p.PredefinedType = IfcPlateTypeEnum.USERDEFINED;
                  });

                relAggregate.RelatedObjects.Add(plate);
            }

            return plateAssembly;
        }

        #endregion
        #region using all of functions in this class to generate the bridge     


        public void build()
        {
            var para = new Technical_Demand();
            var cross = new CrossSection();
            var BridgeConstructionCurve = new List<IfcCurve>();
            cross.calculate_girder_parament(ref para);
            const int START = 30000, END = 60000;

            InitWCS();
            var site = _model.Instances.OfType<IfcSite>().FirstOrDefault();
            if (site == null)
                throw new NotSupportedException("Input IFC file must include an instance of IfcSite");
            var alignment = _model.Instances.OfType<IfcAlignment>().FirstOrDefault();
            if (alignment == null)
                throw new NotSupportedException("Input IFC file must include an instance of IfcAlignment");
            //Create BridgeAlignment
            for (int girdercount = 0; girdercount < cross.lateral_offset_dis.Length; girdercount++)
            {
                Set_Bridge_Construction_Parameter(START, END, cross.vertical_offset_dis * -1, cross.lateral_offset_dis[girdercount] * 1000);
                BridgeConstructionCurve.Add(AddBridgeAlignment(alignment));
            }
            //set overall Section Parameters
            var SectionParams = cross.CreateSectionParams();
            SetOverallSection(SectionParams);

            //set thickness along Alignment
            var SectionPlateThickness = cross.GetThickness();
            var SectionPlateThicknessAlongDistance = CreatePlateThicknessAlongDistance(0, 30000, SectionPlateThickness);
            SetPlateThicknessLists(SectionPlateThicknessAlongDistance);
            var girder = CreateSteelGirder(BridgeConstructionCurve[1]);

            toolkit_factory.AddPrductIntoSpatial(_model, site, girder, "Add Girder to site");

            _model.SaveAs(_outputPath, StorageType.Ifc);
        }
        #endregion
        public void Dispose()
        {
            _model.Dispose();
        }
    }
}
