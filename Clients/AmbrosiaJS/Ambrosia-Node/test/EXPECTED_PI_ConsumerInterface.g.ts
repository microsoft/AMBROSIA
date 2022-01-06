// Generated consumer-side API for the 'PI' Ambrosia Node app/service.
// Publisher: (Not specified).
// Note: This file was generated
// Note [to publisher]: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import Ambrosia = require("ambrosia-node");
import IC = Ambrosia.IC;
import Utils = Ambrosia.Utils;

const _knownDestinations: string[] = []; // All previously used destination instances (the 'PI' Ambrosia app/service can be running on multiple instances, potentially simultaneously); used by the postResultDispatcher (if any)
let _destinationInstanceName: string = ""; // The current destination instance
let _postTimeoutInMs: number = 8000; // -1 = Infinite

/** 
 * Sets the destination instance name that the API targets.\
 * Must be called at least once (with the name of a registered Ambrosia instance that implements the 'PI' API) before any other method in the API is used.
 */
export function setDestinationInstance(instanceName: string): void
{
    _destinationInstanceName = instanceName.trim();
    if (_destinationInstanceName && (_knownDestinations.indexOf(_destinationInstanceName) === -1))
    {
        _knownDestinations.push(_destinationInstanceName);
    }
}

/** Returns the destination instance name that the API currently targets. */
export function getDestinationInstance(): string
{
    return (_destinationInstanceName);
}

/** Throws if _destinationInstanceName has not been set. */
function checkDestinationSet(): void
{
    if (!_destinationInstanceName)
    {
        throw new Error("setDestinationInstance() must be called to specify the target destination before the 'PI' API can be used.");
    }
}

/**
 * Sets the post method timeout interval (in milliseconds), which is how long to wait for a post result from the destination instance before raising an error.\
 * All post methods will use this timeout value. Specify -1 for no timeout. 
 */
export function setPostTimeoutInMs(timeoutInMs: number): void
{
    _postTimeoutInMs = Math.max(-1, timeoutInMs);
}

/**
 * Returns the post method timeout interval (in milliseconds), which is how long to wait for a post result from the destination instance before raising an error.\
 * A value of -1 means there is no timeout.
 */
export function getPostTimeoutInMs(): number
{
    return (_postTimeoutInMs);
}

/**
 * Intersection types are supported
 */
export type IntersectionType = FullName[] & ShortName[];

export class ShortName
{
    first: string;

    constructor(first: string)
    {
        this.first = first;
    }
}

export class FullName
{
    first: string;
    last: string;

    constructor(first: string, last: string)
    {
        this.first = first;
        this.last = last;
    }
}

/**
 * Test of element class creation when an object literal contains a union type
 */
export type ABNames = ABNames_Element[];

export class ABNames_Element
{
    name: "A" | "B";

    constructor(name: "A" | "B")
    {
        this.name = name;
    }
}

/**
 * Test that embedded/trailing @link tags don't break JSDocTag parsing.
 */
export type PersonName = string | null;

export type FirstNames = "Rahee" | "Jonathan" | "Darren" | "Richard";

/**
 * A test of a template string type.
 */
export type Greeting = `Hello ${FirstNames} at ${"MSR" | "Microsoft"}!`;

/**
 * Test that spaces are removed from in-and-around array suffixes.
 */
export type ArrayWithSpaces = string[][][];

/**
 * A test of removeWhiteSpaceAndComments()
 */
export class FooBar
{
    abba: { aaa: boolean, bbb: string }[];

    constructor(abba: { aaa: boolean, bbb: string }[])
    {
        this.abba = abba;
    }
}

/**
 * Generic built-in types can be used, but only with concrete types (not type placeholders, eg. "T"): Example #2
 */
export class EmployeeWithGenerics
{
    firstNames: Set<{ name: string, nickNames: NickNames }>;
    lastName: string;
    birthYear: number;

