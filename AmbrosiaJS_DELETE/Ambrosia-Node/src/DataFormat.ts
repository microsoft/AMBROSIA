// Module for encoding/decoding Ambrosia data types.
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Utils from "./Utils/Utils-Index";

/** The result of a readVarIntNN() method. */
export type varIntResult = 
{ 
    /** The number of bytes read. */
    length: number,
    /** The value of the integer. */
    value: number 
};

/** Converts a 32-bit integer into a 4-byte array (little-endian format). */
export function writeInt32Fixed(value: number): Uint8Array
{
    let bytes: Uint8Array = new Uint8Array(4);

    checkValueToEncode(value, 32);

    // Encode as little-endian
    // Note: This is ~25% faster than "Buffer.alloc(4).writeInt32LE(value)"
    bytes[0] = value & 0xff;
    bytes[1] = (value >> 8) & 0xff;
    bytes[2] = (value >> 16) & 0xff;
    bytes[3] = (value >> 24);

    return (bytes);
}

/** Reads a 32-bit integer from 4 bytes (little-endian format) of the specified byte array. */
export function readInt32Fixed(bytes: Uint8Array | Buffer, startIndex: number = 0): number
{
    let result: number = 0;

    if (bytes.length - startIndex < 4)
    {
        throw new Error(`The value to decode (${bytes.length} bytes) at startIndex ${startIndex} is invalid: it must contain at least 4 bytes`);
    }

    // Decode as little-endian
    // Note: This is ~13x faster than doing "new DataView(bytes.buffer).getInt32(0, true)" which, in turn, is faster than "Buffer.from(bytes).readInt32LE(0)"
    result |= bytes[startIndex];
    result |= bytes[startIndex + 1] << 8;
    result |= bytes[startIndex + 2] << 16;
    result |= bytes[startIndex + 3] << 24;

    return (result);
}

const MIN_64_BIT_VALUE: bigint = -BigInt(Math.pow(2, 63));
const MAX_64_BIT_VALUE: bigint = BigInt(Math.pow(2, 63)) - BigInt(1);

/** 
 * Converts a 64-bit integer (bigint) into a 8-byte array (little-endian format).\
 * Note: This method is at least 25x slower than writeInt32Fixed(). 
 */
export function writeInt64Fixed(value: bigint): Uint8Array
{
    if ((value < MIN_64_BIT_VALUE) || (value > MAX_64_BIT_VALUE))
    {
        throw new Error(`The supplied value (${value}) is larger than 64-bits`);
    }
    let bytes: Buffer = Buffer.alloc(8);
    bytes.writeBigInt64LE(value);
    return (bytes);
}

/** 
 * Reads a 64-bit integer from 8 bytes (little-endian format) of the specified byte array.\
 * Note: This method is at least 25x slower than readInt32Fixed(). 
 */
export function readInt64Fixed(bytes: Uint8Array | Buffer, startIndex: number = 0): bigint
{
    let result: bigint;

    if (bytes.length - startIndex < 8)
    {
        throw new Error(`The value to decode (${bytes.length} bytes) at startIndex ${startIndex} is invalid: it must contain at least 8 bytes`);
    }

    if (bytes instanceof Buffer)
    {
        // This is about 40% faster than the 'else' technique, and avoids the creation of both a new Uint8Array and Buffer
        result = (bytes as Buffer).readBigInt64LE(startIndex);
    }
    else
    {
        let buffer: Buffer = Buffer.from(bytes.slice(startIndex, startIndex + 8));
        result = buffer.readBigInt64LE(0);
    }

    return (result);
}

/** Converts a number into an 8-byte array (little-endian format). This can only handle signed integers up to 54-bits. */
export function writeInt64FixedNumber(value: number): Uint8Array
{
    checkValueToEncode(value, 54);
    return (writeInt64Fixed(BigInt(value)));
}

