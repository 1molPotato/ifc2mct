using ifc2mct.BridgeFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.ProductExtension;

namespace ifc2mct.Tests
{
    /// <summary>
    /// Summary description for BridgeFactoryTest
    /// </summary>
    [TestClass]
    public class BridgeBuilderTest
    {
        const string INPATH = "../../TestFiles/alignment.ifc";
        public BridgeBuilderTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void BuildBridgeTest()
        {
            //const string INPATH = "../../TestFiles/alignment.ifc";
            const string OUTPATH = "../../TestFiles/completed-bridge.ifc";
            // bridge data
            // overall
            const int START = 20000, END = 92302, VEROFFSET = -200, LATOFFSET = 0, STARTGAP = 40, ENDGAP = 40;
            var secDimensions = new List<double>() { 1750, 6400, 50, 5550, 1968 };
            var thicknessLists = new List<List<(double, double)>>()
            {
                new List<(double, double)>() { (28000, 16), (44000, 20), (72262, 16) }, 
                new List<(double, double)>() { (6000, 16), (28000, 14), (33000, 16), (39000, 20), (44000, 16), (66302, 14), (72262, 16) },
                new List<(double, double)>() { (28000, 16), (33000, 20), (39000, 25), (44000, 20), (72262, 16)}
            };
            // stiffeners
            var stiffenerTypeTable = new Dictionary<int, List<double>>()
            {
                { 1, new List<double>(){ 160, 14 } }, // flat stiffener
                { 2, new List<double>(){ 190, 16 } }, // flat stiffener
                { 3, new List<double>(){ 240, 20 } }, // flat stiffener
                { 4, new List<double>(){ 400, 16 } }, // flat stiffener
                { 5, new List<double>(){ 280, 300, 170, 8, 40 } }, // U-shape stiffener
                { 6, new List<double>(){ 400, 16 } } // edge plate
            };
            var stiffenerLayoutTable = new Dictionary<int, List<(int num, double gap)>>()
            {
                { 1, new List<(int, double)>() { (2, 250), (1, 8900), (1, 250) } },
                { 2, new List<(int, double)>() { (1, 870), (1, 530), (1, 850), (9, 600), (1, 850), (1, 530) } },
                { 3, new List<(int, double)>() { (/*1, 450), (2*/3, 400), (7, 450), (2, 400) } },
                { 4, new List<(int, double)>() { (1, 400), (1, 1212) } },
                { 5, new List<(int, double)>() { (1, 8), (1, 9884)} }
            };
            var stiffenerLists = new List<(int parentId, List<(double distanceAlong, int typeId, int layoutId)> stiffList)>()
            {
                { (0, new List<(double, int, int)>() { (72262, 2, 1) }) }, // stiffeners on top flange
                { (0, new List<(double, int, int)>() { (72262, 5, 2) }) }, // stiffeners on top flange
                { (1, new List<(double, int, int)>() { (33000, 1, 4), (39000, 2, 4), (72262, 1, 4) }) }, // stiffeners on left web
                { (2, new List<(double, int, int)>() { (33000, 1, 4), (39000, 2, 4), (72262, 1, 4) }) }, // stiffeners on right web
                { (3, new List<(double, int, int)>() { (28000, 2, 3), (44000, 3, 3), (72262, 2, 3) }) }, // stiffeners on bottom flange
                { (0, new List<(double, int, int)>() { (72262, 6, 5) }) } // edge plate on top flange
            };
            // bearings
            // TODO
            var bearingTypeTable = new Dictionary<int, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)>()
            {
                {1, (true, true, false) }, // dual-fixed (non-guided)
                {2, (false, true, false) }, // longitudinal-fixed (tranversely guided)
                {3, (true, false, true) }, // lateral-fixed (longitudinally guided), pull-resistant
                {4, (false, false, true) } // non-fixed (dual-guided), pull-resistant
            };
            var bearingList = new List<(double distanceAlong, double offsetLateral, int bearingTypeId)>()
            {
                (590, 1300, 3), (590, -1700, 4), (36000, 1500, 1), (36000, -1500, 2), (71712, 1400, 3), (71712, -1900, 4)
            };

            // cross bracing
            var diaphragmTypeTable = new Dictionary<int, double>()
            {
                {1, 20}, {2, 12}, {3, 20}, {4, 25}
            };
            var diaphragmList = new List<(int typeId, int num, double gap)>()
            {
                (1, 1, 550), (2, 1, 1410), (2, 10, 3000), (3, 1, 3000), (4, 1, 1000), (3, 1, 1000), (2, 10, 3000), (2, 1, 1500), (2, 1, 1606), (1, 1, 1606)
            };

