// Module for custom JSON serialization/deserialization.
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "../Configuration";
import * as Meta from "../Meta";
import * as Utils from "../Utils/Utils-Index";

// NOTE:
// We wrote our own custom JSON serialization rather than using an existing npm package (like https://www.npmjs.com/package/json2typescript) because we 
// wanted to retain control over serialization for performance, interoperability (with LB's in other languages), and because of the close relationship 
// between serialization and code-gen (for example, deciding what TS language elements are publishable and therefore must be serializable).
// We also wanted to avoid the risks involved in taking a third-party dependency after this burned the original C# LB (to enable Async methods).
// If a developer wants to use a third-party serializer, they can still do this (albeit in a compromised way) by "tunnelling" the serialized data 
// through a 'raw byte' Uint8Array (for method parameters and appState) and foregoing the use of published types.

const CUSTOM_SERIALIZATION_PREFIX: string = "{__ACSv1__}"; // Note: This MUST start with "{" in order for the produced JSON to not be treated as 'raw' bytes by the IncomingRPC constructor
const BYTE_HEX_VALUES: string[] = [...Array(256)].map((v, i) => (i < 16 ? "0" : "") + i.toString(16)); // ["00".."FF"] Used for fast number-to-hex conversion of a byte value (0..255)

/** Returns true if the specified value requires custom serialization. */
function requiresCustomJSON(value: any): boolean
{
    const _value: any = (value instanceof Object) ? value.valueOf() : value;
    const requiresCustomJSON: boolean = 
        (typeof _value === "bigint") ||
        ((typeof _value === "number") && (isNaN(value) || !isFinite(value))) || // NaN, Infinity or -Infinity
        (value instanceof Date) ||
        (value instanceof Int8Array) ||
        (value instanceof Uint8Array) ||
        (value instanceof Uint8ClampedArray) ||
        (value instanceof Int16Array) ||
        (value instanceof Uint16Array) ||
        (value instanceof Int32Array) ||
        (value instanceof Uint32Array) ||
        (value instanceof Float32Array) ||
        (value instanceof Float64Array) ||
        (value instanceof BigInt64Array) ||
        (value instanceof BigUint64Array) ||
        (value instanceof Set) ||
        (value instanceof Map) ||
        (value instanceof Error);

    return (requiresCustomJSON);
}

let _jsonStringifyRecursionDepth: number = 0; // Flag used to prevent running checkForCircularReferences() more than once when using jsonStringify(); safe to use because JS is single-threaded

/** 
 * Converts the supplied Uint8Array into a string made up of 2-character hexidecimal values (for example, [3, 127] becomes "037f").
 * To decode the produced string, use Uint8ArrayFromHexString().\
 * Note: This is up to 30% slower than Uint8Array.toString(), but the string it produces is up to 50% smaller (44% smaller on average).
 */
export function Uint8ArrayToHexString(byteArray: Uint8Array): string
{
    const hexValues: string[] = [];
    for (let i = 0; i < byteArray.length; i++)
    {
        hexValues.push(BYTE_HEX_VALUES[byteArray[i]]);
    }
    return (hexValues.join(""));
}

/**
 * Converts the supplied hexidecimal string (eg. "037f"), produced by Uint8ArrayToHexString(), back into a Uint8Array.
 * This is up to 40% faster than decoding from a JSON encoded array, ie. Uint8Array.from(JSON.parse(encoded)).
 */
export function Uint8ArrayFromHexString(hex: string): Uint8Array
{
    if (!hex || (hex.length === 0))
    {
        return (new Uint8Array());
    }
    if (hex.length % 2 !== 0)
    {
        throw new Error(`The supplied 'hex' string is of length ${hex.length} when an even number was expected`);
    }

    const byteArray: Uint8Array = new Uint8Array(hex.length / 2);
    for (let i = 0; i < byteArray.length; i++)
    {
        const pos = i * 2;
        byteArray[i] = parseInt(hex[pos] + hex[pos + 1], 16);
    }
    return (byteArray);
}

