// Module for programmer-defined Ambrosia methods and types.
import File = require("fs");
import Path = require("path");
import ChildProcess = require("child_process");
// Note: Because ambrosia-node is a TypeScript library, and therefore any Node app using it will have to
//       install TypeScript to use it, we don't need to add "typescript" to the "dependencies" in package.json.
//       Further, since we're using the TypeScript compiler API to reflect on the developer's code, it's better to use
//       the same version of TypeScript that the developer is using to write their app (instead of a likely different
//       version tied to ambrosia-node [see https://www.typescriptlang.org/docs/handbook/module-resolution.html]).
import TS = require("typescript"); // For TypeScript AST parsing
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "./Configuration";
import * as IC from "./ICProcess";
import * as Messages from "./Messages";
import * as Utils from "./Utils/Utils-Index";
import * as Root from "./AmbrosiaRoot";

// Shorthand for "Utils.assertDefined()".
// For example, this is used with the optional FileGenOptions members (which all have default values) to
// both transform their type (to satisfy 'strictNullChecks') and to verify that they are not _undefined_ 
// (which the FileGenOptions constructor explicitly prevents).
import { assertDefined } from "./Utils/Utils";

/** 
 * The methods that this Ambrosia app/service has published (with publishMethod/publishPostMethod).
 */
let _publishedMethods: { [methodName: string]: { [version: number]: Method } } = {};
/** 
 * The types that this Ambrosia app/service has published (with publishType). Typically, these are complex types but they may also be enums.\
 * The key is the type name (eg. "employee"), and the value is the type definition (eg. "{ name: { firstName: string, lastName: string}, startDate: number }").
 */
let _publishedTypes: { [typeName: string]: Type } = {};
/** 
 * The types that have been referenced by other types, but which have not yet been published (ie. forward references).
 * This list must be empty for publishMethod() and publishPostMethod() to succeed. "Dangling" types (ie. references to
 * unpublished types) can still happen if the user only publishes types but no methods (which is essentially pointless).
 * 
 * Key = Missing type name, Value = Description of the usage context where the missing type was encountered.
 */
let _missingTypes: Map<string, string> = new Map<string, string>();

/** 
 * [Internal] Clears all published types and methods.\
 * This method is for **internal testing only**.
 */
export function clearPublishedEntities(): number
{
    const entitiesCleared: number = Object.keys(_publishedMethods).length + Object.keys(_publishedTypes).length;

    _publishedMethods = {};
    _publishedTypes = {};
    _missingTypes.clear();
    AST.clearPublishedSourceFile();

    return (entitiesCleared);
}

/** 
 * [Interna] Class that describes a method that can be called using Ambrosia.\
 * The meta data is used to both check incoming post method calls (see postInterceptDispatcher) and for documentation purposes (see getPublishedMethodsAsync).
 * Identifiers and types will be checked for correctness. 
 */
export class Method
{
    /** The ID of the method. */
    id: number;
    /** The name of the method. */
    name: string;
    /**
     * The type returned by the method.\
     * Simple type examples: "void", "number", "boolean", "string", "Uint8Array".\
     * Complex type example: "{ userName: string, phoneNumbers: number[] }".\
     * Will always be "void" for a non-post method.
     */
    returnType: string;
    /** The names of the method parameters. */
    parameterNames: string[];
    /** The types of the method parameters (see returnType for examples). */
    parameterTypes: string[];
    /** The expanded versions of parameterTypes (ie. all published types replaced with their expandedDefinitions, and without any generic-type specifiers). */
    expandedParameterTypes: string[];
    /** The version number of the method. Will always be 1 for a non-post method (to retain compatibility with C# Fork/Impulse methods, whose RPC message format doesn't include a version). */
    version: number;
    /** 
     * A flag indicating whether the parameter types of the [post] method should be checked when the method is called. 
     * If the method relies on JavaScript type coercion, then set this to false.
     */
    isTypeChecked: boolean;
    /** [Internal] Options to facilitate code generation for the method. */
    codeGenOptions: CodeGenOptions | null = null;
    
    /** [ReadOnly] True if the method is a 'post' method. */
    get isPost(): boolean { return (this.id === IC.POST_METHOD_ID); }

    /** [ReadOnly] True if the method takes raw-byte (ie. custom serialized) parameters (specified as a single parameter "rawParams: Uint8Array"). */
    get takesRawParams(): boolean { return ((this.parameterNames.length === 1) && (this.parameterNames[0] === "rawParams") && (this.parameterTypes[0] === "Uint8Array")); }

    /** 
     * ReadOnly] The name of the method for use in a TypeScript (TS) wrapper for the method (produced during code generation). 
     * Only when the version of the method is greater than 1 will the 'nameForTsWrapper' be different from 'name' (eg. "myMethod_v3"),
     * which means this only applies to post methods.
     */
    get nameForTSWrapper(): string { return ((this.version === 1) ? this.name : `${this.name}_v${this.version}`); }

    constructor(id: number, name: string, version: number = 1, parameters: string[] = [], returnType: string = "void", doRuntimeTypeChecking: boolean = true, codeGenOptions?: CodeGenOptions)
    {
        this.id = id;
        this.name = name.trim();
        this.parameterNames = [];
        this.parameterTypes = [];
        this.expandedParameterTypes = [];
        this.version = this.isPost ? version : 1;
        this.codeGenOptions = codeGenOptions || null;
        this.returnType = this.isPost ? Type.formatType(returnType) : "void";
        this.isTypeChecked = this.isPost ? doRuntimeTypeChecking : false;
        this.validateMethod(name, parameters, this.returnType);
    }

    /** Throws if any of the method parameters (or the returnType) are invalid. */
    private validateMethod(methodName: string, parameters: string[], returnType?: string): void
    {
        let firstOptionalParameterFound: boolean = false;

        checkName(methodName, "method");

        for (let i = 0; i < parameters.length; i++)
        {
            let pos: number = parameters[i].indexOf(":");
            if (pos === -1)
            {
                throw new Error(`Method '${methodName}' has a malformed method parameter ('${parameters[i]}')`);
            }

            let paramName: string = parameters[i].substring(0, pos).trim();
            let paramType: string = parameters[i].substring(pos + 1).trim();
            let isOptionalParam: boolean = paramName.endsWith("?");
            let isRestParam: boolean = paramName.startsWith("...");
            let description: string = `parameter '${paramName}' of method '${methodName}'`;

             // Note: checkType() will also check paramName
            checkType(TypeCheckContext.Method, paramType, paramName, description, true, true);

            this.parameterNames.push(paramName);
            const formattedParamType: string = Type.formatType(paramType);
            this.parameterTypes.push(formattedParamType);

            // This is an optimization so that we don't have to repeatedly expand parameter types at runtime
            let expandedType: string = Type.expandType(this.parameterTypes[i]);
            if (!expandedType)
            {
                // This should never happen since publishMethod() and publishPostMethod() both call checkForMissingPublishedTypes()
                throw new Error(`Unable to expand the type definition for parameter '${paramName}' of method '${methodName}'`);
            }
            if ((this.parameterTypes[i] !== "any") && (expandedType === "any"))
            {
                reportTypeSimplificationWarning(`the type of parameter '${paramName}' of method '${methodName}'`, this.parameterTypes[i]);
            }
            this.expandedParameterTypes.push(expandedType); 

            // Check that any optional parameters come AFTER all non-optional parameters [to help code-gen in emitTypeScriptFileEx()]
            if (isOptionalParam && !firstOptionalParameterFound)
            {
                firstOptionalParameterFound = true;
            }
            if (firstOptionalParameterFound && !isOptionalParam)
            {
                throw new Error(`Required parameter '${paramName}' of method '${methodName}' must be specified before all optional parameters`);
            }

            if (isRestParam)
            {
                // Check that a rest parameter is an array
                if (!formattedParamType.endsWith("[]"))
                {
                    throw new Error(`Rest parameter '${paramName}' of method '${methodName}' must be an array`);
                }
                // Check that a rest parameter comes last in the parameter list
                if (i !== parameters.length - 1)
                {
                    throw new Error(`Rest parameter '${paramName}' of method '${methodName}' must be specified after all other parameters`);
                }
            }
        }

        // Post methods don't support a single 'rawParams' parameter like Fork and Impulse methods do (post methods ALWAYS serialize parameters as JSON)
        if (this.isPost && this.takesRawParams)
        {
            // Note: The user can easily workaround this error by simply using a parameter name other than "rawParams". The reason
            //       for throwing the error is to ensure that code-gen/Method.makeTSWrappers() and Method.getSignature() produce 
            //       the correct signature for the method. It also informs the user that Post methods do not support the 'rawParams: Uint8Array'
            //       pattern (like non-Post methods do), and that Post method parameters will always be serialized to JSON.
            throw new Error(`Post method '${methodName}' is defined as taking a single 'rawParams' Uint8Array parameter; Post methods do NOT support custom (raw byte) parameter serialization - all parameters are always serialized to JSON`);
        }

        if (returnType && !Utils.equalIgnoringCase(returnType, "void"))
        {
            const typeDescription: string = `return type of method '${methodName}'`;
            const result: { kindFound: CompoundTypeKind, components: string[] } = Type.getCompoundTypeComponents(returnType); // returnType can be an [in-line] union or intersection

            if (result.components.length > 0)
            {
                const kindName: string = CompoundTypeKind[result.kindFound].toLowerCase();
                result.components.map((t, i) => checkType(TypeCheckContext.Method, t, null, `${kindName}-type component #${i + 1} of ${typeDescription}`));
            }
            else
            {
                checkType(TypeCheckContext.Method, returnType, null, typeDescription);
            }
        }
    }

    /** Returns the method definition as XML, including the Ambrosia call signatures for the method. */ 
    getXml(expandTypes: boolean = false): string
    {
        let xml: string = `<Method isPost=\"${this.isPost}\" name=\"${this.name}\" `;
        
        if (this.isPost)
        {
            xml += `version=\"${this.version}\" resultType=\"${expandTypes ? Type.expandType(this.returnType) : this.returnType}\" isTypeChecked=\"${this.isTypeChecked}\">`;
        }
        else
        {
            xml += `id=\"${this.id}\">`;
        }
        
        for (let i = 0; i < this.parameterNames.length; i++)
        {
            xml += `<Parameter name="${this.parameterNames[i]}" type="${expandTypes ? Type.expandType(this.parameterTypes[i]) : this.parameterTypes[i]}"/>`;
        }

        if (this.isPost)
        {
            xml += `<CallSignature type=\"Fork\">${Utils.encodeXmlValue(this.getSignature(Messages.RPCType.Fork, expandTypes))}</CallSignature>`;
            xml += `<CallSignature type=\"Impulse\">${Utils.encodeXmlValue(this.getSignature(Messages.RPCType.Impulse, expandTypes))}</CallSignature>`;
        }
        else
        {
            xml += `<CallSignature type=\"Fork\">${Utils.encodeXmlValue(this.getSignature(Messages.RPCType.Fork, expandTypes))}</CallSignature>`;
            xml += `<CallSignature type=\"Impulse\">${Utils.encodeXmlValue(this.getSignature(Messages.RPCType.Impulse, expandTypes))}</CallSignature>`;
            xml += `<CallSignature type=\"Fork\">${Utils.encodeXmlValue(this.getSignature(Messages.RPCType.Fork, expandTypes, true))}</CallSignature>`;
            xml += `<CallSignature type=\"Impulse\">${Utils.encodeXmlValue(this.getSignature(Messages.RPCType.Impulse, expandTypes, true))}</CallSignature>`;
        }
        xml += "</Method>";
        return (xml);
    }

    /** Returns a psuedo-JS "template" of the Ambrosia call signature for the method. */
    getSignature(rpcType: Messages.RPCType, expandTypes: boolean = false, enqueueVersion: boolean = false): string
    {
        let paramList: string = "";
        let localInstanceName: string = Configuration.loadedConfig().instanceName;

        for (let i = 0; i < this.parameterNames.length; i++)
        {
            let paramName: string = Method.trimRest(this.parameterNames[i]);
            let paramType: string = expandTypes ? Type.expandType(this.parameterTypes[i]) : this.parameterTypes[i];
            
            if (this.isPost)
            {
                let isComplexType: boolean = (paramType[0] === "{");
                paramList += `${(i > 0) ? ", " : ""}IC.arg("${paramName}", ${isComplexType ? paramType : `${paramType}`})`;
            }
            else
            {
                paramList += `${(i > 0) ? ", " : ""}${paramName}: ${paramType}`;
            }
        }

        if (this.isPost)
        {
            return (`IC.post${Messages.RPCType[rpcType]}("${localInstanceName}", "${this.name}", ${this.version}, -1, null /* callContextData */, ${paramList});`);
        }
        else
        {
            paramList = this.takesRawParams ? `<${paramList}>` : `{ ${paramList} }`;
            return (`IC.${enqueueVersion ? "queue" : "call"}${Messages.RPCType[rpcType]}("${localInstanceName}", ${this.id}, ${paramList});`);
        }
    }

    /** Returns the parameters of the method in a form suitable for constructing a TypeScript function/method. */
    makeTSFunctionParameters(): string
    {
        let functionParameters: string = "";
        for (let i = 0; i < this.parameterNames.length; i++)
        {
            functionParameters += `${this.parameterNames[i]}: ${this.parameterTypes[i]}` + ((i === this.parameterNames.length - 1) ? "" : ", ");
        }
        return (functionParameters);
    }

    /** Removes the "..." rest parameter prefix (if any). */
    static trimRest(paramName: string): string
    {
        return (paramName.replace(/^\.\.\./, ""));
    }