    constructor(firstNames: Set<{ name: string, nickNames: NickNames }>, lastName: string, birthYear: number)
    {
        this.firstNames = firstNames;
        this.lastName = lastName;
        this.birthYear = birthYear;
    }
}

/**
 * Test for a literal-object array type; this should generate a 'NickNames_Element' class and then redefine the type of NickNames as NickNames_Element[].
 * This is done just to make it easier for the consumer to create a NickNames instance.
 */
export type NickNames = NickNames_Element[];

export class NickNames_Element
{
    name: string;

    constructor(name: string)
    {
        this.name = name;
    }
}

export type SimpleTypeC = SimpleTypeB;

export type SimpleTypeB = SimpleTypeA;

export type SimpleTypeA = string[];

export class TypeA
{
    pA: TypeB;

    constructor(pA: TypeB)
    {
        this.pA = pA;
    }
}

export class TypeB
{
    pB: TypeC;

    constructor(pB: TypeC)
    {
        this.pB = pB;
    }
}

export class TypeC
{
    pC: string;

    constructor(pC: string)
    {
        this.pC = pC;
    }
}

export class TestOfNewSerializationTypes
{
    s: Set<Foo>;
    m: Map<number, string>;
    d: Date;
    e: Error[];
    r: RegExp;
    again: Foo;

    constructor(s: Set<Foo>, m: Map<number, string>, d: Date, e: Error[], r: RegExp, again: Foo)
    {
        this.s = s;
        this.m = m;
        this.d = d;
        this.e = e;
        this.r = r;
        this.again = again;
    }
}

export class Foo
{
    p1: string;

    constructor(p1: string)
    {
        this.p1 = p1;
    }
}

/**
 * Parameter type for the 'ComputePI' method.
 */
export class Digit3
{
    count: number;

    constructor(count: number)
    {
        this.count = count;
    }
}

/**
 * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
 * 
 * *Note: "_Post" methods should **only** be called from deterministic events.*
 */
export function RestFn_Post(callContextData: any, p1: string, ...p2: { p3: (number | string)[] }[]): number
{
    checkDestinationSet();
    const callID = IC.postFork(_destinationInstanceName, "RestFn", 1, _postTimeoutInMs, callContextData, 
        IC.arg("p1", p1), 
        IC.arg("p2", p2));
    return (callID);
}

/**
 * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
 * 
 * *Note: "_PostByImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
 */
export function RestFn_PostByImpulse(callContextData: any, p1: string, ...p2: { p3: (number | string)[] }[]): void
{
    checkDestinationSet();
    IC.postByImpulse(_destinationInstanceName, "RestFn", 1, _postTimeoutInMs, callContextData, 
        IC.arg("p1", p1), 
        IC.arg("p2", p2));
}

/**
 * *Note: The result ({ r1: string, r2: number | string } | null) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
 * 
 * *Note: "_Post" methods should **only** be called from deterministic events.*
 * 
 * Test: Correctly handle line-breaks and comments in complex return type
 */
export function myComplexReturnFunction_Post(callContextData: any): number
{
    checkDestinationSet();
    const callID = IC.postFork(_destinationInstanceName, "myComplexReturnFunction", 1, _postTimeoutInMs, callContextData);
    return (callID);
}

/**
 * *Note: The result ({ r1: string, r2: number | string } | null) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
 * 
 * *Note: "_PostByImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
 * 
 * Test: Correctly handle line-breaks and comments in complex return type
 */
export function myComplexReturnFunction_PostByImpulse(callContextData: any): void
{
    checkDestinationSet();
    IC.postByImpulse(_destinationInstanceName, "myComplexReturnFunction", 1, _postTimeoutInMs, callContextData);
}

/**
 * *Note: The result (number | string) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
 * 
 * *Note: "_Post" methods should **only** be called from deterministic events.*
 * 
 * Test: Handle inline comments in union in complex function parameter
 */