/** 
 * Custom JSON serializer that preserves the type of typed-arrays (eg. Uint8Array) and BigInt, Set, Map, Date, RegExp and Error objects.\
 * Objects with literal forms will be converted to those literal forms (eg. new String("Test") will become "Test", BigInt("123") will become 123n).\
 * For non-built-in objects, any object property descriptors (eg. 'writable') will be lost.\
 * Note: Typed-arrays, BigInt, Set, Map, Date, RegExp and Error objects will only be preserved between JS language bindings (not cross-language), but only if 'allowCustomSerialization' is true.\
 * Note: Array element with the value 'undefined' will become null. Members with the value 'undefined' will be removed.\
 * Note: Properties named using Symbols, and any prototype-inherited properties, will not be serialized (which is standard JSON.stringify() behavior
 *       [see https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Symbol#symbols_and_json.stringify 
 *        and https://stackoverflow.com/questions/8779249/how-to-stringify-inherited-objects-to-json])
 */
export function jsonStringify(value: any, objectExpected: boolean = true, allowCustomSerialization: boolean = Configuration.loadedConfig().lbOptions.allowCustomJSONSerialization): string
{
    /** [Local function] Returns either a custom serialization string for the supplied value (based on its type), or the unaltered value. */
    function replacer(key: string, value: any): any
    {
        let typedArrayName: string = "";
        if (value instanceof Int8Array) { typedArrayName = "##Int8Array##"; } else
        if (value instanceof Uint8Array) { typedArrayName = "##Uint8Array##"; } else
        if (value instanceof Uint8ClampedArray) { typedArrayName = "##Uint8ClampedArray##"; } else
        if (value instanceof Int16Array) { typedArrayName = "##Int16Array##"; } else
        if (value instanceof Uint16Array) { typedArrayName = "##Uint16Array##"; } else
        if (value instanceof Int32Array) { typedArrayName = "##Int32Array##"; } else
        if (value instanceof Uint32Array) { typedArrayName = "##Uint32Array##"; } else
        if (value instanceof Float32Array) { typedArrayName = "##Float32Array##"; } else
        if (value instanceof Float64Array) { typedArrayName = "##Float64Array##"; } else
        if (value instanceof BigInt64Array) { typedArrayName = "##BigInt64Array##"; } else
        if (value instanceof BigUint64Array) { typedArrayName = "##BigUint64Array##"; }

        if (typedArrayName)
        {
            if (value instanceof Uint8Array)
            {
                // Special case: optimized for both smaller string size (up to 50% smaller), and lower total [encode/decode] execution time (~15% faster)
                return (`${typedArrayName}${Utils.Uint8ArrayToHexString(value)}`); // Note: No square brackets around the value
            }
            else
            {
                return (`${typedArrayName}[${value.toString()}]`);
            }
        }
        else
        {
            switch (typeof ((value instanceof Object) ? value.valueOf() : value))
            {
                case "bigint":
                    return (`${value.toString()}n`);
                case "number":
                    // Note: Without this, NaN, Infinity and -Infinity would all serialize to null
                    if (isNaN(value)) { return ("##NaN##"); };
                    if (!isFinite(value)) { return (value > 0 ? "##Infinity##" : "##-Infinity##"); }
                    break;
                case "string":
                    // Note: We can't simply check "value instanceof Date" because JSON.stringify() has already converted the Date to a string
                    if ((value.length === 24) && value.endsWith("Z") && !isNaN(new Date(value).valueOf()))
                    {
                        return (`##Date##${value}`);
                    }
                    break;
                case "object":
                    // Note: WeakSet objects are not supported because they are not iterable
                    if (value instanceof Set)
                    {
                        return (`##Set##${jsonStringify([...value], false)}`);
                    }
                    // Note: WeakMap objects are not supported because they are not iterable
                    if (value instanceof Map)
                    {
                        return (`##Map##${jsonStringify([...value], false)}`);
                    }
                    if (value instanceof RegExp)
                    {
                        return (`##RegExp##${value.toString()}`);
                    }
                    if (value instanceof Error)
                    {
                        return (`##Error##${jsonStringify({name: value.name, message: value.message, stack: value.stack})}`);
                    }
                    break;
            }

            // Note: We cannot preserve undefined (either as an array element value or as a property value) because JSON.parse() ALWAYS removes all undefined values.
            //       So undefined array elements will become null (which is the default JSON.stringify() behavior); if we tried to preserve them using a "##undefined##"
            //       token, the parsed array would end up with "holes" [missing indicies] where the undefined elements previously were.
            return (value);
        }
    }

    /** [Local function] Returns true if any member [including array element values] is of a type that requires custom JSON serialization. */
    function checkObjectTree(o: Utils.SimpleObject): boolean
    {
        if (requiresCustomJSON(o))
        {
            return (true);
        }

        // Note: When o is an array (including Uint8Array, etc.), propName will be the index, so - in the worst case - we will end up walking ALL array elements
        for (let propName in o) 
        {
            // JSON.stringify() doesn't serialize prototype-inherited properties, so we can skip those [see: https://stackoverflow.com/questions/8779249/how-to-stringify-inherited-objects-to-json]
            if (!Object.prototype.hasOwnProperty.call(o, propName)) // Using Object.prototype because o.hasOwnProperty() can be redefined (shadowed)
            {
                continue;
            }

            const value: any = o[propName];

            if (value !== null)
            {
                // Note: A simple "if (checkObjectTree(value)) { return (true); }" would suffice here, but we include an optimization for
                //       the common case of a homogenous array of non-objects, eg. string[], by not calling checkObjectTree() for every 
                //       element and instead calling requiresCustomJSON() directly (thus skipping an extra function call for each element).
                if (typeof value === "object")
                {
                    if (checkObjectTree(value))
                    {
                        return (true);
                    }
                }
                else
                {
                    if (requiresCustomJSON(value))
                    {
                        return (true);
                    }
                }
            }
        }
        return (false);
    }

    try
    {
        // Any object (like NodeJS.Timeout) that has circular references between members will result in a 'RangeError: Maximum call stack size exceeded' in checkObjectTree().
        // So to prevent this we first explicitly check for circular references.
        try
        {
            // checkForCircularReferences() is exhaustive, so we don't need to run it more than once. Further, by only calling
            // it once at the start, any reported error will have the context of the root object, not some nested object.
            if (_jsonStringifyRecursionDepth++ === 0)
            {
                // This will throw if it finds a circular reference or an unsupported object type (eg. WeakSet)                
                checkForCircularReferences(value, true);
            }
        }
        catch (error: unknown)
        {
            throw new Error(`Unable to serialize object to JSON (reason: ${Utils.makeError(error).message})`);
        }

        // Using a 'replacer' function in JSON.stringify() is expensive (eg. ~5x slower) because it's called not only for each property, but also for EACH item in any array.
        // Consequently, we check whether the object contains any properties [or array items] that are of a type that requires using the 'replacer' at all.
        // This check itself is expensive, but - thanks to early termination and fewer total function calls - it's usually considerably less expensive (2 to 3x) than [unnecessarily] using 'replacer'.
        let customSerializerRequired: boolean = allowCustomSerialization && checkObjectTree(value);
        let json: string = JSON.stringify(value, customSerializerRequired ? replacer : undefined);
        if (objectExpected && ((json === undefined) || (json[0] !== "{")))
        {
            // Note: When serializing method parameters, the generated JSON MUST start with "{" in order for it to NOT be treated as 'raw' bytes by the IncomingRPC constructor
            throw new Error("The value to be serialized to JSON is not an object; either supply an object, or set 'objectExpected' to false");
        }
        if (customSerializerRequired)
        {
            return (CUSTOM_SERIALIZATION_PREFIX + json);
        }
        return (json);
    }
    finally
    {
        _jsonStringifyRecursionDepth--;
    }
}