    /** 
     * Returns definitions for TypeScript wrapper functions for the published method. 
     * Post methods produce a Fork-based ("xxxx_Post") and an Impulse-based ("xxxx_PostByImpulse") wrapper.\
     * Non-post methods produce a Fork and Impulse wrapper, and an EnqueueFork and EnqueueImpulse wrapper [for explicit batching that must be flushed using IC.flushQueue()].
     */
    makeTSWrappers(startingIndent: number = 0, fileOptions: FileGenOptions, jsDocComment?: string): string
    {
        const tabIndent: number = fileOptions.tabIndent || 4;
        const publisherContactInfo: string = fileOptions.publisherContactInfo || ""; 
        const apiName: string = fileOptions.apiName;
        const NL: string = Utils.NEW_LINE; // Just for short-hand
        const pad: string = " ".repeat(startingIndent);
        const tab: string = " ".repeat(tabIndent);
        const NOTE_PLACEHOLDER : string = "[NOTE_PLACEHOLDER]";
        let wrapperFunctions: string = "";
        let functionParameters: string = this.makeTSFunctionParameters();

        jsDocComment = jsDocComment ? jsDocComment.split(NL).map(line => pad + line).join(NL) + NL : "";
        if (jsDocComment)
        {
            const lines: string[] = jsDocComment.trimEnd().split(NL);

            if (lines.length === 1)
            {
                const matches: RegExpExecArray | null = /^\/\*\*(.+)\*\/$/g.exec(lines[0].trim());
                const commentBody: string = (matches && (matches.length > 0)) ? matches[1].trim() : "";
                jsDocComment = pad + "/**" + NL + pad + " * " + NOTE_PLACEHOLDER + NL + pad + " * " + NL + pad + " * " + commentBody + NL + pad + " */" + NL;
            }
            else
            {
                const matches: RegExpExecArray | null = /^\/\*\*(.+)$/g.exec(lines[0].trim());
                const firstLineBody: string = (matches && (matches.length > 0)) ? matches[1].trim() : "";
                jsDocComment = pad + "/**" + NL + pad + " * " + NOTE_PLACEHOLDER + NL + pad + " * " + NL;
                if (firstLineBody)
                {
                    jsDocComment += pad + " * " + firstLineBody + NL;
                }
                jsDocComment += lines.slice(1, lines.length).join(NL) + NL;
            }
        }
        else
        {
            jsDocComment = pad + "/**" + NL + pad + " * " + NOTE_PLACEHOLDER + NL + pad + " */" + NL;
        }

        if (this.isPost)
        {
            let postFunction: string = `${pad}export function ${this.nameForTSWrapper}_Post[WRAPPER_SUFFIX]`; // Since there is no "_PostImpulse" we don't use "_PostFork", just "_Post"
            let postFunctionParameters: string = "";
            let postArgs: string = "";

            postFunctionParameters = `(callContextData: any${(functionParameters.length > 0) ? ", " : ""}${functionParameters}): [RETURN_TYPE]`;

            for (let i = 0; i < this.parameterNames.length; i++)
            {
                if (this.parameterNames.length > 1)
                {
                    postArgs += pad + tab.repeat(2);
                }
                postArgs += `IC.arg("${Method.trimRest(this.parameterNames[i])}", ${Method.trimRest(this.parameterNames[i]).replace("?", "")})` + ((i === this.parameterNames.length - 1) ? ");" : ", ") + NL;
            }

            // Reminder: When IC.postFork()/postByImpulse() is called, the return value is processed via the PostResultDispatcher
            let postFunctionBody: string = pad + "{" + NL;
            postFunctionBody += pad + tab + "checkDestinationSet();" + NL;
            postFunctionBody += pad + tab + `[CALL_ID]IC.post[RPC_TYPE_TOKEN](_destinationInstanceName, "${this.name}", ${this.version}, _postTimeoutInMs, callContextData` + (this.parameterNames.length > 0 ? ", " : `);${NL}`) + (this.parameterNames.length > 1 ? NL : "") + postArgs;
            postFunctionBody += pad + "[RETURN_STATEMENT]" + "}";

            // Add a comment about how to obtain the result (and the type of the result)
            const resultNote: string = `The result (${this.returnType.replace(/`/g, "\\`")}) produced by this post method is received via the PostResultDispatcher provided to IC.start().[CALL_ID_RETURN_COMMENT]`;
            jsDocComment = addNote(jsDocComment, resultNote, false);
           
            const postForkFunction: string = (postFunction + postFunctionParameters + NL + postFunctionBody)
                    .replace(/\[WRAPPER_SUFFIX]/g, "").replace(/\[RPC_TYPE_TOKEN]/g, "Fork").replace(/\[RETURN_TYPE]/g, "number")
                    .replace(/\[CALL_ID]/g, "const callID = ").replace(/\[RETURN_STATEMENT]/g, tab + "return (callID);" + NL + pad);
            const postByImpulseFunction: string = (postFunction + postFunctionParameters + NL + postFunctionBody)
                    .replace(/\[WRAPPER_SUFFIX]/g, "ByImpulse").replace(/\[RPC_TYPE_TOKEN]/g, "ByImpulse").replace(/\[RETURN_TYPE]/g, "void")
                    .replace(/\[CALL_ID]/g, "").replace(/\[RETURN_STATEMENT]/g, "");
            const postForkComment: string = addNote(jsDocComment.replace(/\[CALL_ID_RETURN_COMMENT]/g, " Returns the post method callID."), "\"_Post\" methods should **only** be called from deterministic events.");
            const postByImpulseComment: string = addNote(jsDocComment.replace(/\[CALL_ID_RETURN_COMMENT]/g, ""), "\"_PostByImpulse\" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).");
            wrapperFunctions += postForkComment + postForkFunction + NL.repeat(2);
            wrapperFunctions += postByImpulseComment + postByImpulseFunction;
        }
        else
        {
            let jsonArgs: string = "{}";

            if (this.parameterNames.length > 0)
            {
                jsonArgs = "{ ";
                for (let i = 0; i < this.parameterNames.length; i++)
                {
                    const paramName: string = Method.trimRest(this.parameterNames[i]).replace("?", "");
                    jsonArgs += `${(i > 0) ? ", " : ""}${paramName}: ${paramName}`;
                }
                jsonArgs += " }";
            }

            let jsonOrRawArgs: string = this.takesRawParams ? this.parameterNames[0].replace("?", "") : jsonArgs;
            let functionTemplate: string = `${pad}export function ${this.name}[NAME_TOKEN](${functionParameters}): void` + NL;
            functionTemplate += pad + "{" + NL;
            functionTemplate += pad + tab + "checkDestinationSet();" + NL;
            functionTemplate += pad + tab + `IC.[IC_METHOD_TOKEN](_destinationInstanceName, ${this.id}, ` + jsonOrRawArgs + ");" + NL;
            functionTemplate += pad + "}";

            if (this.takesRawParams)
            {
                // If not already described in the jsDocComment, add a JSDoc @param tag for the [lone] 'rawParams' parameter [because it's "special"]
                if (!jsDocComment || !RegExp("\\*[ ]*@param[ ]+(?:{.+}[ ]+)?rawParams[ ]").test(jsDocComment))
                {
                    const contactInfo: string = publisherContactInfo ? ` (${publisherContactInfo})` : "";
                    const newComment: string = `@param rawParams A custom serialization (byte array) of all required parameters. Contact the '${apiName}' API publisher${contactInfo} for details of the serialization format.`; 
                    if (jsDocComment)
                    {
                        jsDocComment = jsDocComment.trimEnd().replace("*/", "").trimEnd() + NL + pad + " * " + newComment + NL + pad + " */" + NL;
                    }
                    else
                    {
                        jsDocComment = pad + `/** ${newComment} */` + NL;
                    }
                }
            }

            let forkFunction: string = functionTemplate.replace(/\[NAME_TOKEN]/g, "_Fork").replace(/\[IC_METHOD_TOKEN]/g, "callFork");
            let impulseFunction: string = functionTemplate.replace(/\[NAME_TOKEN]/g, "_Impulse").replace(/\[IC_METHOD_TOKEN]/g, "callImpulse");
            let enqueueForkFunction: string = functionTemplate.replace(/\[NAME_TOKEN]/g, "_EnqueueFork").replace(/\[IC_METHOD_TOKEN]/g, "queueFork");
            let enqueueImpulseFunction: string = functionTemplate.replace(/\[NAME_TOKEN]/g, "_EnqueueImpulse").replace(/\[IC_METHOD_TOKEN]/g, "queueImpulse");
            wrapperFunctions += addNote(jsDocComment, "\"_Fork\" methods should **only** be called from deterministic events.") + forkFunction + NL.repeat(2);
            wrapperFunctions += addNote(jsDocComment, "\"_Impulse\" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).") + impulseFunction + NL.repeat(2);
            wrapperFunctions += addNote(jsDocComment, "\"_EnqueueFork\" methods should **only** be called from deterministic events, and will not be sent until IC.flushQueue() is called.") + enqueueForkFunction + NL.repeat(2);
            wrapperFunctions += addNote(jsDocComment, "\"_EnqueueImpulse\" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc), and will not be sent until IC.flushQueue() is called.") + enqueueImpulseFunction;
        }
        
        return (wrapperFunctions);

        /** [Local function] In the supplied jsDocComment, replaces the NOTE_PLACEHOLDER token with a "Note: {note}". */
        function addNote(jsDocComment: string, note: string, isFinalNote: boolean = true): string
        {
            const newPlaceHolder: string = isFinalNote ? "" : (NL + `${pad} * ` + NL + `${pad} * ${NOTE_PLACEHOLDER}`);
            let modifiedComment = jsDocComment.replace(NOTE_PLACEHOLDER, `*Note: ${note}*${newPlaceHolder}`);
            return (modifiedComment);
        }
    }
}

/** Either 'None', 'Union', 'Intersection', or 'All'. */
enum CompoundTypeKind
{
    None,
    Union,
    Intersection,
    All
}

/** 
 * [Internal] Class that describes a published (named) type - typically a complex type - that can be used in an Ambrosia method, or in another published type.\
 * The class also includes several type utility methods, like getNativeTypes().
 */
export class Type
{
    /** The type name, eg. "employee". */
    name: string;
    /** 
     * The definition of the type, which may include references to other published (named) types, 
     * eg. "{ employeeName: name, startDate: number, jobs: Job[] }".
     */
    definition: string;
    /** 
     * The definition of the type with all published (named) types replaced with their expandedDefinitions, eg.
     * "{ employeeName: { firstName: string, lastName: string}, startDate: number }", and without any generic-type specifiers.
     */
    expandedDefinition: string;
    /** If this type is an Enum, these are the available values (eg. "1=Foo,2=Bar"). */
    enumValues: string | null = null; // Note: We do NOT check enum ranges as part of runtime type-checking (this was explored, but deemed to add too much complexity; plus, no other type values are range checked by the LB)
    /** [Internal] Options to facilitate code generation for the type. */
    codeGenOptions: CodeGenOptions | null = null;

    /** [ReadOnly] Whether the type is a complex type. */
    get isComplexType(): boolean { return (this.definition.startsWith("{")); } 

    constructor(typeName: string, typeDefinition: string, enumValues: string | null = null, codeGenOptions?: CodeGenOptions)
    {
        typeName = typeName.trim();
        typeDefinition = typeDefinition.trim();

        const typeDescription: string = `published type '${typeName}'`;
        checkType(TypeCheckContext.Type, typeDefinition, typeName, typeDescription, false); // Note: A published type name cannot be optional (ie. end with '?')

        this.name = typeName;
        this.definition = Type.formatType(typeDefinition);
        this.expandedDefinition = Type.expandType(this.definition);
        if ((typeDefinition === "number") && enumValues)
        {
            this.enumValues = enumValues;
        }
        this.codeGenOptions = codeGenOptions || null;
    }

    /**
     * Attempts to parse the supplied 'typeDefinition' as the specified CompoundTypeKind, and returns a { kindFound, components } result.\
     * For example, given "A | (B & C)" and a 'kind' of CompoundTypeKind.All (the default), the result will be { CompoundTypeKind.UnionType,  ["A", "(B & C)"] }.\
     * Returns { CompoundTypeKind.None, [] }  if the 'typeDefinition' is not a compound type of the specified 'kind'.
     */
    static getCompoundTypeComponents(typeDefinition: string, kind: CompoundTypeKind = CompoundTypeKind.All): { kindFound: CompoundTypeKind, components: string[] }
    {
        const compoundType = typeDefinition.replace(/^\(/, "").replace(/[\[\]]*$/, "").replace(/\)$/, "") // Convert "(A | B)[]" to "A | B"
        const unionRegEx: RegExp = /(?<![:{\(][^|&]+)[|]/g; // Takes into account bracketed/braced components, like "A | (B & C)[] | (D | E) | { p1: F | G}"
        const intersectionRegEx: RegExp = /(?<![:{\(][^|&]+)[&]/g; // Takes into account bracketed/braced components, like "A | (B & C)[] | (D | E) | { p1: F | G}"
        let compoundComponents: string[] = [];
        let foundKind: CompoundTypeKind = CompoundTypeKind.None;

        switch (kind)
        {
            case CompoundTypeKind.Union:
                compoundComponents = compoundType.split(unionRegEx);
                foundKind = (compoundComponents.length > 1) ? kind : CompoundTypeKind.None;
                break;
            case CompoundTypeKind.Intersection:
                compoundComponents = compoundType.split(intersectionRegEx);
                foundKind = (compoundComponents.length > 1) ? kind : CompoundTypeKind.None;
                break;
            case CompoundTypeKind.All:
                // In TypeScript, intersection has higher precedence than union, so "A | B & C" is evaluated as "A | (B & C)" (see https://github.com/microsoft/TypeScript/pull/3622).
                // This is why we parse for union (lower precedence) before intersection (higher precedence).
                const unionComponents: string[] = compoundType.split(unionRegEx);
                if (unionComponents.length > 1) // length can be 1 if the type is an intersection type that contains a [bracketed] union component (eg. "A & (B | C)")
                {
                    compoundComponents = unionComponents;
                    foundKind = CompoundTypeKind.Union;
                }
                else
                {
                    const intersectionComponents: string[] = compoundType.split(intersectionRegEx);
                    if (intersectionComponents.length > 1) // length can be 1 if the type is an union type that contains a [bracketed] intersection component (eg. "A | (B & C)")
                    {
                        compoundComponents = intersectionComponents;
                        foundKind = CompoundTypeKind.Intersection;
                    }
                }
                break;
            default:
                throw new Error(`Unsupported CompoundTypeKind '${CompoundTypeKind[kind]}'`);
        }
        return ({ kindFound: foundKind, components: (compoundComponents.length > 1) ? compoundComponents.map(c => c.trim()) : [] });
    }

    /** Returns true if the type is currently referenced by any published method. */
    isReferenced(): boolean
    {
        for (const name in _publishedMethods)
        {
            for (const version in _publishedMethods[name])
            {
                const method: Method = _publishedMethods[name][version];

                if (method.returnType === this.name)
                {
                    return (true);
                }

                for (let i = 0; i < method.parameterTypes.length; i++)
                {
                    if (method.parameterTypes[i] === this.name)
                    {
                        return (true);
                    }
                }
            }
        }
        return (false);
    }

    /** Returns a TypeScript class, type-alias, or enum definition for the published type. */
    makeTSType(startingIndent: number = 0, tabIndent: number = 4, jsDocComment?: string, isPublic: boolean = true): string
    {
        const NL: string = Utils.NEW_LINE; // Just for short-hand
        const tab: string = " ".repeat(tabIndent);
        const pad: string = " ".repeat(startingIndent);
        let className: string = this.name;
        let classDefinition: string = "";

        jsDocComment = jsDocComment ? jsDocComment.split(NL).map(line => pad + line).join(NL) + NL : "";

        if (!this.isComplexType)
        {
            if (this.enumValues)
            {
                let enumValues: string = "{ " + this.enumValues.split(",").map(pair => `${pair.split("=")[1]} = ${pair.split("=")[0]}`).join(", ") + " }";
                return (`${jsDocComment}${pad}${isPublic ? "export " : ""}enum ${this.name} ${enumValues}`);
            }
            else
            {
                // An aliased simple type doesn't need a full-blown class definition
                return (`${jsDocComment}${pad}${isPublic ? "export " : ""}type ${this.name} = ${this.definition};`);
            }
        }

        // Is the type an array of a complex type, eg. "{ p1: string }[]" ? 
        // If so, we create BOTH a class (eg. "class Foo_Element") for the "base" type (eg. { p1: string }) and an alias (eg. "type Foo = Foo_Element[];") for the array of the "base" type.
        // This makes it easier for the developer using ConsumerInterface.g.ts to create an instance of, in this case, Foo (eg. "let f: Foo = [ new Foo_Element(...) ];").
        // If the user wants to control the naming of the auto-generated "base" type (eg. "Foo_Element"), they can explicitly publish the "base" type separately (which is recommended).
        const arraySuffix: string = Type.getArraySuffix(this.definition);
        if (arraySuffix)
        {
            className = className + "_Element"; // An auto-generated name
            while (_publishedTypes[className])
            {
                className = "_" + className; // Keep prepending "_" until we find a unique name
            }
            classDefinition = `${pad}${isPublic ? "export " : ""}type ${this.name} = ${className}${arraySuffix};` + NL.repeat(2);
        }

        classDefinition += `${pad}${isPublic ? "export " : ""}class ${className}` + NL + pad + '{' + NL;
        let topLevelTokens: string[] = Type.tokenizeComplexType(Type.removeArraySuffix(this.definition), false); // We DON'T remove generics here since we're creating a TS type

        if (topLevelTokens.length % 2 !== 0)
        {
            throw new Error(`Published type '${this.name}' could not be tokenized (it contains ${topLevelTokens.length} tokens, not an even number as expected)`);
        }

        // Add class members [all public]
        for (let i = 0; i < topLevelTokens.length; i += 2)
        {
            let nameToken: string = topLevelTokens[i];
            let typeToken: string = Type.formatType(topLevelTokens[i + 1]);
            classDefinition += `${pad}${tab}${nameToken} ${typeToken};` + NL;
        }

        // Add class constructor
        classDefinition += NL + `${pad}${tab}constructor(`;
        for (let i = 0; i < topLevelTokens.length; i += 2)
        {
            let nameToken: string = topLevelTokens[i];
            let typeToken: string = Type.formatType(topLevelTokens[i + 1]);
            classDefinition += `${nameToken} ${typeToken}` + ((i === topLevelTokens.length - 2) ? ")" : ", " );
        }
        classDefinition += NL + pad + tab + "{" + NL;
        for (let i = 0; i < topLevelTokens.length; i += 2)
        {
            let propertyName: string = topLevelTokens[i].substring(0, topLevelTokens[i].length - 1);
            classDefinition += `${pad}${tab.repeat(2)}this.${propertyName} = ${propertyName};` + NL;
        }
        classDefinition += pad + tab + "}" + NL + pad + "}";

        return (jsDocComment + classDefinition);
    }

    /** Formats the type definition into a displayable, mono-spaced format. */
    static formatType(typeDefinition: string): string
    {
        const isTemplateStringType: boolean = typeDefinition.trim().startsWith("`");

        if (isTemplateStringType)
        {
            // We preserve spacing (or lack thereof) for a template string type
            return (typeDefinition);
        }

        let formattedTypeDefinition: string = typeDefinition.replace(/\s+/g, ""); // Remove all space
        formattedTypeDefinition = formattedTypeDefinition.replace(/}/g, " }").replace(/[{:,]/g, "$& "); // Add space before '}' and after '{', ':', ','
        formattedTypeDefinition = formattedTypeDefinition.replace(/\[](?=[^,\[]])/g, "[] "); // Add space after trailing '[]'
        formattedTypeDefinition = formattedTypeDefinition.replace(/[&|]/g, " $& "); // Add space before and after '|' and '&' (union and intersection)
        return (formattedTypeDefinition); // Note: Arrays of complex types will be formatted as "{...}[]", ie. with no space between '}' and '['
    }

    /** 
     * Returns the expanded definition of the specified type (ie. with named [published] types replaced, but without any generic-types specifiers).
     * Returns "" if the type cannot be expanded (due to unresolved forward references).\
     * Throws if expanding the type isn't possible because of a circular reference.
     */
    static expandType(type: string, parentTypeChain: string[] = []): string
    {
        type = type.trim();

        // Check for a circular reference (since we can't expand in this case)
        if (parentTypeChain.indexOf(type) !== -1)
        {
            // TODO: This message would be better if it could reference the actual published types involved (simply looking
            //       them up by their definition is not viable since multiple published types can have the same definition)
            throw new Error(`Unable to expand type definition '${type}' because it has a circular reference with definition '${parentTypeChain[parentTypeChain.length - 1]}'`);
        }
        parentTypeChain.push(type);

        // The expanded definition of a published type (or published method parameter) is used for comparison with a runtime type. However,
        // because generic-types specifiers are a compile-time feature, they are NOT included in the type definition produced by getRuntimeType()
        // (which, for example, will return "Set" for a serialized "Set<string>" instance). So we don't include them in the expanded definition.
        let expandedDefinition: string = Type.removeGenericsSpecifiers(type);

        // For simplicity, we treat [validated] template string types (eg. `Hello ${number | string}`) as "string"
        const isTemplateStringType: boolean = (expandedDefinition.indexOf("`") === 0);
        if (isTemplateStringType)
        {
            return ("string");
        }

        // For simplicity, we treat [validated] union and intersection types as "any" [thus opting them out of runtime type checking].
        // Note: Simplifying to "any" [during expansion] is different from an explicit use of "any" in a type, because the type being
        //       simplified has already been checked (so, unless it too contains null, we know it will serialize).
        // Note: A complex type with an "in-line" union/intersection type (eg. { p1: number, p2: string | null }) will become "any", whereas a complex type using a
        //       published union/intersection type (eg. { p1: number, p2: MyUnionType }, where MyUnionType is "string | null") will become "{ p1: number, p2: any }",
        //       which will result in better runtime type checking. So the recommendation is to publish, not in-line, compound types. The warning message emitted by
        //       publishType() and publish[Post]Method() calls this out [see reportTypeSimplificationWarning()].
        //       TODO: We could fix this by finding/replacing all compound types in the complex type below (but this would be tricky).
        const isOrContainsCompoundType: boolean = (expandedDefinition.indexOf("|") !== -1) || (expandedDefinition.indexOf("&") !== -1);
        if (isOrContainsCompoundType)
        {
            return ("any");
        }

        if (type.startsWith("{")) // A complex type
        {
            // Find all used type names (by looking for ": typeName, " or ": typeName }" or ": typeName[], " or ": typeName[] }")
            // Note: We don't replace published type names in compound types (unions/intersections) because these are always expanded to "any" (see above).
            // Note: Test at https://regex101.com/ (set "Flavor" to ECMAScript)
            let regex: RegExp = /: ([A-Za-z][A-Za-z0-9_]*)(?:\[])*?(?:, | })/g;
            let result: RegExpExecArray | null;
            let usedPublishedTypeNames: string[] = [];

            while (result = regex.exec(type)) // Because regex use /g, exec() does a stateful search, returning only the next match with each call
            {
                let typeName: string = result[1];
                if (_publishedTypes[typeName] && (usedPublishedTypeNames.indexOf(typeName) === -1))
                {
                    usedPublishedTypeNames.push(typeName);
                }
                if (_missingTypes.has(typeName))
                {
                    // We can't expand (we have to wait until the missing type is published), so return "" (that way we can easily find it and fix it later)
                    return ("");
                }
            }

            for (let i = 0; i < usedPublishedTypeNames.length; i++)
            {
                const publishedTypeName: string = usedPublishedTypeNames[i];
                const publishedType: Type = _publishedTypes[publishedTypeName];
                const publishedTypeExpandedDefinition: string = publishedType.expandedDefinition ? publishedType.expandedDefinition : Type.expandType(publishedType.definition, parentTypeChain);
                if (!publishedTypeExpandedDefinition)
                {
                    // We can't expand (we have to wait until the missing type is published), so return "" (that way we can easily find it and fix it later)
                    // Note: Unlike earlier, in this case it's a dependency of one of the used published types which has not yet been defined
                    return ("");
                }
                expandedDefinition = expandedDefinition.replace(RegExp("(?<=: )" + publishedTypeName + "(?=, | }|\\[)", "g"), publishedTypeExpandedDefinition);
            }
        }
        else
        {
            // The 'type' is [potentially] a published type name (eg. "employee")
            const typeName = type.replace(/\[]/g, ""); // Remove all square brackets (eg. "Name[]" => "Name")
            if (_missingTypes.has(typeName))
            {
                // We can't expand (we have to wait until the missing type is published), so return "" (that way we can easily find it and fix it later)
                return ("");
            }
            if (_publishedTypes[typeName])
            {
                const publishedType: Type = _publishedTypes[typeName];
                const publishedTypeExpandedDefinition: string = publishedType.expandedDefinition ? publishedType.expandedDefinition : Type.expandType(publishedType.definition, parentTypeChain);
                if (!publishedTypeExpandedDefinition)
                {
                    // We can't expand (we have to wait until the missing type is published), so return "" (that way we can easily find it and fix it later)
                    // Note: Unlike earlier, in this case it's a dependency of one of the used published types which has not yet been defined
                    return ("");
                }
                expandedDefinition = type.replace(typeName, publishedTypeExpandedDefinition);
            }
        }
        return (expandedDefinition);
    }

    /** Compares a type definition against an expected definition, returning null if the types match or returning a failure reason if they don't. */
    static compareTypes(typeDefinition: string, expectedDefinition: string, tokenName: string = "", objectPath: string = ""): string | null
    {
        typeDefinition = typeDefinition.replace(/\s+/g,""); // Remove all whitespace
        expectedDefinition = expectedDefinition.replace(/\s+/g,""); // Remove all whitespace

        // Do the fast check first
        if (typeDefinition === expectedDefinition)
        {
            return (null); // Match
        }

        if ((typeDefinition[0] === "{") && (expectedDefinition[0] === "{"))
        {
            return (Type.compareComplexTypes(typeDefinition, expectedDefinition, objectPath));
        }
        else
        {
            // Allow "object" to match with any object
            if (((expectedDefinition === "object") && (typeDefinition[0] === "{")) ||
                ((typeDefinition === "object") && (expectedDefinition[0] === "{")))
            {
                return (null); // Match
            }

            // Allow "object" arrays (of n dimensions) to match an object array (also of n dimensions)
            if ((expectedDefinition.startsWith("object[]") && (typeDefinition[0] === "{") && (Type.getArraySuffix(expectedDefinition) === Type.getArraySuffix(typeDefinition))) ||
                (typeDefinition.startsWith("object[]") && (expectedDefinition[0] === "{") && (Type.getArraySuffix(typeDefinition) === Type.getArraySuffix(expectedDefinition))))
            {
                return (null); // Match
            }

            // Allow "any" to match with any type, and "any[]" to match with any array (including allowing any[] to match any array even when the dimensions don't match)
            // Note: "any" and "any[]" may be inserted into the type definition returned by getRuntimeType().
            if ((typeDefinition === "any") || 
                (expectedDefinition === "any") ||
                ((typeDefinition === "any[]") && expectedDefinition.endsWith("[]")) ||
                ((expectedDefinition === "any[]") && typeDefinition.endsWith("[]")) ||
                ((typeDefinition.startsWith("any[]") || expectedDefinition.startsWith("any[]")) && (Type.getArraySuffix(typeDefinition) === Type.getArraySuffix(expectedDefinition))))
            {
                return (null); // Match
            }

            if (tokenName)
            {
                return (`${tokenName} ${objectPath ? `(in '${objectPath}') ` : ""}should be '${expectedDefinition}', not '${typeDefinition}'`);
            }
            else
            {
                return (`expected '${expectedDefinition}', not '${typeDefinition}'`);
            }
        }
    }

    /** 
     * Compares a [complex] type definition against an expected definition, returning null if the types match or returning an error message if they don't.\
     * Note: This does a simplistic positional [ordered] match of tokens, which is why we don't support optional members in published types.
     */
    static compareComplexTypes(typeDefinition: string, expectedDefinition: string, objectPath: string = ""): string | null
    {
        let failureReason: string | null = null;
        let typeTokens: string[] = Type.tokenizeComplexType(Type.removeArraySuffix(typeDefinition));
        let expectedTokens: string[] = Type.tokenizeComplexType(Type.removeArraySuffix(expectedDefinition));
        let maxTokensToCheck: number = Math.min(typeTokens.length, expectedTokens.length);
        let typeArraySuffix: string = Type.getArraySuffix(typeDefinition);
        let expectedArraySuffix: string = Type.getArraySuffix(expectedDefinition);

        for (let i = 0; i < maxTokensToCheck; i++)
        {
            const isTypeDefinitionToken: boolean = (i > 0) && typeTokens[i - 1].endsWith(":");
            const propertyName: string = isTypeDefinitionToken ? typeTokens[i - 1].slice(0, -1) : "";
            const tokenName: string = isTypeDefinitionToken ? `type of '${propertyName}'` : `property #${Math.floor(i / 2) + 1}`;
            const tokenPath: string = objectPath + ((isTypeDefinitionToken && typeTokens[i].startsWith("{")) ? ((objectPath ? "." : "") + propertyName) : "");

            // Note: We're using compareTypes() here to check ALL tokens, not just types
            if (failureReason = this.compareTypes(typeTokens[i], expectedTokens[i], tokenName, tokenPath))
            {
                break;
            }
        }

        if (!failureReason && (typeTokens.length !== expectedTokens.length))
        {
            const location: string = objectPath ? ` in '${objectPath}'` : "";
            if (typeTokens.length > expectedTokens.length)
            {
                failureReason = `mismatched structure${location}: unexpected member '${typeTokens[expectedTokens.length]}' provided`;
            }
            if (expectedTokens.length > typeTokens.length)
            {
                failureReason = `mismatched structure${location}: expected member '${expectedTokens[typeTokens.length]}' not provided`;
            }
        }

        if (!failureReason && (typeArraySuffix !== expectedArraySuffix))
        {
            const location: string = objectPath ? `type of '${objectPath}' (${Type.formatType(typeDefinition)})` : Type.formatType(typeDefinition);
            failureReason = `${location} should have array suffix ${expectedArraySuffix || '(None)'}, not ${typeArraySuffix || '(None)'}`;
        }

        return (failureReason);
    }

    /** Returns the supplied 'typeDefinition' without its array suffix, if it has any. For example, "{ p1: string }[]" becomes "{ p1: string }" and "Foo[][]" becomes "Foo". */
    static removeArraySuffix(typeDefinition: string): string
    {
        const typeWithoutArraySuffix: string = typeDefinition.replace(/[\[\] ]+$/, "");
        return (typeWithoutArraySuffix);
    }

    /** Returns the array suffix (eg. "[][]") of the supplied 'typeDefinition' (eg. "{ p1: string }[][]"), if it has any. Returns "" otherwise. */
    private static getArraySuffix(typeDefinition: string): string
    {
        let result: RegExpExecArray | null = /[\[\] ]+$/.exec(typeDefinition);
        return (result ? result[0].replace(/\s+/g, "") : "");
    }

    /** Returns the supplied type definition without its generic-types specifier(s) (if any), eg. "Map<number, string>" becomes "Map". */
    // Note: We can't use RegEx for this because it doesn't support finding "balanced pairs" of tokens.
    static removeGenericsSpecifiers(type: string): string
    {
        let result: string = "";

        if (type.indexOf("<") === -1)
        {
            return (type);
        }

        for (let pos = 0; pos < type.length; pos++)
        {
            switch (type[pos])
            {
                case "<":
                    let nestLevel: number = 1;
                    while ((nestLevel > 0) && (++pos < type.length - 1))
                    {
                        if (type[pos] === "<") { nestLevel++; }
                        if (type[pos] === ">") { nestLevel--; }
                    } 
                    break;
                default:
                    result += type[pos];
            }
        }
        return (result);
    }

    /** 
     * Returns all the [top-level]  generic-type specifiers (if any) from the supplied type, for example
     * "{ p1: Set&lt;Foo>, p2: Map<number, Set&lt;string>> }" returns ["&lt;Foo>", "<number, Set&lt;string>>"]. 
     */
    static getGenericsSpecifiers(type: string): string[]
    {
        let results: string[] = [];
        let currentSpecifier: string = "";

        if (type.indexOf("<") === -1)
        {
            return ([]);
        }

        for (let pos = 0; pos < type.length; pos++)
        {
            switch (type[pos])
            {
                case "<":
                    let nestLevel: number = 1;
                    while ((nestLevel > 0) && (pos < type.length - 1))
                    {
                        currentSpecifier += type[pos++];
                        if (type[pos] === "<") { nestLevel++; }
                        if ((type[pos] === ">") && (type[pos - 1] !== "=")) { nestLevel--; } // Note: Have to handle lambda notation ("=>")
                        if (nestLevel === 0)
                        {
                            currentSpecifier += type[pos]; // Add the trailing ">"
                        }
                    }
                    results.push(currentSpecifier);
                    currentSpecifier = "";
                    break;
            }
        }
        return (results);
    }

    /** Given a generic-types specifier (eg. "<number, Map<number, string>>") returns the component types (eg. ["number", "Map<number, string>"]). */
    static parseGenericsSpecifier(specifier: string): string[]
    {
        let results: string[] = [];
        let nestLevel: number = 0;
        let currentType: string = "";

        specifier = specifier.trim();
        if ((specifier.length <= 2) || (specifier[0] !== "<") || (specifier[specifier.length - 1] !== ">"))
        {
            throw new Error (`The supplied specifier ('${specifier}') is invalid; it must start with '<' and end with '>'`);
        }

        if (specifier[1] === "{")
        {
            results.push(specifier.substr(1, specifier.length - 2));
        }
        else
        {
            for (let pos = 1; pos < specifier.length - 1; pos++)
            {
                switch (specifier[pos])
                {
                    case "<":
                    case "[": // This is only to handle tuples [which we don't support], but it will [benignly] trigger for array suffixes too
                        nestLevel++;
                        break;
                    case ">":
                    case "]": // This is only to handle tuples [which we don't support], but it will [benignly] trigger for array suffixes too
                        if ((specifier[pos] === ">") && (specifier[pos - 1] === "=")) // Skip lambda notation ("=>")
                        {
                            break;
                        }
                        nestLevel--;
                        break;
                    case ",":
                        if (nestLevel === 0)
                        {
                            results.push(currentType);
                            currentType = "";
                            pos++;
                        }
                        break;
                }
                currentType += specifier[pos];
            }
            results.push(currentType);
        }

        results.forEach((v, i) => results[i] = v.trim().replace(/\s+/g, " "));
        return (results);
    }

    /** 
     * Parses a complex type into a set of [top-level only] tokens that can be used for comparing it to another [complex] type. 
     * A complex type must start with "{" but must NOT end with an array suffix (eg. "{...}[]" is invalid).
     * Because this only returns top-level tokens, it may need to be called recursively on any returned complex-type tokens.
     */
    private static tokenizeComplexType(type: string, removeGenerics: boolean = true): string[]
    {
        const enum TokenType { None, Name, SimpleType, ComplexType }

        type = type.trim();
        if (type[0] !== "{")
        {
            throw new Error(`The supplied type ('${type}') is not a complex type`);
        }
        if (removeGenerics)
        {
            type = Type.removeGenericsSpecifiers(type);
        }
        if (Type.getArraySuffix(type))
        {
            throw new Error(`The supplied type ('${type}') cannot be tokenized because it has an array suffix`);
        }

        // Example: The type "{ addressLines: string[], name: { firstName: string, lastName: { middleInitial: { mi: string }[], lastName: string }[][] }[], startDate: number }"
        //          would yield these 6 [top-level] tokens:
        //            "addressLines:" -> Name
        //            "string[]"      -> Simple Type
        //            "name:"         -> Name
        //            "{ firstName: string, lastName: { middleInitial: { mi: string }[], lastName: string }[][] }[]" -> Complex Type
        //            "startDate:"    -> Name
        //            "number"        -> Simple Type
        let tokens: string[] = [];
        let nameToken: string = ""; // The current name token
        let simpleTypeToken: string = ""; // The current simple-type token
        let complexTypeToken: string = ""; // The current complex-type token
        let currentTokenType: TokenType = TokenType.None;
        let depth: number = -1;
        let complexTypeStartDepth: number = 0;
        let validCharRegEx: RegExp = /[ A-Za-z0-9_\[\]"'|&]/;

        for (let pos = 0; pos < type.length; pos++)
        {
            let char: string = type[pos];
            switch (char)
            {
                case "{":
                    switch (currentTokenType)
                    {
                        case TokenType.Name:
                            if (nameToken.length === 0)
                            {
                                throw new Error(`Unexpected character '${char}' at position ${pos} of "${type}"`);
                            }
                            // Fall-through
                        case TokenType.SimpleType: // Our earlier assumption that the next token would be a SimpleType was wrong
                            currentTokenType = TokenType.ComplexType;
                            complexTypeToken = char;
                            complexTypeStartDepth = depth;
                            break;
                        case TokenType.ComplexType:
                            complexTypeToken += char;
                            break;
                        case TokenType.None: // Only happens when pos = 0
                            currentTokenType = TokenType.Name;
                            break;
                    }
                    depth++;
                    break;

                case ":":
                    switch (currentTokenType)
                    {
                        case TokenType.Name:
                            if (nameToken.length === 0)
                            {
                                throw new Error(`Unexpected character '${char}' at position ${pos} of "${type}"`);
                            }
                            nameToken += char; // Including the trailing ":" makes it easy to distinguish names from types in the returned tokens list
                            tokens.push(nameToken);
                            nameToken = "";
                            currentTokenType = TokenType.SimpleType; // We'll assume this until we see "{"
                            break;
                        case TokenType.SimpleType:
                            throw new Error(`Unexpected character '${char}' at position ${pos} of "${type}"`);
                        case TokenType.ComplexType:
                            complexTypeToken += char;
                            break;
                    }
                    break;

                case "}":
                    depth--;
                    switch (currentTokenType)
                    {
                        case TokenType.SimpleType:
                            tokens.push(simpleTypeToken);
                            simpleTypeToken = "";
                            // We should now be at the end of 'type'
                            if (pos !== type.length - 1)
                            {
                                throw new Error("tokenizeComplexType() logic error");
                            }
                            break;
                        case TokenType.ComplexType:
                            complexTypeToken += char;
                            if (depth === complexTypeStartDepth)
                            {
                                if ((pos < type.length - 1) && (type[pos + 1] === "["))
                                {
                                    // Note: We're not validating the type here (we're just tokenizing it), so we don't check for balanced bracket characters
                                    while ((++pos < type.length) && ((type[pos] === "[") || (type[pos] === "]")))
                                    {
                                        complexTypeToken += type[pos];
                                    }
                                    if (!complexTypeToken.endsWith("]"))
                                    {
                                        throw new Error(`Unexpected character '${complexTypeToken[complexTypeToken.length - 1]}' at position ${pos} of "${type}"`);
                                    }
                                }
                                tokens.push(complexTypeToken.trim());
                                complexTypeToken = "";
                                currentTokenType = TokenType.Name;
                            }
                            break;
                    }
                    break;

                case ",":
                    switch (currentTokenType)
                    {
                        case TokenType.ComplexType:
                            complexTypeToken += char;
                            break;
                        case TokenType.SimpleType:
                            tokens.push(simpleTypeToken);
                            simpleTypeToken = "";
                            currentTokenType = TokenType.Name;
                            break;
                    }
                    break;
                
                case "<":
                    if (currentTokenType !== TokenType.SimpleType)
                    {
                        throw new Error(`Unexpected character '${char}' at position ${pos} of "${type}"`);
                    }
                    let nestLevel: number = 1;
                    while ((nestLevel > 0) && (pos < type.length - 1))
                    {
                        simpleTypeToken += type[pos++];
                        if (type[pos] === "<") { nestLevel++; }
                        if (type[pos] === ">") { nestLevel--; }
                        if (nestLevel === 0)
                        {
                            simpleTypeToken += type[pos]; // Add the trailing ">"
                        }
                    } 
                    break;

                default: // Space, alphanumeric, double/single quote, union (|), intersection (&), non-terminal [ and ]
                    if (!validCharRegEx.test(char) || 
                        ((char === "[") && (pos < type.length - 1) && (type[pos + 1] !== "]")) || // "[" not followed by "]"
                        ((char === "]") && (pos > 0) && (type[pos - 1] !== "["))) // "]" not preceded by "["
                    {
                        throw new Error(`Unexpected character '${char}' at position ${pos} of "${type}"`);
                    }
                    switch (currentTokenType)
                    {
                        case TokenType.None:
                            throw new Error(`Unexpected character '${complexTypeToken[complexTypeToken.length - 1]}' at position ${pos} of "${type}"`);
                        case TokenType.Name:
                            if (char !== " ") 
                            { 
                                nameToken += char;
                            }
                            break;
                        case TokenType.SimpleType:
                            if (char !== " ") 
                            { 
                                simpleTypeToken += char;
                            }
                            break;
                        case TokenType.ComplexType:
                            complexTypeToken += char;
                            break;
                    }
                    break;
            }
        }

        if ((nameToken.length > 0) || (simpleTypeToken.length > 0) || (complexTypeToken.length > 0) || (tokens.length % 2 !== 0))
        {
            throw new Error(`The type definition is incomplete ("${type}")`);
        }
        return (tokens);
    }

    /** 
     * Returns an array of the supported JavaScript native types, optionally including the boxed versions (eg. String, Number) of the primitive types, the typed arrays (eg. Uint8Array), 
     * and the supported (serializable) built-in types (eg. Date). The list excludes "function" and "Function" for security.\
     * All the types returned are serializable, although the boxed primitives cannot be used in published types/methods - the primitives must be used instead.
     */
    // WARNING: If you change this list, you MUST also update jsonStringify() and jsonParse().
    // See checkType() and getNativeType() for more on the "boxed-primitive vs. primitive" issue. [TODO: This needs a better explanation].
    static getSupportedNativeTypes(includeBoxedPrimitives: boolean = true, includeTypedArrays: boolean = true, includeSupportedBuiltInTypes: boolean = true) : string[]
    {
        let primitives: string[] = ["number", "boolean", "string", "object", "bigint" /* es2020 only*/, "null", "undefined"]; // Note: we omit "function" by design
        let boxedPrimitives: string[] = !includeBoxedPrimitives ? [] : ["Number", "Boolean", "String", "Object", "BigInt" /* es2020 only*/]; // Note: we omit "Function" by design ("Array" is in the supportedBuiltInTypes list)
        let typedArrays: string[] = !includeTypedArrays ? [] : ["Int8Array", "Uint8Array", "Uint8ClampedArray", "Int16Array", "Uint16Array", "Int32Array", "Uint32Array", "Float32Array", "Float64Array", "BigInt64Array", "BigUint64Array"];
        let supportedBuiltInTypes: string[] = !includeSupportedBuiltInTypes ? [] : ["Array", "Set", "Map", "Date", "RegExp", "Error", "EvalError", "RangeError", "ReferenceError", "SyntaxError", "TypeError", "URIError"];
        let nativeTypeNames: string[] = [...primitives, ...boxedPrimitives, ...typedArrays, ...supportedBuiltInTypes];
        return (nativeTypeNames);
    }

    /** 
     * Returns an array of the unsupported JavaScript native types (to distinguish between missing published types and "known unsupported" native types).\
     * The list also includes the unsupported TypeScript built-in types.
     */
    static getUnsupportedNativeTypes(): string[]
    {
        return (["function", "Function", "symbol", "Symbol", "WeakSet", "WeakMap", ...["unknown" /* Use "any" instead */, "never" /* Use "void" instead */]]);
    }

    /** 
     * Returns either the primitive type name ("number", "string", etc.) or the built-in object type name ("Uint8Array", "Date", etc.).
     * User-defined objects will always return "object" (although static classes return "function").
     */
    static getNativeType(value: unknown): string
    {
        let result: string = "";

        if (value && (typeof value === "object") && (value !== null)) // The additional "(value !== null)" check is to make the compiler happy when using 'strictNullChecks'
        {
            // Note: While Ambrosia doesn't do any script "minification", downstream bundlers might. In such cases, 'constructor.name' will return the minified name for
            //       user-defined types. This isn't an issue in our usage below because we're looking for the contructor names of built-in types, which won't get minified.
            const typeName: string = (value.constructor.name === "Object") ? "object" : value.constructor.name;

            if (Type.isConstructedBuiltInObject(value))
            {
                result = typeName;

                // Check for a non-object equivalent type (eg. so that "Number" becomes "number")
                const startsWithUpperCase: boolean = (result.charCodeAt(0) < 97);
                if (startsWithUpperCase)
                {
                    const lcPrimitiveTypeName: string = (typeof value.valueOf()).toLowerCase();
                    if (result.toLowerCase() === lcPrimitiveTypeName)
                    {
                        result = lcPrimitiveTypeName;
                    }
                }
            }
            else
            {
                // A user-defined object (typically a class)
                result = "object";
            }
        }
        else
        {
            // A non-object type (or null, whose typeof is "object")
            result = typeof value;
        }
        return (result);
    }

    /** Returns true if 'obj' is a typed array (eg. Uint8Array). */
    public static isTypedArray(obj: any): boolean
    {
        const isTypedArray: boolean = 
            (obj instanceof Int8Array) ||
            (obj instanceof Uint8Array) ||
            (obj instanceof Uint8ClampedArray) ||
            (obj instanceof Int16Array) ||
            (obj instanceof Uint16Array) ||
            (obj instanceof Int32Array) ||
            (obj instanceof Uint32Array) ||
            (obj instanceof Float32Array) ||
            (obj instanceof Float64Array) ||
            (obj instanceof BigInt64Array) ||
            (obj instanceof BigUint64Array);

        return (isTypedArray);
    }

    /** Returns true if 'obj' is a built-in object that has a constructor (eg. Uint8Array). Excludes Object. */
    private static isConstructedBuiltInObject(obj: any): boolean
    {
        // See https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects
        // Aside: An alternative, but MUCH slower approach, is this:
        //        const isBuiltInObject: boolean = /\{\s+\[native code\]/.test(Function.prototype.toString.call(obj.constructor))
        const isBuiltInObject: boolean = 
            // Fundamental objects
         // (obj instanceof Object) || // Omitted so that user-defined objects (eg. class instances) don't get reported as being built-in objects
            (obj instanceof Function) ||
            (obj instanceof Boolean) ||
            (obj instanceof Symbol) ||
            
            // Error objects
            (obj instanceof Error) ||
         // (obj instanceof AggregateError) || // No TypeScript definition for this [in TS 4.1.2]
            (obj instanceof EvalError) ||
         // (obj instanceof InternalError) || // No TypeScript definition for this [in TS 4.1.2]
            (obj instanceof RangeError) ||
            (obj instanceof ReferenceError) ||
            (obj instanceof SyntaxError) ||
            (obj instanceof TypeError) ||
            (obj instanceof URIError) ||
            
            // Numbers and dates
            (obj instanceof Number) ||
            (obj instanceof BigInt) ||
            (obj instanceof Date) ||

            // Text processing
            (obj instanceof String) ||
            (obj instanceof RegExp) ||

            // Indexed collections
            (obj instanceof Array) ||
            (obj instanceof Int8Array) ||
            (obj instanceof Uint8Array) ||
            (obj instanceof Uint8ClampedArray) ||
            (obj instanceof Int16Array) ||
            (obj instanceof Uint16Array) ||
            (obj instanceof Int32Array) ||
            (obj instanceof Uint32Array) ||
            (obj instanceof Float32Array) ||
            (obj instanceof Float64Array) ||
            (obj instanceof BigInt64Array) ||
            (obj instanceof BigUint64Array) ||

            // Keyed collections
            (obj instanceof Map) ||
            (obj instanceof Set) ||
            (obj instanceof WeakMap) ||
            (obj instanceof WeakSet) ||

            // Structured data
            (obj instanceof ArrayBuffer) ||
            (obj instanceof SharedArrayBuffer) ||
            (obj instanceof DataView) ||

            // Control abstraction objects
            (obj instanceof Promise) ||

            // Internationalization
            (obj instanceof Intl.Collator) ||
            (obj instanceof Intl.DateTimeFormat) ||
            (obj instanceof Intl.NumberFormat) ||
            (obj instanceof Intl.PluralRules) ||

            // WebAssembly
            (obj instanceof WebAssembly.Module) ||
            (obj instanceof WebAssembly.Instance) ||
            (obj instanceof WebAssembly.Memory) ||
            (obj instanceof WebAssembly.Table) ||
            (obj instanceof WebAssembly.CompileError) ||
            (obj instanceof WebAssembly.LinkError) ||
            (obj instanceof WebAssembly.RuntimeError);

        return (isBuiltInObject);
    }

    /** 
     * Returns the type of the supplied object (eg. "string", "number[]", "{ count: number }", "Set").\
     * TODO: This method needs more testing.
     */
    static getRuntimeType(obj: any): string
    {
        let result: string = "";
    
        /** [Local function] If needed, recursively walks the object (or array) 'obj' of the property named 'key'. */
        function buildType(obj: Utils.SimpleObject, key: string, isLastProperty: boolean): void
        {
            let nativeType: string = Type.getNativeType(obj);
    
            if (obj && (nativeType === "object"))
            {
                const lastPropertyName: string = Object.keys(obj)[Object.keys(obj).length - 1];
    
                result += `${key ? `${key}: ` : ""}{ `;
                for (const k in obj)
                {
                    // JSON.stringify() doesn't serialize prototype-inherited properties, so we can skip those [see: https://stackoverflow.com/questions/8779249/how-to-stringify-inherited-objects-to-json]
                    if (Object.prototype.hasOwnProperty.call(obj, k)) // Using Object.prototype because obj.hasOwnProperty() can be redefined (shadowed)
                    { 
                        const isLastProperty: boolean = (k === lastPropertyName);
                        buildType(obj[k], k, isLastProperty);
                    }
                }
                result += isLastProperty ? "} " : "}, ";
            }
            else
            {
                // Special handling for arrays.
                // Arrays usually have their type specified (eg. "let myArr: string[] = [];" or "let myArr: Array<string> = [];"), but may not (eg. "let myArr = []").
                // There are 3 choices for how to handle determining an array's type:
                // 1) Don't even try and simply return the type as "any[]" for all arrays.
                //    While simple and fast, it won't allow us to catch any kind of array type mistmatch.
                // 2) Examine all the elements and if they are all of the same type, return "<type>[]", otherwise return "any[]".
                //    Better accuracy than 1, but won't catch a non-homogenous array being passed when a homogeneous array is expected (when comparing types).
                //    However, if a non-homogenous array IS expected, then this approach will work.
                // 3) Examine all the elements and if they are all of the same type, return "<type>[]", otherwise return "_mixed[]"".
                //    Better accuracy than 1, and will catch a non-homogenous array being passed when a homogeneous array is expected (when comparing types).
                //    However, if a non-homogenous array IS expected (ie. "any[]"), then this approach will fail.
                // 
                // We chose approach #2 because this is the approach that works best with a code-gen'd ConsumerInterface.g.ts, which always specifies types (including "any[]").
                // So if we're receiving a non-homogeneous array, this is because it's expected (ie. it's an "any[]" in the ConsumerInterface.g.ts).
                // But approach #2 (unlike approach #1) still allows us to detect homogenous array type mistmatches, like the case of an accidental change to 
                // ConsumerInterface.g.ts (eg. a number[] being unintentionally changed to a string[]), or the case of not using ConsumerInterface.g.ts at all.
                //
                // Note: For simplicity, we don't try to determine the type of Set or Map objects. While Typescript will allow un-typed Sets and Maps (eg. "let mySet = new Set()'"),
                //       it's more typical - because they represent collections - that the types will be provided via generic-type specifiers (eg. "let mySet: Map<number, string>;").
                //       Because of this, we fully rely on ConsumerInterface.g.ts and the compile-time checks to prevent passing invalid types is Set and Map collections.
                if (Array.isArray(obj))
                {
                    // Are all the array elements (items) the same type?
                    let firstNonNullElementType: string = "";
                    let allElementsHaveTheSameType: boolean = true;
                    
                    if (obj.length > 0)
                    {
                        for (const element of obj)
                        {
                            if (!firstNonNullElementType)
                            {
                                if (element !== null)
                                {
                                    firstNonNullElementType = Type.getRuntimeType(element);
                                }
                            }
                            else
                            {
                                if (element !== null)
                                {
                                    const elementType: string = Type.getRuntimeType(element);
                                    if (Type.compareTypes(firstNonNullElementType, elementType) !== null)
                                    {
                                        allElementsHaveTheSameType = false;
                                        break;
                                    }
                                }
                            }
                        }
                        nativeType = allElementsHaveTheSameType ? `${firstNonNullElementType}[]` : "any[]";
                    }
                    else
                    {
                        nativeType = "any[]";
                    }
                }
    
                // Special handling for null and undefined
                if ((obj === null) || (obj === undefined))
                {
                    nativeType = "any";
                }
    
                result += `${key ? `${key}: ` : ""}${nativeType}${isLastProperty ? " " : ", "}`;
            }
        }
    
        buildType(obj, "", true);
        return (result.trim());
    }
}