/** Reads a [integer] number from 8 bytes (little-endian format) of the specified byte array. This can only handle signed integers up to 54-bits. */
export function readInt64FixedNumber(bytes: Uint8Array, startIndex: number = 0): number
{
    let value: bigint = readInt64Fixed(bytes, startIndex);
    let minInt: bigint = BigInt(Number.MIN_SAFE_INTEGER); // Same as -BigInt(Math.pow(2, 53)) + BigInt(1)
    let maxInt: bigint = BigInt(Number.MAX_SAFE_INTEGER); // Same as BigInt(Math.pow(2, 53)) - BigInt(1)
    
    if ((value < minInt) || (value > maxInt))
    {
        throw new Error(`The value to decode (${value}) exceeds the range of a 54-bit signed integer (${minInt} to ${maxInt})`);
    }
    else
    {
        return (Number(value));
    }
}

const _2Powers: number[] = 
[
    1,
    Math.pow(2, 8 * 1), // 256
    Math.pow(2, 8 * 2), // 65536
    Math.pow(2, 8 * 3), // 16777216
    Math.pow(2, 8 * 4), // 4294967296
    Math.pow(2, 8 * 5), // 1099511627776
    Math.pow(2, 8 * 6), // 281474976710656
    0 // Unusable: Number.MAX_SAFE_INTEGER is 9007199254740991
];

/** 
 * Converts a [integer] number into an 8-byte array (little-endian format).
 * Note: This can only handle signed integers up to 52-bits. This method is also about 150x slower than writeInt32Fixed(), especially for negative values. 
 * Note: This method does not use the Buffer class.
 */
export function writeInt64FixedNumber_NoBuffer(value: number): Uint8Array
{
    let bytes = new Uint8Array(8);
    let remainingValue: number = value;

    checkValueToEncode(value, 52);

    // Encode as little-endian without using roll left/right or other bitwise operators (because these operators automatically cast operands to 32-bit values)
    if (value < 0)
    {
        // "Slow" encoding for negative values [due to lack of native support for Int64 in JavaScript]
        // Note: 'Slow' means ~50x slower than encoding positive values
        let binary: string = value.toString(2).substr(1); // Convert to a binary string and remove the leading "-" sign

        // Create 2's-complement (flip bits and add 1)
        // Note: We prefix the flipped result with "1" to preserve the leading zeros, ie. the bit count (eg. -1052818365); otherwise when we left-pad with 1's later on we'll add too many
        // Note: Use the Windows 10 calculator in 'Programmer' mode to show the ground truth for a negative number's binary QWORD representation (in 2's-complement)
        let flipped: string = "1" + binary.split("").map(c => c === "0" ? "1" : "0").join(""); 
        let twosComplement: number = Number.parseInt(flipped, 2) + 1;
        let tcBinary: string = twosComplement.toString(2);

        if (tcBinary.length < 64)
        {
            tcBinary = "1".repeat(64 - tcBinary.length) + tcBinary;
        }

        for (let pos = tcBinary.length - 8, i = 0; pos >= 0; pos -= 8, i++)
        {
            let byte: number = parseInt(tcBinary.substr(pos, 8), 2);
            bytes[i] = byte;
        }
    }
    else
    {
        // "Fast" encoding for positive values [because we don't have to deal with 2's-complement encoding as we do for negative values]
        bytes[7] = 0; // Unusable: Number.MAX_SAFE_INTEGER is 9007199254740991
        for (let i = 6; i >= 0 ; i--)
        {
            bytes[i] = remainingValue / _2Powers[i];
            remainingValue = remainingValue % _2Powers[i]; 
        }
    }

    return (bytes);
}

/** 
 * Reads a [integer] number from 8 bytes (little-endian format) of the specified byte array.
 * Note: This can only handle signed integers up to 52-bits. This method is also about 150x slower than readInt32Fixed(), especially for negative values. 
 * Note: This method does not use the Buffer class.
 */
