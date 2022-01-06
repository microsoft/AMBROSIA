// A collection of unit tests for ambrosia-node. Run with "npm run unittests [testType=Unit|CodeGen|All|RebuildComps]".
// Note: To debug, uncomment the corresponding "program" line in launch.json
import Process = require("process");
import File = require("fs");
import Path = require("path");
import Ambrosia = require("./Ambrosia");
import Utils = Ambrosia.Utils;
import Meta = Ambrosia.Meta;
import Configuration = Ambrosia.Configuration;
import DataFormat = Ambrosia.DataFormat;

let _testNames: Set<string> = new Set<string>(); // Tracks the [unique] names of tests that have been run
let _passCount: number = 0; // Tracks the number of tests that passed
let _failCount: number = 0; // Tracks the number of tests that failed

main(Process.argv);

function main(args: string[])
{
    enum TestType
    {
        Unit,
        CodeGen,
        RebuildComps,
        DataFormat,
        All
    }
    
    Ambrosia.initialize(function mainInner(error?: Error)
    {
        try
        {
            if (!error)
            {
                const testType: keyof typeof TestType = Utils.getCommandLineArg("testType", TestType[TestType.Unit]) as keyof typeof TestType;
                const rebuildCompFiles: boolean = Utils.equalIgnoringCase(testType, TestType[TestType.RebuildComps]);

                if (TestType[testType] === undefined)
                {
                    throw new Error(`The supplied 'testType' ("${testType}") is invalid; valid values are: "${Utils.getEnumKeys("TestType", TestType).join("\", \"")}"`);
                }

                // Some Unit tests use Utils.getLastMessageLogged(), and all Code-gen tests only work by parsing the output log file, so we enforce the needed logging requirements
                if (Utils.equalIgnoringCase(testType, TestType[TestType.Unit]) || Utils.equalIgnoringCase(testType, TestType[TestType.CodeGen]) || Utils.equalIgnoringCase(testType, TestType[TestType.All]))
                {
                    if (Configuration.loadedConfig().lbOptions.outputLogDestination !== Configuration.OutputLogDestination.ConsoleAndFile)
                    {
                        throw new Error(`To run '${testType}' tests, the 'outputLogDestination' in ${Configuration.loadedConfigFileName()} must be set to '${Configuration.OutputLogDestination[Configuration.OutputLogDestination.ConsoleAndFile]}'`);
                    }
                    if (Configuration.loadedConfig().lbOptions.outputLoggingLevel !== Utils.LoggingLevel.Verbose)
                    {
                        throw new Error(`To run '${testType}' tests, the 'outputLoggingLevel' in ${Configuration.loadedConfigFileName()} must be set to '${Utils.LoggingLevel[Utils.LoggingLevel.Verbose]}'`);
                    }
                }

                switch (testType)
                {
                    case "Unit":
                        runUnitTests(args);
                        break;
                    case "CodeGen":
                    case "RebuildComps":
                        runCodeGenTests(rebuildCompFiles);
                        break;
                    case "DataFormat":
                        runDataFormatTests();
                        break;
                    case "All":
                        runUnitTests(args);
                        runCodeGenTests();
                        runDataFormatTests();
                        break;
                    default:
                        throw new Error(`Unsupported 'testType' value "${testType}"`);
                }
            }
            else
            {
                Utils.tryLog(error);
            }
        }
        catch (error: unknown)
        {
            Utils.tryLog(`Error: ${Utils.makeError(error).message}`);
        }
    }, Ambrosia.LBInitMode.CodeGen);
}

/**
 * Runs a single unit test.\
 * Returns true only if the result [or error message] of the supplied 'test' matches the specified 'expectedResult'. 
 * @param exactMatch A flag that determines whether the result [or error message] must exactly match the 'expectedResult',
 * or (if false) whether the test is passed if the result only includes the 'expectedResult'. Defaults to true.
 * @param silentOnPass A flag that controls whether to suppress logging the outcome if the test passes. Defaults to false.
 */
 function runTest(testName: string, test: () => string, expectedResult: string, exactMatch: boolean = true, silentOnPass: boolean = false): boolean
 {
    let isTestPassed: boolean = false;
    let result: string = "";

    if (_testNames.has(testName))
    {
        throw new Error(`Test '${testName}' has already been run; All tests must have unique names`);
    }
    _testNames.add(testName);

    try
    {
        result = test();
        
        // This shouldn't happen, but it can because - for example - Utils.jsonParse() returns 'any'.
        // If it does happen it's a programming error in the the supplied 'test()' lambda, but we catch it
        // to allow the test to gracefully fail instead of [potentially] crashing at result.indexOf() later
        if (typeof result !== "string")
        {
            result = result["toString"] ? (result as any).toString() : "Error: The supplied unit test did not return a string";
        }
    }
    catch (error: unknown)
    {
        result = Utils.makeError(error).message; 
    }

    isTestPassed = exactMatch ? (result === expectedResult) : (result.indexOf(expectedResult) !== -1);

    if (isTestPassed)
    {
        if (!silentOnPass)
        {
            _passCount++;
            Utils.logWithColor(Utils.ConsoleForegroundColors.Green, `Test '${testName}': PASSED`);
        }
    }
    else
    {
        _failCount++;
        Utils.logWithColor(Utils.ConsoleForegroundColors.Red, `Test '${testName}': FAILED (Expected result: '${expectedResult}'; Actual result: '${result}')`);
    }
    return (isTestPassed);
}

/************************/
/*  UNIT TESTS SECTION  */
/************************/