/** 
 * [Internal] Validates that the specified name (of a parameter or object property) is a valid identifier, returning true if it is or throwing if it's not.
 * The 'nameDescription' describes where 'name' is used (eg. "parameter 'p1' of method 'foo'") so that it can be included in the exception
 * message (if thrown).\
 * Note: A null name (eg. for a function return type) will return true.
 */
export function checkName(name: string | null, nameDescription: string, optionalAllowed: boolean = true, restPrefixAllowed: boolean = false): boolean
{
    const regex: RegExp = optionalAllowed ? /^[A-Za-z_][A-Za-z0-9_]*[\?]?$/ : /^[A-Za-z_][A-Za-z0-9_]*$/;

    if (name && (!regex.test(restPrefixAllowed ? Method.trimRest(name) : name) || (Type.getSupportedNativeTypes().indexOf(name) !== -1)))
    {
        let optionalNotAllowed: string = (name.endsWith("?") && !optionalAllowed) ? "; the optional indicator ('?') is not allowed in this context" : "";
        throw new Error(`The ${nameDescription} has an invalid name ('${name}')${optionalNotAllowed}`);
    }
    return (true);
}

/** The context in which a type is being checked. */
enum TypeCheckContext
{
    /** The type is being checked while creating a published type. */
    Type,
    /** The type is being checked while creating a published method. */
    Method
}

/** 
 * TypeScript built-in "utility" types that create a new type by transforming an existing type (which is supplied as a generic-type specifier).\
 * See https://www.typescriptlang.org/docs/handbook/utility-types.html. \
 * Note: Technically, "ReadonlyArray" is not a utility type, but since it behaves like one we include it in the list.
 */
const _tsUtilityTypes: ReadonlyArray<string> = ["Partial", "Required", "Readonly", "ReadonlyArray", "Record", "Pick", "Omit", "Exclude", "Extract", "NonNullable", "Parameters",
                                                "ConstructorParameters", "ReturnType", "InstanceType", "OmitThisParameter", "ThisType", "Uppercase", "Lowercase", "Capitalize", "Uncapitalize"]; 

/** 
 * Validates if the supplied 'runtimeValue' has a valid type (ie. is supported and serializable). Throws if it's an invalid type.\
 * Typically, this is used to validate parameters passed to LB APIs that can be 'any'.
 */
export function checkRuntimeType(runtimeValue: any, name: string): void
{
    try
    {
        Utils.suppressLoggingOf(/^Warning:/); // 'null' and 'undefined' become 'any' in getRuntimeType(), which checkType() will then log a warning about (which we don't want in this context)
        checkType(TypeCheckContext.Type, Type.getRuntimeType(runtimeValue), name, `runtime type of '${name}'`, false);
    }
    finally
    {
        Utils.suppressLoggingOf(); // Clear the suppression
    }
}

/** 
 * Validates the specified type (eg. "string[]") returning true if the type is valid, or throwing if it's invalid.
 * @param type Either a property type, parameter type, a return type, or a published type definition. This is case-sensitive.
 * @param name Either a property name, parameter name, null (for a return type), or a published type name. This is case-sensitive.
 * @param typeDescription Describes where 'type' is used (eg. "return type of method 'foo'") so that it can be included in the exception message (if thrown).
 * @param optionalAllowed Whether 'name' can have the optional suffix ("?"). Defaults to true.
 * @param restPrefixAllowed Whether 'name' can have the rest-parameters prefix ("..."). Defaults to false.
 */