export function readInt64FixedNumber_NoBuffer(bytes: Uint8Array): number
{
    if (bytes.length !== 8)
    {
        throw new Error(`The value to decode (${bytes}) is invalid: it must contain exactly 8 bytes`);
    }

    let result: number = 0;
    let isNegativeValue: boolean = (bytes[7] & 128) === 128;

    // Decode as little-endian without using roll left/right or other bitwise operators (because these operators automatically cast operands to 32-bit values)
    if (isNegativeValue)
    {
        // "Slow" decoding for negative values [due to lack of native support for Int64 in JavaScript]
        // Note: 'Slow' means ~50x slower than decoding positive values
        if ((bytes[6] < 248 ) || ( bytes[7] !== 255))
        {
            throw new Error(`The value to decode (${bytes}) is invalid: bytes[6] and/or bytes[7] indicate the encoded value is too small (less than -2^51) to be decoded`);
        }

        let binary: string = "";

        for (let i = 0; i < bytes.length; i++)
        {
            let binaryByte: string = bytes[i].toString(2);
            if (binaryByte.length < 8)
            {
                binaryByte = "0".repeat(8 - binaryByte.length) + binaryByte;
            }
            binary = binaryByte + binary;
        }

        // Reverse 2's-complement encoding (flip bits and subtract 1)
        let flipped: string = binary.split("").map(c => c === "0" ? "1" : "0").join("");
        result = -Number.parseInt(flipped, 2) - 1;
    }
    else
    {
        // "Fast" decoding for positive values [because we don't have to deal with 2's-complement encoding as we do for negative values]
        if ((bytes[6] > 7) || ( bytes[7] !== 0))
        {
            throw new Error(`The value to decode (${bytes}) is invalid: bytes[6] and/or bytes[7] indicate the encoded value is too large (greater than 2^51 - 1) to be decoded`);
        }

        for (let i = 0; i < bytes.length; i++)
        {
            result += bytes[i] * _2Powers[i];
        }
    }

    return (result);
}

/** Reads a VarInt32 from the specified byte array. */    
export function readVarInt32(bytes: Uint8Array | Buffer, startIndex: number = 0): varIntResult
{
    return (readVarInt(bytes, startIndex, 32));
}

/** 
 * Reads a VarInt64 from the specified byte array.\
 * Note: This can only handle signed integers up to 53-bits, and is ~2x slower than readVarInt32(). 
 */
export function readVarInt64(bytes: Uint8Array | Buffer, startIndex: number = 0): varIntResult
{
    return (readVarInt(bytes, startIndex, 53));
}

const _2Powers7Bits: number[] = 
[
    1,
    Math.pow(2, 7 * 1), // 128
    Math.pow(2, 7 * 2), // 16384
    Math.pow(2, 7 * 3), // 2097152
    Math.pow(2, 7 * 4), // 268435456
    Math.pow(2, 7 * 5), // 34359738368
    Math.pow(2, 7 * 6), // 4398046511104
    Math.pow(2, 7 * 7), // 562949953421312
    0 // Unusable: Number.MAX_SAFE_INTEGER is 9007199254740991
];