export function myComplexFunction_Post(callContextData: any, p1: { pn1: number | string, pn2: number }, p2?: string): number
{
    checkDestinationSet();
    const callID = IC.postFork(_destinationInstanceName, "myComplexFunction", 1, _postTimeoutInMs, callContextData, 
        IC.arg("p1", p1), 
        IC.arg("p2?", p2));
    return (callID);
}

/**
 * *Note: The result (number | string) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
 * 
 * *Note: "_PostByImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
 * 
 * Test: Handle inline comments in union in complex function parameter
 */
export function myComplexFunction_PostByImpulse(callContextData: any, p1: { pn1: number | string, pn2: number }, p2?: string): void
{
    checkDestinationSet();
    IC.postByImpulse(_destinationInstanceName, "myComplexFunction", 1, _postTimeoutInMs, callContextData, 
        IC.arg("p1", p1), 
        IC.arg("p2?", p2));
}

/**
 * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
 * 
 * *Note: "_Post" methods should **only** be called from deterministic events.*
 */
export function showNicknames_Post(callContextData: any, names: NickNames): number
{
    checkDestinationSet();
    const callID = IC.postFork(_destinationInstanceName, "showNicknames", 1, _postTimeoutInMs, callContextData, IC.arg("names", names));
    return (callID);
}

/**
 * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
 * 
 * *Note: "_PostByImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
 */
export function showNicknames_PostByImpulse(callContextData: any, names: NickNames): void
{
    checkDestinationSet();
    IC.postByImpulse(_destinationInstanceName, "showNicknames", 1, _postTimeoutInMs, callContextData, IC.arg("names", names));
}

/**
 * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
 * 
 * *Note: "_Post" methods should **only** be called from deterministic events.*
 */
export function bug135_Post(callContextData: any): number
{
    checkDestinationSet();
    const callID = IC.postFork(_destinationInstanceName, "bug135", 1, _postTimeoutInMs, callContextData);
    return (callID);
}

/**
 * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
 * 
 * *Note: "_PostByImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
 */
export function bug135_PostByImpulse(callContextData: any): void
{
    checkDestinationSet();
    IC.postByImpulse(_destinationInstanceName, "bug135", 1, _postTimeoutInMs, callContextData);
}

/**
 * *Note: The result (string) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
 * 
 * *Note: "_Post" methods should **only** be called from deterministic events.*
 * 
 * Generics test
 */
export function joinNames_Post(callContextData: any, names: Set<string>): number
{
    checkDestinationSet();
    const callID = IC.postFork(_destinationInstanceName, "joinNames", 1, _postTimeoutInMs, callContextData, IC.arg("names", names));
    return (callID);
}

/**
 * *Note: The result (string) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
 * 
 * *Note: "_PostByImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
 * 
 * Generics test
 */
export function joinNames_PostByImpulse(callContextData: any, names: Set<string>): void
{
    checkDestinationSet();
    IC.postByImpulse(_destinationInstanceName, "joinNames", 1, _postTimeoutInMs, callContextData, IC.arg("names", names));
}

export namespace Foo
{
    export namespace Bar
    {
        /**
         * The Baziest Baz...
         * ...ever!
         * Note: If this namespace is not exported, the Baz namespace will end up in the root of ConsumerInterface.g.ts
         */
        export namespace Baz
        {
            /**
             * Generic built-in types can be used, but only with concrete types (not type placeholders, eg. "T"): Example #1
             */
            export type NameToNumberDictionary = Map<string, number>;
        }
    }

    export namespace Woo
    {
        export namespace Hoo
        {
            export type NumberToNameDictionary = Map<number, string>;
        }
    }
}

/** Some static methods. */
export namespace StaticStuff
{
    /**
     * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
     * 
     * *Note: "_Post" methods should **only** be called from deterministic events.*
     * 
     * The 'Hello' method
     */
    export function hello_Post(callContextData: any, name: string): number
    {
        checkDestinationSet();
        const callID = IC.postFork(_destinationInstanceName, "hello", 1, _postTimeoutInMs, callContextData, IC.arg("name", name));
        return (callID);
    }