function checkType(context: TypeCheckContext, type: string, name: string | null, typeDescription: string, optionalAllowed: boolean = true, restPrefixAllowed: boolean = false): boolean
{
    type = type.replace(/([ ]+)(?=[\[\]])/g, "").trim(); // Remove all space before (or between) array suffix characters ([])

    // Look for a bad parameter (or object property) name, like "some-Name[]"  
    checkName(name, typeDescription, optionalAllowed, restPrefixAllowed);

    if (type.length > 0)
    {
        // Since TS utility types are not meaningful without their generic-type specifier (which we always remove), we cannot support them
        if (type.indexOf("<") > 0)
        {
            for (const tsUtilityType of _tsUtilityTypes)
            {
                if (type.startsWith(`${tsUtilityType}<`))
                {
                    throw new Error(`The ${typeDescription} uses a TypeScript utility type ('${tsUtilityType}'); utility types are not supported`)
                }
            }
        }

        // For type-checking purposes we remove all TS generic specifiers, eg. "Map<number, string>" becomes "Map"; generics [currently] aren't part of native JS.
        // But since the generic specifier(s) will still be used in TS wrappers, we need to check that the generic types are valid too; this also checks that the
        // generic types are serializable.
        for (const genericsSpecifier of Type.getGenericsSpecifiers(type))
        {
            for (const genericType of Type.parseGenericsSpecifier(genericsSpecifier))
            {
                checkType(context, genericType, name, `generic-type specifier in ${typeDescription}`, optionalAllowed, restPrefixAllowed);
            }
        }
        type = Type.removeGenericsSpecifiers(type);

        // Check against the built-in [or published] types, and arrays of built-in [or published] types
        if (type[0] !== "{") // A named type, not a complex/compound type
        {
            // Note: Even though "any" is not a native JavaScript type, we (like TypeScript) use it as the "no specific type" type
            let validTypeNames: string[] = Type.getSupportedNativeTypes(false).concat(Object.keys(_publishedTypes)).concat(["any"]);
            let lcType: string = type.toLowerCase();

            for (let i = 0; i < validTypeNames.length; i++)
            {
                if ((type === validTypeNames[i]) || type.startsWith(validTypeNames[i] + "[]")) // We want to include arrays of arrays, eg. string[][]
                {
                    if (type.startsWith("any") || type.startsWith("object")) // Include any[] and object[]
                    {
                        Utils.log(`Warning: The ${typeDescription} uses type '${type}' which is too general to determine if it can be safely serialized; it is strongly recommended to use a more specific type`);
                    }

                    // We allow null and undefined, but only when being used in a union type (eg. "string | null")
                    // [Note that "string & null" is 'never', so we don't support null (or undefined) with intersection types]
                    if (((type === "null") || (type === "undefined")) && (typeDescription.indexOf("union") === -1)) // TODO: Checking typeDescription is brittle
                    {
                        throw new Error(`The ${typeDescription} has a type ('${type}') that's not supported in this context`);
                    }
                    // "null[]" and "undefined[]" are nonsensical types
                    if (type.startsWith("null[") || type.startsWith("undefined[")) 
                    {
                        throw new Error(`The ${typeDescription} has an unsupported type ('${type}')`);
                    }

                    return (true); // Success
                }

                // Check for mismatched casing [Note: We disallow published type names that differ only by case]
                if (Utils.equalIgnoringCase(type, validTypeNames[i]) || lcType.startsWith(validTypeNames[i].toLowerCase() + "[]"))
                {
                    let brackets: string = type.replace(/[^\[\]]+/g, "");
                    throw new Error(`The ${typeDescription} has an invalid type ('${type}'); did you mean '${validTypeNames[i] + brackets}'?`);
                }
            }

            // We do this to get a better error message; without it the error message asks the user to publish the missing type, which is invalid for a native (or TS built-in) type.
            for (const unsupportedTypeName of Type.getUnsupportedNativeTypes())
            {
                if (type === unsupportedTypeName)
                {
                    throw new Error(`The ${typeDescription} has an unsupported type ('${type}')`);
                }
            }

            // We support template string types (technically TemplateLiteralType) on a "best effort" basis, since the syntax can be arbitrarily complex.
            // Our goal is to cover the common/basic use case for these types (ie. with unions of string literals).
            // Note: Template string types will end up with an expanded definition of "string" (for simplicity).
            if (type.indexOf("`") === 0) // Eg. "`Hello ${number}`"
            {
                const templateStringRegEx: RegExp = /(?<=\${)[^/}]+(?=})/g;// Find all the "type" in "${type}" templates
                let result: RegExpExecArray | null;
                let templateNumber: number = 1;

                while (result = templateStringRegEx.exec(type)) // Because regex use /g, exec() does a stateful search, returning only the next match with each call
                {
                    const templateType: string = result[0];
                    // Note: Since we're always going to use "string" as the expandedType, the only reason to check the individual template types is to catch 
                    //       errors when NOT doing code-gen from source [the TypeScript compiler will have already done the checking in that case], ie. when
                    //       publishing "manually" using hand-written publishType() calls.
                    checkType(context, templateType, name, `replacement template #${templateNumber++} of ${typeDescription}`, optionalAllowed, restPrefixAllowed);
                }
                return (true);
            }

            // A string literal is valid (eg. as can appear in a union)
            if (/^"[^"]*"$/.test(type) || /^'[^']*'$/.test(type))
            {
                return (true);
            }

            // We support union and intersection types on a "best effort" basis, since the syntax can be arbitrarily complex. Our goal is to cover all the common/basic use cases for these types.
            // Note: Union and intersection types will end up with an expanded definition of "any", which is done to opt them out of runtime type checking (for simplicity). But we still want to
            //       check each type used in the the union/intersection so that we can verify they are serializable.
            if ((type.indexOf("|") !== -1) || (type.indexOf("&") !== -1)) // Eg. "string | (number | boolean)[]" or "Foo & (Bar | Baz)[]"
            {
                const result: { kindFound: CompoundTypeKind, components: string[] } = Type.getCompoundTypeComponents(type);
                
                if (result.components.length > 0)
                {
                    const kindName: string = CompoundTypeKind[result.kindFound].toLowerCase();
                    result.components.map((t, i) => checkType(context, t, name, `${kindName}-type component #${i + 1} of ${typeDescription}`, optionalAllowed, restPrefixAllowed));
                    return (true);
                }
            }

            // Check for unsupported types (which are unsupported either for simplicity of our code, or because it's not practical/feasible to support it)
            if (type.indexOf("[") === 0) // Eg. "[string, number]"
            {
                // Note: The developer can downcast a tuple to an any[] to pass it to an Ambrosia method
                throw new Error(`The ${typeDescription} has an invalid type ('${type}'); tuple types are not supported`);
            }
            if (type.indexOf("(") === 0) // Eg. "(p1: string) => number"
            {
                throw new Error(`The ${typeDescription} has an invalid type ('${type}'); function types are not supported`);
            }
            // Conditional types are almost always used with generics, whose use will have been caught by AST.publishTypeAlias() and AST.publishFunction().
            // But there are still valid non-generic (if nonsensical) conditional types that will slip by those checks, and this check will still apply
            // if publishing "manually" using hand-written publishType() calls.
            if (type.indexOf(" extends ") !== -1) // Eg. "string extends undefined? never : string" (this is a valid, but nonsensical, example)
            {
                throw new Error(`The ${typeDescription} has an invalid type ('${type}'); conditional types are not supported`);
            }

            // The type is either an incorrectly spelled native type, or is a yet-to-be published custom type.
            // We'll assume the latter, since this allows forward references to be used when publishing types.
            let missingTypeName: string = type.replace(/\[]/g, ""); // Remove all square brackets (eg. "Name[]" => "Name")

            // Since we're expecting the type to be a name, we check that it is indeed a valid name. This is to catch "unexpected" TypeScript syntax that's other than
            // Tuple/Function type syntax, since we only check for these 2 cases above - but other cases exist (including in future versions of TypeScript).
            try
            {
                checkName(missingTypeName, typeDescription, false);
            }
            catch
            {
                throw new Error(`The ${typeDescription} has an unsupported definition (${type})`);
            }

            if (context === TypeCheckContext.Type)
            {
                if (!_missingTypes.has(missingTypeName))
                {
                    _missingTypes.set(missingTypeName, typeDescription);
                }
            }
            
            // We don't allow forward references for a method parameter/return type, because we don't do any "deferred checking" for methods as we do for types [see updateUnexpandedPublishedTypes()]
            if (context === TypeCheckContext.Method)
            {
                throw new Error(`The ${typeDescription} references an unpublished type ('${type}')`);
            }

            return (true);
        }

        // Check a complex/compound type (eg. "{ names: { firstName: string, lastName: string }, startDate: number, jobs: { title: string, durationInSeconds: bigint[] }[] }")
        if (type[0] === "{")
        {
            let obj: object;
            let json: string = type.replace(/[\s]/g, "").replace(/[\"]/g, "'"); // Remove all spaces and replace all double-quotes with single-quotes (the type may include string literals)

            // Check if the type definition is using JSON-style array notation ("[{}]"), which is not allowed as the definition must use TypeScript-style array notation ("{}[]")
            if (/{\s*\[/g.test(type) || /}\s*]/g.test(type))
            {
                throw new Error(`The ${typeDescription} has an invalid type ('${type}'); the type uses JSON-style array notation ("[{}]") when it should be using TypeScript-style ("{}[]")`);
            }

            if (json === "{}")
            {
                throw new Error(`The ${typeDescription} has an invalid type ('${type}'); the type is empty`);
            }

            // For the purpose of validation ONLY, convert the type from TypeScript-style array notation "{}[]" to JSON-style "[{}]"
            json = convertToJsonArrayNotation(json);

            // Note: Using multiple, simple steps here makes this easier to understand/debug (at the possible expense of some performance)
            json = json.replace(/{/g, "{\"").replace(/}/g, "\"}").replace(/:/g, "\":\"").replace(/,/g, "\",\""); // Add quotes around all keys and values
            json = json.replace(/\"{/g, "{").replace(/}\"/g, "}").replace(/\"\[/g, "[").replace(/]\"/g, "]"); // Remove quotes unintentionally added by previous step
            json = json.replace(/\[](?=[^\[])/g, "[]\""); // Add quotes unintentionally removed by previous step

            try
            {
                // We pass the [complex] type through JSON parsing to validate its structure (ie. that its a valid tree that we can walk)
                obj = JSON.parse(json);
            }
            catch (error: unknown)
            {
                // Note: We remove the "at position N" message because the position applies to the temporary JSON we created, not to the supplied 'type', so it would be misleading
                throw new Error(`Unable to parse the type of ${typeDescription} (reason: ${Utils.makeError(error).message.replace(/ at position \d+/, "").replace(/ in JSON/, "") }); ` + 
                                `check for missing/misplaced '{', '}', '[', ']', ':' or ',' characters in type`);
            }

            /** [Local function] Recursively walks to all leaf nodes and calls checkType() for each. */
            function findBadLeafType(obj: Utils.SimpleObject, key: string): void
            {
                if ((typeof obj === "object") && (Object.keys(obj).length > 0))
                {
                    // We're at a non-leaf node
                    for (const k in obj)
                    {
                        // JSON.stringify() doesn't serialize prototype-inherited properties, so we can skip those [see: https://stackoverflow.com/questions/8779249/how-to-stringify-inherited-objects-to-json]
                        if (Object.prototype.hasOwnProperty.call(obj, k)) // Using Object.prototype because obj.hasOwnProperty() can be redefined (shadowed)
                        { 
                            findBadLeafType(obj[k], k);
                        }
                    }
                }
                else
                {
                    // We're at a leaf node
                    const leafPropertyName: string = key;
                    const leafTypeName: string = obj.toString();
                    // Note: While valid in TypeScript, we don't allow optional object property names (ie. names ending with '?') [see Type.compareComplexTypes()]
                    checkType(context, leafTypeName, leafPropertyName, `${typeDescription} [property '${leafPropertyName}']`, false);
                }
            }
            findBadLeafType(obj, "");

            return (true); // Success
        }
    }
    throw new Error(`The ${typeDescription} has an invalid type ('${type}'); check the syntax or casing or, if this is a custom type, check that it has been published`);

    /** [Local function] Converts the type from TypeScript-style "{}[]" to JSON-style "[{}]". */
    function convertToJsonArrayNotation(type: string): string
    {
        if (type.indexOf(" ") !== -1)
        {
            // Remove whitespace from "{} [ ]  []"
            type = type.replace(/}\s+\[/g, "}[").replace(/]\s+\[/g, "][").replace(/\[\s+\]/g, "[]");
        }
    
        let convertedType: string = type;
        let startPos: number = 0;
        let regex: RegExp = /}]*\[]/; // DON'T use /g (we want regex to NOT be stateful). Note: As we progress, {}[][][] will become {}][][] then {}]][] then finally {}]]]
    
        // Eg. "{ names: { firstName: string, lastName: string }, startDate: number, jobs: { title: { full: string, abbreviation: string}, durationInSeconds: bigint[] }[] }"
        while (true)
        {
            let result: RegExpExecArray | null = regex.exec(convertedType);
            if (result === null)
            {
                break;
            }
            startPos = result.index;
            let match: string = result[0]; // Eg: "}]][]"
            let replacement: string = match.substr(0, match.length - 2) + "]"; // Eg: "}]]]"
    
            // Backtrack to find the matching opening '{'
            let insertionPos: number = 0;
            let depth: number = 0;
    
            for (let i = startPos; i >= 0; i--)
            {
                if (convertedType[i] === '}') depth++;
                if (convertedType[i] === '{') depth--;
                if (depth === 0)
                {
                    insertionPos = i;
                    break;
                }
            }
            convertedType = convertedType.replace(match, replacement); // Note: This ONLY replaces the first occurrance of 'match'
            convertedType = convertedType.substring(0, insertionPos) + "[" + convertedType.substring(insertionPos, convertedType.length);
        }
        return (convertedType);
    }
}

/** 
 * Publishes a 'post' method so that it's available to be called by a consumer; used by code-gen and by the built-in Meta.getPublishedMethods_Post() method.\
 * Note that this only publishes the method signature, not the implementation.\
 * If you use code-gen (ie. emitTypeScriptFileFromSource()) there is no need to explicitly call this method because it will be called for you.\
 * Returns the published method.
 * 
 * Each parameter in 'parameters' must be of the form "name[?]:type", where 'type' can either be 
 * simple (eg. number, string[]) or complex (eg. { name: { firstName: string, lastName: string }, age: number }) or a published type (eg. Employee).\
 * Note: Any optional parameters must be specified (in 'parameters') after all non-optional parameters.\
 * Note: The 'methodName' is case-sensitive.
 * @param codeGenOptions [Internal] For internal use only.
 */
export function publishPostMethod(methodName: string, methodVersion: number, parameters: string[], returnType: string, doRuntimeTypeChecking: boolean = true, codeGenOptions?: CodeGenOptions): Method
{
    checkForMissingPublishedTypes();
    methodName = methodName.trim();

    if (methodName.startsWith("_"))
    {
        // We reserve use of a leading underscore for internal 'post' methods (like "_echo") to prevents name collisions with user-defined 'post' method names
        throw new Error(`A published 'post' method name ('${methodName}') cannot begin with an underscore character`);
    }

    if (!_publishedMethods[methodName])
    {
        _publishedMethods[methodName] = {};
    }
    if (!_publishedMethods[methodName][methodVersion])
    {
        const method: Method = new Method(IC.POST_METHOD_ID, methodName, methodVersion, parameters, returnType, doRuntimeTypeChecking, codeGenOptions);
        _publishedMethods[methodName][methodVersion] = method; 
        return (method);
    }
    else
    {
        throw new Error(`Published 'post' method '${methodName}' (version ${methodVersion}) already exists`);
    }
}

/** 
 * Publishes a [non-post] method so that it's available to be called by a consumer; used by code-gen and by the built-in Meta.getPublishedMethods_Post() method. 
 * Note that this only publishes the method signature, not the implementation.\
 * If you use code-gen (ie. emitTypeScriptFileFromSource()) there is no need to explicitly call this method because it will be called for you.\
 * Returns the published method.
 * 
 * Each parameter in 'parameters' must be of the form "name[?]:type", where 'type' can either be 
 * simple (eg. number, string[]) or complex (eg. { name: { firstName: string, lastName: string }, age: number }) or a published type (eg. Employee).\
 * If the method uses binary serialized parameters (not JSON serialized parameters) then specify a single "rawParams:Uint8Array" parameter.\
 * Note: Any optional parameters must be specified (in 'parameters') after all non-optional parameters.\
 * Note: The 'methodName' is case-sensitive.
 * @param codeGenOptions [Internal] For internal use only.
 */
export function publishMethod(methodID: number, methodName: string, parameters: string[], codeGenOptions?: CodeGenOptions): Method
{
    checkForMissingPublishedTypes();
    methodName = methodName.trim();

    // Negative methodID's are reserved for built-in methods
    if (methodID < 0)
    {
        throw new Error(`Method ID ${methodID} is invalid`);
    }

    for (const name in _publishedMethods)
    {
        for (const version in _publishedMethods[name])
        {
            let method: Method = _publishedMethods[name][version];
            if ((method.id === methodID) && (method.name !== methodName))
            {
                throw new Error(`Method ID ${methodID} is already in use (by method '${method.name}')`);
            }
            if ((method.name === methodName) && (method.id !== methodID))
            {
                throw new Error(`Method name '${methodName}' is invalid (reason: ${method.isPost ? "Post" : "Another"} method ${method.isPost ? `'${method.name}'` : `(ID ${method.id})`} is already using the name)`);
            }
        }
    }

    if (!_publishedMethods[methodName])
    {
        _publishedMethods[methodName] = {};
    }
    // Note: By design, non-post methods do not support versioning (so their version will always be 1).
    //       This is done in order to retain compatibility with C# Fork/Impulse methods (whose RPC message format doesn't include a version).
    let methodVersion: number = 1;
    if (!_publishedMethods[methodName][methodVersion])
    {
        const method: Method = new Method(methodID, methodName, methodVersion, parameters, "void", false, codeGenOptions);
        _publishedMethods[methodName][methodVersion] = method; 
        return (method);
    }
    else
    {
        throw new Error(`Published method '${methodName}' already exists`);
    }
}

/** Throws if the _missingTypes list not empty, since this indicates that a custom type was referenced but not defined (published). */
function checkForMissingPublishedTypes(): void
{
    if (_missingTypes.size > 0)
    {
        const missingTypesList: string = [..._missingTypes].map(kvp => `'${kvp[0]}' found in ${kvp[1]}`).join(", ")
        throw new Error(`The following types must be published before any method can be published: ${missingTypesList}`);
    }
}

/** 
 * Updates the 'expandedDefinition' property of all published types that need it, and returns the number of types updated.
 * Throws if _missingTypes is not empty.
 */
function updateUnexpandedPublishedTypes(): number
{
    let updateCount: number = 0;
    if (_missingTypes.size === 0)
    {
        // There are no missing types, so we can now safely update the expandedDefinition of all types that could not be expanded during publishing (because of forward references).
        // The only "expected" reason for expansion to fail is a circular reference.
        for (const typeName in _publishedTypes)
        {
            const type: Type = _publishedTypes[typeName];
            if (!type.expandedDefinition)
            {
                const expandedDefinition: string = Type.expandType(type.definition);
                if (expandedDefinition)
                {
                    type.expandedDefinition = expandedDefinition;
                    updateCount++;
                }
                else
                {
                    throw new Error(`Unable to expand the definition for type '${type.name}'`);
                }
            }
        }
    }
    else
    {
        throw new Error(`Unable to expand type definitions (reason: There are are still ${_missingTypes.size} missing types ('${[..._missingTypes.keys()].join("', '")}'))`);
    }
    return (updateCount);
}

/** 
 * Unpublishes a method so that it's no longer be available to be called by a consumer, or used by code-gen.
 * Returns true only if the method was unpublished.\
 * **Caution**: If 'methodVersion' is not supplied, all versions will be unpublished.
 */
export function unpublishMethod(methodName: string, methodVersion?: number): boolean
{
    methodName = methodName.trim();

    if (_publishedMethods[methodName])
    {
        if (methodVersion === undefined)
        {
            // Remove ALL versions
            delete _publishedMethods[methodName];
            return (true);
        }
        else
        {
            if (_publishedMethods[methodName][methodVersion])
            {
                // Remove specific version
                delete _publishedMethods[methodName][methodVersion];
                if (Object.keys(_publishedMethods[methodName]).length === 0)
                {
                    delete _publishedMethods[methodName];
                }
                return (true);
            }
        }
    }
    return (false);
}

/** Logs a warning that the expanded definition for the specified type has been simplified to "any". */
function reportTypeSimplificationWarning(typeDescription: string, typeDefinition: string): void
{
    const isCompoundType: boolean = (Type.getCompoundTypeComponents(typeDefinition).components.length > 0);
    const fix: string = !isCompoundType ? "; the strongly recommended fix is to publish all in-line compound types (unions and intersections)" : "";
    Utils.log(`Warning: The expanded definition for ${typeDescription} was simplified to "any", which will bypass runtime type checking${fix}`);
}

/** 
 * Publishes a [typically] complex type (used as either a method parameter or a return value) so that it can be referenced by
 * published methods (see publishMethod and publishPostMethod) or other published types. Returns the published type.
 * 
 * Forward references are allowed, but if not eventually published they will cause both publishMethod() and publishPostMethod() to fail.\
 * Publishing a type is similar to declaring a type alias in TypeScript (but without support for optional members or union/tuple/function types).
 * However, during code generation [emitTypeScriptFile[FromSource]()] it will be converted to a class to a) allow concise constructor syntax 
 * to be used [in generated consumer-side code], and b) to allow independent augmentation with methods (for appropriate encapsulation).
 * But a published type itself does not have any methods: it is only a data structure.\
 * Published types can be queried using the built-in Meta.getPublishedTypes_Post() method.
 * @param typeName The [case-sensitive] name of the type (eg. "Employee").
 * @param typeDefinition The type definition, eg. "{ name: string, age: number }" or "string[&nbsp;][&nbsp;]".
 * @param enumType [Optional] An actual enum type, eg. RPCType (and whose name will typically match the supplied 'typeName').
 * When used, specify the 'typeDefinition' as "number". Specifying a string value for 'enumType' is for internal use only.
 * @param codeGenOptions [Internal] For internal use only.
 */
export function publishType(typeName: string, typeDefinition: string, enumType?: Utils.EnumType | string, codeGenOptions?: CodeGenOptions): Type
{
    // Prevent publishing types that differ only by case, eg. "Employee" and "employee" (even though this is
    // valid in TypeScript). We do this so that checkType() can detect a mis-cased type name in the same way
    // for both native types and published types (ie. case-only name differences are invalid for both) which
    // lets it more accurately detect a forward reference (ie. a reference to a not-yet-published type).
    for (const name in _publishedTypes)
    {
        if (Utils.equalIgnoringCase(typeName, name))
        {
            throw new Error(`Published type '${name}' already exists${(typeName !== name) ? "; published type names cannot differ only by case" : ""}`);
        }
    } 
    
    if (!_publishedTypes[typeName])
    {
        if (enumType && (typeDefinition !== "number"))
        {
            throw new Error("It is invalid to specify an 'enumType' when the 'typeDefinition' is not 'number'");
        }

        let enumValues: string | null = null;
        if (enumType)
        {
            if (typeof enumType === "string") // Only used by AST.publishEnum()
            {
                // Eg. "0=First,1=Second,2=Third"
                if (!RegExp(/^(-?[0-9]+=[A-Za-z][A-Za-z0-9_]*,?)+$/g).test(enumType))
                {
                    throw new Error(`The specified 'enumType' ("${enumType}") is invalid`);
                }
                enumValues = enumType;
            }
            else
            {
                enumValues = Utils.getEnumValues(typeName, enumType);
            }
        }

        const newType: Type = new Type(typeName, typeDefinition, enumValues, codeGenOptions);
        _publishedTypes[typeName] = newType;

        if ((newType.definition !== "any") && (newType.expandedDefinition === "any"))
        {
            reportTypeSimplificationWarning(`type '${newType.name}'`, newType.definition);
        }

        // If needed, take the type off the "missing" list
        if (_missingTypes.has(typeName))
        {
            _missingTypes.delete(typeName);

            if (_missingTypes.size === 0)
            {
                try
                {
                    // Although we don't know if this was the "last" publishType() call, we do know that we can now fix
                    // up any 'expandedDefinition' properties that couldn't be set previously due to forward references
                    const updatedTypeCount: number = updateUnexpandedPublishedTypes();
                    Utils.log(`Expanded ${updatedTypeCount} type(s)`);
                }
                catch (error: unknown)
                {
                    throw new Error(`Deferred expansion of type(s) failed (reason: ${Utils.makeError(error).message})`);
                }
            }
        }
    }
    return (_publishedTypes[typeName]);
}

/** 
 * Result: An XML document (as unformatted text) describing the methods available on the specified instance
 * Handle the result in your PostResultDispatcher() for method name '_getPublishedMethods'.
 * Returns a unique callID for the method. 
 */
export function getPublishedMethods_Post(destinationInstance: string, expandTypes: boolean = false, includePostMethodsOnly: boolean = false, callContextData: any = null): number
{
    let callID: number = IC.postFork(destinationInstance, "_getPublishedMethods", 1, 8000, callContextData,
        IC.arg("expandTypes", expandTypes), 
        IC.arg("includePostMethodsOnly", includePostMethodsOnly));
    return (callID);
}

/** An Impulse wrapper for getPublishedMethods_Post(). Returns void, unlike getPublishedMethods_Post(). */
export function getPublishedMethods_PostByImpulse(destinationInstance: string, expandTypes: boolean = false, includePostMethodsOnly: boolean = false, callContextData: any = null): void
{
    IC.postByImpulse(destinationInstance, "_getPublishedMethods", 1, 8000, callContextData,
        IC.arg("expandTypes", expandTypes), 
        IC.arg("includePostMethodsOnly", includePostMethodsOnly));
}

/** 
 * Result: An XML document (as unformatted text) describing the types available on the specified instance.
 * Handle the result in your PostResultDispatcher() for method name '_getPublishedTypes'.
 * Returns a unique callID for the method. 
 */
export function getPublishedTypes_Post(destinationInstance: string, expandTypes: boolean = false, callContextData: any = null): number
{
    const callID: number = IC.postFork(destinationInstance, "_getPublishedTypes", 1, 8000, callContextData, IC.arg("expandTypes", expandTypes));
    return (callID);
}

/** An Impulse wrapper for getPublishedTypes_Post(). Returns void, unlike getPublishedTypes_Post(). */
export function getPublishedTypes_PostByImpulse(destinationInstance: string, expandTypes: boolean = false, callContextData: any = null): void
{
    IC.postByImpulse(destinationInstance, "_getPublishedTypes", 1, 8000, callContextData, IC.arg("expandTypes", expandTypes));
}

/** 
 * Result: true if the specified method/version has been published (ie. is available) on the specified instance. 
 * Handle the result in your PostResultDispatcher() for method name '_isPublishedMethod'.
 * Returns a unique callID for the method. 
 */
export function isPublishedMethod_Post(destinationInstance: string, methodName: string, methodVersion: number = 1, callContextData: any = null): number
{
    const callID: number = IC.postFork(destinationInstance, "_isPublishedMethod", 1, 8000, callContextData, 
        IC.arg("methodName", methodName), 
        IC.arg("methodVersion", methodVersion));
    return (callID);
}

/** An Impulse wrapper for isPublishedMethod_PostByImpulse(). Returns void, unlike isPublishedMethod_Post(). */
export function isPublishedMethod_PostByImpulse(destinationInstance: string, methodName: string, methodVersion: number = 1, callContextData: any = null): void
{
    IC.postFork(destinationInstance, "_isPublishedMethod", 1, 8000, callContextData, 
        IC.arg("methodName", methodName), 
        IC.arg("methodVersion", methodVersion));
}

/** [Internal] Returns XML text describing all published methods (on the local instance). */
export function getPublishedMethodsXml(expandTypes: boolean = false, includePostMethodsOnly: boolean = false): string
{
    let methodListXml: string = "";
    for (const name in _publishedMethods)
    {
        for (const version in _publishedMethods[name])
        {
            let method: Method = _publishedMethods[name][version];
            if (includePostMethodsOnly && !method.isPost)
            {
                continue;
            }
            methodListXml += method.getXml(expandTypes);
        }
    }
    return (methodListXml);
}

/** [Internal] Returns XML text describing all published types (on the local instance). */
export function getPublishedTypesXml(expandTypes: boolean = false): string
{
    let typeListXml: string = "";
    for (const typeName in _publishedTypes)
    {
        let type: Type = _publishedTypes[typeName];
        typeListXml += `<Type name="${typeName}" definition="${expandTypes ? type.expandedDefinition : type.definition}"${type.enumValues ? ` EnumValues="${type.enumValues}"` : ""}/>`;
    }
    return (typeListXml);
}

/** [Internal] Returns the published Type with the specified typeName (if it exists), or null (if it doesn't). */
export function getPublishedType(typeName: string): Type | null
{
    return (_publishedTypes[typeName] ?? null);
}

/** [Internal] Returns the published Method with the specified name and version (if it exists), or null (if it doesn't). */
export function getPublishedMethod(methodName: string, methodVersion: number = 1): Method | null
{
    if (_publishedMethods[methodName] && _publishedMethods[methodName][methodVersion])
    {
        return (_publishedMethods[methodName][methodVersion]);
    }
    return (null);
}

/** [Internal] Returns true if the specified method name is published. */
export function isPublishedMethod(methodName: string): boolean
{
    return (_publishedMethods[methodName] !== undefined);
}

/** The prefix for replaceable tokens in PublisherFramework.template.ts. */
const CODEGEN_TEMPLATE_TOKEN_PREFIX: string = "// [TOKEN:";

/** The name of the JSDoc tag used to identify which entities to [attempt to] publish in the input source file. */
const CODEGEN_TAG_NAME: string = "@ambrosia";

/** The prefix for a generated comment related to the code-gen process. */
const CODEGEN_COMMENT_PREFIX: string = "Code-gen";

/** Flags for the kind of code file to generate. Can be combined. */
export enum GeneratedFileKind
{
    /** 
     * Generates the code file for the consumer-side interface (method wrappers).\
     * The publisher can (if desired) also include this file to make self-calls.
     */
    Consumer = 1,
    /** Generates the code file for the publisher-side (Ambrosia framework). */
    Publisher = 2,
    /** Generates the code files for both the consumer-side interface (method wrappers) and publisher-side (Ambrosia framework). */
    All = Consumer | Publisher
}

/** When doing code generation, a TypeScript file can either be an Input file (provided by the developer) or a Generated file (created by Ambrosia). */
enum CodeGenerationFileType
{
    Input,
    Generated
}

/** The type of [git] file merge that can be performed on a code file generated by emitTypeScriptFile[FromSource](). If git is not installed, only FileMergeType.None is valid. */
export enum FileMergeType
{
    /** The generated file will always be overwritten, with no merge taking place. Any edits you have made will be lost. If git is not installed, this is the only valid choice. */
    None,
    /** The generated file will be automatically merged. You will still need to check the diff for merge correctness. */
    Auto,
    /** The generated file will be annotated with merge conflict markers. You will need to manually resolve each merge conflict. */
    Annotate
}

/** 
 * The sections of TypeScript code that can be generated by emitPublisherTypeScriptFile().\
 * Except 'None', these MUST match the token names used in PublisherFramework.template.ts. 
 */
export enum CodeGenSection
{
    None = 0,
    Header = 1,
    AppState = 2,
    PostMethodHandlers = 4,
    NonPostMethodHandlers = 8,
    PublishTypes = 16,
    PublishMethods = 32,
    /** Note: This section is only generated when **not** publishing from a source file. */
    MethodImplementations = 64,
    // Note: For all the 'xxxEventHandler' sections, the 'xxx' comes from Messages.AppEventType. See also: _appEventHandlerFunctions.
    ICStartingEventHandler = 4096,
    ICStartedEventHandler = 8192,
    ICStoppedEventHandler = 16384,
    ICReadyForSelfCallRpcEventHandler = 32768,
    RecoveryCompleteEventHandler = 65536,
    UpgradeStateEventHandler = 131072,
    UpgradeCodeEventHandler = 262144,
    IncomingCheckpointStreamSizeEventHandler = 524288,
    FirstStartEventHandler = 1048576,
    BecomingPrimaryEventHandler = 2097152,
    CheckpointLoadedEventHandler = 4194304,
    CheckpointSavedEventHandler = 8388608,
    UpgradeCompleteEventHandler = 16777216,
    ICConnectedEventHandler = 33554432
}

/** Class of details (both known and discovered) about an AppEvent handler function (in an input source file). */
class AppEventHandlerFunctionDetails
{ 
    expectedParameters: string = "";
    expectedReturnType: string = "void";
    foundInInputSource: boolean = false; // Set at runtime
    nsPath: string | null = null; // Set at runtime
    location: string | null = null; // Set at runtime

    constructor(expectedParameters: string = "", expectedReturnType: string = "void")
    {
        this.expectedParameters = expectedParameters;
        this.expectedReturnType = expectedReturnType;
    }

    /** Resets the properties set ("discovered") at runtime. */
    reset(): void
    {
        this.foundInInputSource = false;
        this.nsPath = null;
        this.location = null;
    }
}

/** The "well known" Ambrosia AppEvent handler function names (eg. 'onFirstStart'), and the details about them (both known and discovered). */
// Note: This effectively provides an analog for the 'OnFirstStart()' and 'BecomingPrimary()' overridable methods in the 'abstract class Immortal' in C#.
const _appEventHandlerFunctions: { [functionName: string]: AppEventHandlerFunctionDetails } = {};
Object.keys(Messages.AppEventType).forEach(enumValue =>
{
    if (isNaN(parseInt(enumValue)))
    {
        _appEventHandlerFunctions["on" + enumValue] = new AppEventHandlerFunctionDetails();
    }
});
_appEventHandlerFunctions["on" + Messages.AppEventType[Messages.AppEventType.ICStopped]].expectedParameters = "exitCode: number";
_appEventHandlerFunctions["on" + Messages.AppEventType[Messages.AppEventType.UpgradeState]].expectedParameters = "upgradeMode: Messages.AppUpgradeMode";
_appEventHandlerFunctions["on" + Messages.AppEventType[Messages.AppEventType.UpgradeCode]].expectedParameters = "upgradeMode: Messages.AppUpgradeMode";
_appEventHandlerFunctions["on" + Messages.AppEventType[Messages.AppEventType.CheckpointLoaded]].expectedParameters = "checkpointSizeInBytes: number";

/** 
 * Class that defines options which affect how source files are generated (emitted). 
 * - apiName is required. This is the generic name of the Ambrosia app/service, not an instance name.
 * - fileKind defaults to GeneratedFileKind.All.
 * - mergeType defaults to MergeType.Annotate.
 * - checkGeneratedTS defaults to true.
 * - ignoreTSErrorsInSourceFile defaults to false.
 * - generatedFilePrefix defaults "".
 * - generatedFileName defaults to "ConsumerInterface" for GeneratedFileKind.Consumer and "PublisherFramework" for GeneratedFileKind.Publisher.
 *   If supplied, generatedFileName should not include an extension (if it does it will be ignored: the extension is always ".g.ts") but it can include a path.
 * - outputPath defaults to the current folder.
 * - tabIndent defaults to 4.
 * - emitGeneratedTime defaults to true.
 * - allowImplicitTypes defaults to true.
 * - publisherName defaults to the git user name (if using git), or null otherwise.
 * - publisherEmail defaults to the git user email (if using git), or null otherwise.
 */
export class FileGenOptions
{
    // Indexer (for 'noImplicitAny' compliance), although this does end up hiding ts(2551): "Property 'xxxxx' does not exist on type 'FileGenOptions'"
    [key: string]: unknown;

    /** 
     * [Required] The name of the API (an Ambrosia app/service) that files are being generated for. 
     * This is a generic name, and does **not** refer to a specific registered instance of the app/service (other than by coincidence).
     */
    apiName: string = "";
    /** The kind of file(s) to generate. Defaults to GeneratedFileKind.All. */
    fileKind?: GeneratedFileKind = GeneratedFileKind.All;
    /** How to handle [git] merging any changes (made to a previously generated file) back into the newly generated file. Defaults to FileMergeType.Annotate. */
    // Note: We default 'mergeType' to 'Annotate' for 2 reasons: 
    // 1) It gives us a way to prevent merging again before conflicts have been resolved (repeatedly auto-merging can lead to lots of cruft in the code).
    // 2) Because the merge is non-optimal (because it uses an empty file as the common ancestor) it's better to have the merge conflicts explicitly annotated.
    mergeType?: FileMergeType = FileMergeType.Annotate;
    /** Whether the generated file(s) should be checked for errors. Defaults to true. */
    checkGeneratedTS?: boolean = true;
    /** Whether the source [input] file - if supplied - should be checked for errors. Defaults to false. */
    ignoreTSErrorsInSourceFile?: boolean = false;
    /** 
     * Whether to enable the 'strict' TypeScript compiler flag (and 'noImplicitReturns'). Defaults to true.\
     * See https://www.typescriptlang.org/tsconfig#strict.
     */
    strictCompilerChecks?: boolean = true;
    /** 
     * A prefix that will be applied to the standard generated file names (PublisherFramework.g.ts and ConsumerInterface.g.ts). 
     * To completely change the generated file name, use 'generatedFileName'. Defaults to "". 
     */
    generatedFilePrefix?: string = "";
    /** An override name for the generated file. Can include a path, but not an extension. Overrides 'generatedFilePrefix' if provided. Defaults to "". */
    generatedFileName?: string = "";
    /** The folder where generated files will be written (if a path is specified in 'generatedFileName' it will overwrite this setting). Defaults to the current folder. */
    outputPath?: string = process.cwd();
    /** The number of spaces in a logical tab. Used to format the generated source code. Defaults to 4. */
    tabIndent?: number = 4;
    /** Whether to write a timestamp in the generated file for its creation date/time. Defaults to true. */
    emitGeneratedTime?: boolean = true;
    /** 
     * Whether to allow a published method/type that does not explicitly declare all parameter/property/return types
     * (by default, missing parameter/property types are assumed to be 'any' and missing return types are assumed to be 'void').\
     * If set to false, a missing parameter/property/return type will result in an error.\
     * Defaults to true.
     */
    allowImplicitTypes?: boolean = true;
    /** 
     * [Internal] Whether to stop code generation if an error is encountered.\
     * This setting is for **internal testing only**.\
     * If you set it to false, the generated file(s) **will be unusable**.\
     * Defaults to true.
     */
    // Note: The primary purpose of this flag is so that we can run ALL the tests in NegativeTests.ts at once
    haltOnError?: boolean = true;
    /** 
     * [Experimental] The set of sections (or'd together) to **not** generate publisher code for. Defaults to 'None'. 
     * Setting this can help reduce merge conflicts in some instances, but it can also introduce code errors in the generated code.  
     */
    publisherSectionsToSkip?: CodeGenSection = CodeGenSection.None;
    /** The name of the publisher of the Ambrosia instance (eg. "Microsoft"). Defaults to the git user name (if using git), or "" otherwise. */
    publisherName?: string = "";
    /** The email address of the publisher of the Ambrosia instance. Defaults to the git user email (if using git), or "" otherwise. */
    publisherEmail?: string = "";
    /** 
     * The publisher contact information, as constructed from the supplied 'publisherName' and 'publisherEmail' (in the format "name [email]", "name", or "email").\
     * Alternatively, any contact information can be provided (eg. URL, phone number) which will override the constructed value.\
     * Set to null to omit contact information (not recommended).
     */
    publisherContactInfo?: string | null = "";

    constructor(partialOptions: FileGenOptions)
    {
        for (let optionName in partialOptions)
        {
            if (this[optionName] !== undefined)
            {
                const value: any = partialOptions[optionName];

                // Prevent [accidental] clearing of the default value
                if (value === undefined)
                {
                    continue;
                }
                this[optionName] = value;

                if (optionName === "outputPath")
                {
                    if (!File.existsSync(value))
                    {
                        throw new Error(`The specified FileGenOptions.outputPath ('${value}') does not exist`);
                    }
                    this.outputPath = Path.resolve(value);
                }
            }
            else
            {
                throw new Error(`'${optionName}' is not a valid FileGenOptions setting`);
            }
        }

        if (this.generatedFileName && Path.parse(this.generatedFileName).dir) // Note: the 'dir' property is empty when Path.dirname() returns '.'
        {
            this.outputPath = Path.resolve(Path.dirname(this.generatedFileName));
        }

        if (this.generatedFilePrefix && !/^[A-Za-z0-9-_\.]+$/.test(this.generatedFilePrefix))
        {
            throw new Error(`The specified FileGenOptions.generatedFilePrefix ('${this.generatedFilePrefix}') contains one or more invalid characters`);
        }

        // If needed, read git config settings.
        // Note: The user should set 'publisherContactInfo' (or 'publisherName' AND 'publisherEmail') to suppress the git lookup (eg. if they are using another source control system).
        if ((this.publisherContactInfo === "") && ((this.publisherName === "") || (this.publisherEmail === "")))
        {
            const publisherProperties: string[] = ["Name", "Email"];
            publisherProperties.forEach(prop =>
            {
                const propName: string = "publisher" + prop;
                if (this[propName] === "")
                {
                    try
                    {
                        // Note: In launch.json, set '"autoAttachChildProcesses": false' to prevent the VS Code debugger from attaching to this process
                        // Note: This operation takes about 150ms
                        this[propName] = ChildProcess.execSync(`git config user.${prop.toLowerCase()}`, { encoding: "utf8", windowsHide: true }).trim();
                    }
                    catch (error: unknown)
                    {
                        const errorMsg: string = Utils.makeError(error).message.replace(/\s+/g, " ").trim().replace(/\.$/, "");
                        Utils.log(`Warning: Unable to read user.${prop.toLowerCase()} from git config (reason: ${errorMsg}); FileGenOptions.${propName} (or FileGenOptions.publisherContactInfo) must be set explicitly`);
                    }
                }
            });
        }

        if (this.publisherContactInfo === null) // The "omit contact info" case
        {
            this.publisherContactInfo = "";
        }
        else
        {
            if (this.publisherContactInfo === "") // The "not explicitly set" case
            {
                this.publisherContactInfo = (this.publisherName && this.publisherEmail) ? `${this.publisherName} [${this.publisherEmail}]` : (this.publisherName || this.publisherEmail || "");
            }
        }
    }
}

/**
 * **Note:** It is recommended that emitTypeScriptFileFromSource() is used instead of this method, because it avoids the need to explicitly call 
 * any publishX() methods (publishType, publishMethod, or publishPostMethod).
 * 
 * Generates consumer-side (method wrappers) and/or publisher-side (Ambrosia framework) TypeScript files (*.g.ts) from the currently
 * published types and methods. Returns the number of files successfully generated.
 */
export function emitTypeScriptFile(fileOptions: FileGenOptions): number
{
    return (emitTypeScriptFileEx(null, fileOptions));
}

/** 
 * Generates consumer-side (method wrappers) and/or publisher-side (Ambrosia framework) TypeScript files (*.g.ts)
 * from the annotated functions, static methods, type-aliases and enums in the specified TypeScript source file ('sourceFileName').\
 * Returns the number of files successfully generated.
 * 
 * The types and methods in the supplied TypeScript file that need to be published must be annotated with a special JSDoc tag: @ambrosia.
 * - Example for a type or enum:\
 *   &#64;ambrosia publish=true
 * - Example for a function that implements a post method:\
 *   &#64;ambrosia publish=true, [version=1], [doRuntimeTypeChecking=true]
 * - Example for a function that implements a non-post method:\
 *   &#64;ambrosia publish=true, methodID=123
 * 
 * The only required attribute is 'publish'.
 * 
 * Automatically publishing types and methods from an [annotated] TypeScript source file provides 3 major benefits over hand-crafting 
 * publishType(), publishPostMethod(), and publishMethod() calls then calling emitTypeScriptFile():
 * 1) The developer gets design-time support from the TypeScript compiler and the IDE (eg. VSCode).
 * 2) The types and methods can be "verified correct" before doing code-gen, speeding up the edit/compile/run cycle.
 * 3) The majority of the developer-provided code no longer resides in the generated PublisherFramework.g.ts, so less time is spent resolving merges conflicts.
 */
export function emitTypeScriptFileFromSource(sourceFileName: string, fileOptions: FileGenOptions): number
{
    if (!sourceFileName || !sourceFileName.trim())
    {
        throw new Error("The 'sourceFileName' parameter cannot be null or empty");
    }

    return (emitTypeScriptFileEx(sourceFileName, fileOptions));
}

function emitTypeScriptFileEx(sourceFileName: string | null = null, fileOptions: FileGenOptions): number
{
    fileOptions = new FileGenOptions(fileOptions); // To force assignment of default values for non-supplied properties
    let expectedFileCount: number = (fileOptions.fileKind !== GeneratedFileKind.All) ? 1 : 2;
    let generatedFileCount: number = 0;
    let totalErrorCount: number = 0;
    let totalMergConflictCount: number = 0;
    
    try
    {
        if (Root.initializationMode() !== Root.LBInitMode.CodeGen)
        {
            throw new Error(`Code generation requires Ambrosia to be initialized with the 'CodeGen' LBInitMode, not the '${Root.LBInitMode[Root.initializationMode()]}' mode`);
        }

        if (fileOptions.fileKind === GeneratedFileKind.All)
        {
            Utils.log(`Generating TypeScript files for Consumer and Publisher...`);
        }
        else
        {
            Utils.log(`Generating ${GeneratedFileKind[assertDefined(fileOptions.fileKind)]} TypeScript file...`);
        }

        if (sourceFileName)
        {
            if (AST.publishedSourceFile())
            {
                // Handle the case where emitTypeScriptFileFromSource() is being called more than once [typically this is so that a FileGenOptions.generatedFileName can be supplied].
                // In this case, the second call should not try to publish again since that would fail with duplicate type/methods errors.
                // Further, if a different source file is being specified we must prevent this since the generated consumer/producer files need to be based on the same set of published types/methods.
                let originalSourceFile: string = Path.resolve(AST.publishedSourceFile());
                
                if (Utils.equalIgnoringCase(Path.resolve(sourceFileName), originalSourceFile))
                {
                    Utils.log(`Skipping publish step: Entities have already been published from ${originalSourceFile}`);                    
                }
                else
                {
                    throw new Error(`The 'sourceFileName' (${Path.resolve(sourceFileName)}) cannot be different from the previously used file (${originalSourceFile})`);
                }
            }
            else
            {
                AST.publishFromAST(sourceFileName, fileOptions);
            }

            if (AST.publishedEntityCount() === 0)
            {
                // publishedEntityCount can be 0 because there are no @ambrosia tags, all the @ambrosia 'publish' attributes are 'false', or because none of the tagged entities are exported
                throw new Error(`The input source file (${Path.basename(sourceFileName)}) does not publish any entities (exported functions, static methods, type aliases and enums annotated with an ${CODEGEN_TAG_NAME} JSDoc tag)`);
            }
        }
        
        // This will only happen if no methods have been published because publish[Post]Method() also catches this condition
        if (_missingTypes.size > 0)
        {
            const missingTypesList: string = [..._missingTypes].map(kvp => `'${kvp[0]}' found in ${kvp[1]}`).join(", ")
            throw new Error(`The following types are referenced by other types, but have not been published: ${missingTypesList}`);
        }

        if (fileOptions.generatedFileName && (fileOptions.fileKind === GeneratedFileKind.All))
        {
            throw new Error("When a FileGenOptions.generatedFileName is specified the FileGenOptions.fileKind cannot be GeneratedFileKind.All; instead, call emitTypeScriptFile() or emitTypeScriptFileFromSource() for each required GeneratedFileKind using a different FileGenOptions.generatedFileName in each call");
        }
        
        function incrementTotals(result: SourceFileProblemCheckResult | null): void
        {
            if (result !== null)
            {
                totalErrorCount += result.errorCount;
                totalMergConflictCount += result.mergeConflictCount;
                generatedFileCount++;
            }
        }

        if ((assertDefined(fileOptions.fileKind) & GeneratedFileKind.Consumer) === GeneratedFileKind.Consumer)
        {
            let result: SourceFileProblemCheckResult | null = emitConsumerTypeScriptFile(fileOptions.generatedFileName || (fileOptions.generatedFilePrefix + "ConsumerInterface"), fileOptions, sourceFileName || undefined);
            incrementTotals(result);
        }

        if ((assertDefined(fileOptions.fileKind) & GeneratedFileKind.Publisher) === GeneratedFileKind.Publisher)
        {
            let result: SourceFileProblemCheckResult | null = emitPublisherTypeScriptFile(fileOptions.generatedFileName || (fileOptions.generatedFilePrefix + "PublisherFramework"), fileOptions, sourceFileName || undefined);
            incrementTotals(result);
        }
    }
    catch (error: unknown)
    {
        const err: Error = Utils.makeError(error);
        Utils.log(`Error: ${err.message} [origin: ${Utils.getErrorOrigin(err) ?? "N/A"}]`);
    }

    let success: boolean = (expectedFileCount === generatedFileCount) && (totalErrorCount === 0);
    let prefix: string = (fileOptions.fileKind === GeneratedFileKind.All) ? "Code" : (GeneratedFileKind[assertDefined(fileOptions.fileKind)] + " code");
    let outcomeMessage: string = `${prefix} file generation ${success ? "SUCCEEDED" : "FAILED"}: ${generatedFileCount} of ${expectedFileCount} files generated`;

    if (generatedFileCount > 0)
    {
        outcomeMessage += fileOptions.checkGeneratedTS ? 
            `; ${totalErrorCount} TypeScript errors, ${totalMergConflictCount} merge conflicts` : 
            `; File${expectedFileCount > 1 ? "s" : ""} not checked for TypeScript errors`;
    }

    Utils.log(outcomeMessage);
    return (generatedFileCount);
}

/** 
 * Generates a TypeScript file (called PublisherFramework.g.ts by default) for all published types and methods. 
 * The purpose of this file is to be included by the publishing immortal as the message dispatch framework.\
 * The 'fileName' should not include an extension (if it does it will be ignored) but it may include a path.\
 * Returns a SourceFileProblemCheckResult for the generated file, or null if no file was generated.
 */
function emitPublisherTypeScriptFile(fileName: string, fileOptions: FileGenOptions, sourceFileName?: string): SourceFileProblemCheckResult | null
{
    const NL: string = Utils.NEW_LINE; // Just for short-hand

    // Note: When 'sourceFileName' is supplied, code generation behaves slightly differently:
    // 1) In the Header section it creates a 'import * as PTM from "${sourceFileName}"; // PTM = "Published Types and Methods"'.
    // 2) In the PostMethodHandlers and NonPostMethodHandlers sections it uses "PTM." as the method/type "namespace" qualifier.
    // 3) The MethodImplementations section is left empty (because the implementations are in the supplied input source file).

    try
    {
        let templateFileName: string = "PublisherFramework.template.ts";
        let pathedTemplateFile: string = getPathedTemplateFile(templateFileName);
        let pathedOutputFile: string = Path.join(assertDefined(fileOptions.outputPath), `${Path.basename(fileName).replace(Path.extname(fileName), "")}.g.ts`);
        let template: string = removeDevOnlyTemplateCommentLines(File.readFileSync(pathedTemplateFile, { encoding: "utf8" }));
        let content: string = template;

        checkForFileNameConflicts(pathedOutputFile, sourceFileName);
        checkForGitMergeMarkers(pathedOutputFile);

        // If the user defined their app-state in the input file, then we'll skip adding CodeGenSection.AppState and instead reference their state variable
        if (sourceFileName && AST.appStateVar() !== "")
        {
            // Update the checkpointProducer() and checkpointConsumer() in the template [since they reference _appState] to reference the user-provided app-state variable from the input file
            content = content.replace(/State\._appState/g, SOURCE_MODULE_ALIAS + "." + AST.appStateVar());
            // Also update the "State.AppState" references in checkpointConsumer() 
            content = content.replace(/State\.AppState/g, SOURCE_MODULE_ALIAS + "." + AST.appStateVarClassName());
        }

        content = replaceTemplateToken(content, CodeGenSection.Header, fileOptions, "", sourceFileName);
        content = replaceTemplateToken(content, CodeGenSection.AppState, fileOptions, "", sourceFileName);
        content = replaceTemplateToken(content, CodeGenSection.PostMethodHandlers, fileOptions, `// ${CODEGEN_COMMENT_PREFIX}: Post method handlers will go here` + NL, sourceFileName);
        content = replaceTemplateToken(content, CodeGenSection.NonPostMethodHandlers, fileOptions, `// ${CODEGEN_COMMENT_PREFIX}: Fork/Impulse method handlers will go here` + NL, sourceFileName);
        content = replaceTemplateToken(content, CodeGenSection.PublishTypes, fileOptions, `// ${CODEGEN_COMMENT_PREFIX}: Published types will go here`);
        content = replaceTemplateToken(content, CodeGenSection.PublishMethods, fileOptions, `// ${CODEGEN_COMMENT_PREFIX}: Published methods will go here`);
        content = replaceTemplateToken(content, CodeGenSection.MethodImplementations, fileOptions, sourceFileName ? "" : (NL + `// ${CODEGEN_COMMENT_PREFIX}: Method implementation stubs will go here`), sourceFileName);

        // Wire-up (or simply add "TODO" comments for) AppEvent handlers 
        for (let enumValue in CodeGenSection)
        {
            if (RegExp(/.*EventHandler$/g).test(enumValue))
            {
                const eventHandlerSection: CodeGenSection = CodeGenSection[enumValue as keyof typeof CodeGenSection];
                content = replaceTemplateToken(content, eventHandlerSection, fileOptions, makeEventHandlerComment(eventHandlerSection), sourceFileName);
            }
        }

        // Check that all tokens got replaced
        if (content.indexOf(CODEGEN_TEMPLATE_TOKEN_PREFIX) !== -1)
        {
            const regExp: RegExp = new RegExp(Utils.regexEscape(CODEGEN_TEMPLATE_TOKEN_PREFIX) + "Name=\\w+", "g");
            const matches: RegExpMatchArray | null = content.match(regExp);
            if (matches && (matches.length > 0))
            {
                let tokenNameList: string[] = [...matches].map(item => item.split("=")[1]);
                throw new Error(`The following template token(s) [in ${pathedTemplateFile}] were not handled: ${tokenNameList.join(", ")}`);
            }
            else
            {
                // Safety net in case our RegExp didn't work [so that we don't emit a bad file]
                throw new Error(`Not all template token(s) [in ${pathedTemplateFile}] were handled`);
            }
        }

        writeGeneratedFile(content, pathedOutputFile, assertDefined(fileOptions.mergeType));
        Utils.log(`Code file generated: ${pathedOutputFile}${!fileOptions.checkGeneratedTS ? " (TypeScript checks skipped)" : ""}`);
        return (fileOptions.checkGeneratedTS ? checkGeneratedFile(pathedOutputFile, (fileOptions.mergeType !== FileMergeType.None)) : new SourceFileProblemCheckResult());
    }
    catch (error: unknown)
    {
        Utils.log(`Error: emitPublisherTypeScriptFile() failed (reason: ${Utils.makeError(error).message})`);
        return (null);
    } 

    /** [Local function] Returns a 'call to action' comment for creating the specified app-event handler. */
    function makeEventHandlerComment(section: CodeGenSection): string
    {
        let codeGenComment: string = "// TODO: Add your [non-async] handler here";

        if (sourceFileName)
        {
            const fnName: string = "on" + CodeGenSection[section].replace("EventHandler", "");
            const fnDetails: AppEventHandlerFunctionDetails = _appEventHandlerFunctions[fnName];
            const signature: string = `${fnName}(${fnDetails.expectedParameters}): ${fnDetails.expectedReturnType}`;

            codeGenComment = `// TODO: Add an exported [non-async] function '${signature}' to ${makeRelativePath(assertDefined(fileOptions.outputPath), sourceFileName)}, then (after the next code-gen) a call to it will be generated here`;
            if ((section === CodeGenSection.UpgradeStateEventHandler) || (section === CodeGenSection.UpgradeCodeEventHandler))
            {
                // Needed because this handler takes a Messages.AppUpgradeMode parameter
                codeGenComment += NL + `// Note: You will need to import Ambrosia to ${sourceFileName} in order to reference the 'Messages' namespace.`;
                if (section === CodeGenSection.UpgradeStateEventHandler)
                {
                    codeGenComment += NL + `//       Upgrading is performed by calling _appState.upgrade(), for example:`;
                    codeGenComment += NL + `//       _appState = _appState.upgrade<AppStateVNext>(AppStateVNext);`;
                }
                if (section === CodeGenSection.UpgradeCodeEventHandler)
                {
                    codeGenComment += NL + `//       Upgrading is performed by calling IC.upgrade(), passing the new handlers from the "upgraded" PublisherFramework.g.ts,`;
                    codeGenComment += NL + `//       which should be part of your app (alongside your original PublisherFramework.g.ts).`;
                }
            }
        }
        return (codeGenComment);
    }
}

/** 
 * Returns the relative path to the specified [pathed] file from the specified starting path.
 * For example, if 'fromPath' is "./src" and 'toPathedFile' is "./test/PI.ts", the method returns "../test/PI.ts".
 */
function makeRelativePath(fromPath: string, toPathedFile: string): string
{
    let relativeSourceFileName: string = Path.relative(fromPath, toPathedFile);
    relativeSourceFileName = (relativeSourceFileName.startsWith("..") ? "" : "./") + relativeSourceFileName.replace(/\\/g, "/");
    return (relativeSourceFileName);
}

/** 
 * Generates a TypeScript file (called ConsumerInterface.g.ts by default) for all published types and methods. 
 * The purpose of this file is to be included by another [Ambrosia Node] immortal so that it can call methods on this immortal.
 * The 'fileName' should not include an extension (if it does it will be ignored) but it may include a path.
 * Returns a SourceFileProblemCheckResult for the generated file, or null if no file was generated.
 */
function emitConsumerTypeScriptFile(fileName: string, fileOptions: FileGenOptions, sourceFileName?: string): SourceFileProblemCheckResult | null
{
    try
    {
        const NL: string = Utils.NEW_LINE; // Just for short-hand
        const tab: string = " ".repeat(assertDefined(fileOptions.tabIndent));
        let pathedOutputFile: string = Path.join(assertDefined(fileOptions.outputPath), `${Path.basename(fileName).replace(Path.extname(fileName), "")}.g.ts`);
        let content: string = "";
        let namespaces: string[] = [];
        let namespaceComments: { [namespace: string]: string | undefined } = {}; // Effectively a rebuilt AST._namespaceJSDocComments but just for the namespaces that contain published entities
        let previousNsPath: string = "";

        checkForFileNameConflicts(pathedOutputFile, sourceFileName);
        checkForGitMergeMarkers(pathedOutputFile);

        // Note: We emit the types and methods using their original namespaces. If we didn't they'd all get emitted at the root level in the file (ie. no namespace) so we'd
        // lose the original organizational structure of the code (which imparts the logical grouping/meaning of members). However, using namespaces is not required to prevent
        // name collisions [in Ambrosia] because published types and methods must ALWAYS have unique names, even if defined in different TS namespaces.

        // 1) Populate the 'namespaces' list
        if (Object.keys(_publishedTypes).length > 0)
        {
            for (const typeName in _publishedTypes)
            {
                let type: Type = _publishedTypes[typeName];
                if (type.codeGenOptions?.nsPath && (namespaces.indexOf(type.codeGenOptions.nsPath) === -1))
                {
                    addNamespaces(type.codeGenOptions.nsPath);
                    namespaceComments[type.codeGenOptions.nsPath] = type.codeGenOptions.nsComment;
                }
            }
        }
        if (Object.keys(_publishedMethods).length > 0)
        {
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    let method: Method = _publishedMethods[name][version];
                    if (method.codeGenOptions?.nsPath && (namespaces.indexOf(method.codeGenOptions.nsPath) === -1))
                    {
                        addNamespaces(method.codeGenOptions.nsPath);
                        namespaceComments[method.codeGenOptions.nsPath] = method.codeGenOptions.nsComment;
                    }
                }
            }
        }

        // 2) Emit types and methods
        if (namespaces.length === 0) // The empty namespace (ie. no namespace)
        {
            // Emit types and methods at the root of the file
            emitTypesAndMethods(0);
            content = content.trimRight();
        }
        else
        {
            // Emit types and methods in their originating namespaces
            namespaces.push(""); // We manually add the empty namespace (ie. no namespace) to emit entities which have no nsPath
            namespaces = namespaces.sort(); // Eg. A, A.B, A.B.C, A.D, B, B.D, B.D.E, ...
            for (const nsPath of namespaces)
            {
                const nsNestDepth: number = nsPath.split(".").length - 1;
                const nsIndent: string = tab.repeat(nsNestDepth);
                const nsName: string = nsPath ? nsPath.split(".")[nsNestDepth] : "";
                const nsComment: string = namespaceComments[nsPath] ? AST.formatJSDocComment(assertDefined(namespaceComments[nsPath]), nsIndent.length) : "";
                const previousNsNestDepth: number = previousNsPath.split(".").length - 1;

                if (nsNestDepth > previousNsNestDepth)
                {
                    // Start new nested namespace
                    if (nsComment)
                    {
                        content += nsComment + NL;
                    }
                    content += `${nsIndent}export namespace ${nsName}` + NL;
                    content += `${nsIndent}{` + NL;
                }
                else
                {
                    if (previousNsPath)
                    {
                        // Emit closing braces (from the previous namespace depth back to the current depth)
                        content = content.trimRight() + NL;
                        for (let depth = previousNsNestDepth; depth >= nsNestDepth; depth--)
                        {
                            content += tab.repeat(depth) + "}" + NL;
                        }
                        content = content.trimRight() + NL.repeat(2);
                    }
                    if (nsPath)
                    {
                        if (nsComment)
                        {
                            content += nsComment + NL;
                        }
                        content += `${nsIndent}export namespace ${nsName}` + NL;
                        content += `${nsIndent}{` + NL;
                    }
                }

                emitTypesAndMethods(nsPath ? assertDefined(fileOptions.tabIndent) * (nsNestDepth + 1) : 0, nsPath);
                previousNsPath = nsPath;
            }

            // Emit closing braces (back to the root)
            content = content.trimRight() + NL;
            for (let depth = previousNsPath.split(".").length - 1; depth >= 0; depth--)
            {
                content += tab.repeat(depth) + "}" + (depth !== 0 ? NL : "");
            }
        }
        
        // 3) If there are published post methods, write a PostResultDispatcher().
        //    This is just a minimal outline with "TODO" placeholders where the developer should add their own code.
        //    Further, if the app includes multiple ConsumerInterface.g.ts files (because it uses more than one type of Ambrosia app/service), then the
        //    developer will need to unify the [potentially] multiple PostResultDispatcher's into a single dispatcher which can be passed to IC.start().
        if (publishedPostMethodsExist())
        {
            let postResultDispatcher: string = "";

            postResultDispatcher += "/**" + NL;
            postResultDispatcher += " * Handler for the results of previously called post methods (in Ambrosia, only 'post' methods return values). See Messages.PostResultDispatcher.\\" + NL;
            postResultDispatcher += " * Must return true only if the result (or error) was handled." + NL + " */" + NL;
            postResultDispatcher += "export function postResultDispatcher(senderInstanceName: string, methodName: string, methodVersion: number, callID: number, callContextData: any, result: any, errorMsg: string): boolean" + NL;
            postResultDispatcher += "{" + NL;
            postResultDispatcher += tab + "const sender: string = IC.isSelf(senderInstanceName) ? \"local\" : `'${senderInstanceName}'`;" + NL;
            postResultDispatcher += tab + "let handled: boolean = true;" + NL.repeat(2);

            // We do this to help catch the case where the developer forgets to create a "unified" PostResultDispatcher [ie. that can
            // handle post results for ALL used ConsumerInterface.g.ts files] so they end up accidentally re-using a PostResultDispatcher
            // that's specific to just one type of Ambrosia app/service (and one destination [or set of destinations])
            postResultDispatcher += tab + "if (_knownDestinations.indexOf(senderInstanceName) === -1)" + NL;
            postResultDispatcher += tab + "{" + NL;
            postResultDispatcher += tab + tab + `return (false); // Not handled: this post result is from a different instance than the destination instance currently (or previously) targeted by the '${fileOptions.apiName}' API` + NL;
            postResultDispatcher += tab + "}" + NL.repeat(2);

            postResultDispatcher += tab + "if (errorMsg)" + NL;
            postResultDispatcher += tab + "{" + NL;
            postResultDispatcher += tab + tab + "switch (methodName)" + NL;
            postResultDispatcher += tab + tab + "{" + NL;
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    const method: Method = _publishedMethods[name][version];
                    if (method.isPost)
                    {
                        postResultDispatcher += tab.repeat(3) + `case \"${method.nameForTSWrapper}\":` + NL;
                    }
                }
            }
            postResultDispatcher += tab.repeat(4) + "Utils.log(`Error: ${errorMsg}`);" + NL;
            postResultDispatcher += tab.repeat(4) + "break;" + NL;
            postResultDispatcher += tab.repeat(3) + "default:" + NL;
            postResultDispatcher += tab.repeat(4) + "handled = false;" + NL;
            postResultDispatcher += tab.repeat(4) + "break;" + NL;
            postResultDispatcher += tab + tab + "}" + NL;
            postResultDispatcher += tab + "}" + NL;
            postResultDispatcher += tab + "else" + NL;
            postResultDispatcher += tab + "{" + NL;
            postResultDispatcher += tab + tab + "switch (methodName)" + NL;
            postResultDispatcher += tab + tab + "{" + NL;
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    const method: Method = _publishedMethods[name][version];
                    if (method.isPost)
                    {
                        let nsPathOfReturnType: string = getPublishedType(Type.removeArraySuffix(method.returnType))?.codeGenOptions?.nsPath || "";
                        if (nsPathOfReturnType)
                        {
                            nsPathOfReturnType += ".";
                        }
                        postResultDispatcher += tab.repeat(3) + `case \"${method.nameForTSWrapper}\":` + NL;
                        if (method.returnType === "void")
                        {
                            postResultDispatcher += tab.repeat(4) + `// TODO: Handle the method completion (it returns void), optionally using the callContextData passed in the call` + NL;
                        }
                        else
                        {
                            postResultDispatcher += tab.repeat(4) + `const ${method.nameForTSWrapper}_Result: ${nsPathOfReturnType}${method.returnType} = result;` + NL;
                            postResultDispatcher += tab.repeat(4) + `// TODO: Handle the result, optionally using the callContextData passed in the call` + NL;
                        }
                        postResultDispatcher += tab.repeat(4) + "Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);" + NL;
                        postResultDispatcher += tab.repeat(4) + "break;" + NL;
                    }
                }
            }
            postResultDispatcher += tab.repeat(3) + "default:" + NL;
            postResultDispatcher += tab.repeat(4) + "handled = false;" + NL;
            postResultDispatcher += tab.repeat(4) + "break;" + NL;
            postResultDispatcher += tab + tab + "}" + NL;
            postResultDispatcher += tab + "}" + NL;
            postResultDispatcher += tab + "return (handled);" + NL;
            postResultDispatcher += "}";

            content += NL.repeat(2) + postResultDispatcher;
        }

        /** [Local function] Returns true if any published method is a post method. */
        function publishedPostMethodsExist(): boolean
        {
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    const method: Method = _publishedMethods[name][version];
                    if (method.isPost)
                    {
                        return (true);
                    }
                }
            }
            return (false);
        }

        /** [Local function] Adds all the sub-paths for a given namespace path. */
        function addNamespaces(nsPath: string): void
        {
            let nsSubPath: string = "";
            for (let namespace of nsPath.split("."))
            {
                nsSubPath += ((nsSubPath.length > 0) ? "." : "") + namespace;
                if (namespaces.indexOf(nsSubPath) === -1)
                {
                    namespaces.push(nsSubPath);
                }
            }
        }

        /** [Local function] Adds published Types (as classes, type-definitions, or enum definitions) and published Methods (as function wrappers) to the 'content'. */
        function emitTypesAndMethods(startingIndent: number, nsPath: string = ""): void
        {
            const pad: string = " ".repeat(startingIndent);

            if (Object.keys(_publishedTypes).length > 0)
            {
                for (const typeName in _publishedTypes)
                {
                    let type: Type = _publishedTypes[typeName];
                    if (type.codeGenOptions?.nsPath === nsPath)
                    {
                        content += type.makeTSType(startingIndent, fileOptions.tabIndent, type.codeGenOptions?.jsDocComment) + NL.repeat(2);
                    }
                }
            }

            if (Object.keys(_publishedMethods).length > 0)
            {
                for (const name in _publishedMethods)
                {
                    for (const version in _publishedMethods[name])
                    {
                        let method: Method = _publishedMethods[name][version];
                        if (method.codeGenOptions?.nsPath === nsPath)
                        {
                            content += method.makeTSWrappers(startingIndent, fileOptions, method.codeGenOptions?.jsDocComment) + NL.repeat(2);
                        }
                    }
                }
            }
        }

        if (content.length > 0)
        {
            let header: string = getHeaderCommentLines(GeneratedFileKind.Consumer, fileOptions).join(NL) + NL;
            header += "import Ambrosia = require(\"ambrosia-node\");" + NL;
            header += "import IC = Ambrosia.IC;" + NL;
            header += "import Utils = Ambrosia.Utils;" + NL.repeat(2);
            header += `const _knownDestinations: string[] = []; // All previously used destination instances (the '${fileOptions.apiName}' Ambrosia app/service can be running on multiple instances, potentially simultaneously); used by the postResultDispatcher (if any)` + NL;
            header += "let _destinationInstanceName: string = \"\"; // The current destination instance" + NL;
            header += "let _postTimeoutInMs: number = 8000; // -1 = Infinite" + NL.repeat(2);

            header += "/** " + NL;
            header += " * Sets the destination instance name that the API targets.\\" + NL;
            header += ` * Must be called at least once (with the name of a registered Ambrosia instance that implements the '${fileOptions.apiName}' API) before any other method in the API is used.` + NL;
            header += " */" + NL;
            header += "export function setDestinationInstance(instanceName: string): void" + NL;
            header += "{" + NL;
            header += `${tab}_destinationInstanceName = instanceName.trim();` + NL;
            header += `${tab}if (_destinationInstanceName && (_knownDestinations.indexOf(_destinationInstanceName) === -1))` + NL;
            header += `${tab}{` + NL;
            header += `${tab+tab}_knownDestinations.push(_destinationInstanceName);` + NL;
            header += `${tab}}` + NL;
            header += "}" + NL.repeat(2);
            header += "/** Returns the destination instance name that the API currently targets. */" + NL;
            header += "export function getDestinationInstance(): string" + NL;
            header += "{" + NL;
            header += `${tab}return (_destinationInstanceName);` + NL;
            header += "}" + NL.repeat(2);
            header += "/** Throws if _destinationInstanceName has not been set. */" + NL;
            header += "function checkDestinationSet(): void" + NL;
            header += "{" + NL;
            header += `${tab}if (!_destinationInstanceName)` + NL;
            header += `${tab}{` + NL;
            header += `${tab+tab}throw new Error("setDestinationInstance() must be called to specify the target destination before the '${fileOptions.apiName}' API can be used.");` + NL;
            header += `${tab}}` + NL;
            header += "}" + NL.repeat(2);

            header += "/**" + NL;
            header += " * Sets the post method timeout interval (in milliseconds), which is how long to wait for a post result from the destination instance before raising an error.\\" + NL;
            header += " * All post methods will use this timeout value. Specify -1 for no timeout. " + NL;
            header += " */" + NL;
            header += "export function setPostTimeoutInMs(timeoutInMs: number): void" + NL;
            header += "{" + NL;
            header += `${tab}_postTimeoutInMs = Math.max(-1, timeoutInMs);` + NL;
            header += "}" + NL.repeat(2);
            header += "/**" + NL;
            header += " * Returns the post method timeout interval (in milliseconds), which is how long to wait for a post result from the destination instance before raising an error.\\" + NL;
            header += " * A value of -1 means there is no timeout." + NL;
            header += " */" + NL;
            header += "export function getPostTimeoutInMs(): number" + NL;
            header += "{" + NL;
            header += `${tab}return (_postTimeoutInMs);` + NL;
            header += "}" + NL.repeat(2);

            content = header + content;
            writeGeneratedFile(content, pathedOutputFile, assertDefined(fileOptions.mergeType));
            Utils.log(`Code file generated: ${pathedOutputFile}${!fileOptions.checkGeneratedTS ? " (TypeScript checks skipped)" : ""}`);
            return (fileOptions.checkGeneratedTS ? checkGeneratedFile(pathedOutputFile, (fileOptions.mergeType !== FileMergeType.None)) : new SourceFileProblemCheckResult());
        }
        else
        {
            throw new Error(sourceFileName ? 
                `The input source file (${Path.basename(sourceFileName)}) does not publish any entities (exported functions, static methods, type aliases and enums annotated with an ${CODEGEN_TAG_NAME} JSDoc tag)` : 
                "No entities have been published; call publishType() / publishMethod() / publishPostMethod() then retry");
        }
    }
    catch (error: unknown)
    {
        Utils.log(`Error: emitConsumerTypeScriptFile() failed (reason: ${Utils.makeError(error).message})`);
        return (null);
    }
}

