using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;
using System.IO;


namespace AmbrosiaTest
{
    [TestClass]
    public class JS_CG_NegativeTests
    {

        //************* Init Code *****************
        // NOTE: Build the javascript test app once at beginning of the class.
        // NOTE: Make sure all names be "Azure Safe". No capital letters and no underscore.

        [ClassInitialize()]
        public static void Class_Initialize(TestContext tc)
        {
            // Build the JS app first from a JS file
            JS_Utilities JSUtils = new JS_Utilities();
        }

            [TestInitialize()]
        public void Initialize()
        {
            Utilities MyUtils = new Utilities();
            MyUtils.TestInitialize(true);
        }
        //************* Init Code *****************


        //************* Negative Tests *****************


        // ** Shotgun approach of throwing a bunch of ts files against code gen and see if any fails beyond just saying it is not annotated
        [TestMethod]
        public void JS_CG_Neg_AmbrosiaSrcFiles_Test()
        {
            JS_Utilities JSUtils = new JS_Utilities();
            Utilities MyUtils = new Utilities();

            // get ambrosia-node source files
            string AmbrosiaNodeDir = @"../../../../JSTest/node_modules/ambrosia-node/src/";

            // loop through all the Ambrosia JS src files and generate them
            foreach (string currentSrcFile in Directory.GetFiles(AmbrosiaNodeDir, "*.ts"))
            {

                string fileName = Path.GetFileName(currentSrcFile);

                string PrimaryErrorMessage = "Error: The input source file";
                string SecondaryErrorMessage = " does not publish any entities (exported functions, static methods, type aliases and enums annotated with an @ambrosia JSDoc tag)";

                // Generate the consumer and publisher files and verify output and the generated files to cmp files
                JSUtils.Test_CodeGen_TSFile(fileName, true, PrimaryErrorMessage, SecondaryErrorMessage,true);

            }
        }


        [TestMethod]
        public void JS_CG_Neg_AmbrosiaTagNewLine()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_AmbrosiaTagNewline.ts";
            string PrimaryErrorMessage = "Error: A newline is not allowed in the attributes of an @ambrosia tag";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_AsyncFcthn()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_AsyncFctn.ts";
            string PrimaryErrorMessage = "as a post method (reason: async functions are not supported)";
            string SecondaryErrorMessage = "Error: Unable to publish function 'ComputePI'"; 

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_CircularReference()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_CircReference.ts";
            string PrimaryErrorMessage = "Error: Unable to publish type alias 'CNames'";
            string SecondaryErrorMessage = "as a type (reason: Deferred expansion of type(s) failed (reason: Unable to expand type definition '{ first: string, last: string, priorNames: CNames[] }' because it has a circular reference with definition 'CName[]')) ";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }


        [TestMethod]
        public void JS_CG_Neg_CommaAttrib()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_CommasBetweenAttrib.ts";
            string PrimaryErrorMessage = "Error: Malformed @ambrosia attribute 'publish=true version=1 doRuntimeTypeChecking=true'";
            string SecondaryErrorMessage = "expected format is: attrName=attrValue, ...";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_GenericType()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_GenericType.ts";

            // Consumer and Publisher error msg the same ... since part of message has path (which can differ from machine to machine) - verify first part of message in conumser string and second part in Publisher
            string PrimaryErrorMessage = "Unable to publish function 'generic'";
            string SecondaryErrorMessage = "as a post method (reason: Generic functions are not supported; since the type of 'T' will not be known until runtime, Ambrosia cannot determine [at code-gen time] if the type(s) can be serialized)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_IntersectionType()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NoIntersectionType.ts";