// A VarInt is a byte-encoding scheme [for integer values] where the MSB is used to indicate that the next byte is also part of the encoded value.
// The low 7-bits of a byte are value bits. Byte 0 stores the least significant 7-bits of the value, and the last byte (whose MSB will be 0) stores
// the most significant 7-bits. For example, to encoding 300 (‭1 0010 1100‬): [0] = 1 010 1100 [1] = 0 000 0010.
// However, the integer we are encoding is not a normal integer: it is a ZigZag encoded integer. ZigZag is a technique for encoding the sign (+/-)
// of an integer, where odd numbers represent negative integers and even numbers represent positive integers (ie. the LSB is the sign-bit [1 = negative]).
// For example: ZZ 0 = 0, ZZ 1 = -1, ZZ 2 = 1, ZZ 3 = -2, ZZ 4 = 2, etc. So the VarInt encoding for ZZ 300 above would actually encode integer value 150.
// For more on VarInt and ZigZag encoding see https://developers.google.com/protocol-buffers/docs/encoding. 
// The varIntResult has 2 properties: number of bytes read ('length'), and the integer value ('value'). The 'value' is (by deisgn) a JS number to 
// make it easy to consume (compared to BigInt). However, this also means that it's limited to Number.MAX_SAFE_INTEGER / 2 (it's only half because ZigZag
// always encodes as a positive integer).
function readVarInt(bytes: Uint8Array | Buffer, startIndex: number, numBits: number): varIntResult
{
    let result: varIntResult = { length: 0, value: 0 };
    let zigZagValue: number = 0;
    let pos: number = 0;
    let isMostSignificantBitSet: boolean = false;
    let maxExpectedBytes: number = Math.ceil(numBits / 7);

    do
    {
        isMostSignificantBitSet = (bytes[startIndex + pos] & 128) === 128;
        let low7Bits: number = bytes[startIndex + pos] & 127;
        
        if (numBits <= 32)
        {
            zigZagValue |= (low7Bits << (pos * 7));
        }
        else
        {
            // Check if the value being decoded exceeds Number.MAX_SAFE_INTEGER:
            //   9007199254740991  : Number.MAX_SAFE_INTEGER (Math.pow(2, 53) - 1)
            //   562949953421312‬   : Math.pow(2, 49)
            //   71494644084506624‬ : 127 * Math.pow(2, 49) => Too big for Number
            //   9007199254740992  ‬: 16 * Math.pow(2, 49) => Still too big for Number, so the limit for low7Bits is 15
            if ((pos === 7) && (low7Bits > 15))
            {
                throw new Error(`The supplied varInt64 value (${Utils.makeDisplayBytes(bytes, startIndex, pos)}) encodes a value larger than Number.MAX_SAFE_INTEGER (${Number.MAX_SAFE_INTEGER})`);
            }
            zigZagValue += low7Bits * _2Powers7Bits[pos];
        }

        if (++pos > maxExpectedBytes)
        {
            throw new Error(`The supplied varInt${numBits <= 32 ? "32" : "64"} value (${Utils.makeDisplayBytes(bytes, startIndex, pos)}) encodes a value larger than ${numBits}-bits`);
        }
    } while (isMostSignificantBitSet);

    result.length = pos;

    // Decode ZigZag value    
    if (numBits <= 32)
    {
        // If zigZagValue is 5 (-3) : 0000 0000 0000 0000 0000 0000 0000 0101 (5)
        // A: (zigZagValue >>> 1)   : 0000 0000 0000 0000 0000 0000 0000 0010 (2)
        // B: (zigZagValue & 1)     : 0000 0000 0000 0000 0000 0000 0000 0001 (1) => Isolate the sign-bit
        // C: -(zigZagValue & 1)    : ‭1111 1111 1111 1111 1111 1111 1111 1111‬ (-1) => This "mask" would be all 0's if zigZagValue was even
        // Result: A XOR C          : 1111 1111 1111 1111 1111 1111 1111 1101 (-3)
        result.value = (zigZagValue >>> 1) ^ (-(zigZagValue & 1));
    }
    else
    {
        // Note: We can't use any bitwise operators here because they automatically cast operands to 32-bit values
        if (zigZagValue % 2 === 0) 
        {
            // A positive value (or 0)
            result.value = zigZagValue / 2;
        }
        else
        {
            // A negative value
            result.value = -((zigZagValue + 1) / 2);
        }
    }

    return (result);
}

/** Converts a 32-bit integer into a VarInt32 byte array. */
export function writeVarInt32(value: number): Uint8Array
{
    checkValueToEncode(value, 32);

    let zigZagValue: number = ((value << 1) ^ (value >> 31)); // Convert the value to ZigZag format
    let varInt32: number[] = (zigZagValue === 0) ? [0] : [];

    while (zigZagValue !== 0)
    {
        let byte: number = zigZagValue & 127;
        zigZagValue = zigZagValue >>> 7;
        byte |= ((zigZagValue === 0) ? 0 : 128);
        varInt32.push(byte);
    }

    return (_bytePool.alloc(varInt32)); // return (new Uint8Array(varInt32));
}

/** 
 * Converts an integer into a VarInt64 byte array.\
 * Note: This can only handle signed integers up to 52-bits.
 */