/** 
 * Writes the specified content to the specified pathedOutputFile, merging [using git] any existing changes in 
 * pathedOutputFile back into the newly generated file (according to mergeType). Throws if the merge fails. 
 * Returns the number of merge conflicts if 'mergeType' is FileMergeType.Annotate, or 0 otherwise.
 */
function writeGeneratedFile(content: string, pathedOutputFile: string, mergeType: FileMergeType): number
{
    let conflictCount: number = 0;
    let outputPath: string = Path.dirname(pathedOutputFile);

    if ((mergeType === FileMergeType.None) || !File.existsSync(pathedOutputFile))
    {
        File.writeFileSync(pathedOutputFile, content); // This will overwrite the file if it already exists
    }
    else
    {
        // See https://stackoverflow.com/questions/9122948/run-git-merge-algorithm-on-two-individual-files
        const pathedEmptyFile: string = Path.join(outputPath, "__empty.g.ts"); // This is a temporary file [but we don't want the name to collide with a real file]
        const pathedRenamedOutputFile: string = Path.join(outputPath, "__" + Path.basename(pathedOutputFile) + ".original"); // This is a temporary file [but we don't want the name to collide with a real file]

        try
        {
            // The output file already exists [possibly modified by the user], so merge the user's changes into new generated file 
            // using "git merge-file --union .\PublisherFramework.g.ts .\__empty.g.ts .\__PublisherFramework.g.ts.original". This will result in
            // PublisherFramework.g.ts containing BOTH the new [generated] changes and the existing [user] changes (from .\__PublisherFramework.g.ts.original).
            // Note: To just insert the merge markers that the developer will need to resolve manually, omit "--union" from "git merge-file"
            //       (ie. set mergeType to MergeType.Annotate).
            Utils.log(`${FileMergeType[mergeType]}-merging existing ${pathedOutputFile} into generated version...`);
            File.writeFileSync(pathedEmptyFile, ""); // The "common base" file
            File.renameSync(pathedOutputFile, pathedRenamedOutputFile); // Save the original version (which may contain user edits); Will overwrite pathedRenamedOutputFile if it already exists
            File.writeFileSync(pathedOutputFile, content); // This will overwrite the file

            // Note: In launch.json, set '"autoAttachChildProcesses": false' to prevent the VS Code debugger from attaching to this process
            // Note: See https://docs.npmjs.com/misc/config
            let mergeOutput: string = ChildProcess.execSync(`git merge-file ${(mergeType === FileMergeType.Auto) ? "--union " : ""}-L Generated -L Base -L Original ${pathedOutputFile} ${pathedEmptyFile} ${pathedRenamedOutputFile}`, { encoding: "utf8", windowsHide: true, stdio: ["ignore"] }).trim();
            if (mergeOutput)
            {
                throw (mergeOutput);
            }
        }
        catch (error: unknown)
        {
            // Note: 'error' will be an Error object with some additional properties (output, pid, signal, status, stderr, stdout)
            const err: Error = Utils.makeError(error); 

            // The 'git merge-file' exit code is negative on error, or the number of conflicts otherwise (truncated
            // to 127 if there are more than that many conflicts); if the merge was clean, the exit value is 0
            const gitExitCode: number = (err as any).status; // TODO: Hack to make compiler happy

            if (gitExitCode >= 0) // 0 = clean merge, >0 = conflict count (typically this will only happen for a non-automatic merge, ie. if "--union" is omitted from "git merge-file")
            {
                conflictCount = gitExitCode;
            }
            else
            {
                // An error occurred, so restore the original version
                File.renameSync(pathedRenamedOutputFile, pathedOutputFile);

                const errorMsg: string = err.message.replace(/\s+/g, " ").trim().replace(/\.$/, "");
                throw new Error(`Merge failed (reason: ${errorMsg} [exit code: ${gitExitCode}])`);
            }
        }
        finally
        {
            // Remove temporary files
            Utils.deleteFile(pathedEmptyFile);
            Utils.deleteFile(pathedRenamedOutputFile);
        }
        
        // Note: Resolving merges requires that the "Editor: Code Lens" setting is enabled in VSCode
        let userAction: string = (conflictCount === 0) ? "Please diff the changes to check merge correctness" : `Please manually resolve ${conflictCount} merge conflicts`;
        Utils.logWithColor(Utils.ConsoleForegroundColors.Yellow, `${FileMergeType[mergeType]}-merge succeeded - ${userAction}`);
    }
    return (conflictCount);
}