/** 
 * Custom JSON deserializer that re-creates an object (or primitive value) from the supplied serialization string [produced by jsonStringify()].\
 * See jsonStringify() for the serialization capabilities and limitations.
 */
export function jsonParse(text: string): any
{
    function reviver(key: string, value: any): any
    {
        let isString = (o: any): o is string => ((typeof value === "string") || (value instanceof String)); // Local 'type guard' function
        
        if (isString(value) && /^##[A-Za-z0-9]+Array##\[?/.test(value)) // Note: 'value' can be a complex type string that includes, for example, "Int8Array[" at a location other than the start
        {
            if (value.indexOf("##Int8Array##[") === 0) { return (Int8Array.from(JSON.parse(value.substring(13)))) } else
            if (value.indexOf("##Uint8Array##") === 0) { return (Utils.Uint8ArrayFromHexString(value.substring(14))) } else // Note: No square brackets around the value
            if (value.indexOf("##Uint8ClampedArray##[") === 0) { return (Uint8ClampedArray.from(JSON.parse(value.substring(21)))) } else
            if (value.indexOf("##Int16Array##[") === 0) { return (Int16Array.from(JSON.parse(value.substring(14)))) } else
            if (value.indexOf("##Uint16Array##[") === 0) { return (Uint16Array.from(JSON.parse(value.substring(15)))) } else
            if (value.indexOf("##Int32Array##[") === 0) { return (Int32Array.from(JSON.parse(value.substring(14)))) } else
            if (value.indexOf("##Uint32Array##[") === 0) { return (Uint32Array.from(JSON.parse(value.substring(15)))) } else
            if (value.indexOf("##Float32Array##[") === 0) { return (Float32Array.from(JSON.parse(value.substring(16)))) } else
            if (value.indexOf("##Float64Array##[") === 0) { return (Float64Array.from(JSON.parse(value.substring(16)))) } else
            if (value.indexOf("##BigInt64Array##[") === 0) { return (BigInt64Array.from(value.substring(18, value.length - 1).split(",").map(BigInt))) } else
            if (value.indexOf("##BigUint64Array##[") === 0) { return (BigUint64Array.from(value.substring(19, value.length - 1).split(",").map(BigInt))) }
            return (value);
        }
        else
        {
            if (isString(value))
            {
                if ((value[value.length - 1] === "n") && /^-?\d+n$/.test(value))
                {
                    return (BigInt(value.substring(0, value.length - 1)));
                }

                if (value.startsWith("##"))
                {
                    switch (value)
                    {
                        case "##NaN##":
                            return (NaN);
                        case "##Infinity##":
                            return (Infinity);
                        case "##-Infinity##":
                            return (-Infinity);
                    }

                    if (value.startsWith("##Date##") && (value.length > 8))
                    {
                        return (new Date(value.substring(8, value.length)));
                    }
                    if (value.startsWith("##Set##") && (value.length > 7))
                    {
                        const elementArray: string = value.substring(7, value.length);
                        return (new Set(jsonParse(elementArray)));
                    }
                    if (value.startsWith("##Map##") && (value.length > 7))
                    {
                        const kvElementsArray: string = value.substring(7, value.length);
                        return (new Map(jsonParse(kvElementsArray)));
                    }
                    if (value.startsWith("##RegExp##") && (value.length > 10))
                    {
                        const re: string = value.substring(10, value.length);
                        const matchResults: RegExpMatchArray | null = re.match(/\/(.*)\/(.*)?/);
                        if (!matchResults)
                        {
                            throw new Error(`Malformed serialized RegExp ("${re}")`);
                        }
                        const body: string = matchResults[1];
                        const flags: string = matchResults[2] || "";
                        return (new RegExp(body, flags));
                    }
                    if (value.startsWith("##Error##") && (value.length > 9))
                    {
                        const error: Error = jsonParse(value.substring(9, value.length));
                        const newError = new Error(error.message);
                        newError.name = error.name;
                        newError.stack = error.stack;
                        return (newError);
                    }
                }
            }
            return (value);
        }
    }

    if (text === undefined)
    {
        return (undefined);
    }

    // Note: Only if 'text' comes from jsonStringify() will it start with CUSTOM_SERIALIZATION_PREFIX [so, for example, if 'text' comes from the C# LB it will never have this prefix]
    let customDeserializerRequired: boolean = (text.substr(0, CUSTOM_SERIALIZATION_PREFIX.length) === CUSTOM_SERIALIZATION_PREFIX); 
    return (JSON.parse(customDeserializerRequired ? text.substr(CUSTOM_SERIALIZATION_PREFIX.length) : text, customDeserializerRequired ? reviver : undefined));
}

/** The names of all types that we support serialization for (cached for speed). */
const _supportedTypes: string[] = [...Meta.Type.getSupportedNativeTypes()];

/** [Internal] All the strings that can be used (in the serialization format) as a token for either a built-in constructed object or a special value. */
export const _serializationTokens: string[] = 
[
    "##Map##", "##Set##", "##Date##", "##Error##", "##RegExp##", "##NaN##", "##Infinity##", "##-Infinity##", 
    "##Int8Array##", "##Uint8Array##", "##Uint8ClampedArray##", "##Int16Array##", "##Uint16Array##",
    "##Int32Array##", "##Uint32Array##", "##Float32Array##", "##Float64Array##", "##BigInt64Array##", "##BigUint64Array##"
];

/** 
 * [Internal] Throws if the specified object has members [or array elements] that form a circular reference. 
 * If 'checkForSerializability' is true, also throws if a type is encountered which Ambrosia doesn't serialize.
 */
// Adapted from https://stackoverflow.com/questions/14962018/detecting-and-fixing-circular-references-in-javascript
// Preliminary testing indicates that this is about 20% slower than JSON.stringify() to detect circular references.
export function checkForCircularReferences(obj: object, checkForSerializability: boolean = false): void
{
    const supportedTypes: string[] = !checkForSerializability? [] : _supportedTypes;
    const keyStack: string[] = [];
    const objectStack: Set<object> = new Set<object>();
    let errorMsg: string = "";

    /** [Local function] Returns the path (and description) of 'key'. */
    function getKeyPath(key: string): { path: string, keyDescription: string }
    {
        let objectChain: object[] = [...objectStack];
        let keyPath: string = keyStack.join(".") + (keyStack.length > 0 ? "." : "") + key;
        let keyName: string = (keyStack.length > 0) ? "a property" : "a value";

        if (objectChain[objectChain.length - 1] instanceof Set)
        {
            keyPath = `${keyStack.join(".")}[${key}]`;
            keyName = "a Set entry";
        }
        else
        {
            // Note: We must check for Map before checking for Array because a Map's members are always 2-element [key, value] arrays
            if ((objectChain.length >= 2) && (objectChain[objectChain.length - 2] instanceof Map))
            {
                const mapPart: string = (key === "0") ? "key" : "value";
                keyPath = `${keyStack.slice(0, -1).join(".")}[${keyStack[keyStack.length - 1]}].${mapPart}`;
                keyName = `a Map ${mapPart}`;
            }
            else
            {
                if (objectChain[objectChain.length - 1] instanceof Array)
                {
                    keyPath = `${keyStack.join(".")}[${key}]`;
                    keyName = "an array element";
                }
            }
        }

        return ({ path: keyPath, keyDescription: keyName });
    }

    /** [Local function] Returns true if 'obj' appears anywhere up the current objectStack chain. */
    function detectCircularReference(obj: object, key: string): boolean
    {
        if ((obj !== null) && (typeof obj !== "object")) // Note: If obj is an array, 'typeof obj' will still return "object"
        { 
            return (false);
        }

        if (objectStack.has(obj)) // A Set object has O(1) lookup speed compared to O(N) for an array [Set.has() is about 5000x faster than Array.indexOf(), and Object[key] is 2x faster that Set.has()]
        { 
            // We found a [circular] reference to something earlier in the chain
            let objectChain: object[] = [...objectStack];
            let keyPath: { path: string, keyDescription: string } = getKeyPath(key);
            let referencerName: string = keyPath.path;
            let referencerDescription: string = keyPath.keyDescription;
            let referencedObjectIndex: number = objectChain.indexOf(obj);
            let referencedObjectName: string = keyStack.slice(0, referencedObjectIndex + 1).join(".");
            let referencedObjectType: string = obj.constructor.name;

            errorMsg = `${referencedObjectName} [${referencedObjectType}] has ${referencerDescription} (${referencerName}) that points back to ${referencedObjectName}, creating a circular reference`;
            return (true);
        }

        keyStack.push(key);
        objectStack.add(obj);

        // Recurse through the object's children [Note: 'obj' can also be an array, Set or Map], but don't check typed arrays (eg. Uint8Array) because they cannot contain values
        // that cause circular references.
        // Aside: For an object - but NOT an array [!(obj instanceof Array)] - we could sort the property names so that it matches what the [VS Code] debugger shows, like this:
        //   for (const k of [...Object.keys(obj)].sort()) { ... }
        // But this would have 2 downsides: 1) It would be slower, and 2) It would potentially yield an error mesasage that refers to different elements than the JSON.stringify()
        // error message would refer to [which, evidently, does no such sorting] making it harder to verify the correctness of checkForCircularReferences().
        if (!Meta.Type.isTypedArray(obj))
        {
            const targetObj: Utils.SimpleObject = ((obj instanceof Set) || (obj instanceof Map)) ? [...obj] : obj;
            for (const k in targetObj)
            {
                // JSON.stringify() doesn't serialize prototype-inherited properties, so we can skip those [see: https://stackoverflow.com/questions/8779249/how-to-stringify-inherited-objects-to-json]
                if (Object.prototype.hasOwnProperty.call(targetObj, k)) // Using Object.prototype because obj.hasOwnProperty() can be redefined (shadowed)
                { 
                    if (checkForSerializability)
                    {
                        checkCanSerialize(targetObj[k], k);
                    }
                    if (detectCircularReference(targetObj[k], k))
                    {
                        return (true);
                    }
                }
            }
        }

        keyStack.pop();
        objectStack.delete(obj);
        return (false);
    }

    /** [Local function] Throws if the supplied obj is not of a type that can be serialized, or if it has a [string] value that overlaps with any of our custom serialization tokens (##xxx##). */
    // Note: JSON.stringify() typically returns "{}" for objects that it doesn't serialize, but this "silent failure"
    //       approach makes it harder to find serialization issues, so we "fail fast" instead (at a performance cost).
    function checkCanSerialize(obj: object, currentKey: string): void
    {
        if (obj !== undefined)
        {
            const typeName: string = Meta.Type.getNativeType(obj);
            if (supportedTypes.indexOf(typeName) === -1)
            {
                const keyPath: { path: string, keyDescription: string } = getKeyPath(currentKey);
                throw new Error(`${keyPath.path} (${keyPath.keyDescription}) is of type '${typeName}' which is not supported for serialization`);
            }

            // Check if this is a string that's one of our "reserved" internal tokens (either a specifier for a built-in constructed object (eg. "##Map##"),
            // or a special value placeholder (eg. "##NaN##")).
            // Note: Technically this check is applied too liberally, but limiting it to apply only when truly needed would add unwanted complexity.
            //       Further, checking "greedily" allows us to more easily identify serialization problems (ie. a token being used in the wrong place).
            if ((typeName === "string") && ((obj as unknown) as string).startsWith("##"))
            {
                const value: string = (obj as unknown) as string;
                for (const token of _serializationTokens)
                {
                    if (value.startsWith(token))
                    {
                        const keyPath: { path: string, keyDescription: string } = getKeyPath(currentKey);
                        throw new Error(`Found ${keyPath.keyDescription} (${keyPath.path}) with a value ('${value}') which cannot be serialized because it's used as an internal token`);
                    }
                }
            }
        }
    }

    const startingObjName: string = `(${Meta.Type.getNativeType(obj)})`;
    if (checkForSerializability)
    {
        checkCanSerialize(obj, startingObjName);
    }
    if (detectCircularReference(obj, startingObjName))
    {
        throw new Error(errorMsg);
    }
}

/** This is for testing the performance of our custom JSON serialization [jsonStringify()/jsonParse()], including vs native JSON serialization. */
function testJsonSerializationPerf()
{
    interface IIndexable { [index: number]: any; length: number };

    /** [Local function] Throws if the supplied arrays are either different lengths, or contain elements that don't match when compared using '==='. */
    function compareArrays(arr1: IIndexable, arr2: IIndexable): void
    {
        if (arr1.length !== arr2.length)
        {
            throw new Error(`Arrays are not of the same length (${arr1.length} vs. ${arr2.length})`);
        }

        for (let i: number = 0; i < arr1.length; i++)
        {
            if (arr1[i] !== arr2[i])
            {
                throw new Error(`Array element ${i} does not match (${arr1[i]} vs. ${arr2[i]})`);
            }
        }
    }

    Utils.log("Starting JSON serialization/deserialization perf tests...");
    
    // let o: object = { p1: BigInt(1), p2: 123, p3: "Hello!", p4: true };
    // let d: object = jsonParse(jsonStringify(o));

    let itemCount: number = 1000 * 1000;
    let numArray: number[] = new Array(itemCount);
    let biArray: bigint[] = new Array(itemCount);
    let strArray: string[] = new Array(itemCount);

    for (let i = 0; i < itemCount; i++)
    {
        numArray[i] = Math.floor(Math.random() * itemCount);
        biArray[i] = BigInt(numArray[i]);
        strArray[i] = "*".repeat(Math.floor(Math.random() * 10))
    }
    let bi64Array: BigInt64Array = BigInt64Array.from(biArray as bigint[]);
    
    let startTime: number = Date.now();
    let o1: number[] = jsonParse(jsonStringify(numArray, false));
    let elapsedMs1 = Date.now() - startTime;
    compareArrays(numArray, o1);
    
    startTime = Date.now();
    let o2: bigint[] = jsonParse(jsonStringify(biArray, false));
    let elapsedMs2 = Date.now() - startTime;
    let percent2: number = (elapsedMs2 / elapsedMs1) * 100;
    compareArrays(biArray, o2);

    startTime = Date.now();
    let o3: BigInt64Array = jsonParse(jsonStringify(bi64Array, false));
    let elapsedMs3 = Date.now() - startTime;
    let percent3: number = (elapsedMs3 / elapsedMs1) * 100;
    compareArrays(bi64Array, o3);

    startTime = Date.now();
    let o4: string[] = JSON.parse(JSON.stringify(strArray));
    let elapsedMs4 = Date.now() - startTime;
    compareArrays(strArray, o4);

    startTime = Date.now();
    let o5: string[] = jsonParse(jsonStringify(strArray, false));
    let elapsedMs5 = Date.now() - startTime;
    let percent5: number = (elapsedMs5 / elapsedMs4) * 100;
    compareArrays(strArray, o5);

    startTime = Date.now();
    let o6: string[] = JSON.parse(JSON.stringify(numArray));
    let elapsedMs6 = Date.now() - startTime;
    let percent7: number = (elapsedMs1 / elapsedMs6) * 100;
    compareArrays(numArray, o6);

    Utils.log(`1) Number type perf test finished (${itemCount.toLocaleString()} items): ` + 
        `${elapsedMs1}ms for number[] (1x), ` + 
        `${elapsedMs2}ms for bigint[] (${(percent2 / 100).toFixed(2)}x), ` +
        `${elapsedMs3}ms for BigInt64Array (${(percent3 / 100).toFixed(2)}x)`);
    Utils.log(`2) String serialization comparision perf test finished (${itemCount.toLocaleString()} items): ` +
        `${elapsedMs4}ms for string[] using native JSON serialization (1x), ` + 
        `${elapsedMs5}ms for string[] using custom JSON serialization (${(percent5 / 100).toFixed(2)}x)`);
    Utils.log(`3) Number serialization comparision perf test finished (${itemCount.toLocaleString()} items): ` +
        `${elapsedMs6}ms for number[] using native JSON serialization (1x), ` + 
        `${elapsedMs1}ms for number[] using custom JSON serialization (${(percent7 / 100).toFixed(2)}x)`);

    Utils.log("JSON serialization/deserialization perf tests finished.");
}