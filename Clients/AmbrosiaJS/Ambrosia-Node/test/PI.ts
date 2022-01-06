import Ambrosia = require("../src/Ambrosia"); 
import Messages = Ambrosia.Messages;

/** @ambrosia publish=true */
export function RestFn(p1: string, ...p2: { p3: (number | string)[] }[]): void
{
}

/** 
 * Test: Correctly handle line-breaks and comments in complex return type
 * @ambrosia publish=true 
 */
export function myComplexReturnFunction(): 
{
    // TEST0
    r1: string,
    r2: number |
    // TEST1
    /* 
    TEST2
    */
    string
} | null
{
    return (null);
}
 
/** 
 * Test: Handle inline comments in union in complex function parameter
 * @ambrosia publish=true 
 */
export function myComplexFunction(p1: { pn1: /*Test 1*/ number | string /*Test 2*/, pn2: number }, p2: string = "foo"): number | string
{
    return (0);
}

/**
 * Intersection types are supported
 * @ambrosia publish=true
 */
export type IntersectionType = FullName[] & ShortName[];
/** @ambrosia publish=true */
export type ShortName = { first: string };
/** @ambrosia publish=true */
export type FullName = { first: string, last: string};
 
/** 
 * Test of element class creation when an object literal contains a union type
 * @ambrosia publish = true 
 */
export type ABNames = { name: "A" | "B" }[];

/** 
 * Test that embedded/trailing @link tags don't break JSDocTag parsing.
 * @ambrosia publish {@link https://www.microsoft.com} = true {@link https://www.microsoft.com}
 */
export type PersonName = string | null;

/** @ambrosia publish=true */
export type FirstNames = "Rahee" | "Jonathan" | "Darren" | "Richard";

/**
 * A test of a template string type.
 * @ambrosia publish=true 
 */
export type Greeting = `Hello ${FirstNames} at ${"MSR" | "Microsoft"}!`;

/** 
 * Test that spaces are removed from in-and-around array suffixes.
 * @ambrosia publish=true
 */
export type ArrayWithSpaces = string [ ] [    ]   []   ;

/**
 * A test of removeWhiteSpaceAndComments() 
 * @ambrosia publish=true 
 */
export type FooBar = 
{
    abba:
    {
        /** line one
         * line two */
        aaa: boolean,
        bbb: string
    }[]
}

/** Some static methods. */
export class StaticStuff
{
    /** The 'Hello' method
     * @ambrosia publish=true */
    static hello(name: string): void
    {
        Ambrosia.Utils.log(`Hello ${name}!`);
    }
}

/** The Fooiest Foo ever! */
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
             * @ambrosia publish = true 
             */
            export type NameToNumberDictionary = Map<string, number>;
        }
    }
    export namespace Woo
    {
        /** */
        export namespace Hoo
        {
            /** @ambrosia publish = true */
            export type NumberToNameDictionary = Map<number, string>;
        }
    }
}

/**
 * Generic built-in types can be used, but only with concrete types (not type placeholders, eg. "T"): Example #2
 * @ambrosia publish = true 
 */
export type EmployeeWithGenerics = { firstNames: Set<{ name: string, nickNames: NickNames }>, lastName: string, birthYear: number };

/** 
 * Test for a literal-object array type; this should generate a 'NickNames_Element' class and then redefine the type of NickNames as NickNames_Element[].
 * This is done just to make it easier for the consumer to create a NickNames instance.
 * @ambrosia publish = true 
 */
export type NickNames = { name: string }[];

/**
 * @ambrosia publish = true 
 */
export function showNicknames(names: NickNames): void
{
}

/** @ambrosia publish = true */
export type SimpleTypeC = SimpleTypeB;

/** @ambrosia publish = true */
export type SimpleTypeB = SimpleTypeA;

/** @ambrosia publish = true */
export type SimpleTypeA = string[];

/** @ambrosia publish = true */
export type TypeA = { pA: TypeB };

/** @ambrosia publish = true */
export type TypeB = { pB: TypeC };

/** @ambrosia publish = true */
export type TypeC = { pC: string };

/** 
 * @ambrosia publish = true
 */
