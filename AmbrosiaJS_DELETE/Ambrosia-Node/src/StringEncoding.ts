// Namespace for encoding/decoding UTF-8/16 strings to/from byte arrays.
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Utils from "./Utils/Utils-Index";

// Note: These methods are designed to work in both Node.js and a browser.
//       The native Buffer methods for string encoding/decoding are at least 2-to-3x faster.
//       The native methods might also work in the browser by using a third-party Buffer library, like https://github.com/feross/buffer.
// Note: Internally, JavaScript strings are UTF-16, but Ambrosia messages only use UTF-8, so only toUTF8Bytes() and fromUTF8Bytes() are actually used.
// Note: console.log() [in VSCode at least] does not appear to render Unicode characters greater
//       than 0xFFFF correctly: the Unicode replacement character (U+FFFD �) precedes the character.
//       This seems to describe the [Windows-specific] problem: https://exceptionshub.com/how-to-output-emoji-to-console-in-node-js-on-windows-2.html.
//       See also: https://nodejs.org/api/buffer.html#buffer_buffers_and_character_encodings. 

let _textEncoder: TextEncoder = new TextEncoder();
let _textDecoder: TextDecoder = new TextDecoder();

/** Converts a string to UTF-8, returned as a byte array. */
export function toUTF8Bytes(s: string): Uint8Array
{
    return (Utils.isNode() ? new Uint8Array(Buffer.from(s, "utf8")) : _textEncoder.encode(s));
}

/** Returns a string from an array of UTF-8 bytes. Note: Be careful when specifying startIndex and/or length since these are in bytes, not characters. */
export function fromUTF8Bytes(bytes: Uint8Array | Buffer, startIndex: number = 0, length: number = bytes.length): string
{
    let buffer: Buffer = null;

    if ((startIndex !== 0) || (length !== bytes.length))
    {
        if ((bytes instanceof Buffer) && Utils.isNode())
        {
            // Optimization to [theoretically] minimize memory copies, but [from observation] with no improvement in execution time
            return ((bytes as Buffer).toString("utf8", startIndex, startIndex + length));
        }
        buffer = (bytes instanceof Buffer) ? bytes.slice(startIndex, startIndex + length) : Buffer.from(bytes.slice(startIndex, startIndex + length));
    }
    else
    {
        buffer = (bytes instanceof Buffer) ? bytes : Buffer.from(bytes);
    }

    if (Utils.isNode())
    {
        return (buffer.toString("utf8"));
    }
    else
    {
        return (_textDecoder.decode(buffer));
    }
}

/** Converts a string to UTF-16, returned as a byte array. */
export function toUTF16Bytes(s: string): Uint8Array
{
    let bytes: Uint8Array = null;
    
    if (Utils.isNode())
    {
        if (!Utils.isLittleEndian())
        {
            throw new Error("Node.js only supports UTF-16 strings on little-endian operating systems")
        }
        bytes = new Uint8Array(Buffer.from(s, "utf16le"));
    }
    else
    {
        let buffer: Uint16Array = new Uint16Array(s.length);

        // Note: UTF-16 is the default encoding in JavaScript, requiring either 2 or 4 bytes per character.
        //       If 4 bytes are required for a character, the length of the string will be +1 (because of the surrogate-pair).
        for (let i = 0, length = s.length; i < length; i++) 
        {
            let charCode: number = s.charCodeAt(i);
            buffer[i] = charCode;
        }

        bytes = new Uint8Array(buffer.buffer);
    }

    return (bytes);
}    

/** Returns a string from an array of UTF-16 bytes. Note: Be careful when specifying startIndex and/or length since these are in bytes, not characters. */
export function fromUTF16Bytes(bytes: Uint8Array | Buffer, startIndex: number = 0, length: number = bytes.length): string
{
    let buffer: Buffer = null;
    
    if ((startIndex !== 0) || (length !== bytes.length))
    {
        buffer = (bytes instanceof Buffer) ? bytes.slice(startIndex, startIndex + length) : Buffer.from(bytes.slice(startIndex, startIndex + length));
    }
    else
    {
        buffer = (bytes instanceof Buffer) ? bytes : Buffer.from(bytes);
    }

    if (Utils.isNode())
    {
        if (!Utils.isLittleEndian())
        {
            throw new Error("Node.js only supports UTF-16 strings on little-endian operating systems")
        }
        return (buffer.toString("utf16le"));
    }
    else
    {
        let buffer16: Uint16Array = new Uint16Array(buffer.buffer);
        return (String.fromCharCode(...buffer16));
    }
}

/** Converts a string to UTF-8, returned as a byte array. */
export function toUTF8Bytes_Alt(s: string): Uint8Array
{
    let encoded: string = encodeURI(s); // Converts to a string representation of UTF-8
    let percentCharCount: number = 0;

    // Note: In UTF-8, chars outside of the 0..127 range get encoded [by encodeURI] as "%HH" where "HH" is a 2-digit hex value, eg. "%D1"
    //       [See https://www.fileformat.info/info/unicode/utf8.htm]
    for (let i = 0; i < encoded.length; i++)
    {
        if (encoded[i] === "%")
        {
            percentCharCount++;
        }
    }

    let byteCount: number = encoded.length - (percentCharCount * 2);
    let bytes: Uint8Array = new Uint8Array(byteCount);

    for (let i = 0, pos = 0; i < encoded.length; i++, pos++)
    {
        const charCode: number = encoded.charCodeAt(i);

        if (encoded[i] === "%") // eg. "%D1"
        {
            const encodedCharCode: number = parseInt(encoded.substr(i + 1, 2), 16);
            bytes[pos] = encodedCharCode;
            i += 2;
        }
        else
        {
            bytes[pos] = charCode;
        }
    }

    return (bytes);
}

