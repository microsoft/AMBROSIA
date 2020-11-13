// Module for programmer-defined Ambrosia methods and types.
import File = require("fs");
import Path = require("path");
import ChildProcess = require("child_process");
import TS = require("typescript"); // For TypeScript AST parsing
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "./Configuration";
import * as IC from "./ICProcess";
import * as Messages from "./Messages";
import * as Utils from "./Utils/Utils-Index";

/** 
 * The methods that this Ambrosia node (instance / immortal) has published (with publishMethod/publishPostMethod).
 */
let _publishedMethods: { [methodName: string]: { [version: number]: Method } } = {};
/** 
 * The types that this Ambrosia node (instance / immortal) has published (with publishType). Typically, these are complex types but they may also be enums.\
 * The key is the type name (eg. "employee"), and the value is the type definition (eg. "{ name: { firstName: string, lastName: string}, startDate: number }").
 */
let _publishedTypes: { [typeName: string]: Type } = {};
/** 
 * The types that have been referenced by other types, but which have not yet been published (ie. forward references).
 * This list must be empty for publishMethod() and publishPostMethod() to succeed. "Dangling" types (ie. references to
 * unpublished types) can still happen if the user only publishes types but no methods (which is essentially pointless).
 */
let _missingTypes: string[] = [];

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
     * Complex type example: "{ userName: string, phoneNumbers: number[] }". 
     * Will always be "void" for a non-post method.
     */
    returnType: string;
    /** The names of the method parameters. */
    parameterNames: string[];
    /** The types of the method parameters (see returnType for examples). */
    parameterTypes: string[];
    /** The version number of the method. Will always be 1 for a non-post method (to retain compatibility with C# Fork/Impulse methods, whose RPC message format doesn't include a version). */
    version: number;
    /** 
     * A flag indicating whether the parameter types of the [post] method should be checked when the method is called. 
     * If the method relies on JavaScript type coercion, then set this to false.
     */
    isTypeChecked: boolean;
    /** [Internal] Options to facilitate code generation for the method. */
    codeGenOptions: CodeGenOptions = null;
    
    /** [ReadOnly] True if the method is a 'post' method. */
    get isPost(): boolean { return (this.id === IC.POST_METHOD_ID); }

    /** [ReadOnly] True if the method takes raw-byte (ie. custom serialized) parameters (specified as a single parameter "raw: Uint8Array"). */
    get takesRawParams(): boolean { return ((this.parameterNames.length === 1) && (this.parameterNames[0] === "raw") && (this.parameterTypes[0] === "Uint8Array")); }

    constructor(id: number, name: string, version: number = 1, parameters: string[] = [], returnType: string = "void", doRuntimeTypeChecking: boolean = true, codeGenOptions?: CodeGenOptions)
    {
        this.id = id;
        this.name = name;
        this.parameterNames = [];
        this.parameterTypes = [];
        this.version = this.isPost ? version : 1;
        this.codeGenOptions = codeGenOptions;
        this.returnType = this.isPost ? this.removePromiseWrapper(Type.formatType(returnType)) : "void";
        this.isTypeChecked = this.isPost ? doRuntimeTypeChecking : false;
        this.validateMethod(name, parameters, this.returnType);
    }

    /** 
     * Returns "T" if the specified 'returnType' is of the form "Promise&lt;T>" and CodeGenOptions.isAsync flags the method implementation (function) as being async.
     * This can happen when code-gen is run using an input *.ts source file. */
    private removePromiseWrapper(returnType: string) : string
    {
        if (this.codeGenOptions?.isAsync)
        {
            const matches: RegExpExecArray = RegExp(/Promise\s*<(.+?)>/g).exec(returnType);
            if ((matches || []).length === 2)
            {
                return (matches[1].trim());
            }
        }
        return (returnType);
    }

    /** Throws if any of the method parameters (or the returnType) are invalid. */
    private validateMethod(methodName: string, parameters: string[], returnType?: string): void
    {
        let firstOptionalParameterFound: boolean = false;

        for (let i = 0; i < parameters.length; i++)
        {
            let pos: number = parameters[i].indexOf(":");
            if (pos === -1)
            {
                throw new Error(`Method '${methodName}' has a malformed method parameter ('${parameters[i]}')`);
            }

            let paramName: string = parameters[i].substring(0, pos).trim();
            let paramType: string = parameters[i].substring(pos + 1).trim();
            let isOptionalParam = paramName.endsWith("?");
            let description: string = `parameter '${paramName}' of method '${methodName}'`;

            checkName(paramName, description);
            checkType(TypeCheckContext.Method, paramType, paramName, description);

            this.parameterNames.push(paramName);
            this.parameterTypes.push(Type.formatType(paramType));

            // Check that any optional parameters come AFTER all non-optional parameters [to help code-gen in emitTypeScriptFileEx()]
            if (isOptionalParam && !firstOptionalParameterFound)
            {
                firstOptionalParameterFound = true;
            }
            if (firstOptionalParameterFound && !isOptionalParam)
            {
                throw new Error(`Required parameter '${paramName}' of method '${methodName}' must be specified BEFORE all optional parameters`);
            }
        }

        // Post methods don't support a single 'raw' parameter like Fork and Impulse methods do (post methods ALWAYS serialize parameters as JSON)
        if (this.isPost && this.takesRawParams)
        {
            Utils.log(`Warning: Post method '${methodName}' is defined as taking a single 'raw' Uint8Array parameter; be aware that Post methods do support custom (raw byte) parameter serialization - all parameters are always serialized to JSON`);
        }

        if (returnType && !Utils.equalIgnoringCase(returnType, "void"))
        {
            checkType(TypeCheckContext.Method, returnType, null, `return type of method '${methodName}'`);
        }
    }

    /** Returns the method definition as XML, including the Ambrosia call signatures for the method. */ 
    getXml(expandTypes: boolean = false): string
    {
        let xml: string = `<Method isPost=\"${this.isPost}\" name=\"${this.name}\" `;
        
        if (this.isPost)
        {
            xml += `version=\"${this.version}\" returnType=\"${expandTypes ? Type.expandType(this.returnType) : this.returnType}\" isTypeChecked=\"${this.isTypeChecked}\">`;
        }
        else
        {
            xml += `id=\"${this.id}\">`;
        }
        
        for (let i = 0; i < this.parameterNames.length; i++)
        {
            xml += `<Parameter name="${this.parameterNames[i]}" type="${expandTypes ? Type.expandType(this.parameterTypes[i]) : this.parameterTypes[i]}"/>`;
        }
        xml += `<CallSignature type=\"Fork\">${Utils.encodeXmlValue(this.getSignature(Messages.RPCType.Fork, expandTypes))}</CallSignature>`;
        if (!this.isPost) // By design, there is no postImpulse()
        {
            xml += `<CallSignature type=\"Impulse\">${Utils.encodeXmlValue(this.getSignature(Messages.RPCType.Impulse, expandTypes))}</CallSignature>`;
        }
        else
        {
            // A post method has 2 Fork methods [post() and postAsync()]
            xml += `<CallSignature type=\"Fork\">${Utils.encodeXmlValue(this.getSignature(Messages.RPCType.Fork, expandTypes, true))}</CallSignature>`;
        }
        xml += "</Method>";
        return (xml);
    }

    /** Returns a psuedo-JS "template" of the Ambrosia call signature for the method. */
    getSignature(rpcType: Messages.RPCType, expandTypes: boolean = false, asyncVersion: boolean = false): string
    {
        let paramList: string = "";
        let localInstanceName: string = Configuration.loadedConfig().instanceName;

        for (let i = 0; i < this.parameterNames.length; i++)
        {
            let paramName: string = this.parameterNames[i];
            let paramType: string = expandTypes ? Type.expandType(this.parameterTypes[i]) : this.parameterTypes[i];
            
            if (this.isPost)
            {
                let isComplexType: boolean = (paramType[0] === "{");
                paramList += `${(i > 0) ? ", " : ""}arg("${paramName}", ${isComplexType ? paramType : `<${paramType}>`})`;
            }
            else
            {
                paramList += `${(i > 0) ? ", " : ""}${paramName}: ${paramType}`;
            }
        }

        if (this.isPost)
        {
            if (asyncVersion)
            {
                return (`await IC.postAsync("${localInstanceName}", "${this.name}", ${this.version}, null, -1, ${paramList});`);
            }
            else
            {
                let resultHandler: string = Utils.equalIgnoringCase(this.returnType, "void") ? 
                `(result?: ${this.returnType}, error?: string) => { if (error) { /* Process 'error' */ }}` : 
                `(result: ${expandTypes ? Type.expandType(this.returnType) : this.returnType}, error?: string) => { if (!error) { /* Process 'result' */ }}`;

                return (`IC.post("${localInstanceName}", "${this.name}", ${this.version}, ${resultHandler}, -1, ${paramList});`);
            }
        }
        else
        {
            paramList = this.takesRawParams ? `<${paramList}>` : `{ ${paramList} }`;
            return (`IC.call${Messages.RPCType[rpcType]}("${localInstanceName}", ${this.id}, ${paramList});`);
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

    /** 
     * Returns definitions for 2 TypeScript wrapper functions for the published method. 
     * Post methods produce a sync and an async wrapper (both Fork).\
     * Non-post methods produce a Fork and an Impulse wrapper. 
     */
    makeTSWrappers(startingIndent: number = 0, tabIndent: number = 4, jsDocComment?: string): string
    {
        const NL: string = Utils.NEW_LINE; // Just for short-hand
        const pad: string = " ".repeat(startingIndent);
        const tab: string = " ".repeat(tabIndent);
        let wrapperFunctions: string = "";
        let functionParameters: string = this.makeTSFunctionParameters();
        
        jsDocComment = jsDocComment ? jsDocComment.split(NL).map(line => pad + line).join(NL) + NL : "";

        if (this.isPost)
        {
            let versionSuffix: string = (this.version === 1) ? "" : `_v${this.version}`;
            let asyncFunction: string = `${pad}export async function ${this.name}Async${versionSuffix}`;
            let asyncFunctionParameters: string = "";
            let syncFunction: string = `${pad}export function ${this.name}${versionSuffix}`;
            let syncFunctionParameters: string = "";
            let postArgs: string = "";

            asyncFunctionParameters = `(${functionParameters}): Promise<${this.returnType}>`;
            syncFunctionParameters = `(resultHandler: IC.PostResultHandler<${this.returnType}>${(functionParameters.length > 0) ? ", " : ""}${functionParameters}): void`;

            for (let i = 0; i < this.parameterNames.length; i++)
            {
                postArgs += (this.parameterNames.length > 1 ? pad + tab.repeat(2) : "") + `IC.arg("${this.parameterNames[i]}", ${this.parameterNames[i].replace("?", "")})` + ((i === this.parameterNames.length - 1) ? ");" : ", ") + NL;
            }

            let asyncFunctionBody: string = pad + "{" + NL;
            asyncFunctionBody += pad + tab + `let postResult: ${this.returnType} = await IC.postAsync(DESTINATION_INSTANCE_NAME, "${this.name}", ${this.version}, null, POST_TIMEOUT_IN_MS` + (this.parameterNames.length > 0 ? ", " : `);${NL}`) + (this.parameterNames.length > 1 ? NL : "") + postArgs;
            asyncFunctionBody += pad + tab + "return (postResult);" + NL;
            asyncFunctionBody += pad + "}";

            // Reminder: When IC.post() is called, the return value is processed via the resultHandler, so the synchronous wrapper always returns void
            let syncFunctionBody: string = pad + "{" + NL;
            syncFunctionBody += pad + tab + `IC.post(DESTINATION_INSTANCE_NAME, "${this.name}", ${this.version}, resultHandler, POST_TIMEOUT_IN_MS` + (this.parameterNames.length > 0 ? ", " : `);${NL}`) + (this.parameterNames.length > 1 ? NL : "") + postArgs;
            syncFunctionBody += pad + "}";

            asyncFunction += asyncFunctionParameters + NL + asyncFunctionBody;
            syncFunction += syncFunctionParameters + NL + syncFunctionBody;
            wrapperFunctions += jsDocComment + asyncFunction + NL.repeat(2) + jsDocComment + syncFunction;
        }
        else
        {
            let jsonArgs: string = "{}";

            if (this.parameterNames.length > 0)
            {
                jsonArgs = "{ ";
                for (let i = 0; i < this.parameterNames.length; i++)
                {
                    let paramName: string = this.parameterNames[i].replace("?", "");
                    jsonArgs += `${(i > 0) ? ", " : ""}${paramName}: ${paramName}`;
                }
                jsonArgs += " }";
            }

            let jsonOrRawArgs: string = this.takesRawParams ? this.parameterNames[0].replace("?", "") : jsonArgs;
            let functionTemplate: string = `${pad}export function ${this.name}[TOKEN](${functionParameters}): void` + NL;
            functionTemplate += pad +"{" + NL;
            functionTemplate += pad + tab + `IC.call[TOKEN](DESTINATION_INSTANCE_NAME, ${this.id}, ` + jsonOrRawArgs + ");" + NL;
            functionTemplate += pad + "}";

            if (this.takesRawParams)
            {
                // Make the [lone] function parameter name more descriptive
                let comment: string = `// Note: The 'rawBytes' parameter is a custom serialization of all required parameters. Contact the '${Configuration.loadedConfig().instanceName}' instance publisher for details of the serialization format.`;
                functionTemplate = functionTemplate.replace("(raw:", "(rawBytes:").replace(", raw)", ", rawBytes)"); // Replace in BOTH the parameter list amd the "IC.callX()" call
                functionTemplate = comment + NL + functionTemplate;
            }

            let forkFunction: string = functionTemplate.replace(/\[TOKEN]/g, "Fork");
            let impulseFunction: string = functionTemplate.replace(/\[TOKEN]/g, "Impulse");
            wrapperFunctions += jsDocComment + forkFunction + NL.repeat(2) + jsDocComment + impulseFunction;
        }
        
        return (wrapperFunctions);
    }
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
     * The definition of the type with all published (named) types replaced with their expandedDefinitions,
     * eg. "{ employeeName: { firstName: string, lastName: string}, startDate: number }".
     */
    expandedDefinition: string;
    /** If this type is an Enum, these are the available values (eg. "1=Foo,2=Bar"). */
    enumValues: string = null; // Note: We do NOT check enum ranges as part of runtime type-checking (this was explored, but deemed to add too much complexity; plus, no other type values are range checked by the LB)
    /** [Internal] Options to facilitate code generation for the type. */
    codeGenOptions: CodeGenOptions = null;

    /** [ReadOnly] Whether the type is a complex type. */
    get isComplexType(): boolean { return (this.definition.startsWith("{")); } 

    constructor(typeName: string, typeDefinition: string, enumValues: string = null, codeGenOptions?: CodeGenOptions)
    {
        typeName = typeName.trim();
        checkType(TypeCheckContext.Type, typeDefinition, typeName, `published type '${typeName}'`, false); // Note: A published type name cannot be optional (ie. end with '?')

        this.name = typeName;
        this.definition = Type.formatType(typeDefinition);
        this.expandedDefinition = Type.expandType(this.definition);
        if ((typeDefinition === "number") && enumValues)
        {
            this.enumValues = enumValues;
        }
        if (codeGenOptions)
        {
            this.codeGenOptions = codeGenOptions;
        }
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

        let classDefinition: string = `${pad}${isPublic ? "export " : ""}class ${this.name}` + NL + pad + '{' + NL;
        let topLevelTokens: string[] = Type.tokenizeComplexType(this.definition);

        if (topLevelTokens.length % 2 !== 0)
        {
            throw new Error(`Published type '${this.name}' could not be tokenized (it contains ${topLevelTokens.length} tokens, not an even number as expected)`);
        }

        // Add class members [all public]
        for (let i = 0; i < topLevelTokens.length; i += 2)
        {
            let nameToken: string = topLevelTokens[i];
            let typeToken: string = topLevelTokens[i + 1];
            classDefinition += `${pad}${tab}${nameToken} ${typeToken};` + NL;
        }

        // Add class constructor
        classDefinition += NL + `${pad}${tab}constructor(`;
        for (let i = 0; i < topLevelTokens.length; i += 2)
        {
            let nameToken: string = topLevelTokens[i];
            let typeToken: string = topLevelTokens[i + 1];
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
        let formattedTypeDefinition: string = typeDefinition.replace(/\s+/g, ""); // Remove all space
        formattedTypeDefinition = formattedTypeDefinition.replace(/}/g, " }").replace(/[{:,]/g, "$& "); // Add space before '}' and after '{', ':', ','
        formattedTypeDefinition = formattedTypeDefinition.replace(/\[](?=[^,\[]])/g, "[] "); // Add space after trailing '[]'
        return (formattedTypeDefinition); // Note: Arrays of complex types will be formatted as "{...}[]", ie. with no space between '}' and '['
    }

    /** Returns the expanded definition (ie. with named [published] types replaced) of the specified type. */
    static expandType(type: string): string
    {
        let expandedDefinition: string = type;

        if (type.trim().startsWith("{")) // A complex type
        {
            // Find all used type names (by looking for ": typeName, " or ": typeName }" or ": typeName[], " or ": typeName[] }")
            // Note: Test at https://regex101.com/ (set "Flavor" to ECMAScript)
            let regex: RegExp = /: ([A-Za-z][A-Za-z0-9_]*)(?:\[])*?(?:, | })/g;
            let result: RegExpExecArray = null;
            let publishedTypeNames: string[] = [];

            while (result = regex.exec(type))
            {
                let typeName: string = result[1];
                if (_publishedTypes[typeName])
                {
                    publishedTypeNames.push(typeName);
                }
            }

            for (let i = 0; i < publishedTypeNames.length; i++)
            {
                let publishedTypeName: string = publishedTypeNames[i];
                expandedDefinition = expandedDefinition.replace(": " + publishedTypeName, ": " + _publishedTypes[publishedTypeName].expandedDefinition);
            }
        }
        else
        {
            if (_publishedTypes[type])
            {
                // The 'type' is a published type name (eg. "employee")
                expandedDefinition = _publishedTypes[type].expandedDefinition;
            }
        }
        return (expandedDefinition);
    }

    /** 
     * Compares a [complex] type definition against an expected definition, returning null if the types match or returning an error message if they don't.\
     * Note: This does a simplistic positional [ordered] match of tokens, which is why we don't support optional members in published types.
     */
    static compareComplexTypes(typeDefinition: string, expectedDefinition: string): string
    {
        let failureReason: string = null;
        let typeTokens: string[] = Type.tokenizeComplexType(typeDefinition.replace(/\s+\[/g, "\[")); // Remove any space before "[]""
        let expectedTokens: string[] = Type.tokenizeComplexType(expectedDefinition.replace(/\s+\[/g, "\[")); // Remove any space before "[]"
        let maxTokensToCheck: number = Math.min(typeTokens.length, expectedTokens.length);

        for (let i = 0; i < maxTokensToCheck; i++)
        {
            // Do the fast check first
            if (typeTokens[i] === expectedTokens[i])
            {
                continue;
            }

            // If both tokens are complex types, compare them
            if ((typeTokens[i][0] === "{") && (expectedTokens[i][0] === "{"))
            {
                if (failureReason = this.compareComplexTypes(typeTokens[i], expectedTokens[i]))
                {
                    break;
                }
                continue;
            }

            // Allow "any" to match with any type, and "any[]" to match with any array
            // Note: "any" and "any[]" may be inserted into the type definition returned by getRuntimeType().
            if ((!expectedTokens[i].endsWith(":") && (typeTokens[i] === "any")) ||
                (!typeTokens[i].endsWith(":") && (expectedTokens[i] === "any")) ||
                (expectedTokens[i].endsWith("[]") && (typeTokens[i] === "any[]")) ||
                (typeTokens[i].endsWith("[]") && (expectedTokens[i] === "any[]")))
            {
                continue;
            }

            let tokenName: string = ((i > 0) && typeTokens[i - 1].endsWith(":")) ? `type of '${typeTokens[i - 1].slice(0, -1)}'` : `token #${i + 1}`;
            failureReason = `${tokenName} should be '${expectedTokens[i]}', not '${typeTokens[i]}'`;
            break;
        }

        if (!failureReason && (typeTokens.length !== expectedTokens.length))
        {
            // Not overly helpful, but in all likelihood we've already found the problem above
            failureReason = "mismatched structure";
        }

        return (failureReason);
    }

    /** 
     * Parses a complex type into a set of [top-level only] tokens that can be used for comparing it to another [complex] type. 
     * A complex type must start with "{", but can also be of the form "{...}[]".
     * Because this only returns top-level tokens, it may need to be called recursively on any returned complex-type tokens.
     */
    private static tokenizeComplexType(type: string): string[]
    {
        const enum TokenType { None, Name, SimpleType, ComplexType }

        type = type.trim();
        if (type[0] !== "{")
        {
            throw new Error(`The supplied type ('${type}') is not a complex type`);
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
        let validCharRegEx: RegExp = /[ A-Za-z0-9_\[\]]/;

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
                            if (type[type.length - 1] === "]")
                            {
                                // This is the case of a type like "{ foo: string }[]" (ie. an array of an unnamed complex type)
                                currentTokenType = TokenType.ComplexType;
                                complexTypeToken = char;
                                complexTypeStartDepth = depth;
                            }
                            else
                            {
                                currentTokenType = TokenType.Name;
                            }
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

                default: // Space, alphanumeric, non-terminal [ and ]
                    if (!validCharRegEx.test(char) || 
                        ((char === "[") && (pos < type.length - 1) && (type[pos + 1] !== "]")) ||
                        ((char === "]") && (pos > 0) && (type[pos - 1] !== "[")))
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
     * Returns an array of the JavaScript native types, optionally including the boxed versions (eg. String, Number) of the primitive types and the typed arrays (eg. Uint8Array).
     * Note: The list excludes "function", "Function" and "Array". 
     */
    static getNativeTypes(includeBoxedPrimitives: boolean = true, includeTypedArrays: boolean = true): string[]
    {
        let primitives: string[] = ["number", "boolean", "string", "object", "bigint" /* es2020 only*/, "symbol"]; // Note: we omit "function" by design
        let boxedPrimitives: string[] = !includeBoxedPrimitives ? [] : ["Number", "Boolean", "String", "Object", "BigInt" /* es2020 only*/, "Symbol"]; // Note: we omit "Function" and "Array" by design
        let typedArrays: string[] = !includeTypedArrays ? [] : ["Int8Array", "Uint8Array", "Uint8ClampedArray", "Int16Array", "Uint16Array", "Int32Array", "Uint32Array", "Float32Array", "Float64Array", "BigInt64Array", "BigUint64Array"];
        let nativeTypeNames: string[] = [...primitives, ...boxedPrimitives, ...typedArrays];
        return (nativeTypeNames);
    }

    /** 
     * Returns the type of the supplied object (eg. "string", "number[]", "{ count: number }"). Returns null if the type cannot be determined.\
     * TODO: This method needs more testing.
     */
    static getRuntimeType(obj: any): string
    {
        let typeName: string = getPrimitiveTypeName(obj);
        
        // Handle native types (and arrays)
        // Note: For arrays of objects, we only include the type of the first element [on the assumption that ALL the items in the array are of the same type]
        if (isNativeType(typeName) && (typeName !== "object"))
        {
            return (typeName);
        }
        else
        {
            if (Array.isArray(obj))
            {
                if (obj.length > 0)
                {
                    typeName = getPrimitiveTypeName(obj[0]);
                    if (isNativeType(typeName))
                    {
                        if (typeName !== "object")
                        {
                            return (`${typeName}[]`);
                        }
                        else
                        {
                            // Handle an array of complex objects
                            return (`${Type.getRuntimeType(obj[0])}[]`);
                        }
                    }
                    else
                    {
                        if (typeName === "Array")
                        {
                            // Handle an array of arrays
                            return (`${Type.getRuntimeType(obj[0])}[]`);
                        }
                    }
                }
                else
                {
                    // We do this to avoid having one non-inferable value from preventing all type-checking for a complex type
                    return ("any[]");
                }
            }
        }

        // Handle complex types (eg. { name: { firstName: "Mickey", lastName: "Mouse" }, age: 92 })
        let complexType: string = "";
        let previousDepth: number = 0;
        if (typeName === "object")
        {
            Utils.walkObjectTree(obj, 0,
                // leafTest
                (key: string, value: any, depth: number): boolean =>
                {
                    if (depth < previousDepth) 
                    {
                        complexType += `${" }".repeat(previousDepth - depth)},`;
                    }

                    let typeName: string = getPrimitiveTypeName(value);

                    if ((value === null) || (value === undefined))
                    {
                        // We do this to avoid having one non-inferable value from preventing all type-checking for a complex type
                        typeName = "any";
                    }
                    else
                    {
                        if (Array.isArray(value))
                        { 
                            if (value.length === 0)
                            {
                                // We do this to avoid having one non-inferable value from preventing all type-checking for a complex type
                                typeName = "any[]";
                            }
                            else
                            {
                                typeName = `${Type.getRuntimeType(value[0])}[]`;
                            }
                        }
                    }
                    complexType += ((complexType.length > 0) && (depth === previousDepth) ? ", " : " ") + `${key}: ${typeName}`;
                    previousDepth = depth;
                    return (true); // Keep walking tree
                },
                // nonLeafTest
                (key: string, depth: number): boolean =>
                {
                    if (depth < previousDepth) 
                    {
                        complexType += `${" }".repeat(previousDepth - depth)},`;
                    }
                    complexType += (complexType.length > 0) && (depth === previousDepth) ? ", " : " ";
                    complexType += `${key}: {`;
                    previousDepth = depth;
                    return (true); // Keep walking tree
                }, -1, false);
            
            // Note: We add "}'s" if needed because they only get added by leafTest, which will only have run if there is a leaf node that comes AFTER the complex type
            return (complexType ? `{ ${complexType.trim() + " }".repeat(previousDepth) } }` : null);
        }

        return (null);

        // Local helper function [returns true if 'typeName' is a JS native type]
        function isNativeType(typeName: string): boolean
        {
            let isNativeType: boolean = (Type.getNativeTypes().indexOf(typeName) !== -1);
            return (isNativeType);
        }

        // Local helper function [returns the primitive/native type name (eg. "string", "object", "bigint", "Array", "Uint8Array") of the specified 'obj']
        function getPrimitiveTypeName(obj: any): string
        {
            let typeString: string = Object.prototype.toString.call(obj); // Eg. "[object String]"

            if (typeString.indexOf("[object ") === 0) // This should always be true
            {
                let parts = typeString.split(" ");
                let typeName = parts[1].slice(0, parts[1].length - 1);

                for (const primitiveType of Type.getNativeTypes(false, false))
                {
                    if (Utils.equalIgnoringCase(typeName, primitiveType))
                    {
                        typeName = primitiveType
                        break;
                    }
                }
                return (typeName);
            }
            return (typeString);
        }
    }
}

/** 
 * [Internal] Validates that the specified name (of a parameter or object property) is a valid identifier, returning true if it is or throwing if it's not. 
 * The 'nameDescription' describes where 'name' is used (eg. "parameter 'p1' of method 'foo'") so that it can be included in the exception
 * message (if thrown).
 */
export function checkName(name: string, nameDescription: string, optionalAllowed: boolean = true): boolean
{
    let regex: RegExp = optionalAllowed ? /^[A-Za-z][A-Za-z0-9_]*[\?]?$/ : /^[A-Za-z][A-Za-z0-9_]*$/;
    if (name && (!regex.test(name) || (Type.getNativeTypes().indexOf(name) !== -1)))
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
 * Validates the specified type (eg. "string[]") returning true if the type is valid, or throwing if it's invalid.\
 * The value of 'type' is case-sensitive. The 'typeDescription' describes where 'type' is used (eg. "return type of
 * method 'foo'") so that it can be included in the exception message (if thrown). 
 * @param type Either a property type, parameter type, a return type, or a published type definition.
 * @param name Either a property name, parameter name, null (for a return type), or a published type name.
 */
function checkType(context: TypeCheckContext, type: string, name: string, typeDescription: string, optionalAllowed: boolean = true): boolean
{
    type = type.trim();

    // Look for a bad parameter (or object property) name, like "some-Name[]"  
    checkName(name, typeDescription, optionalAllowed);

    if (type === "any")
    {
        return (true);
    }

    if (type.length > 0)
    {
        // Check against the built-in [or published] types, and arrays of built-in [or published] types
        if (type[0] !== "{") // A named type, not a complex/compound type
        {
            let validTypeNames: string[] = Type.getNativeTypes(false).concat(Object.keys(_publishedTypes));
            let lcType: string = type.toLowerCase();

            for (let i = 0; i < validTypeNames.length; i++)
            {
                if ((type === validTypeNames[i]) || type.startsWith(validTypeNames[i] + "[]")) // We want to include arrays of arrays, eg. string[][]
                {
                    return (true); // Success
                }

                // Check for mismatched casing [Note: We disallow published type names to differ only by case]
                if (Utils.equalIgnoringCase(type, validTypeNames[i]) || lcType.startsWith(validTypeNames[i].toLowerCase() + "[]"))
                {
                    let brackets: string = type.replace(/[^\[\]]+/g, "");
                    throw new Error(`The ${typeDescription} has an invalid type ('${type}'); did you mean '${validTypeNames[i] + brackets}'?`);
                }
            }

            if (context === TypeCheckContext.Type)
            {
                // The type is either an incorrectly spelled native type, or is a yet-to-be published custom type.
                // We'll assume the latter, since this allows forward references to be used when publishing types.
                let missingTypeName: string = type.replace(/\[]/g, ""); // Remove all square brackets
                if (_missingTypes.indexOf(missingTypeName) === -1)
                {
                    _missingTypes.push(missingTypeName);
                }
                return (true);
            }
        }

        // Check a complex/compound type (eg. "{ names: { firstName: string, lastName: string }, startDate: number, jobs: { title: string, durationInSeconds: bigint[] }[] }")
        if (type[0] === "{")
        {
            let obj: object = null;
            let json: string = type.replace(/[\s\"'"]/g, ""); // Remove all spaces and double/single-quotes

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
            catch (error)
            {
                // Note: We remove the "at position N" message because the position applies to the temporary JSON we created, not to the supplied 'type', so it would be misleading
                throw new Error(`Unable to parse the type of ${typeDescription} (reason: ${error.message.replace(/ at position \d+/, "").replace(/ in JSON/, "") }); ` + 
                                `check for missing/misplaced '{', '}', '[', ']', ':' or ',' characters in type`);
            }

            Utils.walkObjectTree(obj, 0,
                (key: string, value: any): boolean => checkType(context, value, key, `${typeDescription} [property '${key}']`, false), // Note: While valid in TypeScript, we don't allow optional object property names (ie. names ending with '?') [see Type.compareComplexTypes()]
                (key: string) => checkName(key, `${typeDescription} [property '${key}']`, false), // Note: While valid in TypeScript, we don't allow optional object property names (ie. names ending with '?') [see Type.compareComplexTypes()]
                1); // The correct type definition for an array of objects is [{name:type}], never [{name1:type1}, {name1:type1}, ...]

            return (true); // Success
        }
    }
    throw new Error(`The ${typeDescription} has an invalid type ('${type}'); check the syntax or casing or, if this is a custom type, check that it has been published`);

    // Local helper function [converts the type from TypeScript-style "{}[]" to JSON-style "[{}]"]
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
            let result: RegExpExecArray = regex.exec(convertedType);
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
 * Publishes a 'post' method so that it can be reported by the built-in 'getPublishedMethodsAsync' method. 
 * Returns true only if the method has not already been published.\
 * Each parameter in 'parameters' must be of the form "name[?]:type", where 'type' can either be 
 * simple (eg. number, string[]) or complex (eg. { name: { firstName: string, lastName: string }, age: number }) or a published type (eg. Employee).\
 * Note: Any optional parameters must be specified (in 'parameters') after all non-optional parameters.\
 * Note: The 'methodName' is case-sensitive.
 * @param codeGenOptions [Internal] For internal use only.
 */
export function publishPostMethod(methodName: string, methodVersion: number, parameters: string[], returnType: string, doRuntimeTypeChecking: boolean = true, codeGenOptions?: CodeGenOptions): boolean
{
    checkForMissingTypes();
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
        _publishedMethods[methodName][methodVersion] = new Method(IC.POST_METHOD_ID, methodName, methodVersion, parameters, returnType, doRuntimeTypeChecking, codeGenOptions); 
        return (true);
    }
    return (false);
}

/** 
 * Publishes a [non-post] method so that it can be reported by the built-in 'getPublishedMethodsAsync' method. 
 * Returns true only if the method has not already been published.\
 * Each parameter in 'parameters' must be of the form "name[?]:type", where 'type' can either be 
 * simple (eg. number, string[]) or complex (eg. { name: { firstName: string, lastName: string }, age: number }) or a published type (eg. Employee).\
 * If the method uses binary serialized parameters (not JSON serialized parameters) then specify a single "raw:Uint8Array" parameter.\
 * Note: Any optional parameters must be specified (in 'parameters') after all non-optional parameters.\
 * Note: The 'methodName' is case-sensitive.
 * @param codeGenOptions [Internal] For internal use only.
 */
export function publishMethod(methodID: number, methodName: string, parameters: string[], codeGenOptions?: CodeGenOptions): boolean
{
    checkForMissingTypes();
    methodName = methodName.trim();

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
        _publishedMethods[methodName][methodVersion] = new Method(methodID, methodName, methodVersion, parameters, "void", false, codeGenOptions); 
        return (true);
    }
    return (false);
}

/** Throws if the _missingTypes list not empty, since this indicates that a custom type was referenced but not defined (published). */
function checkForMissingTypes(): void
{
    if (_missingTypes.length > 0)
    {
        throw new Error(`The following types must be published before any method can be published: ${_missingTypes.join(", ")}`);
    }
}

/** 
 * Unpublishes a method so that it will no longer be reported by the built-in 'getPublishedMethodsAsync' method.
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

/** 
 * Publishes a [typically] complex type (used as either a method parameter or a return value) so that it can be referenced by
 * published methods (see publishMethod and publishPostMethod) or other publish types.\
 * Forward references are allowed, but if not eventually published they will cause both publishMethod() and publishPostMethod() to fail.\
 * When publishing an enum, specify the 'typeDefinition' as "number", and the 'enumType' as the name of the enum type, eg. RPCType.
 * Specifying a string value for 'enumType' is for internal use only.\
 * Publishing a type is similar to declaring a type alias in TypeScript (but without support for optional members or union types).
 * However, during code generation [emitTypeScriptFile[FromSource]()] it will be converted to a class to a) allow concise constructor syntax 
 * to be used [in generated consumer-side code], and b) to allow independent augmentation with methods (for appropriate encapsulation).
 * But a published type itself does not have any methods: it is only a data structure.\
 * Published types can be queried using the built-in 'getPublishedTypesAsync' method.\
 * Returns true only if the type has not already been published.\
 * Note: The 'typeName' is case-sensitive.
 * @param codeGenOptions [Internal] For internal use only.
 */
export function publishType(typeName: string, typeDefinition: string, enumType?: Utils.EnumType | string, codeGenOptions?: CodeGenOptions): boolean
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

        let enumValues: string = null;
        if (enumType)
        {
            if (typeof enumType === "string") // Only used by AST.publishEnum()
            {
                // Eg. "0=First,1=Second,2=Third"
                if (!RegExp(/^([0-9]+=[A-Za-z][A-Za-z0-9_]*,?)+$/g).test(enumType))
                {
                    throw new Error(`The specified 'enumType' ("${enumType}") is invalid`);
                }
                enumValues = enumType;
            }
            else
            {
                enumValues = Utils.getEnumValues(enumType as Utils.EnumType);
            }
        }
        _publishedTypes[typeName] = new Type(typeName, typeDefinition, enumValues, codeGenOptions);

        // If needed, take the type off the "missing" list
        let index: number = _missingTypes.indexOf(typeName);
        if (index !== -1)
        {
            _missingTypes.splice(index, 1);
        }

        return (true);
    }
    return (false);
}

/** Asynchronously returns an XML document (as unformatted text) describing the methods available on the specified instance. */
export async function getPublishedMethodsAsync(destinationInstance: string, expandTypes: boolean = false, includePostMethodsOnly: boolean = false): Promise<string>
{
    let methodListXml: string = await IC.postAsync(destinationInstance, "_getPublishedMethods", 1, null, 8000, 
        IC.arg("expandTypes", expandTypes), 
        IC.arg("includePostMethodsOnly", includePostMethodsOnly));
    return (methodListXml);
}

/** Asynchronously returns true if the specified method/version has been published (ie. is available) on the specified instance. */
export async function isPublishedMethodAsync(destinationInstance: string, methodName: string, methodVersion: number = 1): Promise<boolean>
{
    let isPublished: boolean = await IC.postAsync(destinationInstance, "_isPublishedMethod", 1, null, 8000, 
        IC.arg("methodName", methodName), 
        IC.arg("methodVersion", methodVersion));
    return (isPublished);
}

/** Asynchronously returns an XML document (as unformatted text) describing the types available on the specified instance. */
export async function getPublishedTypesAsync(destinationInstance: string, expandTypes: boolean = false): Promise<string>
{
    let typeListXml: string = await IC.postAsync(destinationInstance, "_getPublishedTypes", 1, null, 8000, IC.arg("expandTypes", expandTypes));
    return (typeListXml);
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
export function getPublishedType(typeName: string): Type
{
    return (_publishedTypes[typeName] ?? null);
}

/** [Internal] Returns the published Method with the specified name and version (if it exists), or null (if it doesn't). */
export function getPublishedMethod(methodName: string, methodVersion: number = 1): Method
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

/** Flags for the kind of code file to generate. Can be combined. */
export enum CodeGenFileKind
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
    MethodImplementations = 64,
    // Note: For all the 'xxxEventHandler' sections, the 'xxx' comes from Messages.AppEventType. See also: _appEventHandlerFunctions.
    ICStartingEventHandler = 4096,
    ICStartedEventHandler = 8192,
    ICStoppedEventHandler = 16384,
    ICReadyForSelfCallRpcEventHandler = 32768,
    RecoveryCompleteEventHandler = 65536,
    UpgradeStateAndCodeEventHandler = 131072,
    IncomingCheckpointStreamSizeEventHandler = 262144,
    FirstStartEventHandler = 524288,
    BecomingPrimaryEventHandler = 1048576
}

/** Class of details (both known and discovered) about an AppEvent handler function (in an input source file). */
class AppEventHandlerFunctionDetails
{ 
    expectedParameters: string = "";
    expectedReturnType: string = "void";
    isAsyncAllowed: boolean = true;
    foundInInputSource: boolean = false; // Set at runtime
    nsPath: string = null; // Set at runtime
    isAsync: boolean = null; // Set at runtime
    location: string = null; // Set at runtime

    constructor(expectedParameters: string = "", expectedReturnType: string = "void", isAsyncAllowed: boolean = true)
    {
        this.expectedParameters = expectedParameters;
        this.expectedReturnType = expectedReturnType;
        this.isAsyncAllowed = isAsyncAllowed;
    }

    /** Resets the properties set ("discovered") at runtime. */
    reset(): void
    {
        this.foundInInputSource = false;
        this.nsPath = null;
        this.isAsync = null;
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
_appEventHandlerFunctions["on" + Messages.AppEventType[Messages.AppEventType.UpgradeStateAndCode]].isAsyncAllowed = false;

/** Class that defines options which affect how source files are generated (emitted). */
export class FileGenOptions
{
    /** The kind of file(s) to generate. Defaults to CodeGenFileKind.All.*/
    fileKind?: CodeGenFileKind = CodeGenFileKind.All;
    /** How to handle [git] merging any changes (made to a previously generated file) back into the newly generated file. Defaults to FileMergeType.Annotate. */
    // Note: We default 'mergeType' to 'Annotate' for 2 reasons: 
    // 1) It gives us a way to prevent merging again before conflicts have been resolved (repeatedly auto-merging can lead to lots of cruft in the code).
    // 2) Because the merge is non-optimal (because it uses an empty file as the common ancestor) it's better to have the merge conflicts explicitly annotated.
    mergeType?: FileMergeType = FileMergeType.Annotate;
    /** Whether the generated file(s) should be checked for errors. Defaults to true.*/
    checkGeneratedTS?: boolean = true;
    /** Whether the source [input] file - if supplied - should be checked for errors. Defaults to false. */
    ignoreTSErrorsInSourceFile?: boolean = false;
    /** An override name for the generated file. Can include a path, but not an extention. Defaults to null.*/
    generatedFileName?: string = null;
    /** The number of spaces in a logical tab. Used to format the generated source code. Defaults to 4.*/
    tabIndent?: number = 4;
    /** Whether to write a timestamp in the generated file for its creation date/time. Defaults to true. */
    emitGeneratedTime?: boolean = true;
    /** 
     * [Experimental] The set of sections (or'd together) to **not** generate publisher code for. Defaults to 'None'. 
     * Setting this can help reduce merge conflicts in some instances, but it can also introduce code errors in the generated code.  
     */
    publisherSectionsToSkip?: CodeGenSection = CodeGenSection.None;

    constructor(partialOptions: FileGenOptions)
    {
        for (let optionName in partialOptions)
        {
            if (this[optionName] !== undefined)
            {
                this[optionName] = partialOptions[optionName];
            }
            else
            {
                throw new Error(`'${optionName}' is not a valid FileGenOptions setting`);
            }
        }
    }
}

/**
 * **Note:** It is recommended that emitTypeScriptFileFromSource() is used instead of this method, because it obviates the need to call any publishX() methods
 * (publishType, publishMethod, or publishPostMethod).
 * 
 * Generates consumer-side (method wrappers) and/or publisher-side (Ambrosia framework) TypeScript files (*.g.ts) from the currently
 * published types and methods.
 * - FileGenOptions.fileKind defaults to CodeGenFileKind.All
 * - FileGenOptions.mergeType defaults to MergeType.Annotate
 * - FileGenOptions.tabIndent defaults to 4
 * - FileGenOptions.generatedFileName defaults to "ConsumerInterface" for CodeGenFileKind.Consumer, and "PublisherFramework" for CodeGenFileKind.Publisher.
 *   If supplied, generatedFileName should not include an extension (if it does it will be ignored) but it can include a path.
 *  
 * Returns the number of files successfully generated.
 */
export function emitTypeScriptFile(fileOptions: FileGenOptions = {}): number
{
    return (emitTypeScriptFileEx(null, fileOptions));
}

/** 
 * Generates consumer-side (method wrappers) and/or publisher-side (Ambrosia framework) TypeScript files (*.g.ts)
 * from the annotated function, type-aliases, and enums in the specified TypeScript source file ('sourceFileName').
 * See emitTypeScriptFile() for information about the 'fileOptions' parameter.
 * 
 * Automatically publishing types and methods from an [annotated] TypeScript source file provides 3 major benefits over hand-crafting 
 * publishType(), publishPostMethod(), and publishMethod() calls then calling emitTypeScriptFile():
 * 1) The developer gets design-time support from the TypeScript compiler and the editor (eg. VSCode).
 * 2) The types and methods can be "verified correct" before doing code-gen, speeding up the edit/compile/run cycle.
 * 3) Because the majority of the developer-provided code is located separately from the generated PublisherFramework.g.ts, there is less time spent resolving merges conflicts.
 * 
 * The types and methods in the supplied TypeScript file that need to be published must be annotated with a special JSDoc tag: @ambrosia.
 * - Example for a type or enum: @ambrosia publish=true
 * - Example for a function (that implements a post method): @ambrosia publish=true, version=3, doRuntimeTypeChecking=false
 * - Example for a function (that implements a non-post method): @ambrosia publish=true, methodID=123
 * 
 * The only required attribute is 'publish'.
 */
export function emitTypeScriptFileFromSource(sourceFileName: string, fileOptions: FileGenOptions): number
{
    return (emitTypeScriptFileEx(sourceFileName, fileOptions));
}

function emitTypeScriptFileEx(sourceFileName: string = null, fileOptions: FileGenOptions)
{
    fileOptions = new FileGenOptions(fileOptions); // To force assignment of default values for non-supplied properties
    let expectedFileCount: number = (fileOptions.fileKind !== CodeGenFileKind.All) ? 1 : 2;
    let generatedFileCount: number = 0;
    let totalErrorCount: number = 0;
    let totalMergConflictCount: number = 0;
    
    try
    {
        if (fileOptions.fileKind === CodeGenFileKind.All)
        {
            Utils.log(`Generating TypeScript files for Consumer and Publisher...`);
        }
        else
        {
            Utils.log(`Generating ${CodeGenFileKind[fileOptions.fileKind]} TypeScript file...`);
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
                AST.publishFromAST(sourceFileName, fileOptions.ignoreTSErrorsInSourceFile);
            }

            if (AST.publishedEntityCount() === 0)
            {
                // publishedEntityCount can be 0 because there are no @ambrosia tags, all the @ambrosia 'publish' attributes are 'false', or because none of the tagged entities are exported
                throw new Error(`The input source file (${Path.basename(sourceFileName)}) does not publish any entities (functions, type aliases and enums annotated with an '@ambrosia' JSDoc tag)`);
            }
        }
        
        // This will only happen if no methods have been published because publish[Post]Method() also catch this condition
        if (_missingTypes.length > 0)
        {
            throw new Error(`The following types are referenced by other types, but have not been published: ${_missingTypes.join(", ")}`);
        }

        if (fileOptions.generatedFileName && (fileOptions.fileKind === CodeGenFileKind.All))
        {
            throw new Error("When a FileGenOptions.generatedFileName is specified the FileGenOptions.fileKind cannot be CodeGenFileKind.All; instead, call emitTypeScriptFile() or emitTypeScriptFileFromSource() for each required CodeGenFileKind using a different FileGenOptions.generatedFileName in each call");
        }
        
        function incrementTotals(result: SourceFileProblemCheckResult): void
        {
            if (result !== null)
            {
                totalErrorCount += result.errorCount;
                totalMergConflictCount += result.mergeConflictCount;
                generatedFileCount++;
            }
        }

        if ((fileOptions.fileKind & CodeGenFileKind.Consumer) === CodeGenFileKind.Consumer)
        {
            let result: SourceFileProblemCheckResult = emitConsumerTypeScriptFile(fileOptions.generatedFileName || "ConsumerInterface", fileOptions, sourceFileName);
            incrementTotals(result);
        }

        if ((fileOptions.fileKind & CodeGenFileKind.Publisher) === CodeGenFileKind.Publisher)
        {
            let result: SourceFileProblemCheckResult = emitPublisherTypeScriptFile(fileOptions.generatedFileName || "PublisherFramework", fileOptions, sourceFileName);
            incrementTotals(result);
        }
    }
    catch (error)
    {
        Utils.log(`Error: ${(error as Error).message}`);
    }

    let success: boolean = (expectedFileCount === generatedFileCount) && (totalErrorCount === 0);
    let prefix: string = (fileOptions.fileKind === CodeGenFileKind.All) ? "Code" : (CodeGenFileKind[fileOptions.fileKind] + " code");
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
function emitPublisherTypeScriptFile(fileName: string, fileOptions: FileGenOptions, sourceFileName?: string): SourceFileProblemCheckResult
{
    const NL: string = Utils.NEW_LINE; // Just for short-hand
    let fileGenerated: boolean = false;

    // Note: When 'sourceFileName' is supplied, code generation behaves slightly differently:
    // 1) In the Header section it creates a 'import * as PTM from "${sourceFileName}"; // PTM = "Published Types and Methods"'.
    // 2) In the PostMethodHandlers and NonPostMethodHandlers sections it uses "PTM." as the method/type "namespace" qualifier.
    // 3) The MethodImplementations section is left empty.

    try
    {
        let templateFileName: string = "PublisherFramework.template.ts";
        let pathedTemplateFile: string = getPathedTemplateFile(templateFileName);
        let outputPath: string = Path.resolve(Path.parse(fileName).dir ? Path.dirname(fileName) : process.cwd());
        let pathedOutputFile: string = Path.join(outputPath, `${Path.basename(fileName).replace(Path.extname(fileName), "")}.g.ts`);
        let template: string = removeDevOnlyTemplateCommentLines(File.readFileSync(pathedTemplateFile, { encoding: "utf8" }));
        let content: string = template;

        checkForFileNameConflicts(pathedOutputFile, sourceFileName);
        checkForGitMergeMarkers(pathedOutputFile);

        content = replaceTemplateToken(content, CodeGenSection.Header, fileOptions, "", sourceFileName);
        content = replaceTemplateToken(content, CodeGenSection.AppState, fileOptions, "", sourceFileName);
        content = replaceTemplateToken(content, CodeGenSection.PostMethodHandlers, fileOptions, "// Code-gen: Post method handlers will go here" + NL, sourceFileName);
        content = replaceTemplateToken(content, CodeGenSection.NonPostMethodHandlers, fileOptions, "// Code-gen: Fork/Impulse method handlers will go here" + NL, sourceFileName);
        content = replaceTemplateToken(content, CodeGenSection.PublishTypes, fileOptions, "// Code-gen: Published types will go here");
        content = replaceTemplateToken(content, CodeGenSection.PublishMethods, fileOptions, "// Code-gen: Published methods will go here");
        content = replaceTemplateToken(content, CodeGenSection.MethodImplementations, fileOptions, sourceFileName ? "" : (NL + "// Code-gen: Method implementation stubs will go here"), sourceFileName);

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
            const matches: RegExpMatchArray = content.match(regExp);
            if (matches && (matches.length > 0))
            {
                let tokenNameList: string[] = [...content.match(regExp)].map(item => item.split("=")[1]);
                throw new Error(`The following template token(s) [in ${pathedTemplateFile}] were not handled: ${tokenNameList.join(", ")}`);
            }
            else
            {
                // Safety net in case our RegExp didn't work [so that we don't emit a bad file]
                throw new Error(`Not all template token(s) [in ${pathedTemplateFile}] were handled`);
            }
        }

        writeGeneratedFile(content, pathedOutputFile, fileOptions.mergeType);
        Utils.log(`Code file generated: ${pathedOutputFile}${!fileOptions.checkGeneratedTS ? " (TypeScript checks skipped)" : ""}`);
        return (fileOptions.checkGeneratedTS ? checkGeneratedFile(pathedOutputFile, (fileOptions.mergeType !== FileMergeType.None)) : new SourceFileProblemCheckResult());
    }
    catch (error)
    {
        Utils.log(`Error: emitPublisherTypeScriptFile() failed (reason: ${(error as Error).message})`);
        return (null);
    } 

    // Local helper function
    function makeEventHandlerComment(section: CodeGenSection): string
    {
        const fnName: string = "on" + CodeGenSection[section].replace("EventHandler", "");
        const fnDetails: AppEventHandlerFunctionDetails = _appEventHandlerFunctions[fnName];
        const signature: string = `${fnName}(${fnDetails.expectedParameters}): ${fnDetails.expectedReturnType}`;
        const codeGenComment: string = `// TODO: Add an exported ${!fnDetails.isAsyncAllowed ? "[non-async] " : ""}function '${signature}' to ${sourceFileName} then (after the next code-gen) a call to it will be generated here`;

        return (sourceFileName ? codeGenComment : `// TODO: Add your ${!fnDetails.isAsyncAllowed ? "[non-async] " : ""}handler here`);
    }
}

/** 
 * Generates a TypeScript file (called ConsumerInterface.g.ts by default) for all published types and methods. 
 * The purpose of this file is to be included by another [Ambosia Node] immortal so that it can call methods on this immortal.
 * The 'fileName' should not include an extension (if it does it will be ignored) but it may include a path.
 * Returns a SourceFileProblemCheckResult for the generated file, or null if no file was generated.
 */
function emitConsumerTypeScriptFile(fileName: string, fileOptions: FileGenOptions, sourceFileName?: string): SourceFileProblemCheckResult
{
    try
    {
        const NL: string = Utils.NEW_LINE; // Just for short-hand
        let localInstanceName: string = Configuration.loadedConfig().instanceName;
        let outputPath: string = Path.resolve(Path.parse(fileName).dir ? Path.dirname(fileName) : process.cwd());
        let pathedOutputFile: string = Path.join(outputPath, `${Path.basename(fileName).replace(Path.extname(fileName), "")}.g.ts`);
        let content: string = "";
        let namespaces: string[] = [];
        let previousNsPath: string = "";

        checkForFileNameConflicts(pathedOutputFile, sourceFileName);
        checkForGitMergeMarkers(pathedOutputFile);

        // Note: We emit the types and methods using their original namespaces. If we didn't they'd all get emitted at the root level in the file (ie. no namespace) so we'd
        // lose the original organizational structure of the code (which imparts the logical grouping/meaning of members). However, using namespaces does NOT prevent
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
                const nsIndent: string = " ".repeat(fileOptions.tabIndent * nsNestDepth);
                const nsName: string = nsPath ? nsPath.split(".")[nsNestDepth] : "";
                const previousNsNestDepth: number = previousNsPath.split(".").length - 1;

                if (nsNestDepth > previousNsNestDepth)
                {
                    // Start new nested namespace
                    content += `${nsIndent}export namespace ${nsName}` + NL;
                    content += `${nsIndent}{` + NL;
                }
                else
                {
                    if (previousNsNestDepth > 0)
                    {
                        // Emit closing braces (from the previous namespace depth back to the current depth)
                        content = content.trimRight() + NL;
                        for (let depth = nsNestDepth; depth >= previousNsNestDepth; depth--)
                        {
                            content += " ".repeat(fileOptions.tabIndent * depth) + "}" + NL;
                        }
                        content = content.trimRight() + NL.repeat(2);
                    }
                    if (nsPath)
                    {
                        content += `${nsIndent}export namespace ${nsName}` + NL;
                        content += `${nsIndent}{` + NL;
                    }
                }

                emitTypesAndMethods(nsPath ? fileOptions.tabIndent * (nsNestDepth + 1) : 0, nsPath);
                previousNsPath = nsPath;
            }

            // Emit closing braces (back to the root)
            content = content.trimRight() + NL;
            for (let depth = previousNsPath.split(".").length - 1; depth >= 0; depth--)
            {
                content += " ".repeat(fileOptions.tabIndent * depth) + "}" + (depth !== 0 ? NL : "");
            }
        }

        // Local helper function [adds all the sub-paths for a given namespace path]
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

        // Local helper function
        function emitTypesAndMethods(startingIndent: number, nsPath: string = ""): void
        {
            const pad: string = " ".repeat(startingIndent);

            if (Object.keys(_publishedTypes).length > 0)
            {
                for (const typeName in _publishedTypes)
                {
                    let type: Type = _publishedTypes[typeName];
                    if (type.codeGenOptions?.nsPath && (type.codeGenOptions.nsPath !== nsPath))
                    {
                        continue;
                    }
                    content += type.makeTSType(startingIndent, fileOptions.tabIndent, type.codeGenOptions?.jsDocComment) + NL.repeat(2);
                }
            }

            if (Object.keys(_publishedMethods).length > 0)
            {
                for (const name in _publishedMethods)
                {
                    for (const version in _publishedMethods[name])
                    {
                        let method: Method = _publishedMethods[name][version];
                        if (method.codeGenOptions?.nsPath && (method.codeGenOptions.nsPath !== nsPath))
                        {
                            continue;
                        }
                        content += method.makeTSWrappers(startingIndent, fileOptions.tabIndent, method.codeGenOptions?.jsDocComment) + NL.repeat(2);
                    }
                }
            }
        }

        if (content.length > 0)
        {
            let header: string = getHeaderCommentLines("consumer-side API", fileOptions.emitGeneratedTime).join(NL) + NL;
            header += "import Ambrosia = require(\"ambrosia-node\");" + NL;
            header += "import IC = Ambrosia.IC;" + NL.repeat(2);
            header += `let DESTINATION_INSTANCE_NAME: string = "${localInstanceName}";` + NL;
            header += `let POST_TIMEOUT_IN_MS: number = 8000; // -1 = Infinite` + NL.repeat(2);
            content = header + content;
            writeGeneratedFile(content, pathedOutputFile, fileOptions.mergeType);
            Utils.log(`Code file generated: ${pathedOutputFile}${!fileOptions.checkGeneratedTS ? " (TypeScript checks skipped)" : ""}`);
            return (fileOptions.checkGeneratedTS ? checkGeneratedFile(pathedOutputFile, (fileOptions.mergeType !== FileMergeType.None)) : new SourceFileProblemCheckResult());
        }
        else
        {
            throw new Error(sourceFileName ? 
                `The input source file (${Path.basename(sourceFileName)}) does not publish any entities` : 
                "No entities have been published; call publishType() / publishMethod() / publishPostMethod() then retry");
        }
    }
    catch (error)
    {
        Utils.log(`Error: emitConsumerTypeScriptFile() failed (reason: ${(error as Error).message})`);
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
        catch (error)
        {
            // The 'git merge-file' exit code is negative on error, or the number of conflicts otherwise (truncated
            // to 127 if there are more than that many conflicts); if the merge was clean, the exit value is 0
            const gitExitCode: number = error.status;

            if (gitExitCode >= 0) // 0 = clean merge, >0 = conflict count (typically this will only happen for a non-automatic merge, ie. if "--union" is omitted from "git merge-file")
            {
                conflictCount = gitExitCode;
            }
            else
            {
                // An error occurred, so restore the original version
                File.renameSync(pathedRenamedOutputFile, pathedOutputFile);
                throw new Error(`Merge failed (reason: ${(error as Error).message.replace(/\s+/g, " ").trim()} [exit code: ${gitExitCode}])`);
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
    let result: SourceFileProblemCheckResult = AST.checkFileForTSProblems(pathedOutputFile, "output", mergeConflictMarkersAllowed);
    if ((result.errorCount > 0))
    {
        Utils.log(`Error: TypeScript checks failed for ${Path.basename(pathedOutputFile)}: ${result.errorCount} error(s) found`);
    }
    else
    {
        Utils.log(`Success: No TypeScript errors found in ${Path.basename(pathedOutputFile)}`);
    }
    return (result);
}

/** Returns the fully pathed version of the supplied TypeScript template file name [which is shipped in the ambrosia-node package], or throws an the file cannot be found. */
function getPathedTemplateFile(templateFileName: string): string
{
    let pathedTemplateFile: string = "";
    let searchFolders: string[] = [process.cwd(), Path.join(process.cwd(), "node_modules/ambrosia-node")]; // This will only work if ambrosia-node has been installed locally (not globally)
    
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
        catch (error)
        {
            Utils.log(`Error: Unable to determine global npm install folder (reason: ${(error as Error).message})`);
        }

        if (pathedTemplateFile.length === 0)
        {
            throw new Error(`Unable to find template file ${templateFileName} in ${searchFolders.join(" or ")}`);
        }
    }

    return (pathedTemplateFile);
}

/** Generates TypeScript code for the specified template section [of PublisherFramework.template.ts]. May return an empty string if there is no code for the section. */
function codeGen(section: CodeGenSection, fileOptions: FileGenOptions, sourceFileName?: string): string
{
    const NL: string = Utils.NEW_LINE; // Just for short-hand
    const SOURCE_MODULE_ALIAS: string = "PTM"; // PTM = "Published Types and Methods"
    const tab: string = " ".repeat(fileOptions.tabIndent);
    let lines: string[] = [];
    let moduleAlias: string = sourceFileName ? SOURCE_MODULE_ALIAS + "." : ""; 

    // Local helper function
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

    // Local helper function
    function trimArraySuffix(typeName: string): string
    {
        return (typeName.replace(/(\[\])+/g, ""));
    }

    // Skip this section if requested
    if ((section & fileOptions.publisherSectionsToSkip) === section)
    {
        return ("");
    }
    
    switch (section)
    {
        case CodeGenSection.Header:
            lines.push(...getHeaderCommentLines("publisher-side framework", fileOptions.emitGeneratedTime));
            if (sourceFileName)
            {
                // Add an 'import' for the developer-provided source file that contains the implementations of published types and methods
                lines.push(`import * as ${SOURCE_MODULE_ALIAS} from "${Path.isAbsolute(sourceFileName) ? sourceFileName : ("./" + sourceFileName.replace(Path.extname(sourceFileName), ""))}"; // ${SOURCE_MODULE_ALIAS} = "Published Types and Methods"`);
            }
            break;

        case CodeGenSection.AppState:
            lines.push("class AppState extends Ambrosia.AmbrosiaAppState" + NL + "{");
            lines.push(tab + "// TODO: Define your application state here" + NL);
            lines.push(tab + "constructor()");
            lines.push(tab + "{");
            lines.push(tab.repeat(2) + "super();");
            lines.push(tab.repeat(2) + "// TODO: Initialize your application state here");
            lines.push(tab + "}" + NL + "}" + NL);
            lines.push("export let _appState: AppState = new AppState();");
            break;
    
        case CodeGenSection.PostMethodHandlers:
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    let method: Method = _publishedMethods[name][version];
                    let variableNames: string[] = method.parameterNames.map(name => name.endsWith("?") ? name.slice(0, -1) : name);
                    let nsPathForMethod: string = method.codeGenOptions?.nsPath ? (method.codeGenOptions.nsPath + ".") : "";
                    let asyncPrefix: string = method.codeGenOptions?.isAsync ? "await " : "";
                    
                    if (method.isPost)
                    {
                        lines.push(`case "${method.name}":`);
                        for (let i = 0; i < variableNames.length; i++)
                        {
                            let prefix: string = makeParamTypePrefix(_publishedTypes[trimArraySuffix(method.parameterTypes[i])]); 
                            lines.push(`${tab}let ${variableNames[i]}: ${prefix}${method.parameterTypes[i]} = IC.getPostMethodArg(rpc, "${method.parameterNames[i]}");`);
                        }
                        let prefix: string = makeParamTypePrefix(_publishedTypes[trimArraySuffix(method.returnType)]); 
                        lines.push(`${tab}IC.postResult<${prefix}${method.returnType}>(rpc, ${asyncPrefix}${moduleAlias + nsPathForMethod}${method.name}(${variableNames.join(", ")}));`);
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
                    let asyncPrefix: string = method.codeGenOptions?.isAsync ? "await " : "";
                    
                    if (!method.isPost)
                    {
                        lines.push(`case ${method.id}:`);
                        for (let i = 0; i < variableNames.length; i++)
                        {
                            let prefix: string = makeParamTypePrefix(_publishedTypes[trimArraySuffix(method.parameterTypes[i])]); 
                            if (method.takesRawParams)
                            {
                                lines.push(`${tab}let ${variableNames[i]}: ${prefix}${method.parameterTypes[i]} = rpc.rawParams;`);
                            }
                            else
                            {
                                lines.push(`${tab}let ${variableNames[i]}: ${prefix}${method.parameterTypes[i]} = rpc.jsonParams["${method.parameterNames[i]}"];`);
                            }
                        }
                        lines.push(`${tab}${asyncPrefix}${moduleAlias + nsPathForMethod}${method.name}(${variableNames.join(", ")});`);
                        lines.push(`${tab}break;${NL}`);
                    }
                }
            }
            break;

        case CodeGenSection.PublishTypes:
            for (const typeName in _publishedTypes)
            {
                let type: Type = _publishedTypes[typeName];
                lines.push(`Meta.publishType("${type.name}", "${type.definition}");`);
            }
            break;

        case CodeGenSection.PublishMethods:
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    let method: Method = _publishedMethods[name][version];
                    let paramList: string[] = [];
                    paramList.push(...method.parameterNames.map((name, index) => `"${name}: ${method.parameterTypes[index]}"`));
                    let methodParams: string = `[${paramList.join(", ")}]`;

                    if (method.isPost)
                    {
                        lines.push(`Meta.publishPostMethod("${method.name}", ${method.version}, ${methodParams}, "${method.returnType}"${method.isTypeChecked ? "" : ", false"});`);
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
                    lines.push(NL +"// This class is for a published type referenced by one or more published methods");
                    lines.push("// CAUTION: Do NOT change the data 'shape' of this class directly; instead, change the Meta.publishType() call in your code-gen program and re-run it");
                    lines.push(type.makeTSType(0, fileOptions.tabIndent, "", false));
                }
            }
 
            // Emit method stubs
            for (const name in _publishedMethods)
            {
                for (const version in _publishedMethods[name])
                {
                    let method: Method = _publishedMethods[name][version];
                    lines.push(`${NL}// CAUTION: Do NOT change the parameter list (or return type) of this method directly; instead, change the Meta.publish${method.isPost ? "Post" : ""}Method() call in your code-gen program and re-run it`);
                    lines.push(`function ${method.name}(${method.makeTSFunctionParameters()}): ${method.returnType}`);
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
                
                if (fnDetails.foundInInputSource)
                {
                    const prefix: string = moduleAlias + (fnDetails.nsPath ? fnDetails.nsPath + "." : "");
                    const argList: string[] = [];

                    if (fnDetails.expectedParameters)
                    {
                        const parameters: string[] = fnDetails.expectedParameters.split(",").map(p => p.replace(/\s/g, ""));

                        for (let i = 0; i < parameters.length; i++)
                        {
                            const paramName: string = parameters[i].split(":")[0];
                            const paramType: string = parameters[i].split(":")[1];
                            lines.push(`const ${paramName}: ${paramType} = appEvent.args[${i}] as ${paramType};`);
                            argList.push(paramName);
                        }
                    }
                    lines.push(`${fnDetails.isAsync ? "await ": ""}${prefix}${fnName}(${argList.join(", ")});`);
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
    
    // Local helper function
    function extractTokenAttributeValue(token: string, attrName: string): string
    {
        let attrValue: string = token.replace(CODEGEN_TEMPLATE_TOKEN_PREFIX, "").replace("]", "").split(",").filter(kvp => (kvp.split("=")[0] === attrName))[0].split("=")[1];
        return (attrValue);
    }

    if ((section & fileOptions.publisherSectionsToSkip) === section)
    {
        defaultReplacementText = `// Code-gen: '${CodeGenSection[section]}' section skipped by request`;
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
function getHeaderCommentLines(fileDescription: string, emitGeneratedTime: boolean = true): string[]
{
    let headerLines: string[] = [];
    let localInstanceName: string = Configuration.loadedConfig().instanceName;

    headerLines.push(`// Generated ${fileDescription} for the '${localInstanceName}' Ambrosia Node instance.`);
    headerLines.push("// Note: This file was generated" + (emitGeneratedTime ? ` on ${Utils.getTime().replace(" ", " at ")}.` : ""));
    headerLines.push(`// Note: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().`);

    return (headerLines);
}

/** Removes lines that comytin "[DEV-ONLY COMMENT]" from the supplied template. */
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
 * [Internal] This method is for **testing only**.\
 * Use emitTypeScriptFileFromSource() instead. 
 */
export function publishFromSource(tsFileName: string, ignoreCompilationErrors: boolean = false) : void
{
    AST.publishFromAST(tsFileName, ignoreCompilationErrors);
}

/** Type for Ambrosia code-gen attributes parsed from an @ambrosia JSDoc tag. */
type AmbrosiaAttrs = { [attrName: string]: boolean | number | string };

/** [Internal] Type for options (eg. details about the entity) used during code generation. */
type CodeGenOptions = 
{ 
    /** The namespace path (in the input source file) where the entity was found, eg. "Foo.Bar.Baz". [Applies to functions, type aliases, and enums].*/
    nsPath: string,
    /** Whether the function is async or not. [Applies to functions only]. */
    isAsync?: boolean,
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
/** Class of static methods used to walk the abstract syntax tree (AST) of a TypeScript file in order to publish types and methods from it. */
class AST
{
    private static _syntaxKindNames: { [kind: number]: string } = {}; // Mapping from SyntaxKind value to SyntaxKind name
    private static _typeChecker: TS.TypeChecker = null;
    private static _sourceFile: TS.SourceFile = null;
    private static _publishedEntityCount: number = 0; // A running count of the entities (functions, type aliases, enums) published during the AST walk
    private static _compilerOptions: TS.CompilerOptions = { module: TS.ModuleKind.CommonJS, target: TS.ScriptTarget.ES2018 };
    private static _targetNodeKinds: TS.SyntaxKind[] = [TS.SyntaxKind.FunctionDeclaration, TS.SyntaxKind.TypeAliasDeclaration, TS.SyntaxKind.EnumDeclaration];
    // The set of supported @ambrosia tag "attributes" for each target node kind
    private static _supportedAttrs: { [nodeKind: number]: string[] } = 
    {
        [TS.SyntaxKind.FunctionDeclaration]: ["publish", "version", "methodID", "doRuntimeTypeChecking"],
        [TS.SyntaxKind.TypeAliasDeclaration]: ["publish"],
        [TS.SyntaxKind.EnumDeclaration]: ["publish"]
    };
    private static _namespaces: string[] = []; // A "stack" of strings in the format "namespaceName:namespaceEndPosition" (eg. "Test:740")
    private static _currentNamespaceEndPos: number = 0; // The end-offset of the current namespace (0 before the first namespace, or the EOF position after leaving the last namespace)
    private static _functionEndPos: number = 0; // The end-offset of the current function (or 0 if the AST walk is not currently in a function)
    private static _publishedSourceFile: string = null; // The TypeScript source file that was used to publish from (only set once publishing has succeeded)

    // Private because AST is a static class
    private constructor()
    {
    }

    /** The TypeScript source file that was used to publish from (only set once publishing has succeeded). */
    public static publishedSourceFile(): string
    {
        return (AST._publishedSourceFile);
    }

    /** The final count of entities (functions, type aliases, enums) published in publishedSourceFile(). */
    public static publishedEntityCount(): number
    {
        return (AST._publishedSourceFile ? AST._publishedEntityCount : 0);
    }

    /** Returns the current namespace path (eg. "Root.Outer.Inner") at the current point in time of the AST walk. May return an empty string. */
    public static getCurrentNamespacePath(): string
    { 
        return (AST._namespaces.map(pair => pair.split(":")[0]).join(".")); 
    } 

    /** 
     * Reads the supplied TypeScript file and dynamically executes publishType/publishPostMethod/publishMethod calls for the annotated functions, type-aliases and enums. 
     * Returns a count of the number of entities published.
     */
    static publishFromAST(tsFileName: string, ignoreCompilationErrors: boolean = false): number
    {
        let pathedFileName: string = Path.resolve(tsFileName);

        // Check that the input TypeScript file exists
        if (!File.existsSync(tsFileName))
        {
            throw new Error(`The TypeScript file specified (${pathedFileName}) does not exist`);
        }

        Utils.log(`Publishing types and methods from ${pathedFileName}...`);

        let program: TS.Program = TS.createProgram([tsFileName], AST._compilerOptions); // Note: Setting 'removeComments' to true does NOT remove JSDocComment nodes
        AST._sourceFile = program.getSourceFile(tsFileName);
        AST._typeChecker = program.getTypeChecker();
        AST._publishedEntityCount = 0;
        AST._namespaces = [];
        AST._currentNamespaceEndPos = 0;
        AST._functionEndPos = 0;

        // Reset the "discovered" attributes of _appEventHandlerFunctions
        Object.keys(_appEventHandlerFunctions).forEach(fnName => _appEventHandlerFunctions[fnName].reset());

        // Check that the [input] source file "compiles"
        let result: SourceFileProblemCheckResult = AST.checkFileForTSProblems(tsFileName, "input");
        if (result.errorCount > 0)
        {
            if (ignoreCompilationErrors)
            {
                Utils.log(`Ignoring ${result.errorCount} TypeScript error(s) in ${Path.basename(tsFileName)}`);
            }
            else
            {
                throw new Error(`TypeScript check failed for ${tsFileName}: ${result.errorCount} error(s) found`);
            }
        }

        AST.walkAST(AST._sourceFile);

        Utils.log(`Publishing finished: ${AST._publishedEntityCount} entities published`);
        AST._publishedSourceFile = tsFileName;
        return (AST._publishedEntityCount);
    }

    /** Reports (logs) any TypeScript compiler errors and warnings present in the specified .ts file. Returns the counts of errors/warnings found. */
    static checkFileForTSProblems(tsFileName: string, fileType: string, mergeConflictMarkersAllowed: boolean = false): SourceFileProblemCheckResult
    {
        let program: TS.Program = TS.createProgram([tsFileName], AST._compilerOptions);
        let sourceFile: TS.SourceFile = program.getSourceFile(tsFileName);
        let diagnostics: readonly TS.Diagnostic[] = TS.getPreEmitDiagnostics(program);
        let result: SourceFileProblemCheckResult = new SourceFileProblemCheckResult();
        let mergeConflictMarkerCount: number = 0;

        if (diagnostics.length > 0)
        {
            diagnostics.forEach(d => 
            {
                // If we did a merge then we don't report the errors that will [knowingly] arise from the resulting merge conflict markers; 
                // without this, the error ouput would be too "noisy" making it harder to see any "real" errors in the generated code
                let canIgnoreError: boolean = ((fileType === "output") && (d.code === 1185) && mergeConflictMarkersAllowed); // 1185 = "Merge conflict marker encountered"

                if (d.code === 1185)
                {
                    mergeConflictMarkerCount++;
                }

                if ((d.category === TS.DiagnosticCategory.Error) && !canIgnoreError)
                {
                    let message: string = d.start ? 
                        `Error: TypeScript error in ${fileType} file: ${d.messageText} (ts:${d.code}) at ${AST.getLocation(d.start, sourceFile)}` :
                        `Error: Unable to check ${fileType} file: ${d.messageText} (ts:${d.code})`;
                    Utils.log(message);
                    result.errorCount++;
                }
                if (d.category === TS.DiagnosticCategory.Warning)
                {
                    Utils.log(`Warning: TypeScript warning in ${fileType} file: ${d.messageText} (ts:${d.code}) at ${AST.getLocation(d.start, sourceFile)}`);
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
    private static walkAST(nodeToWalk: TS.Node, indent: number = 0): void
    {
        let nodes: TS.Node[] = nodeToWalk.getChildren();
        
        nodes.forEach(node =>
        {
            let isNestedFunction: boolean = (node.kind === TS.SyntaxKind.FunctionDeclaration) && (AST._functionEndPos > 0);
            let isWellKnownFunction: boolean = false;

            // Check for an @ambrosia tag on unsupported nodes
            if (AST._targetNodeKinds.indexOf(node.kind) === -1)
            {
                const attrs: AmbrosiaAttrs = AST.getAmbrosiaAttrs(node);
                if (attrs["hasAmbrosiaTag"] === true)
                {
                    const targetNames: string = this._targetNodeKinds.map(kind => AST.getNodeKindName(kind)).join(", ");
                    throw new Error(`The @ambrosia tag is not valid on a ${AST.getNodeKindName(node.kind)}; valid targets are: ${targetNames} (at ${attrs["location"]})`);
                }
            }

            // Keep track of entering/leaving namespaces
            if (node.kind === TS.SyntaxKind.ModuleDeclaration)
            {
                const moduleDecl: TS.ModuleDeclaration = (node as TS.ModuleDeclaration);
                let namespaceName: string = moduleDecl.name.getText();
                let namespaceEndPos: number = moduleDecl.getStart() + moduleDecl.getWidth() - 1;
                AST._namespaces.push(`${namespaceName}:${namespaceEndPos}`);
                AST._currentNamespaceEndPos = namespaceEndPos;
                Utils.log(`Entering namespace '${namespaceName}' (now in '${AST.getCurrentNamespacePath()}') at ${AST.getLocation(node.getStart())}`, null, Utils.LoggingLevel.Verbose);
            }
            if ((node.getStart() >= AST._currentNamespaceEndPos) && (AST._namespaces.length > 0))
            {
                let leavingNamespaceName: string = AST._namespaces.pop().split(":")[0];
                let enteringNamespaceEndPos = (AST._namespaces.length > 0) ? parseInt(AST._namespaces[AST._namespaces.length - 1].split(":")[1]) : AST._sourceFile.getWidth();
                AST._currentNamespaceEndPos = enteringNamespaceEndPos;
                Utils.log(`Leaving namespace '${leavingNamespaceName}' (now in '${AST.getCurrentNamespacePath() || "[Root]"}') at ${AST.getLocation(node.getStart())}`, null, Utils.LoggingLevel.Verbose);
            }

            // Keep track of entering/leaving a function so that we can we can detect nested (local) functions (which are never candidates to be published)
            if ((node.kind === TS.SyntaxKind.FunctionDeclaration) && (AST._functionEndPos === 0))
            {
                const functionDecl: TS.FunctionDeclaration = (node as TS.FunctionDeclaration);
                AST._functionEndPos = functionDecl.getStart() + functionDecl.getWidth() - 1;
                Utils.log(`Entering function '${functionDecl.name.getText()}' at ${AST.getLocation(node.getStart())}`, null, Utils.LoggingLevel.Verbose);
            }
            if ((AST._functionEndPos > 0) && (node.getStart() >= AST._functionEndPos))
            {
                Utils.log(`Leaving function at ${AST.getLocation(node.getStart())}`, null, Utils.LoggingLevel.Verbose);
                AST._functionEndPos = 0;
            }

            // Keep track of whether we have found any of the "well known" Ambrosia AppEvent handlers
            if (node.kind === TS.SyntaxKind.FunctionDeclaration)
            {
                const functionDecl: TS.FunctionDeclaration = node as TS.FunctionDeclaration;
                const isExported: boolean = node.modifiers ? (node.modifiers.filter(n => n.kind === TS.SyntaxKind.ExportKeyword).length === 1) : false;
                const isAsync: boolean = node.modifiers ? (node.modifiers.filter(n => n.kind === TS.SyntaxKind.AsyncKeyword).length === 1) : false;
                const fnName: string = functionDecl.name?.getText() || "N/A";
                const location: string = AST.getLocation(node.getStart());

                if (isExported && (Object.keys(_appEventHandlerFunctions).indexOf(fnName) !== -1))
                {
                    const fnDetails: AppEventHandlerFunctionDetails = _appEventHandlerFunctions[fnName];
                    const ambrosiaAttrs: AmbrosiaAttrs = AST.getAmbrosiaAttrs(functionDecl, AST._supportedAttrs[functionDecl.kind]);

                    if (ambrosiaAttrs["hasAmbrosiaTag"])
                    {
                        throw new Error(`The @ambrosia tag is not valid on an AppEvent handler ('${fnName}') at ${ambrosiaAttrs["location"]}`);
                    }

                    if (isAsync && !fnDetails.isAsyncAllowed)
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
                        let expectedReturnType: string = fnDetails.expectedReturnType;
                        expectedReturnType = isAsync ? `Promise<${expectedReturnType}>` : expectedReturnType;

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
                                fnDetails.nsPath = AST.getCurrentNamespacePath();
                                fnDetails.isAsync = isAsync;
                                fnDetails.location = location;
                            }
                        }
                        isWellKnownFunction = true;
                    }
                }
            }
            
            // Publish methods/types/enums marked with an @ambrosia JSDoc tag, and which are exported
            if (!isWellKnownFunction && (AST._targetNodeKinds.indexOf(node.kind) >= 0))
            {
                let isExported: boolean = node.modifiers ? (node.modifiers.filter(n => n.kind === TS.SyntaxKind.ExportKeyword).length === 1) : false;
                let location: string = AST.getLocation(node.getStart());
                let entityName: string = (node as TS.DeclarationStatement).name?.getText() || "N/A";
                let nodeName: string = `${AST.getNodeKindName(node.kind)} '${entityName}'`;

                if (!isNestedFunction) // We ignore nested (local) functions since they are never candidates to be published
                {
                    if (isExported)
                    {
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
                                let publishedEntity: string = "";
                                switch (node.kind)
                                {
                                    case TS.SyntaxKind.FunctionDeclaration:
                                        publishedEntity = AST.publishFunction(node as TS.FunctionDeclaration, nodeName, location, ambrosiaAttrs);
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
                                Utils.log(`Warning: Skipping ${nodeName} at ${location} because its 'publish' attribute is missing or not 'true'`);
                            }
                        }
                        else
                        {
                            Utils.log(`Warning: Skipping ${nodeName} at ${location} because it has no @ambrosia tag`);
                        }
                    }
                    else
                    {
                        Utils.log(`Warning: Skipping ${nodeName} at ${location} because it is not exported`);
                    }
                }
            }

            this.walkAST(node, indent + 2);
        });
    }

    /** Returns a "friendly" name for a TS.SyntaxKind. */
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
    
    /** 
     * Extracts the @ambrosia JSDoc tag attributes (if present on the supplied declaration node).\
     * Note: Even if there is no tag, an AmbrosiaAttrs object will still be return, but its 'hasAmbrosiaTag' attribute will be false. 
     */
    private static getAmbrosiaAttrs(declNode: TS.Node, supportedAttrs: string[] = []): AmbrosiaAttrs
    {
        let attrs: AmbrosiaAttrs = { "hasAmbrosiaTag": false };
        let closestJSDocComment: TS.JSDoc = null; // The closest (ie. last) JSDoc comment before the declNode
        const childNodes: TS.Node[] = declNode.getChildren();

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

        if (closestJSDocComment)
        {
            let ambrosiaTagCount: number = 0;

            for (const jsDocNode of closestJSDocComment.getChildren())
            {
                if ((jsDocNode.kind === TS.SyntaxKind.JSDocTag) && ((jsDocNode as TS.JSDocTag).tagName.escapedText === "ambrosia"))
                {
                    let jsDocTag: TS.JSDocTag = jsDocNode as TS.JSDocTag;
                    let attrPairs: string[] = jsDocTag.comment?.split(",").map(p => p.trim()) || [];
                    let location: string = AST.getLocation(jsDocNode.pos);
                    let jsDocComment: string = closestJSDocComment.getText().trim()
                        .replace("@ambrosia ", "")
                        .replace(jsDocTag.comment, "") // This may not work as expected (see below)
                        .replace(/[ ]\*[ /*\r\n]+\*\/$/g , " */") // Contract variants of " *  */" endings to just " */" (but avoid contracting "/** */"")
                        .replace(/[ ]+/g, " "); // Condense all space runs to a single space

                    if ((jsDocTag.comment.indexOf("\r") >= 0) || (jsDocTag.comment.indexOf("\n") >= 0))
                    {
                        // We throw because the above '.replace(jsDocTag.comment, "")' won't have worked correctly in this case (the Tag's comment excludes '*' chars, but not NL chars)
                        throw new Error(`A newline character is not allowed in the attributes of an @ambrosia tag (at ${location})`);
                    }

                    // 1) Strip off all leading whitespace from each line
                    // 2) Add back a single leading space for all but the start-comment line (this is just a style perference)
                    jsDocComment = jsDocComment.split(Utils.NEW_LINE).map(line => line.trim()).map(line => (line.indexOf("*") === 0) ? " " + line : line).join(Utils.NEW_LINE);

                    if (++ambrosiaTagCount > 1)
                    {
                        throw new Error(`The @ambrosia tag is defined more than once at ${location}`);
                    }

                    // These are internal-only attributes (they aren't attributes set via the @ambrosia tag)
                    attrs["hasAmbrosiaTag"] = true;
                    attrs["location"] = location;
                    attrs["JSDocComment"] = RegExp(/^\/*\*+\*\/$/g).test(jsDocComment.replace(/\s*/g, "")) ? "" : jsDocComment; // Handle the empty JSDocComment case (eg. "/** */")

                    for (const attrPair of attrPairs)
                    {
                        const parts: string[] = attrPair.split("=").map(p => p.trim());
                        if (parts.length === 2)
                        {
                            const name: string = parts[0]; // Eg. published, version, methodID, doRuntimeTypeChecking
                            const value: string = parts[1];

                            if ((supportedAttrs.length > 0) && (supportedAttrs.indexOf(name) === -1))
                            {
                                throw new Error(`The @ambrosia attribute '${name}' is invalid for a ${AST.getNodeKindName(declNode.kind)} (at ${location}); valid attributes are: ${supportedAttrs.join(", ")}`);
                            }

                            switch (name)
                            {
                                case "publish":
                                    checkBoolean(name, value);
                                    attrs[name] = (value === "true");
                                    break;
                                case "version":
                                    checkInteger(name, value);
                                    attrs[name] = parseInt(value);
                                    break;
                                case "methodID":
                                    checkInteger(name, value);
                                    attrs[name] = parseInt(value);
                                    break;
                                case "doRuntimeTypeChecking":
                                    checkBoolean(name, value);
                                    attrs[name] = (value === "true");
                                    break;
                                default:
                                    throw new Error(`Unknown @ambrosia attribute '${name}' at ${location}`);
                            }
                        }
                        else
                        {
                            throw new Error(`Malformed @ambrosia attribute '${attrPair}' at ${location}; expected format is: attrName=attrValue, ...`);
                        }
                    }
                }
            }
        }
        return (attrs);

        function checkBoolean(attrName: string, attrValue: string)
        {
            if ((attrValue !== "true") && (attrValue !== "false"))
            {
                throw new Error(`The value ('${attrValue}') supplied for @ambrosia attribute '${attrName}' is not a boolean (at ${attrs["location"]})`);
            }
        }

        function checkInteger(attrName: string, attrValue: string)
        {
            if (!RegExp(/^-?[0-9]+$/g).test(attrValue))
            {
                throw new Error(`The value ('${attrValue}') supplied for @ambrosia attribute '${attrName}' is not an integer (at ${attrs["location"]})`);
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

                        rawEnumValues.push(''); // Assume no explcit value is assigned (for now)
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
                    if (!RegExp("^[0-9]$").test(rawEnumValues[i])) // We don't support computed enum values, like "1 + 2" or "'foo'.length"
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

            publishType(enumName, "number", enumDefinition, { nsPath: AST.getCurrentNamespacePath(), jsDocComment: jsDocComment });
            return ("type");
        }
        catch (error)
        {
            throw new Error(`Unable to publish ${nodeName} (at ${location}) as a type (reason: ${(error as Error).message})`);
        }
    }

    /** Executes publishType() for a type alias decorated with an @ambrosia JSDoc comment tag (eg. "@ambrosia publish=true"). */
    private static publishTypeAlias(typeAliasDeclNode: TS.TypeAliasDeclaration, nodeName: string, location: string, ambrosiaAttrs: AmbrosiaAttrs): string
    {
        let typeName: string = typeAliasDeclNode.name.getText();
        let typeDefinition: string = "";
        let jsDocComment: string = ambrosiaAttrs["JSDocComment"] as string; // The complete JSDoc comment containing the @ambrosia tag (sans the @ambrosia tag itself)

        const childNodes: TS.Node[] = typeAliasDeclNode.getChildren();
        for (let i = 0; i < childNodes.length; i++)
        {
            if (childNodes[i].kind === TS.SyntaxKind.EqualsToken)
            {
                walkType(childNodes[i + 1]); // Builds typeDefinition
                typeDefinition = trimTrailingComma(typeDefinition);
                break;
            }
        }
    
        // Local helper function [builds typeDefinition as it walks the type]
        function walkType(node: TS.Node): void
        {
            if (node.kind === TS.SyntaxKind.TypeLiteral)
            {
                const childNodes: TS.Node[] = node.getChildren();
                if ((childNodes[0].kind === TS.SyntaxKind.OpenBraceToken) && (childNodes[1].kind === TS.SyntaxKind.SyntaxList))
                {
                    const propertySignatures: TS.PropertySignature[] = childNodes[1].getChildren().filter(n => n.kind === TS.SyntaxKind.PropertySignature) as TS.PropertySignature[];

                    typeDefinition += "{";    
                    for (let p = 0; p < propertySignatures.length; p++)
                    {
                        const childNodes: TS.Node[] = propertySignatures[p].getChildren();

                        typeDefinition += propertySignatures[p].name.getText() + ":";

                        for (let i = 0; i < childNodes.length; i++)
                        {
                            if (childNodes[i].kind === TS.SyntaxKind.ColonToken)
                            {
                                walkType(childNodes[i + 1]);
                                break;
                            }            
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
                typeDefinition += node.getText() + ",";
            }
        }

        // Local helper function
        function trimTrailingComma(value: string): string
        {
            if (value.substr(value.length - 1, 1) === ",")
            {
                return (value.substr(0, value.length - 1));
            }
            return (value);
        }

        try
        {
            publishType(typeName, typeDefinition, undefined, { nsPath: AST.getCurrentNamespacePath(), jsDocComment: jsDocComment });
            return ("type");
        }
        catch (error)
        {
            throw new Error(`Unable to publish ${nodeName} (at ${location}) as a type (reason: ${(error as Error).message})`);
        }
    }

    /** 
     * Executes publishPostMethod() or publishMethod() for a function decorated with an @ambrosia JSDoc comment tag (eg. "@ambrosia publish=true, version=3, doRuntimeTypeChecking=false").\
     * Functions are assumed to be post method implementations unless the 'methodID' attribute is provided. The only required attribute is 'published'.
     */
    private static publishFunction(functionDeclNode: TS.FunctionDeclaration, nodeName: string, location: string, ambrosiaAttrs: AmbrosiaAttrs): string
    {
        let methodName: string = functionDeclNode.name.getText();
        let methodParams: string[] = [];
        let returnType: string = "void";
        let version: number = (ambrosiaAttrs["version"] || 1) as number; // Extracted from @ambrosia JSDoc tag
        let methodID: number = (ambrosiaAttrs["methodID"] || IC.POST_METHOD_ID) as number; // Extracted from @ambrosia JSDoc tag
        let doRuntimeTypeChecking: boolean = (ambrosiaAttrs["doRuntimeTypeChecking"] || true) as boolean; // Extracted from @ambrosia JSDoc tag
        let isPostMethod: boolean = (methodID === IC.POST_METHOD_ID);
        let isAsyncFunction: boolean = functionDeclNode.modifiers ? (functionDeclNode.modifiers.filter(n => n.kind === TS.SyntaxKind.AsyncKeyword).length === 1) : false;
        let jsDocComment: string = ambrosiaAttrs["JSDocComment"] as string; // The complete JSDoc comment containing the @ambrosia tag (sans the @ambrosia tag itself)

        if ((methodID !== IC.POST_METHOD_ID) && ambrosiaAttrs["doRuntimeTypeChecking"])
        {
            throw new Error(`The 'doRuntimeTypeChecking' attribute does not apply when a 'methodID' is provided in the @ambrosia tag at ${ambrosiaAttrs["location"]}`);
        }

        try
        {
            const childNodes: TS.Node[] = functionDeclNode.getChildren();

            for (let i = 0; i < childNodes.length; i++)
            {
                // Get the parameters (if any)
                if ((childNodes[i].kind === TS.SyntaxKind.OpenParenToken) && (childNodes[i + 1].kind === TS.SyntaxKind.SyntaxList))
                {
                    const parameters: TS.ParameterDeclaration[] = childNodes[i + 1].getChildren().filter(n => n.kind === TS.SyntaxKind.Parameter) as TS.ParameterDeclaration[];
                    for (let p = 0; p < parameters.length; p++)
                    {
                        // Remove whitespace and any inline comments (eg. "/* Foo */" or "/** Bar */")
                        let param: string = parameters[p].getText().replace(/\s+/g, "").replace(/\/\*.*?\*\//g, ""); 

                        // Note: We replace a parameter that specifies a default value with an optional parameter [the function will behave the same when executed]
                        if (param.indexOf("=") !== -1)
                        {
                            param = param.split("=")[0].replace(":", "?: ");
                        }
                        methodParams.push(param);
                    }
                }

                // Get the return type (if any)
                if ((childNodes[i].kind === TS.SyntaxKind.CloseParenToken) && (childNodes[i + 1].kind === TS.SyntaxKind.ColonToken))
                {
                    returnType = childNodes[i + 2].getText();
                    if (isAsyncFunction)
                    {
                        const matches: RegExpExecArray = RegExp(/Promise\s*<(.+?)>/g).exec(returnType);
                        if ((matches || []).length !== 2)
                        {
                            throw new Error(`The function '${methodName}' is async but does not return a Promise`);
                        }
                    }
                    break;
                }
            }    

            if (isPostMethod)
            {
                publishPostMethod(methodName, version, methodParams, returnType, doRuntimeTypeChecking, { nsPath: AST.getCurrentNamespacePath(), isAsync: isAsyncFunction, jsDocComment: jsDocComment });
                return ("post method");
            }
            else
            {
                publishMethod(methodID, methodName, methodParams, { nsPath: AST.getCurrentNamespacePath(), isAsync: isAsyncFunction, jsDocComment: jsDocComment });
                return ("non-post method");
            }
        }
        catch (error)
        {
            throw new Error(`Unable to publish ${nodeName} (at ${location}) as a ${isPostMethod ? "post " : ""}method (reason: ${(error as Error).message})`);
        }
    }
}