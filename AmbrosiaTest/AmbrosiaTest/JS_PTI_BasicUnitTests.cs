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

            // Set config file back to the way it was 
            JSUtils.JS_RestoreJSConfigFile();
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
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string byteSize = "256";
            string testName = "jsptibidiendtoendtest";
            string logOutputFileName_TestApp = testName + "_TestApp.log";
            JSUtils.JS_UpdateJSConfigFile(JSUtils.JSConfig_instanceName, testName);

            JSUtils.StartJSTestApp(JSUtils.JSPTI_CombinedInstanceRole, logOutputFileName_TestApp);

            // Verify the data in the output file
            bool pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "Bytes received: "+ byteSize, 5, false, testName, true); // number of bytes processed
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of bytes ("+byteSize+") have been received", 5, false, testName, true); 
            pass = MyUtils.WaitForProcessToFinish(logOutputFileName_TestApp, "SUCCESS: The expected number of echoed bytes ("+ byteSize + ") have been received", 5, false, testName, true);

            //*#*#*#
            //  TO DO: Write the VerifyAmbrosiaLogFile for JS. JS and C# versions too different for this one function as no TTD etc.
            //*#*#*#

            // Verify integrity of Ambrosia logs by replaying
            //MyUtils.VerifyAmbrosiaLogFile(testName, Convert.ToInt64(byteSize), true, true, "0");
        }
    }
}