export function writeVarInt64(value: number): Uint8Array
{
    checkValueToEncode(value, 53);

    // Note: When we convert the value to ZigZag format we MUST do so without using roll left/right or other bitwise operators (because these operators automatically cast operands to 32-bit values)
    // TODO: We're only encoding into the positive range (ie. zigZagValue will always be positive), which is why we're limiting 'value' to only 53-bits instead of the maximum 54-bits
    let zigZagValue: number = (value >= 0) ? value * 2 : (Math.abs(value) * 2) - 1;
    let varInt64: number[] = (zigZagValue === 0) ? [0] : [];

    while (zigZagValue > 0)
    {
        let byte: number = zigZagValue % 128; // Equivalent of 'value & 127'
        zigZagValue = Math.floor(zigZagValue / 128); // Equivalent of 'value >>> 7'
        byte |= ((zigZagValue === 0) ? 0 : 128); // OK to use bitwise 'or' here as 'byte' will fit in a 32-bit value
        varInt64.push(byte);
    }

    return (_bytePool.alloc(varInt64)); // return (new Uint8Array(varInt64));
}

const _named2Powers: { [ powerName: string ]: number } = {};
_named2Powers["2Pow31"] = Math.pow(2, 31);
_named2Powers["2Pow51"] = Math.pow(2, 51);
_named2Powers["2Pow52"] = Math.pow(2, 52);
_named2Powers["2Pow53"] = Math.pow(2, 53);

/** Throws if the specified value cannot be represented (as a signed integer) in the specified number of bits (1..54). */
function checkValueToEncode(value: number, numBits: number): void
{
    if (isNaN(value) || !Number.isInteger(value))
    {
        throw new Error(`The value to encode (${value}) is not an integer`);
    }

    if ((numBits < 1) || (numBits > 54))
    {
        throw new Error(`numBits (${numBits}) out of range (1..54)`);
    }

    let maxInt: number = 0;
    switch (numBits - 1)
    {
        // This is an optimization because checkValueToEncode() is on the "hot path"
        case 31:
            maxInt = _named2Powers["2Pow31"];
            break;
        case 51:
            maxInt = _named2Powers["2Pow51"];
            break;
        case 52:
            maxInt = _named2Powers["2Pow52"];
            break;
        case 53:
            maxInt = _named2Powers["2Pow53"];
            break;
        default:
            maxInt = Math.pow(2, numBits - 1);
            break;
    }
    const minValue: number = -maxInt + (numBits === 54 ? 1 : 0); // -Number.MIN_SAFE_INTEGER === Number.MAX_SAFE_INTEGER
    const maxValue: number = maxInt - 1;

    if ((value < minValue) || (value > maxValue))
    {
        throw new Error(`The value to encode (${value}) exceeds the range of a ${numBits}-bit signed integer (${minValue} to ${maxValue})`);
    }
}

/** Test utility method to compare the contents of two byte arrays. */
export function compareBytes(bytes1: Uint8Array, bytes2: Uint8Array, length: number, startIndex: number = 0): boolean
{
    let isMatch: boolean = true;

    if ((startIndex + length > bytes1.length) || (startIndex + length > bytes2.length))
    {
        throw new Error(`Either bytes1 (${bytes1.length} bytes) and/or bytes2 (${bytes2.length} bytes) do not contain enough bytes to perform the comparision (start at index ${startIndex} for ${length} bytes)`);
    }

    for (let i = 0; i < length; i++)
    {
        let pos: number = startIndex + i;

        if (bytes1[pos] !== bytes2[pos])
        {
            isMatch = false;
            break;
        }
    }

    return (isMatch);
}

