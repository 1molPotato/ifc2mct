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
        Bridge_Construction() : this("Bridge_Alignment.ifc", "Test_Bridge.ifc")
        {
            //empty
        }

        public Bridge_Construction(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath))
            {
                var bridgeconstruction = new Create_Alignment(inputPath);
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
#region using all of functions in this class to generate the bridge
        private IfcCurve[] BridgeConstructionCurve { get; set; }
        public void build()
        {
            var para = new Technical_Demand();
            var cross = new CrossSection();
            cross.calculate_girder_parament(ref para);
            const int START = 0, END = 15000;

            InitWCS();
            var site = _model.Instances.OfType<IfcSite>().FirstOrDefault();
            if (site == null)
                throw new NotSupportedException("Input IFC file must include an instance of IfcSite");
            var alignment = _model.Instances.OfType<IfcAlignment>().FirstOrDefault();
            if (alignment == null)
                throw new NotSupportedException("Input IFC file must include an instance of IfcAlignment");

            for(int girdercount=0;girdercount<cross.lateral_offset_dis.Length;girdercount++)
            {
                Set_Bridge_Construction_Parameter(START, END, cross.lateral_offset_dis[girdercount], cross.vertical_offset_dis);
                BridgeConstructionCurve[girdercount] = AddBridgeAlignment(alignment);
            }

            _model.SaveAs(_outputPath, StorageType.Ifc);
        }
#endregion
        public void Dispose()
        {
            _model.Dispose();
        }
    }
}
