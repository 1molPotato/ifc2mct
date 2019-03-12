using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ifc2mct.Translator;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace ifc2mct.Tests
{
    [TestClass]
    public class TranslatorTest
    {
        [TestMethod]
        public void TranslateGirderTest()
        {
            const string PATH = "../../TestFiles/completed-bridge.ifc";
            const string OUTPUT = "../../TestFiles/girder-test.mct";
            using (var model = IfcStore.Open(PATH))
            {
                var worker = new Worker(model);
                worker.Run();
                worker.WriteMctFile(OUTPUT);
            }
            Assert.IsTrue(File.Exists(OUTPUT));
        }
    }
}
