using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;


namespace AmbrosiaTest
{
    [TestClass]
    public class JS_CodeGen_Tests
    {

        //************* Init Code *****************
        // NOTE: Build the javascript test app once at beginning of the class.
        [ClassInitialize()]
        public static void Class_Initialize(TestContext tc)
        {
            // Build the JS app first from a JS file
            JS_Utilities JSUtils = new JS_Utilities();
//*#*#*#*#            JSUtils.BuildJSTestApp();
        }

        // NOTE: Make sure all names be "Azure Safe". No capital letters and no underscore.
        [TestInitialize()]
        public void Initialize()
        {
            Utilities MyUtils = new Utilities();
            MyUtils.TestInitialize();
        }
        //************* Init Code *****************

        [TestMethod]
        public void JS_CodeGen_UnitTest()
        {
            Utilities MyUtils = new Utilities();
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "PI.ts";
            JSUtils.Test_CodeGen_TSFile(testfileName);

            string DG = "Done!";

        }
    }
}