/** Returns a string from an array of UTF-8 bytes. */
export function fromUTF8Bytes_Alt(bytes: Uint8Array): string
{
    let s: string = "";

    for (let i = 0; i < bytes.length; i++)
    {
        if (bytes[i] < 128)
        {
            s += String.fromCharCode(bytes[i]);
        }
        else
        {
            s += "%" + bytes[i].toString(16).toUpperCase();
        }
    }

    return (decodeURI(s));
}

/** Runs unit/perf tests. */
export function runUnitTests()
{
    // UTF-16 encoding of 😀 (\u{1F600}): (see https://en.wikipedia.org/wiki/UTF-16)  
    //   Note: The curly braces are the ES6 code-point escape syntax for unicode characters over 4 hex digits
    //   0x1F600 - 0x10000 = 0xF600 = 00001111011000000000‬ = 2 groups of 10-bits: ‭0000111101 (0x3D) and 1000000000 (0x200)
    //   0xD800 + 0x3D = 0xD83D = 55357 (High surrogate) = charCodeAt(0)
    //   0xDC00 + 0x200 = 0xDE00 = 56832 (Low surrogate) = charCodeAt(1)
    //   = [3D, D8, 00, DE] - Note: Node.js only supports little-endian UTF-16
    // UTF-8 encoding of 😀: (see https://en.wikipedia.org/wiki/UTF-8)
    //   0x1F600 is greater than 0x10000 so requires 21 code-point bits = 0 0001 1111 0110 0000 0000
    //   11110 [This indicates that 4 bytes are needed] + 0 00 = 11110000 = 0xF0
    //   10 [This indicates a continuation byte] + 01 1111     = 10011111 = 0x9F
    //   10 [This indicates a continuation byte] + 0110 00     = 10011000 = 0x98
    //   10 [This indicates a continuation byte] + 00 0000     = 10000000 - 0x80
    //   = [F0, 9F, 98, 80]

    // 1) Buffer (vs. Uint8Array) tests
    let sBufTest: string = "ABC😀шеллы!"
    let sBuf: string = fromUTF8Bytes(Buffer.from(sBufTest, "utf8"));
    if (sBuf !== sBufTest)
    {
        throw new Error(`UTF-8 Buffer test failed for '${sBufTest}'`);
    }
    sBuf = fromUTF8Bytes(Buffer.from(sBufTest, "utf8"), 3, 4);
    if (sBuf !== "😀")
    {
        throw new Error(`UTF-8 Buffer offset test failed for '${sBufTest}'`);
    }

    // 2) Offset tests
    let utf8Bytes: Uint8Array = toUTF8Bytes("AB😀C");
    let s8: string = fromUTF8Bytes(utf8Bytes, 2, 4); // 😀 takes 4 bytes
    if (s8 !== "😀")
    {
        throw new Error("UTF-8 startIndex/length test failed for 'AB😀C'");
    }

    let utf16Bytes: Uint8Array = toUTF16Bytes("ABCшеллы");
    let s16: string = fromUTF16Bytes(utf16Bytes, 3 * 2, 3 * 2);
    if (s16 !== "шел")
    {
        throw new Error("UTF-16 startIndex/length test failed for 'ABCшеллы'");
    }

    let s: string = "ABC😀шеллы!";
    if (fromUTF8Bytes_Alt(toUTF8Bytes_Alt(s)) !== s)
    {
        throw new Error(`UTF-8_Alt test failed for '${s}'`);
    }

    // 3) Performance tests
    let testStrings: string[] = ["AB😀C", "ABCшеллы", "\u0041\u0042\u0043"];

    for (let s of testStrings)
    {
        let startTimeInMs: number = 0;
        let iterations: number = 100000;
        let elapsedMs: number = 0;

        startTimeInMs = Date.now();
        for (let i = 0; i < iterations; i++)
        {
            let utf8Bytes: Uint8Array = toUTF8Bytes(s);
            let s8: string = fromUTF8Bytes(utf8Bytes);
            if (s !== s8)
            {
                throw new Error("UTF-8 test failed for '" + s + "'");
            }
        }

        elapsedMs = Date.now() - startTimeInMs;
        Utils.log(`UTF-8 test elapsed time: ${elapsedMs}ms for ${iterations} iterations of '${s}' (Node: ${Utils.isNode()})`);
        
        startTimeInMs = Date.now();
        for (let i = 0; i < iterations; i++)
        {
            let utf16Bytes: Uint8Array = toUTF16Bytes(s);
            let s16: string = fromUTF16Bytes(utf16Bytes);
            if (s !== s16)
            {
                throw new Error("UTF-16 test failed for '" + s + "'");
            }
        }

        elapsedMs = Date.now() - startTimeInMs;
        Utils.log(`UTF-16 test elapsed time: ${elapsedMs}ms for ${iterations} iterations of '${s}' (Node: ${Utils.isNode()})`);
    }

    Utils.log("Success: All tests passed!");
}