/** Checks the generated 'pathedOutputFile' for TypeScript errors. Returns a SourceFileProblemCheckResult. */
function checkGeneratedFile(pathedOutputFile: string, mergeConflictMarkersAllowed: boolean = false): SourceFileProblemCheckResult
{
    let result: SourceFileProblemCheckResult = AST.checkFileForTSProblems(pathedOutputFile, CodeGenerationFileType.Generated, mergeConflictMarkersAllowed);
    if ((result.errorCount > 0))
    {
        Utils.log(`Error: TypeScript [${TS.version}] check failed for generated file ${Path.basename(pathedOutputFile)}: ${result.errorCount} error(s) found`);
    }
    else
    {
        Utils.log(`Success: No TypeScript errors found in generated file ${Path.basename(pathedOutputFile)}`);
    }
    return (result);
}

/** Returns the fully pathed version of the supplied TypeScript template file name [which is shipped in the ambrosia-node package], or throws an the file cannot be found. */
function getPathedTemplateFile(templateFileName: string): string
{
    let pathedTemplateFile: string = "";
    let searchFolders: string[] = [process.cwd(), Path.join(process.cwd(), "node_modules/ambrosia-node")]; // This only works if ambrosia-node has been installed locally (not globally, which we handle below)
    
    for (const folder of searchFolders)
    {
        if (File.existsSync(Path.join(folder, templateFileName)))
        {
            pathedTemplateFile = Path.join(folder, templateFileName);
            break;
        }
    }

    if (pathedTemplateFile.length === 0)
    {
        // Last ditch (and costly) attempt, try to locate the global npm install location
        try
        {
            // Note: In launch.json, set '"autoAttachChildProcesses": false' to prevent the VS Code debugger from attaching to this process
            // Note: See https://docs.npmjs.com/misc/config
            let globalNpmInstallFolder: string = ChildProcess.execSync("npm config get prefix", { encoding: "utf8", windowsHide: true }).trim();
            // See https://docs.npmjs.com/files/folders
            globalNpmInstallFolder = Path.join(globalNpmInstallFolder, Utils.isWindows() ? "" : "lib", "node_modules/ambrosia-node"); 
            pathedTemplateFile = Path.join(globalNpmInstallFolder, templateFileName);

            if (!File.existsSync(pathedTemplateFile))
            {
                searchFolders.push(globalNpmInstallFolder); // So that we can report where we tried to look for the [shipped] template
                pathedTemplateFile = "";
            }
        }
        catch (error: unknown)
        {
            const errorMsg: string = Utils.makeError(error).message.replace(/\s+/g, " ").trim().replace(/\.$/, "");
            Utils.log(`Error: Unable to determine global npm install folder (reason: ${errorMsg})`);
        }

        if (pathedTemplateFile.length === 0)
        {
            throw new Error(`Unable to find template file ${templateFileName} in ${searchFolders.join(" or ")}`);
        }
    }

    return (pathedTemplateFile);
}

/** The [TypeScript] alias used to reference the input source file in the generated PublisherFramework.g.ts file. */
const SOURCE_MODULE_ALIAS: string = "PTM"; // PTM = "Published Types and Methods"

/** Generates TypeScript code for the specified template section [of PublisherFramework.template.ts]. May return an empty string if there is no code for the section. */
function codeGen(section: CodeGenSection, fileOptions: FileGenOptions, sourceFileName?: string): string
{
    const NL: string = Utils.NEW_LINE; // Just for short-hand
    const tab: string = " ".repeat(assertDefined(fileOptions.tabIndent));
    let lines: string[] = [];
    let moduleAlias: string = sourceFileName ? SOURCE_MODULE_ALIAS + "." : ""; 

    /** [Local function] Returns the TypeScript namespace (if any) of the supplied published type (if it exists), including the trailing ".". */
    function makeParamTypePrefix(publishedType?: Type): string 
    {
        if (publishedType)
        {
            if (publishedType.codeGenOptions?.nsPath)
            {
                return (moduleAlias + publishedType.codeGenOptions.nsPath + ".");
            }
            else
            {
                return (moduleAlias);
            }
        }
        else
        {
            // No prefix required for a non-published type (eg. "string")
            return ("");
        }
    }

    // Skip this section if requested
    if ((section & assertDefined(fileOptions.publisherSectionsToSkip)) === section)
    {
        return ("");
    }
    
    switch (section)
    {
        case CodeGenSection.Header:
            lines.push(...getHeaderCommentLines(GeneratedFileKind.Publisher, fileOptions));
            if (sourceFileName)
            {
                // Add an 'import' for the developer-provided source file that contains the implementations of published types and methods
                // Note: We don't just want to use an absolute path to the source file (even though that would be simpler for us) because 
                //       we want to retain any relative path so that it's easier for the user to move their code-base around. 

                // The reference to the source file must be relative to location of the generated file. For example, if the generated
                // file is in ./src and the input source file is ./test/Foo.ts, then the import file reference should be '../test/Foo'
                const relativeSourceFileName: string = Path.relative(assertDefined(fileOptions.outputPath), sourceFileName);
                
                let filePath: string = Path.dirname(relativeSourceFileName.replace(/\\/g, "/"));
                if ((filePath !== ".") && !filePath.startsWith("../") && !filePath.startsWith("./"))
                {
                    filePath = "./" + filePath;
                }
                let fileReference: string = filePath + "/" + Path.basename(relativeSourceFileName).replace(Path.extname(relativeSourceFileName), "");
                
                lines.push(`import * as ${SOURCE_MODULE_ALIAS} from "${fileReference}"; // ${SOURCE_MODULE_ALIAS} = "Published Types and Methods", but this file can also include app-state and app-event handlers`);
            }
            break;

        case CodeGenSection.AppState:
            if (sourceFileName)
            {
                if (AST.appStateVar() !== "")
                {
                    lines.push(`// ${CODEGEN_COMMENT_PREFIX}: '${CodeGenSection[section]}' section skipped (using provided state variable '${SOURCE_MODULE_ALIAS}.${AST.appStateVar()}' and class '${AST.appStateVarClassName()}' instead)`);
                    break;
                }
                else
                {
                    // Note: The _appState variable MUST be in an exported namespace (or module) so that it becomes a [mutable] property of an exported object [the namespace],
                    //       thus allowing it to be set [by checkpointConsumer()] at runtime (see https://stackoverflow.com/questions/53617972/exported-variables-are-read-only).
                    //       If it's exported from the root-level of the source file, the generated PublisherFramework.g.ts code will contain this error [in checkpointConsumer()]:
                    //         Cannot assign to '_myAppState' because it is a read-only property. (ts:2540)
                    lines.push(`// TODO: It's recommended that you move this namespace to your input file (${makeRelativePath(assertDefined(fileOptions.outputPath), sourceFileName)}) then re-run code-gen`);
                }
            }
            lines.push("export namespace State");
            lines.push("{");
            lines.push(tab + "export class AppState extends Ambrosia.AmbrosiaAppState" + NL + tab + "{");
            lines.push(tab.repeat(2) + "// TODO: Define your application state here" + NL);
            lines.push(tab.repeat(2) + "/**"); 
            lines.push(tab.repeat(2) + " * @param restoredAppState Supplied only when loading (restoring) a checkpoint, or (for a \"VNext\" AppState) when upgrading from the prior AppState.\\");
            lines.push(tab.repeat(2) + " * **WARNING:** When loading a checkpoint, restoredAppState will be an object literal, so you must use this to reinstantiate any members that are (or contain) class references.");
            lines.push(tab.repeat(2) + " */");
            lines.push(tab.repeat(2) + "constructor(restoredAppState?: AppState)");
            lines.push(tab.repeat(2) + "{");
            lines.push(tab.repeat(3) + "super(restoredAppState);" + NL);
            lines.push(tab.repeat(3) + "if (restoredAppState)");
            lines.push(tab.repeat(3) + "{");
            lines.push(tab.repeat(4) + "// TODO: Re-initialize your application state from restoredAppState here");
            lines.push(tab.repeat(4) + "// WARNING: You MUST reinstantiate all members that are (or contain) class references because restoredAppState is data-only");
            lines.push(tab.repeat(3) + "}");
            lines.push(tab.repeat(3) + "else");
            lines.push(tab.repeat(3) + "{");
            lines.push(tab.repeat(4) + "// TODO: Initialize your application state here");
            lines.push(tab.repeat(3) + "}");
            lines.push(tab.repeat(2) + "}" + NL + tab + "}" + NL);
            lines.push(tab + "/**");
            lines.push(tab + " * Only assign this using the return value of IC.start(), the return value of the upgrade() method of your AmbrosiaAppState");
            lines.push(tab + " * instance, and [if not using the generated checkpointConsumer()] in the 'onFinished' callback of an IncomingCheckpoint object.");
            lines.push(tab + " */");
            lines.push(tab + "export let _appState: AppState;");
            lines.push("}")
            break;
    
        case CodeGenSection.PostMethodHandlers:
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    let method: Method = _publishedMethods[name][version];
                    let variableNames: string[] = method.parameterNames.map(name => name.endsWith("?") ? name.slice(0, -1) : name);
                    let nsPathForMethod: string = method.codeGenOptions?.nsPath ? (method.codeGenOptions.nsPath + ".") : "";
                    
                    if (method.isPost)
                    {
                        let caseTab: string = tab;
                        lines.push(`case "${method.name}":`);
                        if (variableNames.length > 0) 
                        {
                            // To prevent variable name collisions in the switch statement (eg. if 2 methods use the same parameter name), if needed, we create a new block scope for each case statement
                            lines.push(`${tab}{`);
                            caseTab = tab + tab;
                        }
                        for (let i = 0; i < variableNames.length; i++)
                        {
                            let prefix: string = makeParamTypePrefix(_publishedTypes[Type.removeArraySuffix(method.parameterTypes[i])]); 
                            lines.push(`${caseTab}const ${Method.trimRest(variableNames[i])}: ${prefix}${method.parameterTypes[i]} = IC.getPostMethodArg(rpc, "${Method.trimRest(method.parameterNames[i])}");`);
                        }
                        let prefix: string = makeParamTypePrefix(_publishedTypes[Type.removeArraySuffix(method.returnType)]); 
                        lines.push(`${caseTab}IC.postResult<${prefix}${method.returnType}>(rpc, ${moduleAlias + nsPathForMethod}${method.name}(${variableNames.join(", ")}));`);
                        if (variableNames.length > 0)
                        {
                            lines.push(`${tab}}`);
                        }
                        lines.push(`${tab}break;${NL}`);
                    }
                }
            }
            break;

        case CodeGenSection.NonPostMethodHandlers:
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    let method: Method = _publishedMethods[name][version];
                    let variableNames: string[] = method.parameterNames.map(name => name.endsWith("?") ? name.slice(0, -1) : name);
                    let nsPathForMethod: string = method.codeGenOptions?.nsPath ? (method.codeGenOptions.nsPath + ".") : "";
                    
                    if (!method.isPost)
                    {
                        let caseTab: string = tab;
                        lines.push(`case ${method.id}:`);
                        if (variableNames.length > 0) 
                        {
                            // To prevent variable name collisions in the switch statement (eg. if 2 methods use the same parameter name), if needed, we create a new block scope for each case statement
                            lines.push(`${tab}{`);
                            caseTab = tab + tab;
                        }
                        for (let i = 0; i < variableNames.length; i++)
                        {
                            let prefix: string = makeParamTypePrefix(_publishedTypes[Type.removeArraySuffix(method.parameterTypes[i])]); 
                            if (method.takesRawParams)
                            {
                                lines.push(`${caseTab}const ${variableNames[i]}: ${prefix}${method.parameterTypes[i]} = rpc.getRawParams();`);
                            }
                            else
                            {
                                const isOptionalParam: boolean = method.parameterNames[i].endsWith("?");
                                const paramName: string = isOptionalParam ? method.parameterNames[i].slice(0, -1) : method.parameterNames[i];
                                lines.push(`${caseTab}const ${Method.trimRest(variableNames[i])}: ${prefix}${method.parameterTypes[i]} = rpc.getJsonParam("${Method.trimRest(paramName)}");${isOptionalParam ? " // Optional parameter" : ""}`);
                            }
                        }
                        lines.push(`${caseTab}${moduleAlias + nsPathForMethod}${method.name}(${variableNames.join(", ")});`);
                        if (variableNames.length > 0)
                        {
                            lines.push(`${tab}}`);
                        }
                        lines.push(`${tab}break;${NL}`);
                    }
                }
            }
            break;

        case CodeGenSection.PublishTypes:
            for (const typeName in _publishedTypes)
            {
                let type: Type = _publishedTypes[typeName];
                lines.push(`Meta.publishType("${type.name}", "${type.definition.replace(/"/g, "\\\"")}");`);
            }
            break;

        case CodeGenSection.PublishMethods:
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    let method: Method = _publishedMethods[name][version];
                    let paramList: string[] = [];
                    paramList.push(...method.parameterNames.map((name, index) => `"${name}: ${method.parameterTypes[index].replace(/"/g, "\\\"")}"`));
                    let methodParams: string = `[${paramList.join(", ")}]`;

                    if (method.isPost)
                    {
                        lines.push(`Meta.publishPostMethod("${method.name}", ${method.version}, ${methodParams}, "${method.returnType.replace(/"/g, "\\\"")}"${method.isTypeChecked ? "" : ", false"});`);
                    }
                    else
                    {
                        lines.push(`Meta.publishMethod(${method.id}, "${method.name}", ${methodParams});`);
                    }
                }
            }
            break;

        case CodeGenSection.MethodImplementations:
            if (sourceFileName)
            {
                // The methods (and supporting types) have already been implemented in the developer-provided source file, so we're done
                return ("");
            }
    
            // First, emit classes/type-aliases/enums for types that are used by the methods
            for (const typeName in _publishedTypes)
            {
                let type: Type = _publishedTypes[typeName];
                if (type.isReferenced())
                {
                    lines.push(NL + "// This class is for a published type referenced by one or more published methods");
                    lines.push("// CAUTION: Do NOT change the data 'shape' of this class directly; instead, change the Meta.publishType() call in your code-gen program and re-run it");
                    lines.push(type.makeTSType(0, fileOptions.tabIndent, "", false));
                }
            }
 
            // Emit method stubs [that the developer must implement]
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    let method: Method = _publishedMethods[name][version];
                    lines.push(`${NL}// CAUTION: Do NOT change the parameter list (or return type) of this method directly; instead, change the Meta.publish${method.isPost ? "Post" : ""}Method() call in your code-gen program and re-run it`);
                    lines.push(`function ${method.nameForTSWrapper}(${method.makeTSFunctionParameters()}): ${method.returnType}`);
                    lines.push(`{${NL}${tab}// TODO: Implement this method`);
                    lines.push(`${tab}throw new Error("The '${method.name}' method has not been implemented");`);
                    if (method.returnType !== "void")
                    {
                        lines.push(`${tab}return (undefined);`);
                    }
                    lines.push(`}`);
                }
            }
            break;

        // This case handles all CodeGenSection.xxxEventHandler sections
        case RegExp(/.*EventHandler$/g).test(CodeGenSection[section]) ? section : CodeGenSection.None:
            if (section !== CodeGenSection.None)
            {
                const fnName: string = "on" + CodeGenSection[section].replace("EventHandler", "");
                const fnDetails: AppEventHandlerFunctionDetails = _appEventHandlerFunctions[fnName];
                
                if (fnDetails.foundInInputSource || !sourceFileName)
                {
                    const prefix: string = moduleAlias + (fnDetails.nsPath ? fnDetails.nsPath + "." : "");
                    const argList: string[] = [];
                    const blockTab: string = fnDetails.expectedParameters ? tab : "";

                    if (fnDetails.expectedParameters)
                    {
                        // To prevent variable name collisions in the switch statement (eg. if 2 event handlers use the same parameter name), if needed, we create a new block scope for each case statement
                        lines.push("{")
                    }
                    if (fnDetails.expectedParameters)
                    {
                        const parameters: string[] = fnDetails.expectedParameters.split(",").map(p => p.replace(/\s/g, ""));

                        for (let i = 0; i < parameters.length; i++)
                        {
                            const paramName: string = parameters[i].split(":")[0];
                            const paramType: string = parameters[i].split(":")[1];
                            lines.push(`${blockTab}const ${paramName}: ${paramType} = appEvent.args[${i}];`);
                            argList.push(paramName);
                        }
                    }
                    if (fnDetails.foundInInputSource)
                    {
                        lines.push(`${blockTab}${prefix}${fnName}(${argList.join(", ")});`);
                    }
                    if (!sourceFileName)
                    {
                        lines.push(`${blockTab}// TODO: Add your [non-async] handler here`);
                    }
                    if (fnDetails.expectedParameters)
                    {
                        lines.push("}")
                    }
                }
            }
            break;

        default:
            throw new Error(`The '${CodeGenSection[section]}' CodeGenSection is not currently supported`);
    }

    return ((lines.length > 0) ? lines.join(NL) : "");
}

/** Replaces the token in 'template' for the specified CodeGenSection with code (or with 'defaultReplacementText' if no code is generated) and returns the result. */
function replaceTemplateToken(template: string, section: CodeGenSection, fileOptions: FileGenOptions, defaultReplacementText: string = "", sourceFileName?: string): string
{
    let tokenName: string = CodeGenSection[section];
    let replacementText: string = codeGen(section, fileOptions, sourceFileName);
    let updatedTemplate: string = template;
    let tokenStartIndex: number = template.indexOf(`${CODEGEN_TEMPLATE_TOKEN_PREFIX}Name=${tokenName}`);
    let tokenEndIndex: number = template.indexOf("]", tokenStartIndex);
    
    /** [Local function] Returns the value of the specified attribute (eg. "StartingIndent") from the specified replaceable token (eg. "[TOKEN:Name=Header,StartingIndent=0]"). */
    function extractTokenAttributeValue(token: string, attrName: string): string
    {
        let attrValue: string = token.replace(CODEGEN_TEMPLATE_TOKEN_PREFIX, "").replace("]", "").split(",").filter(kvp => (kvp.split("=")[0] === attrName))[0].split("=")[1];
        return (attrValue);
    }

    if ((section & assertDefined(fileOptions.publisherSectionsToSkip)) === section)
    {
        defaultReplacementText = `// ${CODEGEN_COMMENT_PREFIX}: '${CodeGenSection[section]}' section skipped by request`;
    }

    if ((tokenStartIndex !== -1) && (tokenEndIndex !== -1))
    {
        let token: string = template.substring(tokenStartIndex, tokenEndIndex + 1);
        let startingIndent: number = Number.parseInt(extractTokenAttributeValue(token, "StartingIndent"));
        let indent: string = " ".repeat(isNaN(startingIndent) ? 0 : startingIndent);
        let newText: string = replacementText || defaultReplacementText;

        let newLines: string[] = newText.split(Utils.NEW_LINE).map((line, index) => ((index === 0) ? "" : indent) + line);
        updatedTemplate = template.replace(token, newLines.join(Utils.NEW_LINE));
    }
    return (updatedTemplate);
}

/** Returns an array of lines containing the comments that should appear at the start of a generated code file. */
function getHeaderCommentLines(fileKind: GeneratedFileKind, fileOptions: FileGenOptions): string[]
{
    let headerLines: string[] = [];
    let localInstanceName: string = Configuration.loadedConfig().instanceName;
    let fileDescription: string = (fileKind === GeneratedFileKind.Consumer) ? "consumer-side API" : (fileKind === GeneratedFileKind.Publisher ? "publisher-side framework" : "(unknown description)")

    headerLines.push(`// Generated ${fileDescription} for the '${fileOptions.apiName}' Ambrosia Node app/service.`);
    if (fileKind === GeneratedFileKind.Consumer)
    {
        headerLines.push(`// Publisher: ${fileOptions.publisherContactInfo || "(Not specified)"}.`);
    }
    headerLines.push("// Note: This file was generated" + (fileOptions.emitGeneratedTime ? ` on ${Utils.getTime().replace(" ", " at ")}.` : ""));
    headerLines.push(`// Note [to publisher]: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().`);
    return (headerLines);
}

/** Removes lines that contain "[DEV-ONLY COMMENT]" from the supplied template. */
function removeDevOnlyTemplateCommentLines(template: string): string
{
    let lines: string[] = template.split(Utils.NEW_LINE);
    return (lines.filter(line => line.indexOf("[DEV-ONLY COMMENT]") === -1).join(Utils.NEW_LINE));
}

/** Throws if the specified [generated] output file contains a git merge conflict marker. */
function checkForGitMergeMarkers(pathedFile: string): void
{
    if (File.existsSync(pathedFile))
    {
        let content: string = File.readFileSync(pathedFile, { encoding: "utf8" });
        let startMarkerIndex: number = content.indexOf(Utils.NEW_LINE + "<<<<<<< ");
        let splitMarkerIndex: number = content.indexOf(Utils.NEW_LINE + "=======");
        let endMarkerIndex: number = content.indexOf(Utils.NEW_LINE + ">>>>>>> ");

        if ((startMarkerIndex !== -1) && (splitMarkerIndex !== -1) && (endMarkerIndex !== -1) && (startMarkerIndex < splitMarkerIndex) && (splitMarkerIndex < endMarkerIndex))
        {
            throw new Error(`${pathedFile} contains a git merge conflict marker: resolve the conflict(s) then retry`)
        }
    }
}

/** Throws if there is a name or location collision between 'pathedOutputFile' and 'sourceFileName'. */
function checkForFileNameConflicts(pathedOutputFile: string, sourceFileName?: string): void
{
    if (sourceFileName)
    {
        if (Path.resolve(sourceFileName) === pathedOutputFile)
        {
            throw new Error(`The input source file (${Path.resolve(sourceFileName)}) cannot be the same as the generated file`);
        }
        if (Path.basename(sourceFileName) === Path.basename(pathedOutputFile))
        {
            throw new Error(`To avoid confusion, the input source file (${Path.basename(sourceFileName)}) should not have the same name as the generated file`);
        }
    }
}

/** 
 * [Internal] This method is for **internal testing only**.\
 * Use emitTypeScriptFileFromSource() instead. 
 */
export function publishFromSource(tsFileName: string, fileOptions?: FileGenOptions) : void
{
    if (!fileOptions)
    {
        const apiName: string = Path.basename(tsFileName).replace(Path.extname(tsFileName), "");
        fileOptions = new FileGenOptions({ apiName: apiName});
    }
    AST.publishFromAST(tsFileName, fileOptions);
}

/** Type for Ambrosia code-gen attributes parsed from an @ambrosia JSDoc tag. */
type AmbrosiaAttrs = { [attrName: string]: boolean | number | string };

/** [Internal] Type for options (eg. details about the entity) used during code generation. */
type CodeGenOptions = 
{ 
    /** The namespace path (in the input source file) where the entity was found, eg. "Foo.Bar.Baz". [Applies to functions, type aliases, and enums].*/
    nsPath: string,
    /** If available, the text of the JSDoc comment for nsPath. */
    nsComment?: string,
    /** If available, the JSDoc comment that describes the entity. [Applies to functions, type aliases, and enums]. */
    jsDocComment?: string
};

/** Type of the result from checkFileForTSProblems(). */
class SourceFileProblemCheckResult
{ 
    errorCount: number = 0;
    warningCount: number = 0;
    mergeConflictCount: number = 0;

    constuctor()
    {
    }
 }