export type TestOfNewSerializationTypes = { s: Set<Foo>, m: Map<number, string>, d: Date, e: Error[], r: RegExp, again: Foo };

/** @ambrosia publish = true */
export type Foo = { p1: string };

/** Multiple JSDoc comments test. */
/** Since these appear before [not with] the comment containing the ambrosia tag, these will not propagate to the .g.ts file. */
/** @ambrosia publish=true 
*/
export function bug135(): void
{
}

/**
 * Generics test 
 * @ambrosia publish=true 
 */
export function joinNames(names: Set<string>): string
{
    return ([...names].join(","));
}

export namespace MyAppState
{
    class MyAppState extends Ambrosia.AmbrosiaAppState
    {
        constructor(restoredAppState?: MyAppState)
        {
            super(restoredAppState);
        }
    }

    export class MyIntermediateAppState extends MyAppState
    {
        constructor(restoredAppState?: MyIntermediateAppState)
        {
            super(restoredAppState);
        }
    }
}

export namespace State
{
    export let foo: string = "", _myAppState: MyAppState.MyIntermediateAppState = new MyAppState.MyIntermediateAppState();
}

export module Test
{
    // /** @ambrosia publish=true */
    // export function genericTest<T>(p1: T): T
    // {
    //     return (p1);
    // }

    /** 
     * Method to test custom serialized parameters.
     * @ambrosia publish=true, methodID=2 
     */
    export function takesCustomSerializedParams(rawParams: Uint8Array): void
    {
    }

    /** Some new test. 
     * @ambrosia publish=true @param person Details of a person.
     * 
     */
    // Private stuff!
    export function NewTest(person: { age: number }): { age: number }
    {
        return (person);
    }

    /** 
     * Parameter type for the 'Today' method.
     * @ambrosia publish=true
     */
    export enum DayOfWeek
    {
        Sunday = -1,
        Monday = +1,
        Tuesday,
        Wednesday,
        Thursday,
        Friday,
        Saturday
    }

    /** @ambrosia publish=true */
    // export interface foo { abc: number };

    /** 
     * Parameter type for the 'ComputePI' method.
     * @ambrosia publish = true
     */
    export type Digits = { count: number };

    /** The 'TestInner' namespace. */
    export namespace TestInner
    {
        export function onFirstStart(): void
        {
        }

        /** 
         * Returns the current day of the week.
         * @ambrosia publish=false
         * @param dow The day of the week.
         * Foobar!
         */
        export function Today(dow: DayOfWeek): DayOfWeek
        {
            return (dow);
        }

        /**
         * Returns pi computed to the specified number of digits.
         * @ambrosia publish=true, version=1, doRuntimeTypeChecking=true
         */
        export function ComputePI(/** Foo */ 
            digits /* Bar */ : 
            /** Baz */ Digits = 
            { 
                count: 12 /** a Dozen! */
            }): number
        {
            function localfn(): void
            {
                console.log("foo!");
            }
            // Note: Because 'digits' has a default parameter value, it's implicitly optional (ie. it's the same as "digits?: Digits = {...}")
            let pi: number = Number.parseFloat(Math.PI.toFixed(digits?.count ?? 10));
            return (pi);
        }
    }

    /** 
     * Parameter type for the 'ComputePI' method.
     * @ambrosia publish=true
     */
    export type Digit2 = { count: number };

    /** @ambrosia publish=true, methodID=1 */
    export function DoIt(dow: DayOfWeek): void
    {
    }

    // export namespace AnotherInnerTest
    // {
    //     /** 
    //      * Parameter type for the 'ComputePI' method.
    //      * @ambrosia publish=true
    //      */
    //     export type Digit2 = { count: number };
    // }
}

/** 
 * Parameter type for the 'ComputePI' method.
 * @ambrosia publish=true
 */
export type Digit3 = { count: number };

/** An event handler */
export function onRecoveryComplete(/** Bar! */): /** Foo! */ void
{
}

export function onBecomingPrimary(): void
{
}

export function onICStopped(exitCode: number): void
{
    console.log(`The IC stopped with exit code ${exitCode}`);
}