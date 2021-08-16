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
            //JSUtils.BuildJSTestApp(); -- maybe don't do this - not needed to build every time ... could assume it is built as well
        }
        //************* Init Code *****************


        [TestCleanup()]
        public void Cleanup()
        {
            // Kill all exes associated with tests
            JS_Utilities JSUtils = new JS_Utilities();
            JSUtils.JS_TestCleanup();
        }

        [TestMethod]
        public void JS_PTI_BasicBiDiEndToEnd_Test()
        {
            // ** Probably set in Init
            // ** Set AUtoregister = true, Log dirs, binary directory

            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string testName = "jsptibidiendtoendtest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);


            JSUtils.StartJSTestApp(JSUtils.JSPTI_CombinedInstanceRole, logOutputFileName_TestApp);

            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: 256", 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes (256) have been received", 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes (256) have been received", 5, false, testName, true); // number of bytes processed

        }
    }
}