            // build bridge components
            using (var builder = new BridgeBuilder(INPATH, OUTPATH))
            {
                builder.SetBridgeAlignment(START, END, VEROFFSET, LATOFFSET);
                builder.SetGaps(STARTGAP, ENDGAP);
                builder.SetOverallSection(secDimensions);
                builder.SetThicknesses(thicknessLists);
                foreach (var stiffenerType in stiffenerTypeTable)
                    builder.AddStiffenerType(stiffenerType.Key, stiffenerType.Value);
                foreach (var stiffenerLayout in stiffenerLayoutTable)
                    builder.AddStiffenerLayout(stiffenerLayout.Key, stiffenerLayout.Value);
                foreach (var (parentId, stiffList) in stiffenerLists)
                    builder.AddStiffeners(parentId, stiffList);
                foreach (var bearingType in bearingTypeTable)
                    builder.AddBearingType(bearingType.Key, bearingType.Value);
                foreach (var bearing in bearingList)
                    builder.AddBearing(bearing);
                foreach (var diaphragmType in diaphragmTypeTable)
                    builder.AddDiaphragmType(diaphragmType.Key, diaphragmType.Value);
                builder.AddDiaphragm(diaphragmList);
                builder.Build();
            }                
        }

        [TestMethod]
        public void BuildDoubleBoxGirderBridgeTest()
        {
            const string ROADFILE = "../../TestFiles/alignment-straight.ifc";
            const string OUTPATH = "../../TestFiles/double-box-girder-1.ifc";
            const string OUTPATH2 = "../../TestFiles/double-box-girder-2.ifc";
            // bridge data
            // overall
            const int START = 20000, END = 70000, VEROFFSET = -200, LATOFFSET = 3500, STARTGAP = 40, ENDGAP = 40;
            var secDimensions = new List<double>() { 1500, 4000, 50, 4000, 2200 };
            var thicknessLists = new List<List<(double, double)>>()
            {
                new List<(double, double)>() { (50000, 16)},
                new List<(double, double)>() { (50000, 14)},
                new List<(double, double)>() { (50000, 16)}
            };

            // stiffeners
            var stiffenerTypeTable = new Dictionary<int, List<double>>()
            {
                { 1, new List<double>(){ 160, 14 } },
                { 2, new List<double>(){ 400, 16 } }
            };
            var stiffenerLayoutTable = new Dictionary<int, List<(int num, double gap)>>()
            {
                { 1, new List<(int, double)>() { (1, 400), (3, 300), (1, 400), (11, 300), (1, 400), (3, 300) } },
                //{ 2, new List<(int, double)>() { (1, 870), (1, 530), (1, 850), (9, 600), (1, 850), (1, 530) } },
                { 4, new List<(int, double)>() { (1, 300), (11, 300) } },
                { 3, new List<(int, double)>() { (1, 400), (1, 550) } },
                { 2, new List<(int, double)>() { (1, 100), (1, 6800)} }
                
            };
            var stiffenerLists = new List<(int parentId, List<(double distanceAlong, int typeId, int layoutId)> stiffList)>()
            {
                { (0, new List<(double, int, int)>() { (50000, 1, 1) }) }, // stiffeners on top flange
                { (1, new List<(double, int, int)>() { (50000, 1, 3) }) }, // stiffeners on left web
                { (2, new List<(double, int, int)>() { (50000, 1, 3) }) }, // stiffeners on right web
                { (3, new List<(double, int, int)>() { (50000, 1, 4) }) }, // stiffeners on bottom flange
                { (0, new List<(double, int, int)>() { (50000, 2, 2) }) } // edge plate on top flange
            };

            // bearings
            var bearingTypeTable = new Dictionary<int, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)>()
            {
                {1, (true, true, false) }, // dual-fixed (non-guided)
                {2, (false, true, false) }, // longitudinal-fixed (tranversely guided)
                {3, (true, false, true) }, // lateral-fixed (longitudinally guided), pull-resistant
                {4, (false, false, true) } // non-fixed (dual-guided), pull-resistant
            };
            var bearingList = new List<(double distanceAlong, double offsetLateral, int bearingTypeId)>()
            {
                (500, 1300, 3), (500, -1300, 4), (49500, 1300, 1), (49500, -1400, 1)
            };

