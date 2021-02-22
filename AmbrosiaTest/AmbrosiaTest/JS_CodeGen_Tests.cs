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
//*#*#*# COMMENT OUT FOR NOW - EASIER WITH TEST WRITING ETC .. JSUtils.BuildJSTestApp();
        }

        // NOTE: Make sure all names be "Azure Safe". No capital letters and no underscore.
        [TestInitialize()]
        public void Initialize()
        {
            Utilities MyUtils = new Utilities();
            MyUtils.TestInitialize();
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
        public void JS_CG_Misc_AST_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "ASTTest.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }


        [TestMethod]
        public void JS_CG_Types_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_Types.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }

        [TestMethod]
        public void JS_CG_AmbrosiaTag_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_AmbrosiaTag.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }

        [TestMethod]
        public void JS_CG_EventHandler_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_EventHandlers.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }

        [TestMethod]
        public void JS_CG_CustomSerialParam_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_CustomSerialParam.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }

        [TestMethod]
        public void JS_CG_CustomSerialParamNoRaw_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_CustomSerialParamNoRawParam.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }


        [TestMethod]
        public void JS_CG_EventHandlerWarnings_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_EventHandlerWarnings.ts";

            // Warning message in Event Handlers - not really consumer vs publisher so overloading use here
            string ConsumerWarning = "Warning: Skipping Ambrosia AppEvent handler function 'onRecoveryComplete'";
            string PublisherWarning = "Warning: Skipping Ambrosia AppEvent handler function 'onBecomingPrimary'";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, false, ConsumerWarning, PublisherWarning);
        }

        [TestMethod]
        public void JS_CG_GenTypeConcrete_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_GenType1.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }

        [TestMethod]
        public void JS_CG_GenTypeConcrete2_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_GenType2.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }

        [TestMethod]
        public void JS_CG_LiteralObjArray_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_LitObjArray.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }

        [TestMethod]
        public void JS_CG_StaticMethod_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_StaticMethod.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }



        //**** Misc valid tests that are just a "catch all" if don't know where to put test
        [TestMethod]
        public void JS_CG_Misc_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_MiscTests.ts";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName);
        }

    }
}