// See https://github.com/Microsoft/TypeScript/wiki/Using-the-Compiler-API
// Note: A great tool to help with debugging is https://ts-ast-viewer.com/#
/** Class of static methods used to walk the abstract syntax tree (AST) of a TypeScript file in order to publish types and methods from it. */
class AST
{
    private static _fileGenOptions: FileGenOptions;
    private static _typeChecker: TS.TypeChecker;
    private static _sourceFile: TS.SourceFile;
    private static _publishedEntityCount: number = 0; // A running count of the entities (functions, type aliases, enums) published during the AST walk
    private static _ignoredErrorCount: number = 0; // A running count of ignored errors (ie. errors encountered while FileGenOptions.haltOnError is false)
    private static _compilerOptions: TS.CompilerOptions = { module: TS.ModuleKind.CommonJS, target: TS.ScriptTarget.ES2018, strict: true }; // Controls the "flavor" of TS to check/emit, and the strictness of the compiler checks
    private static _targetNodeKinds: TS.SyntaxKind[] = [TS.SyntaxKind.FunctionDeclaration, TS.SyntaxKind.MethodDeclaration, TS.SyntaxKind.TypeAliasDeclaration, TS.SyntaxKind.EnumDeclaration]; // The kinds of AST nodes we can publish from
    // The set of supported @ambrosia tag "attributes" for each target node kind
    private static _supportedAttrs: { [nodeKind: number]: string[] } = 
    {
        [TS.SyntaxKind.FunctionDeclaration]: ["publish", "version", "methodID", "doRuntimeTypeChecking"],
        [TS.SyntaxKind.MethodDeclaration]: ["publish", "version", "methodID", "doRuntimeTypeChecking"], // Note: Static methods only
        [TS.SyntaxKind.TypeAliasDeclaration]: ["publish"],
        [TS.SyntaxKind.EnumDeclaration]: ["publish"]
    };
    private static _knownAttrs: string[] = Object.values(AST._supportedAttrs).reduce((acc, values) => { acc.push(...values.filter(v => acc.indexOf(v) === -1)); return (acc); }); 
    private static _namespaces: string[] = []; // A "stack" of strings in the format "namespaceName:namespaceEndPosition" (eg. "Test:740")
    private static _currentNamespaceEndPos: number = 0; // The end-offset of the current namespace (0 before the first namespace, or the EOF position after leaving the last namespace)
    private static _functionEndPos: number = 0; // The end-offset of the current function (or 0 if the AST walk is not currently in a function)
    private static _publishedSourceFile: string = ""; // The TypeScript source file that was used to publish from (only set once publishing has succeeded)
    private static _appStateVar: string = ""; // The name [with namespace path] of the exported variable (if found) in the input source file that extends AmbrosiaAppState
    private static _appStateVarClassName: string = ""; // The name [with namespace path] of the class of _appStateVar
    private static _namespaceJSDocComments: { [pathedNamespace: string]: string } = {}; // Value is the JSDocComment text

    // Private because AST is a static class
    private constructor()
    {
    }

    /** The name [with namespace path] of the exported variable (if found) in the input source file that extends AmbrosiaAppState. */
    public static appStateVar(): string
    {
        return (AST._appStateVar);
    }

    /** The name [with namespace path] of the class of the exported variable (if found) in the input source file that extends AmbrosiaAppState. */
    public static appStateVarClassName(): string
    {
        return (AST._appStateVarClassName);
    }

    /** The TypeScript source file that was used to publish from (only set once publishing has succeeded). */
    public static publishedSourceFile(): string
    {
        return (AST._publishedSourceFile);
    }

    /** 
     * [Internal] Clears the last published source file (to enable iterative publishing of source files).
     * This method is for **internal testing only**.
     */
    public static clearPublishedSourceFile(): void
    {
        AST._publishedSourceFile = "";
    }

    /** The final count of entities (functions, type aliases, enums) published in publishedSourceFile(). */
    public static publishedEntityCount(): number
    {
        return (AST._publishedSourceFile ? AST._publishedEntityCount : 0);
    }

    /** 
     * Returns the current namespace path (eg. "Root.Outer.Inner") at the current point in time of the AST walk. May return an empty string.\
     * DEPRECATED: Use AST.getNamespacePath() instead.
     */
    public static getCurrentNamespacePath(): string
    { 
        return (AST._namespaces.map(pair => pair.split(":")[0]).join(".")); 
    } 

    /** Returns the namespace path (eg. "Root.Outer.Inner") of the supplied node. May return an empty string. */
    public static getNamespacePath(node: TS.Node): string
    {
        // TODO: The cast to 'any' here is a workaround for a "bug" in getSymbolAtLocation() [see https://github.com/Microsoft/TypeScript/issues/5218].
        //       Despite getSymbolAtLocation() being observed to almost always return 'undefined' [F11 into it to see why], we still call it in the 
        //       hope that one day it will be fixed to work as "expected".
        const symbol: TS.Symbol = AST._typeChecker.getSymbolAtLocation(node) || (node as any).symbol;

        if (!symbol)
        {
            throw new Error(`Unable to determine symbol for node '${node.getText()}' at ${AST.getLocation(node.getStart())}`);
        }
        return (AST.getNamespacePathOfSymbol(symbol));
    }

    /** Returns the namespace path (eg. "Root.Outer.Inner") of the supplied symbol. May return an empty string. */
    public static getNamespacePathOfSymbol(symbol: TS.Symbol): string
    {
        // Note: TypeChecker.getFullyQualifiedName() returns a string in one of two forms: 
        //       Either "test/PI".State._myAppState (if exported) or State._myAppState (if not exported) [see https://github.com/dsherret/ts-morph/issues/132].
        //       BUGBUG: If a nested namespace is not exported (eg. if Baz is not exported in Foo.Bar.Baz) then TypeChecker.getFullyQualifiedName() will 
        //               return 'Baz' for the Baz namespace node instead of 'Foo.Bar' as expected. In practice, this is never a problem since the generated
        //               PublisherFramework.g.ts will contain errors for each reference to the non-exported type/method, which the developer would fix by
        //               adding the missing 'export' keyword to the namespace. Only in the case of an unreferenced published type would the containing
        //               namespace be silently moved to the root in ConsumerInterface.g.ts; but since the type is unreferenced, this is harmless.
        let nsPath: string = AST._typeChecker.getFullyQualifiedName(symbol); 

        // Remove the leading file-path portion (if any) of the path [Note: this may contain "."]
        if (nsPath.startsWith("\""))
        {
            nsPath = nsPath.replace(/^"[^"]*"\./, "");
        }

        // Remove the trailing member name
        nsPath = nsPath.split(".").slice(0, -1).join(".");
        return (nsPath);
    }

    /** 
     * Reads the supplied TypeScript file and dynamically executes publishType/publishPostMethod/publishMethod calls for the annotated functions, static methods, type-aliases and enums. 
     * Returns a count of the number of entities published.
     */
    static publishFromAST(tsFileName: string, fileOptions: FileGenOptions): number
    {
        let pathedFileName: string = Path.resolve(tsFileName);

        // Check that the input TypeScript file exists
        if (!File.existsSync(tsFileName))
        {
            throw new Error(`The TypeScript file specified (${pathedFileName}) does not exist`);
        }

        Utils.log(`Publishing types and methods from ${pathedFileName}...`);

        AST._compilerOptions.strict = fileOptions.strictCompilerChecks; // We support this omnibus flag from the "strictness" family because it's the most comprehensive (and for simplicity in our code)
        AST._compilerOptions.noImplicitReturns = fileOptions.strictCompilerChecks; // We also include this flag in our [single] 'strictCompilerChecks' option
        // Note: Setting 'noImplicitReturns' to true will enable the [additional] error "Not all code paths return a value (ts:7030)", but when it's false [the default] it will
        //       NOT suppress "Function lacks ending return statement and return type does not include 'undefined' (ts:2366)" which is reported when 'strictNullCheck' is enabled.
        let program: TS.Program = TS.createProgram([tsFileName], AST._compilerOptions); // Note: Setting 'removeComments' to true does NOT remove JSDocComment nodes
        AST._fileGenOptions = fileOptions;
        AST._sourceFile = assertDefined(program.getSourceFile(tsFileName));
        AST._typeChecker = program.getTypeChecker();
        AST._publishedEntityCount = 0;
        AST._ignoredErrorCount = 0;
        AST._namespaces = [];
        AST._currentNamespaceEndPos = 0;
        AST._functionEndPos = 0;
        AST._appStateVar = "";
        AST._appStateVarClassName = "";
        AST._namespaceJSDocComments = {};

        // Reset the "discovered" attributes of _appEventHandlerFunctions
        Object.keys(_appEventHandlerFunctions).forEach(fnName => _appEventHandlerFunctions[fnName].reset());

        // Check that the [input] source file "compiles"
        let result: SourceFileProblemCheckResult = AST.checkFileForTSProblems(tsFileName, CodeGenerationFileType.Input);
        if (result.errorCount > 0)
        {
            if (fileOptions.ignoreTSErrorsInSourceFile)
            {
                Utils.log(`Ignoring ${result.errorCount} TypeScript error(s) found compiling input file ${Path.basename(tsFileName)}`);
            }
            else
            {
                throw new Error(`TypeScript [${TS.version}] check failed for input file ${tsFileName}: ${result.errorCount} error(s) found`);
            }
        }

        // Note: A great tool to help with debugging is https://ts-ast-viewer.com/#
        AST.walkAST(AST._sourceFile, fileOptions.haltOnError);

        Utils.log(`Publishing finished: ${AST._publishedEntityCount} entities published${!fileOptions.haltOnError && (AST._ignoredErrorCount > 0) ? `, ${AST._ignoredErrorCount} errors found` : ""}`);
        AST._publishedSourceFile = tsFileName;
        return (AST._publishedEntityCount);
    }

    /** Reports (logs) any TypeScript compiler errors and warnings present in the specified .ts file. Returns the counts of errors/warnings found. */
    static checkFileForTSProblems(tsFileName: string, fileType: CodeGenerationFileType, mergeConflictMarkersAllowed: boolean = false): SourceFileProblemCheckResult
    {
        let program: TS.Program = TS.createProgram([tsFileName], AST._compilerOptions);
        let diagnostics: readonly TS.Diagnostic[] = TS.getPreEmitDiagnostics(program);
        let result: SourceFileProblemCheckResult = new SourceFileProblemCheckResult();
        let mergeConflictMarkerCount: number = 0;
        let displayFileType: string = CodeGenerationFileType[fileType].toLowerCase();

        /** [Local function] Type guard: returns true if 'obj' is a TS.DiagnosticMessageChain. */
        function isDiagnosticMessageChain(obj: any): obj is TS.DiagnosticMessageChain
        {
            return (obj.hasOwnProperty("messageText") && obj.hasOwnProperty("category") && obj.hasOwnProperty("code"));
        }

        if (diagnostics.length > 0)
        {
            diagnostics.forEach(diagnostic => 
            {
                // If we did a merge then we don't report the errors that will [knowingly] arise from the resulting merge conflict markers; 
                // without this, the error ouput would be too "noisy" making it harder to see any "real" errors in the generated code
                const canIgnoreError: boolean = ((fileType === CodeGenerationFileType.Generated) && (diagnostic.code === 1185) && mergeConflictMarkersAllowed); // 1185 = "Merge conflict marker encountered"
                let diagnosticMsg: string = "Unknown failure";

                if (diagnostic.code === 1185)
                {
                    mergeConflictMarkerCount++;
                }

                if (typeof diagnostic.messageText === "string") 
                { 
                    diagnosticMsg = diagnostic.messageText; 
                }
                else
                {
                    if (isDiagnosticMessageChain(diagnostic.messageText)) 
                    { 
                        diagnosticMsg = diagnostic.messageText.messageText;
                        // if (diagnostic.messageText.next)
                        // {
                        //     diagnosticMsg += "; " + diagnostic.messageText.next.map(d => Utils.trimTrailingChar(d.messageText, ".")).join("; ");
                        // }
                    }
                }
                diagnosticMsg = Utils.trimTrailingChar(diagnosticMsg, ".");
                
                // Note: If tsFileName imports another file which has a problem, then diagnostic.file will refer to the imported file instead of tsFileName
                if ((diagnostic.category === TS.DiagnosticCategory.Error) && !canIgnoreError)
                {
                    let message: string = `Error: TypeScript error compiling ${displayFileType} file: ${diagnosticMsg} (ts:${diagnostic.code})` + 
                                           (diagnostic.start ? ` at ${AST.getLocation(assertDefined(diagnostic.start), diagnostic.file)}` : "");
                    Utils.log(message);
                    result.errorCount++;
                }
                if (diagnostic.category === TS.DiagnosticCategory.Warning)
                {
                    let message: string = `Warning: TypeScript warning compiling ${displayFileType} file: ${diagnosticMsg} (ts:${diagnostic.code})` + 
                                           (diagnostic.start ? ` at ${AST.getLocation(assertDefined(diagnostic.start), diagnostic.file)}` : "");
                    Utils.log(message);
                    result.warningCount++;
                }
            });
        }

        if (mergeConflictMarkerCount > 0)
        {
            result.mergeConflictCount = mergeConflictMarkerCount / 3;
        }
        return (result);
    }

    // Walks ALL nodes (including those for keywords, punctuation, and - critically - JSDoc)
    // Note: A great tool to help with debugging is https://ts-ast-viewer.com/# (just copy/paste your input .ts file into the viewer to explore the AST)
    // Note: The 'haltOnError' parameter is for testing purposes only. Setting it to false is unsafe.
    private static walkAST(nodeToWalk: TS.Node, haltOnError: boolean = true): void
    {
        let nodes: TS.Node[] = nodeToWalk.getChildren();
        
        nodes.forEach(node =>
        {
            try
            {
                const isStatic: boolean = (node.modifiers !== undefined) && (node.modifiers.filter(m => m.kind === TS.SyntaxKind.StaticKeyword).length === 1);
                const isPrivate: boolean = (node.modifiers !== undefined) && (node.modifiers.filter(m => m.kind === TS.SyntaxKind.PrivateKeyword).length === 1);
                let isExported: boolean = (node.modifiers !== undefined) && (node.modifiers.filter(m => m.kind === TS.SyntaxKind.ExportKeyword).length === 1);
                const isStaticMethod: boolean = isStatic && (node.kind === TS.SyntaxKind.MethodDeclaration);
                const isNestedFunction: boolean = (node.kind === TS.SyntaxKind.FunctionDeclaration) && (AST._functionEndPos > 0);
                let isWellKnownFunction: boolean = false;

                // Check for an @ambrosia tag on unsupported nodes
                if (AST._targetNodeKinds.indexOf(node.kind) === -1)
                {
                    const attrs: AmbrosiaAttrs = AST.getAmbrosiaAttrs(node);
                    if (attrs["hasAmbrosiaTag"] === true)
                    {
                        const targetNames: string = (this._targetNodeKinds
                            .slice(0, this._targetNodeKinds.length - 1)
                            .map(kind => AST.getNodeKindName(kind))
                            .join(", ") + ", and " + AST.getNodeKindName(this._targetNodeKinds[this._targetNodeKinds.length - 1]))
                            .replace("method", "static method");
                        throw new Error(`The ${CODEGEN_TAG_NAME} tag is not valid on a ${AST.getNodeKindName(node.kind)} (at ${attrs["location"]}); valid targets are: ${targetNames}`);
                    }
                }
                // Checks for an @ambrosia tag on a method
                if (node.kind === TS.SyntaxKind.MethodDeclaration)
                {
                    const attrs: AmbrosiaAttrs = AST.getAmbrosiaAttrs(node);
                    if (attrs["hasAmbrosiaTag"] === true)
                    {
                        // Check for an @ambrosia tag on a non-static method
                        if (!isStatic)
                        {
                            throw new Error(`The ${CODEGEN_TAG_NAME} tag is not valid on a non-static method (at ${attrs["location"]})`);
                        }
                        // A static method has to directly belong to an exported class: it cannot belong to a class expression [because there is no way to reference the method]
                        if (TS.isClassExpression(node.parent))
                        {
                            throw new Error(`The ${CODEGEN_TAG_NAME} tag is not valid on a static method of a class expression (at ${attrs["location"]})`);
                        }
                        // Check for an @ambrosia tag on a private static method
                        if (isPrivate && isStatic) 
                        {
                            throw new Error(`The ${CODEGEN_TAG_NAME} tag is not valid on a private static method (at ${attrs["location"]})`);
                        }
                    }
                }
                // Check for an @ambrosia tag on a local function
                if (isNestedFunction)
                {
                    const attrs: AmbrosiaAttrs = AST.getAmbrosiaAttrs(node);
                    if (attrs["hasAmbrosiaTag"] === true)
                    {
                        throw new Error(`The ${CODEGEN_TAG_NAME} tag is not valid on a local function (at ${attrs["location"]})`);
                    }
                }

                // Look for the first exported variable whose type is a class that extends AmbrosiaAppState
                // TODO: This is brittle, since the target variable may not be the first matching variable we find (it may be declared later in the input file).
                if ((AST._appStateVar === "") && (node.kind === TS.SyntaxKind.VariableDeclaration))
                {
                    const result: { varName: string, varType: TS.Type } | null = AST.getVarOfBaseType(node as TS.VariableDeclaration, "AmbrosiaAppState", "AmbrosiaRoot.ts");
                    if (result)
                    {
                        // Note: The app state variable may no longer be in the same namespace as the AppState class (as they were in the generated code). So we must explicitly
                        //       determine the namespace path for the type (class) of the app state variable rather than assuming it's the same as AST.getNamespacePath(node).
                        const varClassSymbol: TS.Symbol = result.varType.symbol;
                        AST._appStateVar = AST.getNamespacePath(node) ? (AST.getNamespacePath(node) + "." + result.varName) : result.varName;
                        AST._appStateVarClassName = AST.getNamespacePathOfSymbol(varClassSymbol) ? (AST.getNamespacePathOfSymbol(varClassSymbol) + "." + varClassSymbol.name) : varClassSymbol.name;
                        // Utils.log(`DEBUG: Exported variable ${AST._appStateVar} (of type ${AST._appStateVarClassName}) extends AmbrosiaAppState`);
                    }
                }

                // Keep track of JSDoc comments for namespaces/classes [although we won't need all of these since not all namespaces/classes contain published entities]
                if ((node.kind === TS.SyntaxKind.ModuleDeclaration) || (node.kind === TS.SyntaxKind.ClassDeclaration))
                {
                    const jsDocComment: TS.JSDoc | null = AST.getJSDocComment(node);
                    if (jsDocComment)
                    {
                        const namespaceName: string = node.getChildren().filter(c => TS.isIdentifier(c))[0].getText();
                        const namespacePath: string = AST.getNamespacePath(node);
                        const pathedNamespace: string = namespacePath ? `${namespacePath}.${namespaceName}` : namespaceName;
                        AST._namespaceJSDocComments[pathedNamespace] = jsDocComment.getText();
                    }
                }

                // Keep track of entering/leaving namespaces and classes [so that we can track the "path" to published entities]
                if ((node.kind === TS.SyntaxKind.ModuleDeclaration) || (node.kind === TS.SyntaxKind.ClassDeclaration))
                {
                    const moduleOrClassDecl: TS.ModuleDeclaration = (node as TS.ModuleDeclaration);
                    const namespaceName: string = moduleOrClassDecl.name.getText();
                    const namespaceEndPos: number = moduleOrClassDecl.getStart() + moduleOrClassDecl.getWidth() - 1;
                    AST._namespaces.push(`${namespaceName}:${namespaceEndPos}`);
                    AST._currentNamespaceEndPos = namespaceEndPos;
                    Utils.log(`Entering namespace '${namespaceName}' (now in '${AST.getCurrentNamespacePath()}') at ${AST.getLocation(node.getStart())}`, null, Utils.LoggingLevel.Debug);
                }
                if ((node.getStart() >= AST._currentNamespaceEndPos) && (AST._namespaces.length > 0))
                {
                    const leavingNamespaceName: string = assertDefined(AST._namespaces.pop()).split(":")[0];
                    const enteringNamespaceEndPos = (AST._namespaces.length > 0) ? parseInt(AST._namespaces[AST._namespaces.length - 1].split(":")[1]) : AST._sourceFile.getWidth();
                    AST._currentNamespaceEndPos = enteringNamespaceEndPos;
                    Utils.log(`Leaving namespace '${leavingNamespaceName}' (now in '${AST.getCurrentNamespacePath() || "[Root]"}') at ${AST.getLocation(node.getStart())}`, null, Utils.LoggingLevel.Debug);
                }

                // Keep track of entering/leaving a function (or static method) so that we can we can detect nested (local) functions (which are never candidates to be published)
                if (((node.kind === TS.SyntaxKind.FunctionDeclaration) || isStaticMethod) && (AST._functionEndPos === 0))
                {
                    const functionDecl: TS.FunctionDeclaration = (node as TS.FunctionDeclaration);
                    AST._functionEndPos = functionDecl.getStart() + functionDecl.getWidth() - 1;
                    Utils.log(`Entering ${isStaticMethod ? "static method" : "function"} '${functionDecl.name?.getText() || "N/A"}' at ${AST.getLocation(node.getStart())}`, null, Utils.LoggingLevel.Debug);
                }
                if ((AST._functionEndPos > 0) && (node.getStart() >= AST._functionEndPos))
                {
                    Utils.log(`Leaving function (or static method) at ${AST.getLocation(node.getStart())}`, null, Utils.LoggingLevel.Debug);
                    AST._functionEndPos = 0;
                }

                // Keep track of whether we have found any of the "well known" Ambrosia AppEvent handlers
                if (node.kind === TS.SyntaxKind.FunctionDeclaration)
                {
                    const functionDecl: TS.FunctionDeclaration = node as TS.FunctionDeclaration;
                    const isAsync: boolean = node.modifiers ? (node.modifiers.filter(m => m.kind === TS.SyntaxKind.AsyncKeyword).length === 1) : false;
                    const fnName: string = functionDecl.name?.getText() || "N/A";
                    const location: string = AST.getLocation(node.getStart());

                    if (isExported && (Object.keys(_appEventHandlerFunctions).indexOf(fnName) !== -1))
                    {
                        const fnDetails: AppEventHandlerFunctionDetails = _appEventHandlerFunctions[fnName];
                        const ambrosiaAttrs: AmbrosiaAttrs = AST.getAmbrosiaAttrs(functionDecl, AST._supportedAttrs[functionDecl.kind]);

                        if (ambrosiaAttrs["hasAmbrosiaTag"])
                        {
                            throw new Error(`The ${CODEGEN_TAG_NAME} tag is not valid on an AppEvent handler ('${fnName}') at ${ambrosiaAttrs["location"]}`);
                        }

                        if (isAsync)
                        {
                            throw new Error(`The AppEvent handler '${fnName}' (at ${location}) cannot be async`);
                        }

                        if (fnDetails.foundInInputSource)
                        {
                            throw new Error(`The AppEvent handler '${fnName}' (at ${location}) has already been defined (at ${fnDetails.location})`);
                        }
                        else
                        {
                            const parameters: string = functionDecl.parameters.map(p => p.getText()).join(", ");
                            const expectedParameters: string = fnDetails.expectedParameters;
                            const returnType: string = functionDecl.type?.getText() || "void";
                            const expectedReturnType: string = fnDetails.expectedReturnType;

                            if (parameters.replace(/\s*/, "") !== expectedParameters.replace(/\s*/, ""))
                            {
                                Utils.log(`Warning: Skipping Ambrosia AppEvent handler function '${fnName}' (at ${location}) because it has different parameters (${parameters.replace(/\s/g, "").replace(":", ": ")}) than expected (${expectedParameters})`);
                            }
                            else
                            {
                                if (returnType.replace(/\s*/, "") !== expectedReturnType.replace(/\s*/, ""))
                                {
                                    Utils.log(`Warning: Skipping Ambrosia AppEvent handler function '${fnName}' (at ${location}) because it has a different return type (${returnType}) than expected (${expectedReturnType})`);
                                }
                                else
                                {
                                    fnDetails.foundInInputSource = true;
                                    fnDetails.nsPath = AST.getNamespacePath(functionDecl);
                                    fnDetails.location = location;
                                }
                            }
                            isWellKnownFunction = true;
                        }
                    }
                }
                
                // Publish functions/types/enums and static methods marked with an @ambrosia JSDoc tag, and which are exported
                if (!isWellKnownFunction && (AST._targetNodeKinds.indexOf(node.kind) >= 0))
                {
                    let location: string = AST.getLocation(node.getStart());
                    let entityName: string = (node as TS.DeclarationStatement).name?.getText() || "N/A";
                    let nodeName: string = `${isStatic ? "static " : ""}${AST.getNodeKindName(node.kind)} '${entityName}'`;
                    let skipSilently: boolean = ((node.kind === TS.SyntaxKind.MethodDeclaration) && !isStatic) || isNestedFunction || isPrivate;

                    if (!skipSilently)
                    {
                        // Static methods are not explicitly exported, rather they have to [directly] belong to an exported class
                        if (!isExported && (node.kind === TS.SyntaxKind.MethodDeclaration))
                        {
                            if (TS.isClassDeclaration(node.parent) && node.parent.modifiers && (node.parent.modifiers.filter(m => m.kind === TS.SyntaxKind.ExportKeyword).length === 1))
                            {
                                isExported = true;
                            }
                        }

                        if (AST._supportedAttrs[node.kind] === undefined)
                        {
                            throw new Error(`Internal error: No _supportedAttrs defined for a ${TS.SyntaxKind[node.kind]}`);
                        }
                        let ambrosiaAttrs: AmbrosiaAttrs = AST.getAmbrosiaAttrs(node, AST._supportedAttrs[node.kind]);
                        let hasAmbrosiaTag: boolean = ambrosiaAttrs["hasAmbrosiaTag"] as boolean;
                        let isPublished: boolean = ambrosiaAttrs["publish"] as boolean;

                        if (hasAmbrosiaTag)
                        {
                            if (isPublished)
                            {
                                if (isExported)
                                {
                                    let publishedEntity: string = "";
                                    switch (node.kind)
                                    {
                                        case TS.SyntaxKind.FunctionDeclaration:
                                            publishedEntity = AST.publishFunction(node as TS.FunctionDeclaration, nodeName, location, ambrosiaAttrs);
                                            break;
                                        case TS.SyntaxKind.MethodDeclaration: // Note: Static methods only
                                            publishedEntity = AST.publishFunction(node as TS.MethodDeclaration, nodeName, location, ambrosiaAttrs);
                                            break;
                                        case TS.SyntaxKind.TypeAliasDeclaration:
                                            publishedEntity = AST.publishTypeAlias(node as TS.TypeAliasDeclaration, nodeName, location, ambrosiaAttrs);
                                            break;
                                        case TS.SyntaxKind.EnumDeclaration:
                                            publishedEntity = AST.publishEnum(node as TS.EnumDeclaration, nodeName, location, ambrosiaAttrs);
                                            // To check the result:
                                            // Utils.log(getPublishedType(entityName).makeTSType());
                                            break;
                                        default:
                                            throw new Error(`Unsupported AST node type '${nodeName}'`);
                                    }
                                    Utils.log(`Successfully published ${nodeName} as a ${publishedEntity}`);
                                    AST._publishedEntityCount++;
                                }
                                else
                                {
                                    Utils.log(`Warning: Skipping ${nodeName} at ${location} because it is not exported`);
                                }
                            }
                            else
                            {
                                Utils.log(`Warning: Skipping ${nodeName} at ${location} because its ${CODEGEN_TAG_NAME} 'publish' attribute is missing or 'false'`);
                            }
                        }
                        else
                        {
                            // We don't support the @ambrosia tag on the declaration of an overloaded function, so it's expected to be missing in this case,
                            // therefore we don't report the warning. However, this will also be a common "mistake", so publishFunction() checks for this too.
                            let isOverloadFunctionDeclaration: boolean = (node.kind === TS.SyntaxKind.FunctionDeclaration) && ((node as TS.FunctionDeclaration).body === undefined);
                            if (!isOverloadFunctionDeclaration) 
                            {
                                Utils.log(`Warning: Skipping ${nodeName} at ${location} because it has no ${CODEGEN_TAG_NAME} JSDoc tag`);
                            }
                        }
                    }
                }
            }
            catch (error: unknown)
            {
                if (haltOnError)
                {
                    // Halt the walk
                    throw error;
                }
                else
                {
                    // Log the error and continue [even though continuing is potentially unsafe]
                    Utils.log(`Error: ${Utils.makeError(error).message}`);
                    AST._ignoredErrorCount++;
                }
            }
            this.walkAST(node, haltOnError);
        });
    }

    /** 
     * Returns the type(s) of root base class(es) of the specified class type.\
     * The returned array will typically have a single element.\
     * If the supplied type is not a class, returns an empty array.
     */
    // Note: It's unclear how a type would ever have multiple base types [unless it's for future support of polymorphism]
    //       yet this is what the compiler API supports [via TS.Type.getBaseTypes()], so we adhere to it. Note that having 
    //       both an 'extends' and an 'implements' clause does not lead to TS.Type.getBaseTypes() returning more than one type.
    private static getRootBaseTypes(type: TS.BaseType): TS.Type[]
    {
        const baseTypes: TS.BaseType[] = type.getBaseTypes() || [];

        if (!type.isClass())
        {
            return ([]);
        }

        if (baseTypes.length > 0)
        {
            const types: TS.Type[] = [];
            baseTypes.forEach(t => types.push(...AST.getRootBaseTypes(t))); // Recurse
            return (types);
        }
        else
        {
            return ([type]);
        }
    }