            // build bridge components
            using (var builder = new BridgeBuilder(ROADFILE, OUTPATH))
            {
                builder.SetBridgeAlignment(START, END, VEROFFSET, LATOFFSET);
                builder.SetGaps(STARTGAP, ENDGAP);
                // flanges and webs
                builder.SetOverallSection(secDimensions);
                builder.SetThicknesses(thicknessLists);
                // ribs
                foreach (var stiffenerType in stiffenerTypeTable)
                    builder.AddStiffenerType(stiffenerType.Key, stiffenerType.Value);
                foreach (var stiffenerLayout in stiffenerLayoutTable)
                    builder.AddStiffenerLayout(stiffenerLayout.Key, stiffenerLayout.Value);
                foreach (var (parentId, stiffList) in stiffenerLists)
                    builder.AddStiffeners(parentId, stiffList);
                // bearings
                foreach (var bearingType in bearingTypeTable)
                    builder.AddBearingType(bearingType.Key, bearingType.Value);
                foreach (var bearing in bearingList)
                    builder.AddBearing(bearing);
                builder.Build();
            }            

            const int LATOFFSET2 = -3500;
            // build bridge components
            using (var builder2 = new BridgeBuilder(OUTPATH, OUTPATH2))
            {                
                builder2.SetBridgeAlignment(START, END, VEROFFSET, LATOFFSET2);
                builder2.SetGaps(STARTGAP, ENDGAP);
                // flanges and webs
                builder2.SetOverallSection(secDimensions);
                builder2.SetThicknesses(thicknessLists);
                // ribs
                foreach (var stiffenerType in stiffenerTypeTable)
                    builder2.AddStiffenerType(stiffenerType.Key, stiffenerType.Value);
                foreach (var stiffenerLayout in stiffenerLayoutTable)
                    builder2.AddStiffenerLayout(stiffenerLayout.Key, stiffenerLayout.Value);
                foreach (var (parentId, stiffList) in stiffenerLists)
                    builder2.AddStiffeners(parentId, stiffList);
                // bearings
                foreach (var bearingType in bearingTypeTable)
                    builder2.AddBearingType(bearingType.Key, bearingType.Value);
                foreach (var bearing in bearingList)
                    builder2.AddBearing(bearing);
                builder2.Build();
            }                
        }

        [TestMethod]
        public void BuildBoxGirderTest()
        {
            const string OUTPATH = "../../TestFiles/box-girder.ifc";
            // bridge data
            // overall
            const int START = 20000, END = 92302, VEROFFSET = -200, LATOFFSET = 0, STARTGAP = 40, ENDGAP = 40;
            var secDimensions = new List<double>() { 1750, 6400, 50, 5550, 1968 };
            var thicknessLists = new List<List<(double, double)>>()
            {
                new List<(double, double)>() { (28000, 16), (44000, 20), (72262, 16) },
                new List<(double, double)>() { (6000, 16), (28000, 14), (33000, 16), (39000, 20), (44000, 16), (66302, 14), (72262, 16) },
                new List<(double, double)>() { (28000, 16), (33000, 20), (39000, 25), (44000, 20), (72262, 16)}
            };

            // bearings
            var bearingTypeTable = new Dictionary<int, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)>()
            {
                {1, (true, true, false) }, // dual-fixed (non-guided)
                {2, (false, true, false) }, // longitudinal-fixed (tranversely guided)
                {3, (true, false, true) }, // lateral-fixed (longitudinally guided), pull-resistant
                {4, (false, false, true) } // non-fixed (dual-guided), pull-resistant
            };
            var bearingList = new List<(double distanceAlong, double offsetLateral, int bearingTypeId)>()
            {
                (590, 1300, 3), (590, -1700, 4), (36000, 1500, 1), (36000, -1500, 2), (71752, 1400, 3), (71752, -1900, 4)
            };

            // build bridge components
            using (var builder = new BridgeBuilder(INPATH, OUTPATH))
            {
                builder.SetBridgeAlignment(START, END, VEROFFSET, LATOFFSET);
                builder.SetGaps(STARTGAP, ENDGAP);
                builder.SetOverallSection(secDimensions);
                builder.SetThicknesses(thicknessLists);
                foreach (var bearingType in bearingTypeTable)
                    builder.AddBearingType(bearingType.Key, bearingType.Value);
                foreach (var bearing in bearingList)
                    builder.AddBearing(bearing);
                builder.Build();
            }                
        }

        [TestMethod]
        public void BuildBearingsTest()
        {
            const string OUTPATH = "../../TestFiles/bearings.ifc";
            // bridge data
            // overall
            const int START = 20000, END = 92302, VEROFFSET = -200, LATOFFSET = 0, STARTGAP = 40, ENDGAP = 40;
            var secDimensions = new List<double>() { 1750, 6400, 50, 5550, 1968 };
            var thicknessLists = new List<List<(double, double)>>()
            {
                new List<(double, double)>(),
                new List<(double, double)>(),
                new List<(double, double)>() 
            };

            // bearings
            var bearingTypeTable = new Dictionary<int, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)>()
            {
                {1, (true, true, false) }, // dual-fixed (non-guided)
                {2, (false, true, false) }, // longitudinal-fixed (tranversely guided)
                {3, (true, false, true) }, // lateral-fixed (longitudinally guided), pull-resistant
                {4, (false, false, true) } // non-fixed (dual-guided), pull-resistant
            };
            var bearingList = new List<(double distanceAlong, double offsetLateral, int bearingTypeId)>()
            {
                (590, 1300, 3), (590, -1700, 4), (36000, 1500, 1), (36000, -1500, 2), (71752, 1400, 3), (71752, -1900, 4)
            };

            // build bridge components
            using (var builder = new BridgeBuilder(INPATH, OUTPATH))
            {
                builder.SetBridgeAlignment(START, END, VEROFFSET, LATOFFSET);
                builder.SetGaps(STARTGAP, ENDGAP);
                builder.SetOverallSection(secDimensions);
                builder.SetThicknesses(thicknessLists);
                foreach (var bearingType in bearingTypeTable)
                    builder.AddBearingType(bearingType.Key, bearingType.Value);
                foreach (var bearing in bearingList)
                    builder.AddBearing(bearing);
                builder.Build();
            }                
        }

        [TestMethod]
        public void BuildStiffenersTest()
        {
            const string OUTPATH = "../../TestFiles/top-flange-with-stiffeners.ifc";
            // bridge data
            // overall
            const int START = 20000, END = 92302, VEROFFSET = -200, LATOFFSET = 0, STARTGAP = 40, ENDGAP = 40;
            var secDimensions = new List<double>() { 1750, 6400, 50, 5550, 1968 };
            var thicknessLists = new List<List<(double, double)>>()
            {
                new List<(double, double)>() { (28000, 16), (44000, 20), (72262, 16) },
                new List<(double, double)>(),
                new List<(double, double)>()
            };

            // stiffeners
            var stiffenerTypeTable = new Dictionary<int, List<double>>()
            {
                { 1, new List<double>(){ 280, 300, 170, 8, 40 } }, // U-shape stiffener
                {2, new List<double>(){ 250, 280, 160, 8, 35} }
            };
            var stiffenerLayoutTable = new Dictionary<int, List<(int num, double gap)>>()
            {               
                { 1, new List<(int, double)>() { (1, 870), (1, 530), (1, 850), (9, 600), (1, 850), (1, 530) } }
            };
            var stiffenerLists = new List<(int parentId, List<(double distanceAlong, int typeId, int layoutId)> stiffList)>()
            {
                { (0, new List<(double, int, int)>() { (33000, 1, 1), (39000, 2, 1), (72262, 1, 1) }) } // stiffeners on top flange
            };

            // build bridge components
            using (var builder = new BridgeBuilder(INPATH, OUTPATH))
            {
                builder.SetBridgeAlignment(START, END, VEROFFSET, LATOFFSET);
                builder.SetGaps(STARTGAP, ENDGAP);
                builder.SetOverallSection(secDimensions);
                builder.SetThicknesses(thicknessLists);

                foreach (var stiffenerType in stiffenerTypeTable)
                    builder.AddStiffenerType(stiffenerType.Key, stiffenerType.Value);
                foreach (var stiffenerLayout in stiffenerLayoutTable)
                    builder.AddStiffenerLayout(stiffenerLayout.Key, stiffenerLayout.Value);
                foreach (var (parentId, stiffList) in stiffenerLists)
                    builder.AddStiffeners(parentId, stiffList);

                builder.Build();
            }                                
        }

        [TestMethod]
        public void BuildDiaphragmsTest()
        {
            const string OUTPATH = "../../TestFiles/diaphragms.ifc";
            // bridge data
            // overall
            const int START = 20000, END = 92302, VEROFFSET = -200, LATOFFSET = 0, STARTGAP = 40, ENDGAP = 40;
            var secDimensions = new List<double>() { 1750, 6400, 50, 5550, 1968 };
            var thicknessLists = new List<List<(double, double)>>()
            {
                new List<(double, double)>() ,
                new List<(double, double)>() ,
                new List<(double, double)>() 
            };
            // stiffeners
            var stiffenerTypeTable = new Dictionary<int, List<double>>()
            {
                { 1, new List<double>(){ 160, 14 } }, // flat stiffener
                { 2, new List<double>(){ 190, 16 } }, // flat stiffener
                { 3, new List<double>(){ 240, 20 } }, // flat stiffener
                { 4, new List<double>(){ 400, 16 } }, // flat stiffener
                { 5, new List<double>(){ 280, 300, 170, 8, 40 } }, // U-shape stiffener
                { 6, new List<double>(){ 400, 16 } } // edge plate
            };
            var stiffenerLayoutTable = new Dictionary<int, List<(int num, double gap)>>()
            {
                { 1, new List<(int, double)>() { (2, 250), (1, 8900), (1, 250) } },
                { 2, new List<(int, double)>() { (1, 870), (1, 530), (1, 850), (9, 600), (1, 850), (1, 530) } },
                { 3, new List<(int, double)>() { (/*1, 450), (2*/3, 400), (7, 450), (2, 400) } },
                { 4, new List<(int, double)>() { (1, 400), (1, 1212) } },
                { 5, new List<(int, double)>() { (1, 8), (1, 9884)} }
            };
            var stiffenerLists = new List<(int parentId, List<(double distanceAlong, int typeId, int layoutId)> stiffList)>()
            {
                { (0, new List<(double, int, int)>() { (72262, 2, 1) }) }, // stiffeners on top flange
                { (0, new List<(double, int, int)>() { (72262, 5, 2) }) }, // stiffeners on top flange
                { (1, new List<(double, int, int)>() { (33000, 1, 4), (39000, 2, 4), (72262, 1, 4) }) }, // stiffeners on left web
                { (2, new List<(double, int, int)>() { (33000, 1, 4), (39000, 2, 4), (72262, 1, 4) }) }, // stiffeners on right web
                { (3, new List<(double, int, int)>() { (28000, 2, 3), (44000, 3, 3), (72262, 2, 3) }) }, // stiffeners on bottom flange
                { (0, new List<(double, int, int)>() { (72262, 6, 5) }) } // edge plate on top flange
            };

            // cross bracing
            var diaphragmTypeTable = new Dictionary<int, double>()
            {
                {1, 20}, {2, 12}, {3, 20}, {4, 25}
            };
            var diaphragmList = new List<(int typeId, int num, double gap)>()
            {
                (1, 1, 550), (2, 1, 15000)
            };

            // build bridge components
            using (var builder = new BridgeBuilder(INPATH, OUTPATH))
            {
                builder.SetBridgeAlignment(START, END, VEROFFSET, LATOFFSET);
                builder.SetGaps(STARTGAP, ENDGAP);
                builder.SetOverallSection(secDimensions);
                builder.SetThicknesses(thicknessLists);

                foreach (var stiffenerType in stiffenerTypeTable)
                    builder.AddStiffenerType(stiffenerType.Key, stiffenerType.Value);
                foreach (var stiffenerLayout in stiffenerLayoutTable)
                    builder.AddStiffenerLayout(stiffenerLayout.Key, stiffenerLayout.Value);
                foreach (var (parentId, stiffList) in stiffenerLists)
                    builder.AddStiffeners(parentId, stiffList);

                foreach (var diaphragmType in diaphragmTypeTable)
                    builder.AddDiaphragmType(diaphragmType.Key, diaphragmType.Value);
                builder.AddDiaphragm(diaphragmList);
                builder.Build();
            }                                
        }

    }

    [TestClass]
    public class AlignmentBuilderTest
    {
        [TestMethod]
        public void BuildCurvedAlignmentTest()
        {
            const string PATH = "../../TestFiles/alignment.ifc";
            var builder = new AlignmentBuilder(PATH) { IsStraight = false };
            builder.Run();
        }

        [TestMethod]
        public void BuildStraightAlignmentTest()
        {
            const string PATH = "../../TestFiles/alignment-straight.ifc";
            var builder = new AlignmentBuilder(PATH) { IsStraight = true };
            builder.Run();
        }
        [TestMethod]
        public void CheckNullAlignment()
        {
            const string PATH = "../../TestFiles/sectioned-spine.ifc";
            using (var model = IfcStore.Open(PATH))
            {
                var alignment = model.Instances.OfType<IfcAlignment>().FirstOrDefault();
                Assert.IsNull(alignment);
            }
        }
    }

    
}
