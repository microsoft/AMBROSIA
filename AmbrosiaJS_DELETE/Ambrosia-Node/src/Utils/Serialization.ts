// Module for custom JSON serialization/deserialization.
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "../Configuration";
import * as Utils from "../Utils/Utils-Index";

const CUSTOM_SERIALIZATION_PREFIX: string = "{__ACS__}"; // Note: This MUST start with "{" in order for the produced JSON to not be treated as 'raw' bytes by the IncomingRPC constructor

/** 
 * Custom JSON serializer that preserves the type of typed-arrays (eg. Uint8Array) and BigInt.
 * Note: Typed-arrays and BigInt will only be preserved between JS language bindings (not cross-language), 
 *       but only if 'allowCustomSerialization' is set to true.
 */
export function jsonStringify(value: any, objectExpected: boolean = true, allowCustomSerialization: boolean = Configuration.loadedConfig().lbOptions.allowCustomJSONSerialization): string
{
    // Local helper function
    function replacer(key: string, value: any): any
    {
        let typedArrayName: string = null;
        if (value instanceof Int8Array) { typedArrayName = "Int8Array"; } else
        if (value instanceof Uint8Array) { typedArrayName = "Uint8Array"; } else
        if (value instanceof Uint8ClampedArray) { typedArrayName = "Uint8ClampedArray"; } else
        if (value instanceof Int16Array) { typedArrayName = "Int16Array"; } else
        if (value instanceof Uint16Array) { typedArrayName = "Uint16Array"; } else
        if (value instanceof Int32Array) { typedArrayName = "Int32Array"; } else
        if (value instanceof Uint32Array) { typedArrayName = "Uint32Array"; } else
        if (value instanceof Float32Array) { typedArrayName = "Float32Array"; } else
        if (value instanceof Float64Array) { typedArrayName = "Float64Array"; } else
        if (value instanceof BigInt64Array) { typedArrayName = "BigInt64Array"; } else
        if (value instanceof BigUint64Array) { typedArrayName = "BigUint64Array"; }

        if (typedArrayName)
        {
            return (`${typedArrayName}[${value.toString()}]`);
        }
        else
        {
            if ((typeof value === "bigint") || (value instanceof BigInt))
            {
                return (`${value.toString()}n`);
            }
            return (value);
        }
    }

    // Local helper function
    function checkObjectTree(o: object): boolean
    {
        for (let propName in o)
        {
            let value: any = o[propName];
            if ((typeof value === "object") && (value !== null))
            {
                if (checkObjectTree(value))
                {
                    return (true);
                }
            }
            else
            {
                let requiresCustomJSON: boolean = 
                    ((typeof value === "bigint") || (value instanceof BigInt)) ||
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
                    (value instanceof BigUint64Array);

                if (requiresCustomJSON)
                {
                    return (true);
                }
            }
        }
        return (false);
    }

    // Using a 'replacer' function in JSON.stringify() is expensive (eg. ~5x slower) because it's called not only for each property, but also for EACH item in any array.
    // Consequently, we check whether the object contains any properties [or array items] that are of a type that requires using the 'replacer' at all.
    // This check itself is expensive, but - thanks to early termination and fewer total function calls - it's usually considerably less expensive (2 to 3x) than [unnecessarily] using 'replacer'.
    let customSerializerRequired: boolean = allowCustomSerialization && checkObjectTree(value);
    let json: string = JSON.stringify(value, customSerializerRequired ? replacer : null);
    if (objectExpected && (json[0] !== "{"))
    {
        // Note: When serializing method parameters, the generated JSON MUST start with "{" in order for it to not be treated as 'raw' bytes by the IncomingRPC constructor        
        throw new Error("The value to be serialized to JSON is not an object; either supply an object, or set 'objectExpected' to false");
    }
    if (customSerializerRequired)
    {
        return (CUSTOM_SERIALIZATION_PREFIX + json);
    }
    return (json);
}

/** 
 * Custom JSON deserializer that preserves the type of typed-arrays (eg. Uint8Array) and BigInt.
 * Note: Typed-arrays and BigInt will only be preserved between JS language bindings (not cross-language).
 */
export function jsonParse(text: string): any
{
    function reviver(key: string, value: any): any
    {
        let isString = (o: any): o is string => ((typeof value === "string") || (value instanceof String)); // Local 'type guard' function
        
        if (isString(value) && (value.indexOf("Array[") > 0))
        {
            if (value.indexOf("Int8Array[") === 0) { return (Int8Array.from(JSON.parse(value.substring(9)))) } else
            if (value.indexOf("Uint8Array[") === 0) { return (Uint8Array.from(JSON.parse(value.substring(10)))) } else
            if (value.indexOf("Uint8ClampedArray[") === 0) { return (Uint8ClampedArray.from(JSON.parse(value.substring(17)))) } else
            if (value.indexOf("Int16Array[") === 0) { return (Int16Array.from(JSON.parse(value.substring(10)))) } else
            if (value.indexOf("Uint16Array[") === 0) { return (Uint16Array.from(JSON.parse(value.substring(11)))) } else
            if (value.indexOf("Int32Array[") === 0) { return (Int32Array.from(JSON.parse(value.substring(10)))) } else
            if (value.indexOf("Uint32Array[") === 0) { return (Uint32Array.from(JSON.parse(value.substring(11)))) } else
            if (value.indexOf("Float32Array[") === 0) { return (Float32Array.from(JSON.parse(value.substring(12)))) } else
            if (value.indexOf("Float64Array[") === 0) { return (Float64Array.from(JSON.parse(value.substring(12)))) } else
            if (value.indexOf("BigInt64Array[") === 0) { return (BigInt64Array.from(value.substring(14, value.length - 1).split(",").map(BigInt))) } else
            if (value.indexOf("BigUint64Array[") === 0) { return (BigUint64Array.from(value.substring(15, value.length - 1).split(",").map(BigInt))) }
            return (value);
        }
        else
        {
            if (isString(value) && (value[value.length - 1] === "n") && /^-?\d+n$/.test(value))
            {
                return (BigInt(value.substring(0, value.length - 1)));
            }
            return (value);
        }
    }

    // Note: Only if 'text' comes from jsonStringify() will it start with CUSTOM_SERIALIZATION_PREFIX [so, for example, if 'text' comes from the C# LB it will never have this prefix]
    let customDeserializerRequired: boolean = (text.substr(0, CUSTOM_SERIALIZATION_PREFIX.length) === CUSTOM_SERIALIZATION_PREFIX); 
    return (JSON.parse(customDeserializerRequired ? text.substr(CUSTOM_SERIALIZATION_PREFIX.length) : text, customDeserializerRequired ? reviver : null));
}

/** This is for testing the performance of our custom JSON serialization [jsonStringify()/jsonParse()], including vs native JSON serialization. */
export function testJsonSerializationPerf()
{
    interface IIndexable { [index: number]: any; length: number };

    // Local helper function
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

/** This is for testing jsonStringify() and jsonParse() with typed-arrays (eg. Uint8Array) and BigInt. */
function testJsonCustomSerialization()
{
    const o: object = 
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
    const d: object = Utils.jsonParse(s);

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
    Utils.log("Custom JSON serialization/deserializaton test passed");
}