            // Consumer and Publisher error msg the same ... since part of message has path (which can differ from machine to machine) - verify first part of message in conumser string and second part in Publisher
            string PrimaryErrorMessage = "Error: The following types are referenced by other types, but have not been published: 'FullName' found in intersection-type component #1 of published type 'IntersectionType', 'ShortName' found in intersection-type component #2 of published type 'IntersectionType'";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }


        [TestMethod]
        public void JS_CG_Neg_MethodIDInt()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_MethodIDInt.ts";
            string PrimaryErrorMessage = "Error: The value ('Hello') supplied for @ambrosia attribute 'methodID' is not an integer";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }


        [TestMethod]
        public void JS_CG_Neg_MethodIDNeg()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_MethodIDNeg.ts";
            string PrimaryErrorMessage = "Error: The value (-2) supplied for @ambrosia";
            string SecondaryErrorMessage = "attribute 'methodID' cannot be negative";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_MethodIDOnType()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_MethodIDOnType.ts";
            string PrimaryErrorMessage = "Error: The value ('Hello') supplied for @ambrosia attribute 'methodID' is not an integer";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_NamespaceModule()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NamespaceModule.ts";
            string PrimaryErrorMessage = "Error: The @ambrosia tag is not valid on a module";
            string SecondaryErrorMessage = "valid targets are: function, static method, type alias, and enum";
        
            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_NestedFctn()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NestedFunction.ts";  // Cannot publish a local (nested) function
            string PrimaryErrorMessage = "Error: The @ambrosia tag is not valid on a local function";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_NestedFctn2()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NestedFunction2.ts";  // Cannot publish a local (nested) function in a static method
            string PrimaryErrorMessage = "Error: The @ambrosia tag is not valid on a local function";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }


        [TestMethod]
        public void JS_CG_Neg_NoTaggedItems()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NoTaggedItems.ts";
            string PrimaryErrorMessage = "Error: The input source file (TS_NoTaggedItems.ts) does not publish any entities (exported functions, static methods, type aliases and enums annotated with an @ambrosia JSDoc tag)";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_NoFunctionComplexTypes()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NoFunctionComplexType.ts";
            string PrimaryErrorMessage = "Error: Unable to publish type alias 'myComplexType'";
            string SecondaryErrorMessage = "(reason: The published type 'myComplexType' [property 'fn'] has an invalid type ('()=>void'); function types are not supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }


        [TestMethod]
        public void JS_CG_Neg_NoFunctionTypes()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NoFunctionType.ts";
            string PrimaryErrorMessage = "Error: Unable to publish type alias 'fnType'";
            string SecondaryErrorMessage = "as a type (reason: The published type 'fnType' has an invalid type ('(p1: number) => string'); function types are not supported) ";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }


        [TestMethod]
        public void JS_CG_Neg_OptionalProp()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_OptionalProperties.ts";
            string PrimaryErrorMessage = "Error: Unable to publish type alias 'MyTypeWithOptionalMembers'";
            string SecondaryErrorMessage = "as a type (reason: Property 'bar' is optional; types with optional properties are not supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_OverloadFctn()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_OverloadedFunction.ts";
            string PrimaryErrorMessage = "Error: Unable to publish function 'fnOverload'";
            string SecondaryErrorMessage = "as a post method (reason: The @ambrosia tag must appear on the implementation of an overloaded function";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_PublishClass()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_PublishClass.ts";
            string PrimaryErrorMessage = "Error: The @ambrosia tag is not valid on a class";
            string SecondaryErrorMessage = "valid targets are: function, static method, type alias, and enum";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_PublishMethodRef()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_PublishMethodBeforeRef.ts";
            string PrimaryErrorMessage = "Error: Unable to publish function 'fn'";
            string SecondaryErrorMessage = "as a post method (reason: The following types must be published before any method can be published: 'Name' found in published type 'MyType')";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_QuoteAttribVal()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_QuoteAttributeValue.ts";
            string PrimaryErrorMessage = "Error: The value ('\"true\"') supplied for @ambrosia attribute 'publish' is not a boolean";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_RunTimeBool()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_RunTimeBool.ts";
            string PrimaryErrorMessage = "Error: The value ('Hello') supplied for @ambrosia attribute 'doRuntimeTypeChecking' is not a boolean ";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_StaticMethod1()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_StaticMethod1.ts";  // he parent class of a published static method must be exported.
            string PrimaryErrorMessage = "Warning: Skipping static method 'hello'";
            string SecondaryErrorMessage = "Error: The input source file (TS_StaticMethod1.ts) does not publish any entities (exported functions, static methods, type aliases and enums annotated with an @ambrosia JSDoc tag)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_StaticMethod2()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_StaticMethod2.ts"; // A method must have the 'static' modifier to be published.
            string PrimaryErrorMessage = "Error: The @ambrosia tag is not valid on a non-static method";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_StaticMethod3()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_StaticMethod3.ts"; // Cannot publish a static method from a class expression
            string PrimaryErrorMessage = "Error: The @ambrosia tag is not valid on a static method of a class expression";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_StaticMethod4()  
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_StaticMethod4.ts";  // Can't publish a private static method
            string PrimaryErrorMessage = "Error: The @ambrosia tag is not valid on a private static method";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_StringEnum()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_StringEnum.ts";  // Can't publish a private static method
            string PrimaryErrorMessage = "Error: Unable to publish enum 'PrintMediaString'";
            string SecondaryErrorMessage = "reason: Unable to parse enum value 'NewspaperStringEnum' (\"NEWSPAPER\"); only integers are supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_TagInterface()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_TagInterface.ts";
            string PrimaryErrorMessage = "Error: The input source file (TS_TagInterface.ts) does not publish any entities (exported functions, static methods, type aliases and enums annotated with an @ambrosia JSDoc tag)";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }


        [TestMethod]
        public void JS_CG_Neg_TagMethod()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_TagMethod.ts";
            string PrimaryErrorMessage = "Error: The input source file (TS_TagMethod.ts) does not publish any entities (exported functions, static methods, type aliases and enums annotated with an @ambrosia JSDoc tag)";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_TupleType()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_TupleType.ts";
            string PrimaryErrorMessage = "Error: Unable to publish type alias 'MyTupleType'";
            string SecondaryErrorMessage = "as a type (reason: The published type 'MyTupleType' has an invalid type ('[string, number]'); tuple types are not supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }


        [TestMethod]
        public void JS_CG_Neg_TwoAmbrTag()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_TwoAmbrTags.ts";
            string PrimaryErrorMessage = "Error: The @ambrosia tag is defined more than once";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_UnknownAtt_Method()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_UnknownAtt_Method.ts";
            string PrimaryErrorMessage = "Error: Unknown @ambrosia attribute 'published'";
            string SecondaryErrorMessage = "valid attributes are: publish, version, methodID, doRuntimeTypeChecking";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_UnknownAtt_Type()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_UnknownAtt_Type.ts";
            string PrimaryErrorMessage = "Error: Unknown @ambrosia attribute 'published'";
            string SecondaryErrorMessage = "valid attributes are: publish";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }


        [TestMethod]
        public void JS_CG_Neg_VersionInt()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_VersionInt.ts";
            string PrimaryErrorMessage = "Error: The value ('Hello') supplied for @ambrosia attribute 'version' is not an integer";
            string SecondaryErrorMessage = "";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }

        [TestMethod]
        public void JS_CG_Neg_SingleUInt8Array()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_SingleUInt8Array.ts";
            string PrimaryErrorMessage = "Unable to publish function 'takesCustomSerializedParams'";
            string SecondaryErrorMessage = "Uint8Array parameter; Post methods do NOT support custom (raw byte) parameter serialization - all parameters are always serialized to JSON)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, PrimaryErrorMessage, SecondaryErrorMessage);
        }


    }
}