    /** 
     * If the supplied 'varDecl' is a for a variable of a type [or of a union type] that derives (at its root) from the specified
     * 'baseTypeName', then it returns the name and type [extracted from the union if needed] of the variable. Otherwise, returns null.\
     * The supplied 'baseTypeHostFileName' (eg. "AmbrosiaRoot.ts") should **not** include a path.
     */
     private static getVarOfBaseType(varDecl: TS.VariableDeclaration, baseTypeName: string, baseTypeHostFileName: string): { varName: string, varType: TS.Type } | null
     {
         let result: { varName: string, varType: TS.Type } | null = null;
         const varName: string = varDecl.name.getText(); // Although we don't need this until later on, it helps (for debugging) to know this early
 
         if ((varDecl.type?.kind === TS.SyntaxKind.TypeReference) || (varDecl.type?.kind === TS.SyntaxKind.UnionType))
         {
             const varTypes: TS.TypeNode[] = [];
             if (varDecl.type?.kind === TS.SyntaxKind.TypeReference)
             {
                 varTypes.push(varDecl.type);
             }
             if (varDecl.type?.kind === TS.SyntaxKind.UnionType)
             {
                 varTypes.push(...(varDecl.type as TS.UnionTypeNode).types);
             }
 
             for (const varType of varTypes)
             {
                 const referencedType: TS.Type = AST._typeChecker.getTypeAtLocation(varType);
                 if (referencedType.isClass())
                 {
                     const rootBaseTypes: TS.Type[] = [...AST.getRootBaseTypes(referencedType)];
                     for (let i = 0; i < rootBaseTypes.length; i++)
                     {
                         const baseType: TS.Type = rootBaseTypes[i];
                         if (baseType && baseType.symbol && (baseType.symbol.name === baseTypeName) && baseType.symbol.declarations)
                         {
                             const typeSourceFile: TS.SourceFile = baseType.symbol.declarations[0].getSourceFile(); // Can refer to either the .ts or .d.ts file name
                             const typeSourceFileName: string = Path.basename(typeSourceFile.fileName, ".ts");
                             if (Utils.equalIgnoringCase(Path.basename(typeSourceFileName, ".d"), Path.basename(baseTypeHostFileName, ".ts")))
                             {
                                 // A VariableDeclaration is under a VariableStatement, which is what the 'export' keyword (if specified) applies to
                                 let parent: TS.Node = varDecl.parent;
                                 while (parent && (parent.kind !== TS.SyntaxKind.VariableStatement))
                                 {
                                     parent = parent.parent;
                                 }
 
                                 if (parent)
                                 {
                                     const isVarExported: boolean = parent.modifiers ? (parent.modifiers.filter(n => n.kind === TS.SyntaxKind.ExportKeyword).length === 1) : false;
                                     if (isVarExported)
                                     {
                                         return ({ varName: varName, varType: referencedType });
                                     }
                                 }
                             }
                         }
                     }
                 }
             }
         }
         return (result);
     }
 
    /** Returns a "friendly" name for a TS.SyntaxKind; for example, returns "method" for a MethodDeclaration. */
    private static getNodeKindName(nodeKind: TS.SyntaxKind): string
    {
        let spacedName: string = TS.SyntaxKind[nodeKind].split("").map(ch => /^[A-Z]*$/g.test(ch) ? " " + ch : ch).join("");
        return (spacedName.replace("Declaration", "").trim().toLowerCase());
    }

    /** Given a character offset into the source file, returns a string (clickable in VSCode) that describes the location. */
    private static getLocation(offset: number, sourceFile: TS.SourceFile = AST._sourceFile): string
    {
        let position: TS.LineAndCharacter = sourceFile.getLineAndCharacterOfPosition(offset); // Note: line and character are both 0-based
        let location: string = `${Path.resolve(sourceFile.fileName)}:${position.line + 1}:${position.character + 1}`; // Note: This format makes it clickable in VSCode
        return (location);
    }

    /** Returns the closest (ie. last) JSDoc comment before the supplied 'declNode', or null if there is no JSDoc comment for the node. */
    private static getJSDocComment(node: TS.Node): TS.JSDoc | null
    {
        let closestJSDocComment: TS.JSDoc | null = null; // The closest (ie. last) JSDoc comment before the node
        const childNodes: TS.Node[] = node.getChildren();

        for (let i = 0; i < childNodes.length; i++)
        {
            if (childNodes[i].kind === TS.SyntaxKind.JSDocComment)
            {
                closestJSDocComment = childNodes[i] as TS.JSDoc;
            }
            else
            {
                break;
            }
        }
        return (closestJSDocComment);
    }

    /** 
     * Returns a formatted (for multi-line) JSDoc comment, optionally indented by the specified 'tabIndent' spaces.
     * If the comment is empty, returns an empty string.
     */
    static formatJSDocComment(jsDocCommentText: string, tabIndent: number = 0): string
    {
        // Handle the empty JSDocComment case (eg. "/** */")
        const isEmptyComment: boolean = RegExp(/^\/*\*+\*\/$/g).test(jsDocCommentText.replace(/\s*/g, ""));
        if (isEmptyComment)
        {
            return ("");
        }

        // 1) Strip off all leading whitespace from each line
        // 2) Add back a single leading space for all but the start-comment line (this is just a style preference)
        const indent: string = " ".repeat(tabIndent);
        const formattedComment: string = indent + jsDocCommentText.split(Utils.NEW_LINE).map(line => line.trim()).map(line => (line.indexOf("*") === 0) ? indent + " " + line : line).join(Utils.NEW_LINE);
        return (formattedComment);
    }
    
    /** 
     * Extracts the @ambrosia JSDoc tag attributes (if present on the supplied declaration node).\
     * If an attribute is encountered that is not one of the [optionally] supplied 'supportedAttrs' then an error will be thrown.\
     * Note: Even if there is no tag, an AmbrosiaAttrs object will still be returned, but its 'hasAmbrosiaTag' attribute will be false. 
     */
    private static getAmbrosiaAttrs(declNode: TS.Node, supportedAttrs: string[] = []): AmbrosiaAttrs
    {
        let attrs: AmbrosiaAttrs = { "hasAmbrosiaTag": false };
        let closestJSDocComment: TS.JSDoc | null = AST.getJSDocComment(declNode);

        if (closestJSDocComment)
        {
            let ambrosiaTagCount: number = 0;

            for (const jsDocNode of closestJSDocComment.getChildren())
            {
                if ((jsDocNode.kind === TS.SyntaxKind.JSDocTag) && ((jsDocNode as TS.JSDocTag).tagName.escapedText === CODEGEN_TAG_NAME.substr(1)))
                {
                    let jsDocTag: TS.JSDocTag = jsDocNode as TS.JSDocTag;
                    let attrsString: string = "";

                    if (typeof jsDocTag.comment === "string")
                    {
                        attrsString = jsDocTag.comment; // The JSDocTag.comment is the text (if any) that comes after "@ambrosia" (the comment excludes '*' chars, but not NL chars)
                    }
                    if (jsDocTag.comment instanceof Array)
                    {
                        // Combine the JSDocText node(s) to create our attributes string
                        const jsDocCommentNodes: TS.NodeArray<TS.Node> = jsDocTag.comment; // As of TypeScript 4.3.5, the only node types are JSDocText | TS.JSDocLink, but that may change in future
                        attrsString = jsDocCommentNodes.filter(n => n.kind === TS.SyntaxKind.JSDocText).map(n => (n as TS.JSDocText).text.trim()).join("");
                    }

                    let attrPairs: string[] = attrsString.split(",").map(p => p.trim()).filter(p => p.length > 0);
                    let location: string = AST.getLocation(jsDocNode.pos);
                    let jsDocCommentWithoutAmbrosiaTag: string = closestJSDocComment.getText().trim().split(Utils.NEW_LINE).filter(line => line.indexOf(CODEGEN_TAG_NAME) === -1).join(Utils.NEW_LINE);

                    if (jsDocCommentWithoutAmbrosiaTag.endsWith("*/") && !jsDocCommentWithoutAmbrosiaTag.startsWith("/**"))
                    {
                        // This can happen when the closestJSDocComment is of the form:
                        // /** @ambrosia publish=true
                        // */
                        jsDocCommentWithoutAmbrosiaTag = "/**" + jsDocCommentWithoutAmbrosiaTag;
                    }
                    
                    if (jsDocCommentWithoutAmbrosiaTag.startsWith("/**") && !jsDocCommentWithoutAmbrosiaTag.endsWith("*/"))
                    {
                        // This can happen when the closestJSDocComment is of the form:
                        // /** 
                        // @ambrosia publish=true */
                        jsDocCommentWithoutAmbrosiaTag += " */";
                    }
                    
                    let jsDocComment: string = jsDocCommentWithoutAmbrosiaTag
                        .replace(/[ ]\*[ /*\r\n]+\*\/$/g , " */") // Contract variants of " *  */" endings to just " */" (but avoid contracting "/** */"")
                        .replace(/[ ]+/g, " "); // Condense all space runs to a single space

                    if (jsDocTag.comment && (typeof jsDocTag.comment === "string") && ((jsDocTag.comment.indexOf("\r") >= 0) || (jsDocTag.comment.indexOf("\n") >= 0)))
                    {
                        // We throw because we removed the '@ambrosia' line, so the [associated] line that follows it is now out of context
                        throw new Error(`A newline is not allowed in the attributes of an ${CODEGEN_TAG_NAME} tag (at ${location})`);
                    }

                    if (++ambrosiaTagCount > 1)
                    {
                        throw new Error(`The ${CODEGEN_TAG_NAME} tag is defined more than once (at ${location})`);
                    }

                    // These are internal-only attributes (they aren't attributes set via the @ambrosia tag)
                    attrs["hasAmbrosiaTag"] = true;
                    attrs["location"] = location;
                    attrs["JSDocComment"] = AST.formatJSDocComment(jsDocComment);

                    for (const attrPair of attrPairs)
                    {
                        const parts: string[] = attrPair.split("=").map(p => p.trim());
                        if (parts.length === 2)
                        {
                            const name: string = parts[0]; // Eg. published, version, methodID, doRuntimeTypeChecking
                            const value: string = parts[1];

                            if (AST._knownAttrs.indexOf(name) === -1)
                            {
                                throw new Error(`Unknown ${CODEGEN_TAG_NAME} attribute '${name}' at ${location}${(supportedAttrs.length > 0) ? `; valid attributes are: ${supportedAttrs.join(", ")}` : ""}`);
                            }
                            if ((supportedAttrs.length > 0) && (supportedAttrs.indexOf(name) === -1))
                            {
                                throw new Error(`The ${CODEGEN_TAG_NAME} attribute '${name}' is invalid for a ${AST.getNodeKindName(declNode.kind)} (at ${location}); valid attributes are: ${supportedAttrs.join(", ")}`);
                            }

                            switch (name)
                            {
                                case "publish":
                                    checkBoolean(name, value);
                                    attrs[name] = (value === "true");
                                    break;
                                case "version":
                                    checkPositiveInteger(name, value);
                                    attrs[name] = parseInt(value);
                                    break;
                                case "methodID":
                                    checkPositiveInteger(name, value);
                                    attrs[name] = parseInt(value);
                                    break;
                                case "doRuntimeTypeChecking":
                                    checkBoolean(name, value);
                                    attrs[name] = (value === "true");
                                    break;
                                default:
                                    throw new Error(`Unsupported ${CODEGEN_TAG_NAME} attribute '${name}' at ${location}`);
                            }
                        }
                        else
                        {
                            throw new Error(`Malformed ${CODEGEN_TAG_NAME} attribute '${attrPair}' at ${location}; expected format is: attrName=attrValue, ...`);
                        }
                    }
                }
            }
        }
        return (attrs);

        /** [Local function] Throws if the specified 'attrValue' is not a boolean. */
        function checkBoolean(attrName: string, attrValue: string): void
        {
            if ((attrValue !== "true") && (attrValue !== "false"))
            {
                throw new Error(`The value ('${attrValue}') supplied for ${CODEGEN_TAG_NAME} attribute '${attrName}' is not a boolean (at ${attrs["location"]})`);
            }
        }

        /** [Local function] Throws if the specified 'attrValue' is not a positive integer. */
        function checkPositiveInteger(attrName: string, attrValue: string): void
        {
            if (!RegExp(/^-?[0-9]+$/g).test(attrValue))
            {
                throw new Error(`The value ('${attrValue}') supplied for ${CODEGEN_TAG_NAME} attribute '${attrName}' is not an integer (at ${attrs["location"]})`);
            }
            if (parseInt(attrValue) < 0)
            {
                throw new Error(`The value (${parseInt(attrValue)}) supplied for ${CODEGEN_TAG_NAME} attribute '${attrName}' cannot be negative (at ${attrs["location"]})`);
            }
        }
    }

    /** 
     * Executes publishType() for an enum decorated with an @ambrosia JSDoc comment tag (eg. "@ambrosia publish=true").\
     * Note: The enum cannot contain any expressions or computed values.
     */
    private static publishEnum(enumDeclNode: TS.EnumDeclaration, nodeName: string, location: string, ambrosiaAttrs: AmbrosiaAttrs): string
    {
        try
        {
            if (enumDeclNode.members.length === 0)
            {
                throw new Error("The enum contains no values");
            }
         
            // Note: Here we are extracting the names and MANUALLY computing the values of the enum (which the compiler would 
            // normally do if we were using Utils.getEnumValues(), but which we can't use when statically processing the AST).
            let rawEnumValues: string[] = [];
            let enumValueNames: string[] = [];
        
            const childNodes: TS.Node[] = enumDeclNode.getChildren();
            for (let i = 0; i < childNodes.length; i++)
            {
                if ((childNodes[i].kind === TS.SyntaxKind.OpenBraceToken) && (childNodes[i + 1].kind === TS.SyntaxKind.SyntaxList))
                {
                    const enumMembers: TS.EnumMember[] = childNodes[i + 1].getChildren().filter(n => n.kind === TS.SyntaxKind.EnumMember) as TS.EnumMember[];
                    for (let m = 0; m < enumMembers.length; m++)
                    {
                        const childNodes: TS.Node[] = enumMembers[m].getChildren();

                        rawEnumValues.push(''); // Assume no explicit value is assigned (for now)
                        enumValueNames.push(enumMembers[m].name.getText());

                        for (let i = 0; i < childNodes.length; i++)
                        {
                            if (childNodes[i].kind === TS.SyntaxKind.EqualsToken)
                            {
                                rawEnumValues[m] = childNodes[i + 1].getText();
                                break;
                            }            
                        }
                    }
                }
            }    

            let enumValues: number[] = [enumValueNames.length];
            let lastValue: number = -1;

            for (let i = 0; i < rawEnumValues.length; i++)
            {
                if (rawEnumValues[i])
                {
                    if (!RegExp("^[+-]?[0-9]+$").test(rawEnumValues[i])) // We don't support computed enum values, like "1 + 2" or "'foo'.length"
                    {
                        throw new Error(`Unable to parse enum value '${enumValueNames[i]}' (${rawEnumValues[i]}); only integers are supported`);
                    }
                    else
                    {
                        enumValues[i] = lastValue = parseInt(rawEnumValues[i]);
                    }
                }
                else
                {
                    enumValues[i] = ++lastValue;
                }
            }

            let enumDefinition: string = enumValues.map((value, i) => `${value}=${enumValueNames[i]}`).join(","); // Matches the format returned by Utils.getEnumValues() [which we can't use during AST processing]
            let enumName: string = enumDeclNode.name.getText();
            let jsDocComment: string = ambrosiaAttrs["JSDocComment"] as string; // The complete JSDoc comment containing the @ambrosia tag (sans the @ambrosia tag itself)
            let nsPath: string = AST.getNamespacePath(enumDeclNode);

            publishType(enumName, "number", enumDefinition, { nsPath: nsPath, nsComment: AST._namespaceJSDocComments[nsPath], jsDocComment: jsDocComment });
            return ("type");
        }
        catch (error: unknown)
        {
            throw new Error(`Unable to publish ${nodeName} (at ${location}) as a type (reason: ${Utils.makeError(error).message})`);
        }
    }

    /** Executes publishType() for a type alias decorated with an @ambrosia JSDoc comment tag (eg. "@ambrosia publish=true"). */
    private static publishTypeAlias(typeAliasDeclNode: TS.TypeAliasDeclaration, nodeName: string, location: string, ambrosiaAttrs: AmbrosiaAttrs): string
    {
        try
        {
            let typeName: string = typeAliasDeclNode.name.getText();
            let jsDocComment: string = ambrosiaAttrs["JSDocComment"] as string; // The complete JSDoc comment containing the @ambrosia tag (sans the @ambrosia tag itself)
            let nsPath: string = AST.getNamespacePath(typeAliasDeclNode);
            let tsType: TS.Type = AST._typeChecker.getTypeAtLocation(typeAliasDeclNode);
            let typeDefinition: string = "";

            if (tsType.aliasTypeArguments || typeAliasDeclNode.typeParameters)
            {
                // Given the declaration "type PersonName<T extends string | number> = boolean | T;", tsType.aliasTypeArguments will return "T"
                let genericTypePlaceholderNames: string = "";
                if (tsType.aliasTypeArguments)
                {
                    genericTypePlaceholderNames = tsType.aliasTypeArguments.map(arg => arg.symbol.name).join("' and '");
                }
                else
                {
                    // Given the declaration "type misusedGeneric<T> = string;", tsType.aliasTypeArguments will be undefined, but typeAliasDeclNode.typeParameters will return "T"
                    if (typeAliasDeclNode.typeParameters)
                    {
                        genericTypePlaceholderNames = typeAliasDeclNode.typeParameters.map(p => p.getText()).join("' and '");
                    }
                }
                throw new Error(`Generic type aliases are not supported; since the type of '${genericTypePlaceholderNames}' will not be known until runtime, ` +
                                "Ambrosia cannot determine [at code-gen time] if the type(s) can be serialized");
            }

            const childNodes: TS.Node[] = typeAliasDeclNode.getChildren();
            for (let i = 0; i < childNodes.length; i++)
            {
                if (childNodes[i].kind === TS.SyntaxKind.EqualsToken)
                {
                    typeDefinition = AST.buildTypeDefinition(childNodes[i + 1]);
                    break;
                }
            }

            publishType(typeName, typeDefinition, undefined, { nsPath: nsPath, nsComment: AST._namespaceJSDocComments[nsPath], jsDocComment: jsDocComment });
            return ("type");
        }
        catch (error: unknown)
        {
            throw new Error(`Unable to publish ${nodeName} (at ${location}) as a type (reason: ${Utils.makeError(error).message})`);
        }
    }

    /** 
     * Executes publishPostMethod() or publishMethod() for a function (or static method) decorated with an @ambrosia JSDoc comment tag (eg. "@ambrosia publish=true, version=3, doRuntimeTypeChecking=false").\
     * Functions are assumed to be post method implementations unless the 'methodID' attribute is provided. The only required attribute is 'published'.
     */
    private static publishFunction(functionDeclNode: TS.FunctionDeclaration | TS.MethodDeclaration, nodeName: string, location: string, ambrosiaAttrs: AmbrosiaAttrs): string
    {
        let methodName: string | undefined = functionDeclNode.name?.getText();
        let methodParams: string[] = [];
        let returnType: string = "void";
        let version: number = (ambrosiaAttrs["version"] || 1) as number; // Extracted from @ambrosia JSDoc tag
        let methodID: number = (ambrosiaAttrs["methodID"] || IC.POST_METHOD_ID) as number; // Extracted from @ambrosia JSDoc tag
        let doRuntimeTypeChecking: boolean = (ambrosiaAttrs["doRuntimeTypeChecking"] || true) as boolean; // Extracted from @ambrosia JSDoc tag
        let isPostMethod: boolean = (methodID === IC.POST_METHOD_ID);
        let isAsyncFunction: boolean = functionDeclNode.modifiers ? (functionDeclNode.modifiers.filter(m => m.kind === TS.SyntaxKind.AsyncKeyword).length === 1) : false;
        let isGenericFunction: boolean = false;
        let genericTypePlaceholderNames: string = "";
        let isOverloadDeclaration: boolean = (functionDeclNode.body === undefined);
        let jsDocComment: string = ambrosiaAttrs["JSDocComment"] as string; // The complete JSDoc comment containing the @ambrosia tag (sans the @ambrosia tag itself)
        let nsPath: string = AST.getNamespacePath(functionDeclNode);

        if (functionDeclNode.typeParameters)
        {
            genericTypePlaceholderNames = functionDeclNode.typeParameters.map(p => p.getText()).join(", ");
            isGenericFunction = true;
        }

        if ((methodID !== IC.POST_METHOD_ID) && ambrosiaAttrs["doRuntimeTypeChecking"])
        {
            // Only post methods provide a way (the postResultDispatcher) to communicate a type-mismatch back to the caller, so we don't support type checking for non-post methods
            throw new Error(`The 'doRuntimeTypeChecking' attribute only applies to a post method (ie. when a 'methodID' is not provided in the ${CODEGEN_TAG_NAME} tag) at ${ambrosiaAttrs["location"]}`);
        }

        try
        {
            if (!methodName)
            {
                throw new Error("The function has no name");
            }

            if (isOverloadDeclaration)
            {
                throw new Error(`The ${CODEGEN_TAG_NAME} tag must appear on the implementation of an overloaded function`);
            }

            // Note: publish[Post]Method() - which runs later - would typically catch this, but as an "unpublished type" error and only if the
            //       name of the generic type parameter (eg. "T") - which are aliases (placeholders), NOT actual type names - didn't match a
            //       [non-primitive] native type name or published type. For example, "fn<Uint8Array>(p1: Uint8Array): void" would pass the 
            //       publish[Post]Method() checks (although only because we pass the method name as "fn", not "fn<Uint8Array>" which would error).
            //       So we take advantage of the available AST information to report a much more accurate error here.
            if (isGenericFunction)
            {
                throw new Error(`Generic functions are not supported; since the type of '${genericTypePlaceholderNames}' will not be known until runtime, ` +
                                "Ambrosia cannot determine [at code-gen time] if the type(s) can be serialized");
            }
    
            // Get the parameters (if any)
            for (let i = 0; i < functionDeclNode.parameters.length; i++)
            {
                // Note: We replace a parameter that specifies a default value with an optional parameter [the function will behave the same when executed]
                const isOptionalParam: boolean = (functionDeclNode.parameters[i].questionToken !== undefined) || (functionDeclNode.parameters[i].initializer !== undefined);
                const isRestParam: boolean = (functionDeclNode.parameters[i].dotDotDotToken !== undefined);
                const paramName: string = (isRestParam ? "..." : "") + functionDeclNode.parameters[i].name.getText();
                const paramType: string = functionDeclNode.parameters[i].type ? AST.buildTypeDefinition(assertDefined(functionDeclNode.parameters[i].type)) : "any"; // We use 'any' if the parameter type is not specified
                const param: string = `${paramName}${isOptionalParam ? "?:" : ":"}${paramType}`;

                if (!functionDeclNode.parameters[i].type && !AST._fileGenOptions.allowImplicitTypes)
                {
                    throw new Error(`Implicit 'any' type not allowed for parameter '${paramName}'`)
                }
                methodParams.push(param);
            }

            // Get the return type (if any)
            returnType = functionDeclNode.type ? AST.buildTypeDefinition(functionDeclNode.type) : "void";
            if (!functionDeclNode.type && !AST._fileGenOptions.allowImplicitTypes)
            {
                throw new Error("Implicit 'void' return type not allowed");
            }
            if (isAsyncFunction)
            {
                // All message handling must be done synchronously
                throw new Error("async functions are not supported");
            }
            if (!isPostMethod && (returnType !== "void"))
            {
                throw new Error("A non-post method can only have a return type of 'void'");
            }

            if (isPostMethod)
            {
                publishPostMethod(methodName, version, methodParams, returnType, doRuntimeTypeChecking, { nsPath: nsPath, nsComment: AST._namespaceJSDocComments[nsPath], jsDocComment: jsDocComment });
                return ("post method");
            }
            else
            {
                publishMethod(methodID, methodName, methodParams, { nsPath: nsPath, nsComment: AST._namespaceJSDocComments[nsPath], jsDocComment: jsDocComment });
                return ("non-post method");
            }
        }
        catch (error: unknown)
        {
            throw new Error(`Unable to publish ${nodeName} (at ${location}) as a ${isPostMethod ? "post " : ""}method (reason: ${Utils.makeError(error).message})`);
        }
    }

    /** 
     * Returns the text-only (and single-line) definition of a type.\
     * startNode must be a TS.TypeNode and the method will throw if it's not.
     */
    private static buildTypeDefinition(startNode: TS.Node): string
    {
        let typeDefinition: string = "";

        if (!TS.isTypeNode(startNode))
        {
            throw new Error(`The specified 'startNode' is a ${TS.SyntaxKind[startNode.kind]} when a TypeNode was expected`);
        }

        walkType(startNode); // Builds typeDefinition
        typeDefinition = trimTrailingComma(typeDefinition);
        return (typeDefinition);

        /** [Local function] Builds typeDefinition as it walks the type. */
        function walkType(node: TS.Node): void
        {
            if (node.kind === TS.SyntaxKind.TypeLiteral) // A complex type
            {
                const childNodes: TS.Node[] = node.getChildren();
                if ((childNodes[0].kind === TS.SyntaxKind.OpenBraceToken) && (childNodes[1].kind === TS.SyntaxKind.SyntaxList))
                {
                    const propertySignatures: TS.PropertySignature[] = childNodes[1].getChildren().filter(n => n.kind === TS.SyntaxKind.PropertySignature) as TS.PropertySignature[];

                    typeDefinition += "{";    
                    for (let p = 0; p < propertySignatures.length; p++)
                    {
                        const childNodes: TS.Node[] = propertySignatures[p].getChildren();
                        const propertyName: string = propertySignatures[p].name.getText();
                        let propertyHasType: boolean = false;

                        if (propertySignatures[p].questionToken)
                        {
                            throw new Error(`Property '${propertyName}' is optional; types with optional properties are not supported`);
                        }

                        typeDefinition += propertyName + ":";

                        for (let i = 0; i < childNodes.length; i++)
                        {
                            if (childNodes[i].kind === TS.SyntaxKind.ColonToken)
                            {
                                walkType(childNodes[i + 1]);
                                propertyHasType = true;
                                break;
                            }            
                        }

                        if (!propertyHasType)
                        {
                            if (!AST._fileGenOptions.allowImplicitTypes)
                            {
                                throw new Error(`Implicit 'any' type not allowed for property '${propertyName}'`)
                            }
                            typeDefinition += "any,";
                        }
                    }
                    typeDefinition = trimTrailingComma(typeDefinition) + "},";
                }
                else
                {
                    throw new Error(`Unexpected syntax on or after ${AST.getLocation(childNodes[0].getStart())}`);
                }
            }
            else
            {
                // Note: Instead of checking here for unsupported types (like TupleType and FunctionType), we let publishType() / publish[Post]Method()
                //       do the type validation; this way the validation will always be the same no matter how we publish (manually or from source).
                // Note: The node could be an array of a complex type, in which case 'node.getText()' will return the complete complex type (plus the array suffix). Further,
                //       since TypeScript allows the use of BOTH ';' and ',' as member separators, we normalize to only use ',' because this is what publishType() expects.
                typeDefinition += AST.removeWhiteSpaceAndComments(node.getText()).replace(/;/g, ",") + ",";
            }
        }

        /** [Local function] Returns the specified value with any trailing comma removed. */
        function trimTrailingComma(value: string): string
        {
            return (Utils.trimTrailingChar(value, ","));
        }
    }

    /** 
     * Removes "excess" whitespace [including newlines], any inline [including multi-line] comments (eg. "/* Foo \*\/" or "/** Bar \*\/") and any comment lines (eg. "// Baz"). 
     * This simplifies subsequent parsing. For example, this is needed when node.getText() is called on a complex or union type, and also to remove spaces in-and-around array suffixes.
     */
    private static removeWhiteSpaceAndComments(value: string): string
    {
        const valueWithoutCommentLines: string = value.split(Utils.NEW_LINE).filter(line => !RegExp("^\/\/.+$").test(line.trim())).join(""); // Condense to a single line, eliminating any "//" comment lines
        const newValue: string = valueWithoutCommentLines.replace("[\t\r\n\f]+/g", "") // Remove newlines and tabs
            .replace(/[ ]+/g, " ") // Condense all space runs to a single space
            .replace(/([ ]+)(?=[\[\]])/g, "") // Remove all space before (or between) array suffix characters ([])
            .replace(/\/\*.*?\*\//g, ""); // Remove "/*[*] */" comments

        return (newValue);
    }
 }