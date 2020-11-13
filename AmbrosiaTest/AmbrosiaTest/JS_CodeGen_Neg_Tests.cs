using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;


namespace AmbrosiaTest
{
    [TestClass]
    public class JS_CodeGen_NegativeTests
    {

        //************* Init Code *****************
        // NOTE: Build the javascript test app once at beginning of the class.
        // NOTE: Make sure all names be "Azure Safe". No capital letters and no underscore.

        [ClassInitialize()]
        public static void Class_Initialize(TestContext tc)
        {
            // Build the JS app first from a JS file
            JS_Utilities JSUtils = new JS_Utilities();
//*#*#*# COMMENT OUT FOR NOW - EASIER WITH TEST WRITING ETCJSUtils.BuildJSTestApp();        
        }

            [TestInitialize()]
        public void Initialize()
        {
            Utilities MyUtils = new Utilities();
            MyUtils.TestInitialize();
        }
        //************* Init Code *****************


        //************* Negative Tests *****************
        [TestMethod]
        public void JS_CodeGen_GenericType_NegTest()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_GenericType.ts";

            // Consumer and Publisher error msg the same ... since part of message has path (which can differ from machine to machine) - verify first part of message in conumser string and second part in Publisher
            string ConsumerErrorMsg = "Unable to publish function 'generic'";
            string PublisherErrorMsg = "TS_GenericType.ts:8:5) as a post method (reason: Generic functions are not supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName,true,ConsumerErrorMsg,PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CodeGen_NoTaggedItems_NegTest()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NoTaggedItems.ts";
            string ConsumerErrorMsg = "Error: The input source file (TS_NoTaggedItems.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";
            string PublisherErrorMsg = "Error: The input source file (TS_NoTaggedItems.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CodeGen_AmbrosiaTagNewLine_NegTest()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_AmbrosiaTagNewline.ts";
            string ConsumerErrorMsg = "Error: A newline character is not allowed in the attributes of an @ambrosia tag";
            string PublisherErrorMsg = "Error: A newline character is not allowed in the attributes of an @ambrosia tag";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CodeGen_StringEnum_NegTest()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_StringEnum.ts";

            // Consumer and Publisher error msg the same ... since part of message has path (which can differ from machine to machine) - verify first part of message in conumser string and second part in Publisher
            string ConsumerErrorMsg = "Error: Unable to publish enum 'PrintMediaString'";
            string PublisherErrorMsg = "TS_StringEnum.ts:6:5) as a type (reason: Unable to parse enum value 'NewspaperStringEnum' (\"NEWSPAPER\"); only integers are supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CodeGen_TagInterface_NegTest()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_TagInterface.ts";
            string ConsumerErrorMsg = "Error: The input source file (TS_TagInterface.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";
            string PublisherErrorMsg = "Error: The input source file (TS_TagInterface.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


        [TestMethod]
        public void JS_CodeGen_TagMethod_NegTest()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_TagMethod.ts";
            string ConsumerErrorMsg = "Error: The input source file (TS_TagMethod.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";
            string PublisherErrorMsg = "Error: The input source file (TS_TagMethod.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


    }
}