    /**
     * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
     * 
     * *Note: "_PostByImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
     * 
     * The 'Hello' method
     */
    export function hello_PostByImpulse(callContextData: any, name: string): void
    {
        checkDestinationSet();
        IC.postByImpulse(_destinationInstanceName, "hello", 1, _postTimeoutInMs, callContextData, IC.arg("name", name));
    }
}

export namespace Test
{
    /**
     * Parameter type for the 'Today' method.
     */
    export enum DayOfWeek { Sunday = -1, Monday = 1, Tuesday = 2, Wednesday = 3, Thursday = 4, Friday = 5, Saturday = 6 }

    /**
     * Parameter type for the 'ComputePI' method.
     */
    export class Digits
    {
        count: number;

        constructor(count: number)
        {
            this.count = count;
        }
    }

    /**
     * Parameter type for the 'ComputePI' method.
     */
    export class Digit2
    {
        count: number;

        constructor(count: number)
        {
            this.count = count;
        }
    }

    /**
     * *Note: "_Fork" methods should **only** be called from deterministic events.*
     * 
     * Method to test custom serialized parameters.
     * @param rawParams A custom serialization (byte array) of all required parameters. Contact the 'PI' API publisher for details of the serialization format.
     */
    export function takesCustomSerializedParams_Fork(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.callFork(_destinationInstanceName, 2, rawParams);
    }

    /**
     * *Note: "_Impulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
     * 
     * Method to test custom serialized parameters.
     * @param rawParams A custom serialization (byte array) of all required parameters. Contact the 'PI' API publisher for details of the serialization format.
     */
    export function takesCustomSerializedParams_Impulse(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.callImpulse(_destinationInstanceName, 2, rawParams);
    }

    /**
     * *Note: "_EnqueueFork" methods should **only** be called from deterministic events, and will not be sent until IC.flushQueue() is called.*
     * 
     * Method to test custom serialized parameters.
     * @param rawParams A custom serialization (byte array) of all required parameters. Contact the 'PI' API publisher for details of the serialization format.
     */
    export function takesCustomSerializedParams_EnqueueFork(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.queueFork(_destinationInstanceName, 2, rawParams);
    }

    /**
     * *Note: "_EnqueueImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc), and will not be sent until IC.flushQueue() is called.*
     * 
     * Method to test custom serialized parameters.
     * @param rawParams A custom serialization (byte array) of all required parameters. Contact the 'PI' API publisher for details of the serialization format.
     */
    export function takesCustomSerializedParams_EnqueueImpulse(rawParams: Uint8Array): void
    {
        checkDestinationSet();
        IC.queueImpulse(_destinationInstanceName, 2, rawParams);
    }

    /**
     * *Note: The result ({ age: number }) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
     * 
     * *Note: "_Post" methods should **only** be called from deterministic events.*
     * 
     * Some new test.
     */
    export function NewTest_Post(callContextData: any, person: { age: number }): number
    {
        checkDestinationSet();
        const callID = IC.postFork(_destinationInstanceName, "NewTest", 1, _postTimeoutInMs, callContextData, IC.arg("person", person));
        return (callID);
    }

    /**
     * *Note: The result ({ age: number }) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
     * 
     * *Note: "_PostByImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
     * 
     * Some new test.
     */
    export function NewTest_PostByImpulse(callContextData: any, person: { age: number }): void
    {
        checkDestinationSet();
        IC.postByImpulse(_destinationInstanceName, "NewTest", 1, _postTimeoutInMs, callContextData, IC.arg("person", person));
    }

    /**
     * *Note: "_Fork" methods should **only** be called from deterministic events.*
     */
    export function DoIt_Fork(dow: DayOfWeek): void
    {
        checkDestinationSet();
        IC.callFork(_destinationInstanceName, 1, { dow: dow });
    }

    /**
     * *Note: "_Impulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
     */
    export function DoIt_Impulse(dow: DayOfWeek): void
    {
        checkDestinationSet();
        IC.callImpulse(_destinationInstanceName, 1, { dow: dow });
    }