/** Runs all unit tests. */
function runUnitTests(args: string[]): void
{
    _passCount = _failCount = 0;

    const startTime: number = Date.now();
    let componentTestPassCount: number = 0;

    Utils.log("RUNNING UNIT TESTS...");

    runTest("Compare simple type - basic",
        () => (Meta.Type.compareTypes("string", "string") || "Match"), "Match");
    runTest("Compare simple type - missing aray suffix",
        () => (Meta.Type.compareTypes("string", "string[][]") || "ShouldNotMatch"), "expected 'string[][]', not 'string'");

    runTest("Compare object type - basic (1)",
        () => (Meta.Type.compareTypes("object", "{ p1: string }") || "Match"), "Match");
    runTest("Compare object type - basic (2)",
        () => (Meta.Type.compareTypes("{ p1: string }", "object") || "Match"), "Match");
    runTest("Compare object type - array (1)",
        () => (Meta.Type.compareTypes("object[]", "{ p1: string }[]") || "Match"), "Match");
    runTest("Compare object type - array (2)",
        () => (Meta.Type.compareTypes("{ p1: string }[]", "object[]") || "Match"), "Match");
    runTest("Compare object type - array (3)",
        () => (Meta.Type.compareTypes("{ p1: string }[]", "object[][]") || "ShouldNotMatch"), "expected 'object[][]', not '{p1:string}[]'");

    runTest("Compare any type - basic (1)",
        () => (Meta.Type.compareTypes("any", "{ p1: string }") || "Match"), "Match");
    runTest("Compare any type - basic (2)",
        () => (Meta.Type.compareTypes("{ p1: string }", "any") || "Match"), "Match");
    runTest("Compare any type - array (1)",
        () => (Meta.Type.compareTypes("any[]", "{ p1: string }[]") || "Match"), "Match");
    runTest("Compare any type - array (2)",
        () => (Meta.Type.compareTypes("{ p1: string }[]", "any[]") || "Match"), "Match");
    runTest("Compare any type - array dimension mismatch (1)",
        () => (Meta.Type.compareTypes("any[]", "{ p1: string }[][]") || "Match"), "Match"); // "any" here is a valid match for the type "{ p1: string }[]"
    runTest("Compare any type - array dimension mismatch (2)",
        () => (Meta.Type.compareTypes("{ p1: string }[][]", "any[]") || "Match"), "Match"); // "any" here is a valid match for the type "{ p1: string }[]"

    runTest("Compare basic complex type array",
        () => (Meta.Type.compareTypes("{ p1: string }[]", "{ p1: string }[]") || "Match"), "Match");
    runTest("Compare basic complex type array - incorrect array suffix ",
        () => (Meta.Type.compareTypes("{ p1: string }[]", "{ p1: string }[][]") || "ShouldNotMatch"), "{ p1: string }[] should have array suffix [][], not []");

    runTest("Compare complex type - basic",
        () => (Meta.Type.compareTypes("{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number }", "{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number }") || "Match"), "Match");
    runTest("Compare complex type - detect misnamed root-level property name",
        () => (Meta.Type.compareTypes("{ p1: string, p2: { p4: number, p5: { p6: string } }, p31: number }", "{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number }") || "ShouldNotMatch"), "property #3 should be 'p3:', not 'p31:'");
    runTest("Compare complex type - detect misnamed nested property name",
        () => (Meta.Type.compareTypes("{ p1: string, p2: { p4: number, p5: { p61: string } }, p3: number }", "{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number }") || "ShouldNotMatch"), "property #1 (in 'p2.p5') should be 'p6:', not 'p61:'");
    runTest("Compare complex type - detect incorrect root-level property type",
        () => (Meta.Type.compareTypes("{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: string }", "{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number }") || "ShouldNotMatch"), "type of 'p3' should be 'number', not 'string'");
    runTest("Compare complex type - detect incorrect nested property type",
        () => (Meta.Type.compareTypes("{ p1: string, p2: { p4: number, p5: { p6: boolean } }, p3: number }", "{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number }") || "ShouldNotMatch"), "type of 'p6' (in 'p2.p5') should be 'string', not 'boolean'");
    runTest("Compare complex type - mismatched structure (missing expected property)",
        () => (Meta.Type.compareTypes("{ p1: string, p2: { p4: number, p5: { p6: string } } }", "{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number }") || "ShouldNotMatch"), "mismatched structure: expected member 'p3:' not provided");
    runTest("Compare complex type - mismatched structure (unexpected additional property)",
        () => (Meta.Type.compareTypes("{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number, pExtra: boolean }", "{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number }") || "ShouldNotMatch"), "mismatched structure: unexpected member 'pExtra:' provided");
    runTest("Compare complex type - unexpected array suffix",
        () => (Meta.Type.compareTypes("{ p1: string, p2: { p4: number, p5: { p6: string }[][] }, p3: number }", "{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number }") || "ShouldNotMatch"), "type of 'p2.p5' ({ p6: string }[][]) should have array suffix (None), not [][]");
    runTest("Compare complex type - missing array suffix",
        () => (Meta.Type.compareTypes("{ p1: string, p2: { p4: number, p5: { p6: string } }, p3: number }", "{ p1: string, p2: { p4: number, p5: { p6: string }[] }, p3: number }") || "ShouldNotMatch"), "type of 'p2.p5' ({ p6: string }) should have array suffix [], not (None)");
    runTest("Compare complex type - with generics",
        () => (Meta.Type.compareTypes(Meta.Type.getRuntimeType({ p1: new Set<boolean>([true, false, true]), p2: new Map<number, string>([[123, "a"]]) }), "{ p1: Set, p2: Map }") || "Match"), "Match");

    runTest("Get runtime type - basic",
        () => Meta.Type.getRuntimeType([{addressLines: { lines: ["Line1", "Line"] }, zip: 98052}]), "{ addressLines: { lines: string[] }, zip: number }[]");
    runTest("Get runtime type - \"mixed\" array",
        () => Meta.Type.getRuntimeType([{addressLines: { lines: ["Line1", 123] }, zip: 98052}]), "{ addressLines: { lines: any[] }, zip: number }[]");
    runTest("Get runtime type - basic generic",
        () => Meta.Type.getRuntimeType(new Set<string>(["a", "b", "c"])), "Set");
    runTest("Get runtime type - object with generics",
        () => Meta.Type.getRuntimeType({ p1: new Set<string>(["a", "b", "c"]), p2: new Map<number, string>([[123, "a"]]) }), "{ p1: Set, p2: Map }");
    runTest("Get runtime type - object with various built-in types",
        () => Meta.Type.getRuntimeType({ m: new Map<number, string>(), s: new Set<number>(), e: new Error("e"), d: new Date(), r: RegExp(/a/g) }), "{ m: Map, s: Set, e: Error, d: Date, r: RegExp }");

    let genericSpecifiers: string[] = Meta.Type.getGenericsSpecifiers("{ p1: Bar<Foo, {p2: string}, [string[], () => number], Map<number, Set<{p3: string, p4: Set<boolean>}>>>, p12: Set<Date> }");
    runTest("Get generic-type specifiers",
        () => genericSpecifiers.join(", "), "<Foo, {p2: string}, [string[], () => number], Map<number, Set<{p3: string, p4: Set<boolean>}>>>, <Date>");
    runTest("Parse generic-type specifiers",
        () => Meta.Type.parseGenericsSpecifier(genericSpecifiers[0]).join(", "), "Foo, {p2: string}, [string[], () => number], Map<number, Set<{p3: string, p4: Set<boolean>}>>");

    runTest("Published type - check expanded definition removes generics",
        () => Meta.publishType("GenericsTest", "{ p1: Set<Foo>, p2: Map<number, Set<{ p3: Set<string[][]> }>[]>[][] }").expandedDefinition, "{ p1: Set, p2: Map[][] }");
    runTest("Published type - check user-defined generic type is not allowed",
        () => { Meta.publishType("GenericsTest2<T>", "Set<T>"); return (""); }, "The published type 'GenericsTest2<T>' has an invalid name ('GenericsTest2<T>')");

    runTest("Published type - check expanded definition of forward reference is initially empty",
        () => Meta.publishType("ForwardReferenceTest", "{ p1: Foo }").expandedDefinition, "");
    Meta.publishType("Foo", "{ p2: string }");
    runTest("Published type - check expanded definition of forward reference is auto-fixed",
        () => Meta.getPublishedType("ForwardReferenceTest")?.expandedDefinition || "TypeNotFound", "{ p1: { p2: string } }");

    Meta.publishType("FooArray", "Foo[][]");
    runTest("Published type - check expanded definition for a type that's an array of a published type",
        () => Meta.getPublishedType("FooArray")?.expandedDefinition || "TypeNotFound", "{ p2: string }[][]");

    runTest("Published type - null type is not supported",
        () => Meta.publishType("NullTypeTest", "null").toString(), "The published type 'NullTypeTest' has a type ('null') that's not supported in this context");
    runTest("Published type - null[] type is not supported",
        () => Meta.publishType("NullArrayTypeTest", "null[]").toString(), "The published type 'NullArrayTypeTest' has an unsupported type ('null[]')");
    runTest("Published type - null is supported in a union type",
        () => Meta.publishType("NullableUnionTypeTest", "string | null").expandedDefinition, "any");

    runTest("Published type - undefined type is not supported",
        () => Meta.publishType("UndefinedTypeTest", "undefined").toString(), "The published type 'UndefinedTypeTest' has a type ('undefined') that's not supported in this context");
    runTest("Published type - undefined[] type is not supported",
        () => Meta.publishType("UndefinedArrayTypeTest", "undefined[]").toString(), "The published type 'UndefinedArrayTypeTest' has an unsupported type ('undefined[]')");
    runTest("Published type - undefined is supported in a union type",
        () => Meta.publishType("UndefinedableUnionTypeTest", "string | undefined").expandedDefinition, "any");

    runTest("Published type - using 'any' generates a warning",
        () => { Meta.publishType("AnyWarningTest", "any"); return (Utils.getLastMessageLogged() as string); }, "The published type 'AnyWarningTest' uses type 'any' which is too general to determine if it can be safely serialized", false);

    runTest("Published type - tuple types are not supported",
        () => Meta.publishType("TupleTest", "[string, number]").toString(), "tuple types are not supported", false);
    runTest("Published type - tuple types are not supported (in a generic)",
        () => Meta.publishType("TupleTestInGeneric", "Set<[string, number]>").toString(), "tuple types are not supported", false);

    runTest("Published type - union types are supported (as 'any')",
        () => Meta.publishType("UnionTest", "string | (number | boolean)[] | (string & number) | { p1: boolean | number, p2: boolean & number} | boolean").expandedDefinition, "any");
    runTest("Published type - union types are supported (in a generic)",
        () => Meta.publishType("UnionTestGeneric", "Set<string | number>").expandedDefinition, "Set");
    runTest("Published type - intersection types are supported (as 'any')",
        () => Meta.publishType("IntersectionTest", "string & (number & boolean)[] & (string | number) & { p1: boolean & number, p2: boolean | number } & boolean").expandedDefinition, "any");
    runTest("Published type - intersection types are supported (in a generic)",
        () => Meta.publishType("IntersectionTestGeneric", "Set<string & number>").expandedDefinition, "Set");

    runTest("Published type - union type emits a type simplification warning",
        () => { Meta.publishType("UnionTypeWarningTest", "string | number");  return (Utils.getLastMessageLogged() as string); }, "Warning: The expanded definition for type 'UnionTypeWarningTest' was simplified to \"any\", which will bypass runtime type checking");
    runTest("Published type - type using an in-line union type emits a type simplification warning",
        () => { Meta.publishType("TypeUsingInlineUnionWarningTest", "{ p1: string | number }"); return (Utils.getLastMessageLogged() as string); }, "Warning: The expanded definition for type 'TypeUsingInlineUnionWarningTest' was simplified to \"any\", which will bypass runtime type checking; the strongly recommended fix is to publish all in-line compound types (unions and intersections)");
    runTest("Published type - intersection type emits a type simplification warning",
        () => { Meta.publishType("IntersectionTypeWarningTest", "string & number");  return (Utils.getLastMessageLogged() as string); }, "Warning: The expanded definition for type 'IntersectionTypeWarningTest' was simplified to \"any\", which will bypass runtime type checking");
    runTest("Published type - type using an in-line intersection type emits a type simplification warning",
        () => { Meta.publishType("TypeUsingInLineIntersectionWarningTest", "{ p1: string & number }"); return (Utils.getLastMessageLogged() as string); }, "Warning: The expanded definition for type 'TypeUsingInLineIntersectionWarningTest' was simplified to \"any\", which will bypass runtime type checking; the strongly recommended fix is to publish all in-line compound types (unions and intersections)");
    runTest("Published type - intersection types containing object literals are supported",
        () => Meta.publishType("IntersectionWithObjectLiteralTest", "string & { p1: number }").expandedDefinition, "any");
    runTest("Published type - union types containing object literals are supported",
        () => Meta.publishType("UnionWithObjectLiteralTest", "string | { p1: number }").expandedDefinition, "any");

    runTest("Published type - intersection types cannot use 'null'",
        () => Meta.publishType("IntersectionNullTest", "string & null").toString(), "The intersection-type component #2 of published type 'IntersectionNullTest' has a type ('null') that's not supported in this context");
    runTest("Published type - intersection types cannot use 'undefined'",
        () => Meta.publishType("IntersectionUndefinedTest", "string & undefined").toString(), "The intersection-type component #2 of published type 'IntersectionUndefinedTest' has a type ('undefined') that's not supported in this context");

    runTest("Published type - unions of string literals are supported",
        () => Meta.publishType("FirstNames", "'Rahee' | \"Jonathan\" | \"Darren\" | \"Richard\"").definition, "'Rahee' | \"Jonathan\" | \"Darren\" | \"Richard\"");
    runTest("Published type - template string types are supported",
        () => Meta.publishType("TemplateStringTest", "`Hello ${FirstNames} at ${'MSR' | 'Microsoft'}`").expandedDefinition, "string");
    runTest("Published type - complex type that references a published compound type has localized simplification",
        () => Meta.publishType("LocalizedTypeSimplification", "{ p1: string, p2: FirstNames, p3: TemplateStringTest }").expandedDefinition, "{ p1: string, p2: any, p3: string }");

    runTest("Published method - rest params are supported in post method",
        () => Meta.publishPostMethod("RestFnPostTest", 1, ["p1: string", "...p2: number[]"], "number").parameterNames.join(", "), "p1, ...p2");
    runTest("Published method - rest params are supported in non-post method",
        () => Meta.publishMethod(1, "RestFnNonPostTest", ["np1: string", "...np2: number[]"]).parameterNames.join(", "), "np1, ...np2");
    runTest("Published method - bad rest params syntax is caught (too many dots)",
        () => Meta.publishPostMethod("RestFnBadSyntax", 1, ["....p1: number[]"], "number").toString(), "has an invalid name ('....p1')", false);
    runTest("Published method - bad rest params syntax is caught (not an array)",
        () => Meta.publishPostMethod("RestFnBadSyntax", 1, ["...p1: number"], "number").toString(), "Rest parameter '...p1' of method 'RestFnBadSyntax' must be an array");
    runTest("Published method - bad rest params syntax is caught (must be last parameter)",
        () => Meta.publishPostMethod("RestFnBadSyntax", 1, ["...p1: number[]", "p2: string"], "number").toString(), "Rest parameter '...p1' of method 'RestFnBadSyntax' must be specified after all other parameters");

    runTest("Published type - functions types are not supported",
        () => Meta.publishType("FunctionTest", "() => number").toString(), "function types are not supported", false);
    runTest("Published type - functions types are not supported (in a generic)",
        () => Meta.publishType("FunctionTestGeneric", "Map<string, () => number>").toString(), "function types are not supported", false);
    runTest("Published type - the 'never' type is not supported",
        () => Meta.publishType("NeverTest", "string | never").toString(), "has an unsupported type ('never')", false);
    runTest("Published method - the 'unknown' type is not supported",
        () => Meta.publishPostMethod("UnknownTest", 1, ["p1: string"], "unknown").toString(), "has an unsupported type ('unknown')", false);
    runTest("Published type - TypeScript utility types are not supported",
        () => Meta.publishType("TSUtilityTypeTest", "NonNullable<string[] | null>").toString(), "utility types are not supported", false);
    runTest("Published type - \"Other\" TypeScript types are not supported",
        () => Meta.publishType("TSOtherTypeTest", "string extends null ? never: string").toString(), "conditional types are not supported", false);
    runTest("Published type - Space in-and-around array suffixes is removed",
        () => Meta.publishType("SpaceRemovalTest", "string [ ] [    ]   []   ").expandedDefinition, "string[][][]", true);

    runTest("Published method - method name must be valid",
        () => Meta.publishMethod(2, "TestFn<T, V>", ["p1: string"]).toString(), "The method has an invalid name ('TestFn<T, V>')", false);
    runTest("Published method - cannot reference an unpublished type",
        () => Meta.publishPostMethod("MethodUsingUnpublishedType", 1, ["p1: SomeNotYetPublishedType"], "void").toString(), "references an unpublished type ('SomeNotYetPublishedType')", false);
    runTest("Published method - method using a union type emits a type simplification warning",
        () => { Meta.publishPostMethod("MethodUsingUnionWarningTest", 1, ["p1: string | number"], "void"); return (Utils.getLastMessageLogged() as string); }, "Warning: The expanded definition for the type of parameter 'p1' of method 'MethodUsingUnionWarningTest' was simplified to \"any\", which will bypass runtime type checking");
    runTest("Published method - method using a union type member emits a type simplification warning",
        () => { Meta.publishPostMethod("MethodUsingUnionMemberWarningTest", 1, ["p1: { a: number, b: string | number }"], "void"); return (Utils.getLastMessageLogged() as string); }, "Warning: The expanded definition for the type of parameter 'p1' of method 'MethodUsingUnionMemberWarningTest' was simplified to \"any\", which will bypass runtime type checking; the strongly recommended fix is to publish all in-line compound types (unions and intersections)");
    
    let oA = { x: {} };
    let oB = { y: oA };
    oA.x = oB;
    runTest("JSON serialization - check for circular reference (simple)", // oA.x -> oB, oB.y -> oA
        () => Utils.jsonStringify(oA), "Unable to serialize object to JSON (reason: (object) [Object] has a property ((object).x.y) that points back to (object), creating a circular reference)");

    let o3 = { z: [{}, {}] };
    let o2 = { y: o3 };
    let o1 = { x: o2 };
    o3.z[1] = o2;
    runTest("JSON serialization - check for circular reference (via array element)", // o1.x -> o2, o2.y -> o3, o3.z[1] -> o2
        () => Utils.jsonStringify(o1), "Unable to serialize object to JSON (reason: (object).x [Object] has an array element ((object).x.y.z[1]) that points back to (object).x, creating a circular reference)");

    let oWithMap1 = { x: new Map<object, string>() };
    oWithMap1.x.set(oWithMap1, "Test");
    runTest("JSON serialization - check for circular reference (via Map key)",
        () => Utils.jsonStringify(oWithMap1), "Unable to serialize object to JSON (reason: (object) [Object] has a Map key ((object).x[0].key) that points back to (object), creating a circular reference)");

    let oWithMap2 = { x: new Map<string, object>() };
    oWithMap2.x.set("test", oWithMap2);
    runTest("JSON serialization - check for circular reference (via Map value)",
        () => Utils.jsonStringify(oWithMap2), "Unable to serialize object to JSON (reason: (object) [Object] has a Map value ((object).x[0].value) that points back to (object), creating a circular reference)");

    let oWithSet = { x: new Set<object>() };
    oWithSet.x.add(oWithSet);
    runTest("JSON serialization - check for circular reference (via Set entry)",
        () => Utils.jsonStringify(oWithSet), "Unable to serialize object to JSON (reason: (object) [Object] has a Set entry ((object).x[0]) that points back to (object), creating a circular reference)");

    runTest("JSON serialization - quick test", () => quickTest().toString(), "true");
    runTest("JSON serialization - object test", () => objectTest().toString(), "true");
    runTest("JSON serialization - special values test", () => specialValuesTest().toString(), "true");
    runTest("JSON serialization - set test", () => setTest().toString(), "true");

    runTest("JSON serialization - check that Error sub-types serialize to Error",
        () => (Utils.jsonParse(Utils.jsonStringify({ p1: new Error(), p2: new EvalError("e2") })).p1 instanceof Error).toString(), "true");
    runTest("JSON serialization - check that root-level empty object serializes",
        () => Utils.jsonStringify(Utils.jsonParse(Utils.jsonStringify({}))), "{}");
    runTest("JSON serialization - check that undefined object property is removed", // Note: This is standard JSON.stringify() behavior
        () => (Utils.jsonParse(Utils.jsonStringify({ p1: 123, p2: undefined })).hasOwnProperty("p2")).toString(), "false");
    runTest("JSON serialization - check that undefined array element becomes null", // Note: This is standard JSON.stringify() behavior
        () => (Utils.jsonParse(Utils.jsonStringify([1, null, 2], false))[1] === null).toString(), "true");
    runTest("JSON serialization - check nested custom serialization",
        () => ([...Utils.jsonParse(Utils.jsonStringify({ p1: new Date(2021, 1, 10), p2: new Set([ new Uint8Array([1, 2, 3]) ]) } )).p2.values()][0][2] === 3).toString(), "true");

    componentTestPassCount = 0;
    for (const token of Utils._serializationTokens)
    {
        if (runTest(`JSON serialization - check for rejection of string values that overlap with internal token '${token}' (in a Set)`,
            () => Utils.jsonStringify(new Set<string>(["test", token]), false), `Found a Set entry ((Set)[1]) with a value ('${token}') which cannot be serialized because it's used as an internal token`, false, true))
        { componentTestPassCount++; }
    }
    runTest(`JSON serialization - [${componentTestPassCount} sub-tests passed] check for rejection of string values that overlap with internal tokens [in a Set]`,
        () => (componentTestPassCount === Utils._serializationTokens.length).toString(), "true");

    componentTestPassCount = 0;
    for (const token of Utils._serializationTokens)
    {
        if (runTest(`JSON serialization - check for rejection of string values that overlap with internal token '${token}' (in a Map key)`,
            () => Utils.jsonStringify(new Map<string, number>([[token, 123]]), false), `Found a Map key ((Map)[0].key) with a value ('${token}') which cannot be serialized because it's used as an internal token`, false, true))
        { componentTestPassCount++; }
    }
    runTest(`JSON serialization - [${componentTestPassCount} sub-tests passed] check for rejection of string values that overlap with internal tokens [in a Map key]`,
        () => (componentTestPassCount === Utils._serializationTokens.length).toString(), "true");

    componentTestPassCount = 0;
    for (const token of Utils._serializationTokens)
    {
        if (runTest(`JSON serialization - check for rejection of string values that overlap with internal token '${token}' (in a Map value)`,
            () => Utils.jsonStringify(new Map<number, string>([[123, token]]), false), `Found a Map value ((Map)[0].value) with a value ('${token}') which cannot be serialized because it's used as an internal token`, false, true))
        { componentTestPassCount++; }
    }
    runTest(`JSON serialization - [${componentTestPassCount} sub-tests passed] check for rejection of string values that overlap with internal tokens [in a Map value]`,
        () => (componentTestPassCount === Utils._serializationTokens.length).toString(), "true");

    componentTestPassCount = 0;
    for (const token of Utils._serializationTokens)
    {
        if (runTest(`JSON serialization - check for rejection of string values that overlap with internal token '${token}' (in an array)`,
            () => Utils.jsonStringify(["test", token], false), `Found an array element ((Array)[1]) with a value ('${token}') which cannot be serialized because it's used as an internal token`, false, true))
        { componentTestPassCount++; }
    }
    runTest(`JSON serialization - [${componentTestPassCount} sub-tests passed] check for rejection of string values that overlap with internal tokens [in an array]`,
        () => (componentTestPassCount === Utils._serializationTokens.length).toString(), "true");

    componentTestPassCount = 0;
    for (const token of Utils._serializationTokens)
    {
        if (runTest(`JSON serialization - check for rejection of string values that overlap with internal token '${token}' (in an object)`,
            () => Utils.jsonStringify({ p0: "test", p1: token }), `Found a property ((object).p1) with a value ('${token}') which cannot be serialized because it's used as an internal token`, false, true))
        { componentTestPassCount++; }
    }
    runTest(`JSON serialization - [${componentTestPassCount} sub-tests passed] check for rejection of string values that overlap with internal tokens [in an object]`,
        () => (componentTestPassCount === Utils._serializationTokens.length).toString(), "true");

    runTest("JSON serialization - check that 'Int8Array' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new Int8Array([1, 2, 3]), false))[1] === 2).toString(), "true");
    runTest("JSON serialization - check that 'Uint8Array' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new Uint8Array([1, 2, 3]), false))[1] === 2).toString(), "true");
    runTest("JSON serialization - check that 'Uint8Array' serializes (case #2)",
        () => Utils.jsonParse(Utils.jsonStringify(new Uint8Array([0, 7, 10, 63, 127, 255]), false)).toString(), "0,7,10,63,127,255");
    runTest("JSON serialization - check that 'Uint8ClampedArray' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new Uint8ClampedArray([1, 2, 3]), false))[1] === 2).toString(), "true");
    runTest("JSON serialization - check that 'Int16Array' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new Int16Array([1, 2, 3]), false))[1] === 2).toString(), "true");
    runTest("JSON serialization - check that 'Uint16Array' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new Uint16Array([1, 2, 3]), false))[1] === 2).toString(), "true");
    runTest("JSON serialization - check that 'Int32Array' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new Int32Array([1, 2, 3]), false))[1] === 2).toString(), "true");
    runTest("JSON serialization - check that 'Uint32Array' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new Uint32Array([1, 2, 3]), false))[1] === 2).toString(), "true");
    runTest("JSON serialization - check that 'Float32Array' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new Float32Array([1, 2.123, 3]), false))[1].toFixed(3) === "2.123").toString(), "true");
    runTest("JSON serialization - check that 'Float64Array' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new Float64Array([1, 2.123, 3]), false))[1].toFixed(3) === "2.123").toString(), "true");
    runTest("JSON serialization - check that 'BigInt64Array' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new BigInt64Array([BigInt(-1), BigInt(-2), BigInt(-3)]), false))[1] === BigInt(-2)).toString(), "true");
    runTest("JSON serialization - check that 'BigUint64Array' serializes",
        () => (Utils.jsonParse(Utils.jsonStringify(new BigUint64Array([BigInt(1), BigInt(2), BigInt(3)]), false))[1] === BigInt(2)).toString(), "true");

    runTest("JSON serialization - check that unsupported type 'WeakSet' does not serialize",
        () => Utils.jsonStringify({ p1: new WeakSet<String>() }, false), "(object).p1 (a property) is of type 'WeakSet' which is not supported for serialization", false);
    runTest("JSON serialization - check that unsupported type 'WeakMap' does not serialize",
        () => Utils.jsonStringify({ p1: new WeakMap<Number, String>() }, false), "(object).p1 (a property) is of type 'WeakMap' which is not supported for serialization", false);
    runTest("JSON serialization - check that unsupported type 'function' does not serialize",
        () => Utils.jsonStringify({ p1: [function foo (): void {}] }, false), "(object).p1[0] (an array element) is of type 'function' which is not supported for serialization", false);
    runTest("JSON serialization - check that unsupported type 'symbol' does not serialize",
        () => Utils.jsonStringify({ p1: new Set<symbol>([Symbol("sym")]) }, false), "(object).p1[0] (a Set entry) is of type 'symbol' which is not supported for serialization", false);

    const oVin = { m: new Map<number, string>([[1, "abc"], [2, "def"]]), s: new Set<number>([1,2,3]), e: new Error("e"), d: new Date(2021, 0, 27), r: RegExp(/a/g) };
    const oVout = Utils.jsonParse(Utils.jsonStringify(oVin));
    runTest("JSON serialization - check fidelity of various built-in types",
        () => ((oVout.m.get(2) === "def") && oVout.s.has(3) && (oVout.e.message === "e") && (oVout.d.toLocaleDateString() === "1/27/2021") && oVout.r.test("cat")).toString(), "true");

    class Foo {}
    const misc: any[] = [null, undefined, BigInt(123), new Error(), new EvalError(), new Foo(), new Date(), {}, new Object({}), 123, new Number(123), 
                         new Uint8Array([1,2,3]), true, new Boolean(false), "Hello", new String("Hello"), Symbol("symbol")];
    runTest("Get native type - miscellaneous check",
        () => misc.map(element => Meta.Type.getNativeType(element)).join(", "), "object, undefined, bigint, Error, EvalError, object, Date, object, object, number, number, Uint8Array, boolean, boolean, string, string, symbol");

    const testCount: number = _passCount + _failCount;
    const elapsedMs: number = Date.now() - startTime;

    // Attention: The AmbrosiaTest VS solution looks for "UNIT TESTS COMPLETE", so if you change it here you must change it there too (in \AmbrosiaTest\AmbrosiaTest\JS_Tests.cs)
    Utils.log(`...${testCount} UNIT TESTS COMPLETE (in ${elapsedMs}ms)`);
    reportTestSummary();
}

/** Logs a summary message about the number of tests that passed or failed. */
function reportTestSummary(): void
{
    const testCount: number = _passCount + _failCount;
    const passPercentage: number = (testCount === 0) ? 0 : ((_passCount / testCount) * 100);
    const failPercentage: number = (testCount === 0) ? 0 : (100 - passPercentage);
    const summaryColor: Utils.ConsoleForegroundColors = ((passPercentage === 100) || (testCount === 0)) ? Utils.ConsoleForegroundColors.Cyan : Utils.ConsoleForegroundColors.Red;
    const summaryMessage: string = `SUMMARY: ${_passCount} passed (${+passPercentage.toFixed(2)}%), ${_failCount} failed (${+failPercentage.toFixed(2)}%)`;

    Utils.logWithColor(summaryColor, "=".repeat(summaryMessage.length), undefined);

    // Attention: If you change the formatting, wording, or the use of color in this final message, then you MUST also update build.ps1 (which parses it)
    Utils.logWithColor(summaryColor, summaryMessage, undefined, Utils.LoggingLevel.Minimal);
}

// Custom serialization "quick-test"
function quickTest(): boolean
{
    let r0: RegExp = RegExp(/^[^"abc]$/g);
    let d0: Date = new Date();
    let s0: Set<string> = new Set<string>(["A"]);
    let m0: Map<number, string> = new Map([[1, "B"]]);
    let a0: Uint8Array = new Uint8Array([1, 2, -3]);
    let b0: BigInt = BigInt(-123);
    let o0: Object = new Object({ p3: [new String("s"), undefined, d0, Infinity, NaN, new Number(-Infinity), null, s0, a0, b0, m0] });
    let serialized: string = Utils.jsonStringify({ p1: r0, p2: [new Number(1.01), 2, new Boolean(true), o0, new Error("e")] });
    let deserialized: any = Utils.jsonParse(serialized);
    let success: boolean = (serialized === Utils.jsonStringify(deserialized)) && 
        (deserialized.p1 instanceof RegExp) && 
        (deserialized.p2[3].p3[2] instanceof Date) && 
        (deserialized.p2[3].p3[7] instanceof Set) && deserialized.p2[3].p3[7].has("A") &&
        (deserialized.p2[3].p3[10] instanceof Map) && (deserialized.p2[3].p3[10].get(1) === "B") &&
        (deserialized.p2[4] instanceof Error);
    return (success);
}

// Custom serialization test of a complex object with array members
function objectTest(): boolean
{
    const o: Utils.SimpleObject = 
    { 
        p01: Int8Array.from([-1,-2,-3]) ,
        p02: Uint8Array.from([1,2,3]),
        p03: Uint8ClampedArray.from([4,5,6]),
        p04: Int16Array.from([-7,-8,-9]),
        p05: Uint16Array.from([7,8,9]),
        p06: Int32Array.from([-10,-11,-12]),
        p07: Uint32Array.from([10,11,12]),
        p08: Float32Array.from([13.1,14.2,15.3]),
        p09: Float64Array.from([16.1,17.2,18.3]),
        p10: BigInt64Array.from([BigInt("-1234567891234567891"),BigInt("-1234567891234567892"),BigInt("-1234567891234567893")]),
        p11: BigUint64Array.from([BigInt("1234567891234567891"),BigInt("1234567891234567892"),BigInt("1234567891234567893")]),
        p12: [BigInt(-123),BigInt(456)]
    };

    const s: string = Utils.jsonStringify(o);
    const d: Utils.SimpleObject = Utils.jsonParse(s);

    for (let p = 1; p <= Object.keys(o).length; p++)
    {
        const propertyName: string = "p" + `0${p}`.slice(-2);
        for (let i = 0; i < o[propertyName].length; i++)
        {
            const originalValue: any = o[propertyName][i];
            const decodedValue: any = d[propertyName][i];
            if (originalValue !== decodedValue)
            {
                throw new Error(`Decoded value (${decodedValue}) does not match the original value (${originalValue}) for '${propertyName}'`);
            }
        }
    }
    return (true);
}

// Custom serialization test for special values (undefined, null, NaN, Infinty, -Infinity)
function specialValuesTest(): boolean
{
    if ((Utils.jsonParse(Utils.jsonStringify(undefined, false)) !== undefined) ||
        (Utils.jsonParse(Utils.jsonStringify(null, false)) !== null) ||
        !Number.isNaN(Utils.jsonParse(Utils.jsonStringify(NaN, false))) ||
        (Utils.jsonParse(Utils.jsonStringify(Infinity, false)) !== Infinity) ||
        (Utils.jsonParse(Utils.jsonStringify(-Infinity, false)) !== -Infinity))
    {
        throw new Error("null, NaN, Infinity or -Infinity serialization test failed");
    }
    return (true);
}

// Custom serialization test for a nested Set
function setTest(): boolean
{
    const set: Set<any> = new Set<any>();
    const subset: Set<any> = new Set<any>();
    
    set.add({ a: 1 });
    subset.add({ b: 2 });
    set.add(subset);
    
    const target = { x: set };
    const revivedTarget: any = Utils.jsonParse(Utils.jsonStringify(target));
    const setTestPassed: boolean = (revivedTarget.x instanceof Set) && 
        (revivedTarget.x.size === 2) && 
        ([...revivedTarget.x][0].a === 1) &&
        ([...revivedTarget.x][1] instanceof Set) && 
        ([...[...revivedTarget.x][1]][0].b === 2);

    return (setTestPassed);
}

/****************************/
/*  CODE-GEN TESTS SECTION  */
/****************************/

/** 
 * Runs all code-gen tests.
 * 
 * Note: These tests use "comp" files that represent the expected output of code-gen.
 * As code-gen changes, these files need to be kept up to date to avoid reporting spurious test failures.
 * This can be done automatically by using the command-line parameter 'testType=rebuildComps'.
 * 
 * The comp files ("EXPECTED_*.*") must be present in the same folder as the source input files.
 * A generated file is always deleted if it matches its comp file, but if it doesn't match it's
 * left on disk to enable diff'ing (with the comp file) to investigate what changed.
 */
function runCodeGenTests(rebuildCompFiles: boolean = false): void
{
    _passCount = _failCount = 0;

    const startTime: number = Date.now();
    const taskName: string = rebuildCompFiles ? "COMP-FILE REBUILD" : "TESTS";
    Utils.log(`RUNNING CODE-GEN ${taskName}...`);

    runCodeGenTest("./test/PI.ts", rebuildCompFiles);
    runCodeGenTest("./test/ASTTest.ts", rebuildCompFiles);
    runCodeGenTest("./test/NegativeTests.ts", rebuildCompFiles, true);

    const elapsedMs: number = Date.now() - startTime;
    Utils.log(`...CODE-GEN ${taskName} COMPLETE (in ${elapsedMs}ms)`);
    reportTestSummary();
}

/** 
 * Runs an individual code-gen test, which consists of either:
 * 1) Two tests that check both the generated consumer-side and publisher-side files against their comp files (when 'isNegativeTest' is false).
 * 2) One test that checks the output log file against a comp file (when 'isNegativeTest' is true).
 * 3) A rebuild of the comp file(s) used by the test (when 'rebuildCompFiles' is true).
 */
function runCodeGenTest(inputTSFile: string, rebuildCompFiles: boolean = false, isNegativeTest: boolean = false): void
{
    let generatedFileName: string = "";
    let compFileName: string = "";
    let inputTSFileName: string = inputTSFile;

    try
    {
        const outputPath: string = Path.dirname(inputTSFile);
        const suppressLogging: boolean = !rebuildCompFiles;

        if (!Path.extname(inputTSFile))
        {
            inputTSFile += ".ts";
        }
        inputTSFileName = Path.basename(inputTSFile).replace(Path.extname(inputTSFile), "");

        Meta.clearPublishedEntities();

        try
        {
            if (suppressLogging)
            {
                Utils.suppressLoggingOf(/.+/, isNegativeTest); // Suppress ALL logging (except errors) for brevity; for negative tests, even errors will be suppressed
            }
            const fileGenOptions: Meta.FileGenOptions =
            { 
                apiName: inputTSFileName, 
                fileKind: Meta.GeneratedFileKind.All, 
                mergeType: Meta.FileMergeType.None, 
                emitGeneratedTime: false,
                generatedFilePrefix: inputTSFileName + "_",
                publisherContactInfo: null,
                outputPath: outputPath,
                haltOnError: !isNegativeTest,
                // We set 'checkGeneratedTS' to false primarily to suppress the "Cannot find module 'ambrosia-node'" compilation issue when ambrosia-node isn't npm installed.
                // But further, because the comp files are "known good" we don't need to waste time compiling the generated files again to confirm what we already know.
                // When re-building comp files, we set 'checkGeneratedTS' to true so that we can check for any new errors.
                checkGeneratedTS: rebuildCompFiles
            };

            if (isNegativeTest)
            {
                // For a negative test, we only care about the publishing phase (not the emit phase)
                Meta.publishFromSource(inputTSFile, fileGenOptions);
            }
            else
            {
                Meta.emitTypeScriptFileFromSource(inputTSFile, fileGenOptions);
            }
        }
        finally
        {
            if (suppressLogging)
            {
                Utils.suppressLoggingOf(); // Resume logging
            }
        }

        /** [Local function] If needed, re-writes the compare file. */
        function rebuildCompFile(compFileName: string, generatedFileName: string):void
        {
            if (rebuildCompFiles)
            {
                // Copy the generated file (if it has changed) over the comp file
                if (compareFiles(compFileName, generatedFileName) !== "")
                {
                    Utils.log(`Warning: Updating comp file '${compFileName}'`);
                    File.copyFileSync(generatedFileName, compFileName);
                }
                else
                {
                    Utils.log(`Warning: Comp file '${compFileName}' did not change`);
                }
                _passCount++;
            }
        }

        /** [Local function] Compares the generated file with the compare file for the specified target ("consumer" or "publisher"). Optionally, also re-writes the compare file. */
        function runCompareTest(target: string): void
        {
            let outputFileName: string = "";
            if (target === "consumer") outputFileName = "ConsumerInterface.g.ts";
            if (target === "publisher") outputFileName = "PublisherFramework.g.ts";
            if (target === "") throw new Error(`Invalid 'target' ("${target}")`);

            generatedFileName = Path.join(outputPath, `${inputTSFileName}_${outputFileName}`);
            compFileName = Path.join(outputPath, `EXPECTED_${inputTSFileName}_${outputFileName}`);

            rebuildCompFile(compFileName, generatedFileName);

            // Note: This runs "silently" (no output) when rebuildCompFiles is true 
            if (runTest(`CodeGen - Check '${inputTSFileName}' ${target}-side file`, () => compareFiles(compFileName, generatedFileName), "", true, rebuildCompFiles))
            {
                Utils.deleteFile(generatedFileName);
            }
        }

        /** [Local function] Generates a filtered output log from the current output log, then compares it with the corresponding compare file for the input file. Optionally, also re-writes the compare file. */
        function runLogCompareTest(): void
        {
            generatedFileName = Path.join(outputPath, `${inputTSFileName}_FilteredOutputLog.txt`);
            compFileName = Path.join(outputPath, `EXPECTED_${inputTSFileName}_FilteredOutputLog.txt`);
            generateFilteredOutputLog(Utils.getOutputLog(), generatedFileName);

            rebuildCompFile(compFileName, generatedFileName);

            // Note: This runs "silently" (no output) when rebuildCompFiles is true 
            if (runTest(`CodeGen - Verify negative tests for '${inputTSFileName}.ts'`, () => compareFiles(compFileName, generatedFileName), "", true, rebuildCompFiles))
            {
                 Utils.deleteFile(generatedFileName);
            }
        }

        if (isNegativeTest)
        {
            runLogCompareTest();
        }
        else
        {
            runCompareTest("consumer");
            runCompareTest("publisher");
        }
    }
    catch (error: unknown)
    {
        _failCount++;
        Utils.logWithColor(Utils.ConsoleForegroundColors.Red, `Test 'CodeGen for ${inputTSFileName}.ts': FAILED (${Utils.makeError(error).message})`);
    }
}

/** Filters the contents of the specified 'logFileName' (by removing timestamps and references to [pathed] .ts files) and writes the filtered content to 'filteredLogFileName'. */
function generateFilteredOutputLog(logFileName: string, filteredLogFileName: string): void
{
    const lines = File.readFileSync(logFileName, { encoding: "utf8" }).split(Utils.NEW_LINE);
    const filteredLines: string[] = [];
    const TIMESTAMP_LENGTH: number = 25;
    let startLine: number = 0;

    // First, find the most recent "Publishing types and methods" section by searching the file from the end.
    // Note: We do this because when running with "testType=rebuildComps" the output log may contain multiple "Publishing types and methods" sections.
    for (let l = lines.length - 1; l >= 0; l--)
    {
        // Trim off timestamp
        let filteredLine: string = lines[l].substr(TIMESTAMP_LENGTH);

        if (filteredLine.startsWith("Publishing types and methods"))
        {
            startLine = l + 1;
            break;
        }
    } 

    // Create the filtered log file
    for (let l = startLine; l < lines.length; l++)
    {
        // Trim off timestamp
        let filteredLine: string = lines[l].substr(TIMESTAMP_LENGTH);

        if (filteredLine.startsWith("Publishing finished"))
        {
            break;
        }

        // Remove references to pathed .ts files
        filteredLine = filteredLine.replace(/(at|from)[ ].+\.ts(:[0-9]+:[0-9]+)?/g, "");

        filteredLines.push(filteredLine);
    }
    File.writeFileSync(filteredLogFileName, filteredLines.join(Utils.NEW_LINE));
}

/** 
 * Does a character comparison of 2 files, returning a reason for why the files don't match, or returning "" if they do match.
 * The content of 'fileA' is the "expected" content referred to in any returned reason.
 */
function compareFiles(fileA: string, fileB: string): string
{
    let linesA: string[];
    let linesB: string[];

    try
    {
        linesA = File.readFileSync(fileA, { encoding: "utf8" }).split(Utils.NEW_LINE);
        linesB = File.readFileSync(fileB, { encoding: "utf8" }).split(Utils.NEW_LINE);
    }
    catch (error: unknown)
    {
        return (Utils.makeError(error).message);
    }

    if (linesA.length !== linesB.length)
    {
        return (`files differ in their number of lines: ${linesA.length} lines expected but ${linesB.length} found`);
    }
    else
    {
        for (let l = 0; l < linesA.length; l++)
        {
            if (linesA[l].length !== linesB[l].length)
            {
                return (`line #${l + 1} differs in length: ${linesA[l].length} characters expected but ${linesB[l].length} found`);
            }
            for (let c = 0; c < linesA[l].length; c++)
            {
                const charA: string = linesA[l][c];
                const charB: string = linesB[l][c];
                if (charA !== charB)
                {
                    return (`at line ${l + 1} position ${c + 1}: '${charA}' expected but '${charB}' found`);
                }
            }
        }
        return (""); // Success: files match
    }
}

/*******************************/
/*  DATA-FORMAT TESTS SECTION  */
/*******************************/

/** Runs all data-format tests. */
function runDataFormatTests()
{
    _passCount = _failCount = 0;

    const startTime: number = Date.now();
    Utils.log("RUNNING DATA-FORMAT TESTS...");

    runTest("VarInt tests", () => runDataFormatTest(DataFormat.runVarIntTests, "VarInt"), "SUCCESS");
    runTest("FixedInt tests", () => runDataFormatTest(DataFormat.runFixedIntTests, "FixedInt"), "SUCCESS");

    const testCount: number = _passCount + _failCount;
    const elapsedMs: number = Date.now() - startTime;

    Utils.log(`...${testCount} DATA-FORMAT TESTS COMPLETE (in ${elapsedMs}ms)`);
    reportTestSummary();
}

/** Runs an individual data-format test. */
function runDataFormatTest(testFn: () => void, dataFormatTestType: string): string
{
    try
    {
        Utils.suppressLoggingOf(new RegExp(`${dataFormatTestType}|^$`, "g")); // This suppresses blank lines too
        testFn(); // If this throws, the test fails
    }
    finally
    {
        Utils.suppressLoggingOf();
    }
    return ("SUCCESS");
}