export function runFixedIntTests()
{
    let range32: number = Math.pow(2, 31);
    let range64: bigint = BigInt(Math.pow(2, 63));
    let startTimeInMS: number = 0;
    let stepSize: number = 21;
    let elapsedMS: number = 0;
    let numTests: number = 0;

    // First, spot-check the 3 flavors of Int64 methods for result parity
    let int64ParityTestValues: number[] = [123456789, -123456789, 256, -256, 1, -1, 0, Math.pow(2, 50 - 1), -Math.pow(2, 50) - 1];
    for (let i = 0; i < int64ParityTestValues.length; i++)
    {
        let value: number = int64ParityTestValues[i];

        if (!compareBytes(writeInt64Fixed(BigInt(value)), writeInt64FixedNumber(value), 8) ||
            !compareBytes(writeInt64Fixed(BigInt(value)), writeInt64FixedNumber_NoBuffer(value), 8))
        {
            throw new Error("Int64 encoding method parity test failed for " + value);
        }

        if ((readInt64Fixed(writeInt64FixedNumber(value)) !== BigInt(value)) ||
            (readInt64FixedNumber(writeInt64FixedNumber_NoBuffer(value)) !== value) ||
            (readInt64FixedNumber_NoBuffer(writeInt64Fixed(BigInt(value))) !== value))
        {
            throw new Error("Int64 decoding method parity test failed for " + value);
        }
    }

    // Spot-check that readInt64Fixed() works with an offset
    let targetValue: bigint = BigInt(-123456789);
    let buf1: Buffer = writeInt64Fixed(BigInt(777777777)) as Buffer;
    let buf2: Buffer = writeInt64Fixed(targetValue) as Buffer;
    let buf3: Buffer = Buffer.concat([buf1, buf2]);
    let offsetTestResult = readInt64Fixed(buf3, 8);
    if (offsetTestResult !== targetValue)
    {
        throw new Error(`Error: readInt64Fixed() offset test failed: expected ${targetValue} but got ${offsetTestResult}`);
    }

    Utils.log("Starting FixedInt 32-bit tests...");

    startTimeInMS = Date.now();
    for (let value32 = -range32; value32 < range32; value32 += stepSize)
    {
        let encoded32: Uint8Array = writeInt32Fixed(value32);
        let decoded32: number = readInt32Fixed(encoded32);

        if (value32 !== decoded32)
        {
            throw new Error(`Decoded 32-bit value (${decoded32}) does not match encoded value (${value32})`);
        }
    }

    elapsedMS = Date.now() - startTimeInMS;
    numTests = Math.floor((range32 * 2) / stepSize);
    Utils.log(`${numTests} FixedInt 32-bit tests completed in ${elapsedMS}ms (${(numTests / elapsedMS).toFixed(2)} encode/decodes per ms).`);

    Utils.log("");
    Utils.log("Starting FixedInt 64-bit tests...");

    startTimeInMS = Date.now();
    stepSize = 3242148931456; // 3242148931 (for 51-bit)

    let value64: bigint = BigInt(-range64);
    for (numTests = 0; value64 < range64; value64 += BigInt(stepSize), numTests++)
    {
        let encoded64: Uint8Array = writeInt64Fixed(value64);
        let decoded64: bigint = readInt64Fixed(encoded64);

        if (value64 !== decoded64)
        {
            throw new Error(`Decoded 64-bit value (${decoded64}) does not match encoded value (${value64})`);
        }
    }

    elapsedMS = Date.now() - startTimeInMS;
    Utils.log(`${numTests} FixedInt 64-bit tests completed in ${elapsedMS}ms (${(numTests / elapsedMS).toFixed(2)} encode/decodes per ms).`);

    Utils.log("");
    Utils.log("FixedInt tests finished.");
}