    /**
     * *Note: "_EnqueueFork" methods should **only** be called from deterministic events, and will not be sent until IC.flushQueue() is called.*
     */
    export function DoIt_EnqueueFork(dow: DayOfWeek): void
    {
        checkDestinationSet();
        IC.queueFork(_destinationInstanceName, 1, { dow: dow });
    }

    /**
     * *Note: "_EnqueueImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc), and will not be sent until IC.flushQueue() is called.*
     */
    export function DoIt_EnqueueImpulse(dow: DayOfWeek): void
    {
        checkDestinationSet();
        IC.queueImpulse(_destinationInstanceName, 1, { dow: dow });
    }

    /** The 'TestInner' namespace. */
    export namespace TestInner
    {
        /**
         * *Note: The result (number) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
         * 
         * *Note: "_Post" methods should **only** be called from deterministic events.*
         * 
         * Returns pi computed to the specified number of digits.
         */
        export function ComputePI_Post(callContextData: any, digits?: Digits): number
        {
            checkDestinationSet();
            const callID = IC.postFork(_destinationInstanceName, "ComputePI", 1, _postTimeoutInMs, callContextData, IC.arg("digits?", digits));
            return (callID);
        }

        /**
         * *Note: The result (number) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
         * 
         * *Note: "_PostByImpulse" methods should **only** be called from non-deterministic events (UI events, timers, web service calls, etc).*
         * 
         * Returns pi computed to the specified number of digits.
         */
        export function ComputePI_PostByImpulse(callContextData: any, digits?: Digits): void
        {
            checkDestinationSet();
            IC.postByImpulse(_destinationInstanceName, "ComputePI", 1, _postTimeoutInMs, callContextData, IC.arg("digits?", digits));
        }
    }
}

/**
 * Handler for the results of previously called post methods (in Ambrosia, only 'post' methods return values). See Messages.PostResultDispatcher.\
 * Must return true only if the result (or error) was handled.
 */
export function postResultDispatcher(senderInstanceName: string, methodName: string, methodVersion: number, callID: number, callContextData: any, result: any, errorMsg: string): boolean
{
    const sender: string = IC.isSelf(senderInstanceName) ? "local" : `'${senderInstanceName}'`;
    let handled: boolean = true;

    if (_knownDestinations.indexOf(senderInstanceName) === -1)
    {
        return (false); // Not handled: this post result is from a different instance than the destination instance currently (or previously) targeted by the 'PI' API
    }

    if (errorMsg)
    {
        switch (methodName)
        {
            case "RestFn":
            case "myComplexReturnFunction":
            case "myComplexFunction":
            case "hello":
            case "showNicknames":
            case "bug135":
            case "joinNames":
            case "NewTest":
            case "ComputePI":
                Utils.log(`Error: ${errorMsg}`);
                break;
            default:
                handled = false;
                break;
        }
    }
    else
    {
        switch (methodName)
        {
            case "RestFn":
                // TODO: Handle the method completion (it returns void), optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "myComplexReturnFunction":
                const myComplexReturnFunction_Result: { r1: string, r2: number | string } | null = result;
                // TODO: Handle the result, optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "myComplexFunction":
                const myComplexFunction_Result: number | string = result;
                // TODO: Handle the result, optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "hello":
                // TODO: Handle the method completion (it returns void), optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "showNicknames":
                // TODO: Handle the method completion (it returns void), optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "bug135":
                // TODO: Handle the method completion (it returns void), optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "joinNames":
                const joinNames_Result: string = result;
                // TODO: Handle the result, optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "NewTest":
                const NewTest_Result: { age: number } = result;
                // TODO: Handle the result, optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "ComputePI":
                const ComputePI_Result: number = result;
                // TODO: Handle the result, optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            default:
                handled = false;
                break;
        }
    }
    return (handled);
}