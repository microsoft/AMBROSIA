using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;


namespace AmbrosiaTest
{
    [TestClass]
    public class JS_PTI_BasicUnitTests
    {
        //************* Init Code *****************

        // NOTE: Make sure all names be "Azure Safe". No capital letters and no underscore.
        [TestInitialize()]
        public void Initialize()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            MyUtils.TestInitialize();
            JSUtils.BuildJSTestApp();
        }
        //************* Init Code *****************


        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all exes associated with tests
            JS_Utilities JSUtils = new JS_Utilities();
            JSUtils.JS_PTI_TestCleanup();
        }

        [TestMethod]
        public void JS_PTI_BasicEndToEnd_Test()
        {

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string testName = "jsptiendtoendtest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";


        }
    }
}