export function runVarIntTests(): void
{
    let range32: number = Math.pow(2, 31);
    let range64: number = Math.pow(2, 51);
    let startTimeInMS: number = Date.now();
    let stepSize: number = 399;
    let elapsedMS: number = 0;
    let numTests: number = 0;

    // Utils.log("Starting VarInt size checks...");
    // for (let i = 0, lastNumBytesRead = 0; i <= Math.pow(2,28); i++)
    // {
    //     let numBytesRead = fromVarInt32(toVarInt32(i)).numBytesRead;
    // 
    //     if (numBytesRead != lastNumBytesRead)
    //     {
    //         console.log(`Value: ${i}, NumBytes: ${numBytesRead}`);
    //         lastNumBytesRead = numBytesRead;
    //     }
    // }

    Utils.log("Starting VarInt 32-bit tests...");

    for (let value32 = -range32; value32 < range32; value32 += stepSize)
    {
        // Utils.log((Date.now() - startTimeInMS).toString()); // This helped investigate the "breakpoints won't hit in first 30ms" bug

        let encoded32: Uint8Array = writeVarInt32(value32);
        let decoded32: varIntResult = readVarInt32(encoded32);
    
        if (value32 !== decoded32.value)
        {
            throw new Error(`Decoded 32-bit value (${decoded32.value}) does not match encoded value (${value32})`);
        }
    }

    elapsedMS = Date.now() - startTimeInMS;
    numTests = Math.floor((range32 * 2) / stepSize);
    Utils.log(`${numTests} VarInt 32-bit tests completed in ${elapsedMS}ms (${(numTests / elapsedMS).toFixed(2)} encode/decodes per ms).`);

    Utils.log("");
    Utils.log("Starting VarInt 64-bit tests...");

    startTimeInMS = Date.now();
    stepSize = 392148932;

    for (let value64 = -range32; value64 < range64; value64 += stepSize)
    {
        let encoded64: Uint8Array = writeVarInt64(value64);
        let decoded64: varIntResult = readVarInt64(encoded64);
        if (value64 !== decoded64.value)
        {
            throw new Error(`Decoded 64-bit value (${decoded64.value}) does not match encoded value (${value64})`);
        }
    }

    elapsedMS = Date.now() - startTimeInMS;
    numTests = Math.floor((range64 * 2) / stepSize);
    Utils.log(`${numTests} VarInt 64-bit tests completed in ${elapsedMS}ms (${(numTests / elapsedMS).toFixed(2)} encode/decodes per ms).`);

    Utils.log("");
    Utils.log("VarInt tests finished.");
}

/** 
 * [Internal] A class (for performance optimization) that enables creating contiguous blocks of memory from which a Uint8Array can be produced. 
 * Also provides a faster alternative to "new Uint8Array(number[])". 
 */
export class BytePool
{
    private _pool: Uint8Array = null;
    private _size: number;
    private _pos: number = 0;
    private _blockStartPos: number = -1;

    /** The size (in bytes) of the pool. Set via constructor. */
    get size(): number { return (this._size); }

    constructor(sizeInBytes: number = 1024 * 1024 * 2)
    {
        this._size = sizeInBytes;
        this._pool = new Uint8Array(this._size);
    }

    /** Starts a block of contiguous allocation. Only one block can be active at a time, and the block cannot exceed the size of the BytePool. */
    startBlock(): void
    {
        if (this._blockStartPos !== -1)
        {
            let blockSize: number = this._pos - this._blockStartPos;
            throw new Error(`A BytePool block has already been started (${blockSize} bytes at position ${this._blockStartPos})`);
        }
        this._blockStartPos = this._pos;
    }

    /** 
     * Ends a block of contiguous allocation, and returns the block (copied, by default, from the pool). Returns null if there is no active block. \
     * **WARNING**: Setting 'copy' to false will return a [faster] temporary-copy, but it MUST be used (copied) before the underlying pool data is overwritten.
     */
    endBlock(copy: boolean = true): Uint8Array
    {
        if (this._blockStartPos === -1)
        {
            return (null);
        }
        // Note: When 'copy' is true we return a new Uint8Array - not just a view - so that it's not temporary [ie. it won't get overwritten
        //       when _pool wraps]. This allows the caller to queue the returned block indefinitely (eg. when building a batch).
        let block: Uint8Array = copy ? this._pool.slice(this._blockStartPos, this._pos) : this._pool.subarray(this._blockStartPos, this._pos);
        this._blockStartPos = -1;
        return (block);
    }

    /** Cancels the current block (if any), and reclaims the space it consumed. */
    cancelBlock(): void
    {
        if (this._blockStartPos !== -1)
        {
            this._pos = this._blockStartPos;
            this._blockStartPos = -1;
        }
    }

