using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Windows.Forms; // need this to handle threading issue on sleeps
using System.Configuration;


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
        public void JS_CG_Neg_AmbrosiaTagNewLine()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_AmbrosiaTagNewline.ts";
            string ConsumerErrorMsg = "Error: A newline character is not allowed in the attributes of an @ambrosia tag";
            string PublisherErrorMsg = "Error: A newline character is not allowed in the attributes of an @ambrosia tag";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_CommaAttrib()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_CommasBetweenAttrib.ts";
            string ConsumerErrorMsg = "Error: Malformed @ambrosia attribute 'publish=true version=1 doRuntimeTypeChecking=true'";
            string PublisherErrorMsg = "expected format is: attrName=attrValue, ...";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_GenericType()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_GenericType.ts";

            // Consumer and Publisher error msg the same ... since part of message has path (which can differ from machine to machine) - verify first part of message in conumser string and second part in Publisher
            string ConsumerErrorMsg = "Unable to publish function 'generic'";
            string PublisherErrorMsg = "TS_GenericType.ts:8:5) as a post method (reason: Generic functions are not supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


        [TestMethod]
        public void JS_CG_Neg_MethodIDInt()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_MethodIDInt.ts";
            string ConsumerErrorMsg = "Error: The value ('Hello') supplied for @ambrosia attribute 'methodID' is not an integer";
            string PublisherErrorMsg = "Error: The value ('Hello') supplied for @ambrosia attribute 'methodID' is not an integer";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


        [TestMethod]
        public void JS_CG_Neg_MethodIDNeg()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_MethodIDNeg.ts";
            string ConsumerErrorMsg = "Error: Unable to publish function 'MyFn'";
            string PublisherErrorMsg = "as a method (reason: Method ID -2 is invalid";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_MethodIDOnType()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_MethodIDOnType.ts";
            string ConsumerErrorMsg = "Error: The value ('Hello') supplied for @ambrosia attribute 'methodID' is not an integer";
            string PublisherErrorMsg = "Error: The value ('Hello') supplied for @ambrosia attribute 'methodID' is not an integer";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_NamespaceModule()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NamespaceModule.ts";
            string ConsumerErrorMsg = "Error: The @ambrosia tag is not valid on a module; valid targets are: function, type alias, enum";
            string PublisherErrorMsg = "Error: The @ambrosia tag is not valid on a module; valid targets are: function, type alias, enum";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


        [TestMethod]
        public void JS_CG_Neg_NestedFctn()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NestedFunction.ts";
            string ConsumerErrorMsg = "Error: The @ambrosia tag is not valid on a local function ('localFn'";
            string PublisherErrorMsg = "Error: The @ambrosia tag is not valid on a local function ('localFn'";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_NoTaggedItems()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NoTaggedItems.ts";
            string ConsumerErrorMsg = "Error: The input source file (TS_NoTaggedItems.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";
            string PublisherErrorMsg = "Error: The input source file (TS_NoTaggedItems.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_NoFunctionComplexTypes()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NoFunctionComplexType.ts";
            string ConsumerErrorMsg = "Error: Unable to publish type alias 'myComplexType'";
            string PublisherErrorMsg = "as a type (reason: The published type 'myComplexType' [property 'fn'] has an invalid type ('()=>void'); function types are not supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


        [TestMethod]
        public void JS_CG_Neg_NoFunctionTypes()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_NoFunctionType.ts";
            string ConsumerErrorMsg = "Error: Unable to publish type alias 'fnType'";
            string PublisherErrorMsg = "as a type (reason: The published type 'fnType' has an invalid type ('(p1:number)=>string'); function types are not supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


        [TestMethod]
        public void JS_CG_Neg_OptionalProp()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_OptionalProperties.ts";
            string ConsumerErrorMsg = "Error: Unable to publish type alias 'MyTypeWithOptionalMembers'";
            string PublisherErrorMsg = "as a type (reason: Property 'bar' is optional, but types with optional properties are not supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_OverloadFctn()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_OverloadedFunction.ts";
            string ConsumerErrorMsg = "Error: Unable to publish function 'fnOverload'";
            string PublisherErrorMsg = "as a post method (reason: The @ambrosia tag must appear on the implementation of an overloaded function";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


        [TestMethod]
        public void JS_CG_Neg_PublishClass()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_PublishClass.ts";
            string ConsumerErrorMsg = "Error: The @ambrosia tag is not valid on a class; valid targets are: function, type alias, enum";
            string PublisherErrorMsg = "Error: The @ambrosia tag is not valid on a class; valid targets are: function, type alias, enum";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_PublishMethodRef()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_PublishMethodBeforeRef.ts";
            string ConsumerErrorMsg = "Error: Unable to publish function 'fn'";
            string PublisherErrorMsg = "as a post method (reason: The following types must be published before any method can be published: Name)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_QuoteAttribVal()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_QuoteAttributeValue.ts";
            string ConsumerErrorMsg = "Error: The value ('\"true\"') supplied for @ambrosia attribute 'publish' is not a boolean";
            string PublisherErrorMsg = "Error: The value ('\"true\"') supplied for @ambrosia attribute 'publish' is not a boolean";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_RunTimeBool()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_RunTimeBool.ts";
            string ConsumerErrorMsg = "Error: The value ('Hello') supplied for @ambrosia attribute 'doRuntimeTypeChecking' is not a boolean ";
            string PublisherErrorMsg = "Error: The value ('Hello') supplied for @ambrosia attribute 'doRuntimeTypeChecking' is not a boolean ";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_StringEnum()
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
        public void JS_CG_Neg_TagInterface()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_TagInterface.ts";
            string ConsumerErrorMsg = "Error: The input source file (TS_TagInterface.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";
            string PublisherErrorMsg = "Error: The input source file (TS_TagInterface.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


        [TestMethod]
        public void JS_CG_Neg_TagMethod()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_TagMethod.ts";
            string ConsumerErrorMsg = "Error: The input source file (TS_TagMethod.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";
            string PublisherErrorMsg = "Error: The input source file (TS_TagMethod.ts) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_TupleType()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_TupleType.ts";
            string ConsumerErrorMsg = "Error: Unable to publish type alias 'MyTupleType'";
            string PublisherErrorMsg = "as a type (reason: The published type 'MyTupleType' has an invalid type ('[string,number]'); tuple types are not supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


        [TestMethod]
        public void JS_CG_Neg_TwoAmbrTag()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_TwoAmbrTags.ts";
            string ConsumerErrorMsg = "Error: The @ambrosia tag is defined more than once at";
            string PublisherErrorMsg = "Error: The @ambrosia tag is defined more than once at";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_UnionType()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_UnionType.ts";
            string ConsumerErrorMsg = "Error: Unable to publish type alias 'MyUnionType'";
            string PublisherErrorMsg = "as a type (reason: The published type 'MyUnionType' has an invalid type ('string|number'); union types are not supported)";
                                        
            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_UnionTypeCommented()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_UnionTypeCommented.ts";
            string ConsumerErrorMsg = "Error: Unable to publish function 'myComplexReturnFunction'";
            string PublisherErrorMsg = "as a post method (reason: The return type of method 'myComplexReturnFunction' [property 'r2'] has an invalid type ('number|string'); union types are not supported)";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }


        [TestMethod]
        public void JS_CG_Neg_UnknownAttribute()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_UnknownAttribute.ts";
            string ConsumerErrorMsg = "Error: The @ambrosia attribute 'published' is invalid for a function";
            string PublisherErrorMsg = "valid attributes are: publish, version, methodID, doRuntimeTypeChecking";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }

        [TestMethod]
        public void JS_CG_Neg_VersionInt()
        {
            JS_Utilities JSUtils = new JS_Utilities();

            string testfileName = "TS_VersionInt.ts";
            string ConsumerErrorMsg = "Error: The value ('Hello') supplied for @ambrosia attribute 'version' is not an integer";
            string PublisherErrorMsg = "Error: The value ('Hello') supplied for @ambrosia attribute 'version' is not an integer";

            // Generate the consumer and publisher files and verify output and the generated files to cmp files
            JSUtils.Test_CodeGen_TSFile(testfileName, true, ConsumerErrorMsg, PublisherErrorMsg);
        }



    }
}