    /** Throws if a block is not in-progress. If needed, moves the current block to the head of the pool, throwing if there's not enough space. */
    private canAddToBlock(caller: string, addLength: number)
    {
        if (this._blockStartPos === -1)
        {
            throw new Error(`BytePool.${caller}() can only be called between startBlock() and endBlock()`);
        }

        if (this._pos + addLength > this._size)
        {
            // Move the block to the head of the pool.
            // Note: We don't split the allocated block [part at tail, part at head] so that it's always contiguous.
            //       The downside is that we can't use all the space in the pool (if the new allocaton won't fit in the remaining space).
            let blockSize: number = this._pos - this._blockStartPos;
            if (blockSize + addLength > this._blockStartPos)
            {
                // We're being conservative (safe) here by assuming that an overlapping copy may not work as expected in all JavaScript engines
                throw new Error(`Cannot add ${addLength} bytes because the active block cannot be moved (Pool: ${this._size} bytes, Block: ${blockSize} bytes at offset ${this._blockStartPos})`);
            }
            if (blockSize > 0)
            {
                this._pool.copyWithin(0, this._blockStartPos, this._blockStartPos + blockSize);
            }
            this._blockStartPos = 0;
            this._pos = blockSize;
            // Utils.log("DEBUG: BytePool Wrapped", null, Utils.LoggingLevel.Minimal);
        }
    }

    /** Adds (copies) the supplied buffer to the current block. */
    addBuffer(sourceBuffer: Uint8Array): number
    {
        if (sourceBuffer.length === 0)
        {
            return (0);
        }
        this.canAddToBlock("addBuffer", sourceBuffer.length);
        this._pool.set(sourceBuffer, this._pos);
        this._pos += sourceBuffer.length;
        return (sourceBuffer.length);
    }
    
    /** Adds (copies) the supplied bytes to the current block. */
    addBytes(bytes: number[]): number
    {
        if (bytes.length === 0)
        {
            return (0);
        }
        this.canAddToBlock("addBytes", bytes.length);
        for (let i = 0; i < bytes.length; i++)
        {
            if (!Number.isInteger(bytes[i]) || (bytes[i] < 0) || (bytes[i] > 255))
            {
                throw new Error(`The value (${bytes[i]}) at position ${i} is not a byte (0..255)`);
            }
        }
        this._pool.set(bytes, this._pos);
        this._pos += bytes.length;
        return (bytes.length);
    }

    /** Adds (copies) the bytes for a VarInt32 (for the specified value) to the current block. */
    addVarInt32(value: number): number
    {
        checkValueToEncode(value, 32);
    
        // Note: We're duplicating the code from writeVarInt32() here for performance (to avoid another function call)
        let zigZagValue: number = ((value << 1) ^ (value >> 31)); // Convert the value to ZigZag format
        let varInt32: number[] = (zigZagValue === 0) ? [0] : [];
    
        while (zigZagValue !== 0)
        {
            let byte: number = zigZagValue & 127;
            zigZagValue = zigZagValue >>> 7;
            byte |= ((zigZagValue === 0) ? 0 : 128);
            varInt32.push(byte);
        }
        return (this.addBytes(varInt32));
    }    

    /** 
     * Returns a new (but temporary) Uint8Array from the specified bytes.\
     * Note: The expectation is that the returned buffer will quickly (ie. within the calling
     *       function) be copied into another buffer, eg. via Buffer.concat().
     *       If too much time passes, the underlying data in the pool will be overwritten.\
     * Note: NOT for use inside a block, ie. not between startBlock() and endBlock().
     */
    alloc(bytes: number[]): Uint8Array
    {
        if (this._blockStartPos !== -1)
        {
            throw new Error(`BytePool.alloc() cannot be called between startBlock() and endBlock()`);
        }

        if (bytes.length > this._size)
        {
            throw new Error(`The requested allocation (${bytes.length}) is larger than the pool size (${this._size} bytes)`);
        }

        for (let i = 0; i < bytes.length; i++)
        {
            if (!Number.isInteger(bytes[i]) || (bytes[i] < 0) || (bytes[i] > 255))
            {
                throw new Error(`The value (${bytes[i]}) at position ${i} is not a byte (0..255)`);
            }
        }

        if (this._pos + bytes.length > this._size)
        {
            // No room at the tail, so add to the head of the pool
            // Utils.log("DEBUG: BytePool Wrapped", null, Utils.LoggingLevel.Minimal);
            this._pos = 0;
        }

        this._pool.set(bytes, this._pos);
        let buffer: Uint8Array = this._pool.subarray(this._pos, this._pos + bytes.length)
        this._pos += bytes.length;
        return (buffer);
    }
}

let _bytePool: BytePool = new BytePool(16 * 1024); // Used to speed-up writeVarInt32/64()
