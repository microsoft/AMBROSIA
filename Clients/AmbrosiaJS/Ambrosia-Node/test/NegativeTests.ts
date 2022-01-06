// Note: Because these [expected] failures result in an exception that - by default - halts code-generation, it's necessary to set
//       FileGenOptions.haltOnError to false [when calling emitTypeScriptFileFromSource()] in order to run ALL the tests at once
//       (rather than just stopping when the first test fails).
export namespace TypescriptNegativeTests
{
    /** @ambrosia publish=true */
    export type CName = 
    {
        first: string,
        last: string,
        priorNames: CNames[]
    }
    /** 
     * Cannot publish a type that has a circular reference
     * @ambrosia publish=true
     */
    export type CNames = CName[];

    // Can't publish a private static method
    class MyClassWithPrivateMember
    {
        /** @ambrosia publish=true */
        private static privateMethod(): void
        {
        }
    }
 
    // Can't publish a static method from a class expression
    export class MoreStaticStuff
    {
        public utilities = new class Foo
        {
            constructor() 
            {
            }

            /** @ambrosia publish=true */
            static helloAgain(name: string)
            {
                console.log(`Hello ${name}!`);
            }
        }();
    }

    /** 
     * Method with single 'rawParams: Uint8Array' parameter cannot be a Post method (ie. missing the 'methodID=' attribute)
     * @param rawParams Description of the format of the custom serialized byte array.
     * @ambrosia publish=true
     */
    export function takesCustomSerializedParams(rawParams: Uint8Array): void
    {
    }

    /** 
     * Unsupported type (FunctionType) in complex type property
     * @ambrosia publish=true 
     */
    export type myComplexType = 
    {
        p1: 
        {
            fn: /* Test 1*/ () => /* Test 2*/ void,
            p3: number
        },
        p2: string
    };

    /** 
     * Can't publish any method while references to unpublished types exist
     * @ambrosia publish=true 
     */
    export type MyType = Name[];
    /** @ambrosia publish=true */
    export function fn(): void
    {
    }
    /** @ambrosia publish=true */
    export type Name = { first: string, last: string};
    
    /** 
     * The ambrosia tag must be on the implementation of an overloaded function
     * @ambrosia publish=true 
     */
    export function fnOverload(): void;
    export function fnOverload(name?: string): void
    {
    }

    /** 
     * Cannot publish a local (nested) function 
     * @ambrosia publish=true
     */
    export function parentFn(): void
    {
        /** @ambrosia publish=true */
        function localFn(): void
        {
        }
    }

     // Cannot publish a local (nested) function in a static method
     class SomeClass
     {
         static someStaticMethod(): void
         {
            /** @ambrosia publish=true */
            function localFn(): void
            {
            }
         }
     }
 
    /** 
     * Cannot publish a method before all the types it uses are published
     * @ambrosia publish=true 
     */
    export function showNames(names: NameList): void {}
    /** @ambrosia publish=true */
    export type NameList = string[];

    /** 
     * Functions with generics are not supported
     * @ambrosia publish=true
     */
    export function myGenericFn<T>(p1: T): void
    {
    }

    /** 
     * Types with generic-type placeholders are not supported
     * @ambrosia publish=true
     */
    export type MyTypeWithGenerics<T> = { foo: T, bar: number };

    /** 
     * Another example of unsupported generic-type placeholders
     * @ambrosia publish = true 
     */
    export type Dictionary<K, V> = Map<K, V>;

    interface Todo
    {
        title: string;
        description: string;
        completed: boolean;
    }
      
    /** 
     * TypeScript utility types are not supported.
     * @ambrosia publish=true 
     */
    export type TodoPreview = Pick<Todo, "title" | "completed">;

    /** 
     * Types with optional properties are not supported
     * @ambrosia publish=true
     */
    export type MyTypeWithOptionalMembers = { foo: string, bar?: number };

    /** 
     * Tuple types are not supported
     * @ambrosia publish=true
     */
    export type MyTupleType = [string, number];
 
    /** 
     * Function types are not supported
     * @ambrosia publish=true 
     */
    export type MyFunctionType = (p1: number) => string;

    /** 
     * Cannot publish an unsupported type definition (this tests the "catch all" case of unsupported TS type syntax)
     * @ambrosia publish=true 
     */
     export type MyUnsupportedType = string extends null ? never: string;
}

export namespace TagNegativeTests
{
    /** 
     * Cannot use inline comments after tag
     * @ambrosia publish=true // For ambrosia
     */
    export function MyFn9(): void
    {
    }

    /** 
     * Cannot use quotes around attribute values
     * @ambrosia publish="true"
     */
    export function MyFn8(): void
    {
    }

    /** 
     * Ambrosia tag can only appear once
     * @ambrosia publish=false
     * @ambrosia publish=true
     */
    export function MyFn7(): void
    {
    }

    /** 
     * Unknown attribute name [on a method]
     * @ambrosia published=true
     */
    export function MyFn6(): void
    {
    }

    /** 
     * Unknown attribute name [on a type]
     * @ambrosia published=true
     */
    export type NewType = number[];
 
    /** 
     * Can't have a methodID on a type (only a method)
     * @ambrosia publish=true, methodID=1
     */
    export type MyType = string[];

    /** 
     * Can't have a version on a enum (only a method)
     * @ambrosia publish=true, version=1
     */
    export enum MyEnum { foo = 0, bar = 1 };

    /** 
     * There must be commas between attributes
     * @ambrosia publish=true version=1 doRuntimeTypeChecking=true
     */
    export function MyFn5(): void
    {
    }

    /** 
     * Can't publish a namespace (module)
     * @ambrosia publish=true 
     */
    namespace MyNS
    {
    }

    /** 
     * Can't publish a class
     * @ambrosia publish=true 
     */
    class MyClass
    {
    }

    export class Time
    {
        /** 
         * Can't publish a method.
         * @ambrosia publish=true 
         */
        currentYear(): number
        {
            return (2021);
        }
    }

    /** 
     * Can't publish an interface.
     * @ambrosia publish=true 
     */
    interface IFoo
    {
        foo: number;
    }

    /** 
     * doRuntimeTypeChecking attribute must be a boolean
     * @ambrosia publish=true, doRuntimeTypeChecking=Hello
     */
    export function MyFn4(): void
    {
    }

    /** 
     * version attribute must be an integer
     * @ambrosia publish=true, version=Hello
     */
    export function MyFn3(): void
    {
    }

    /** 
     * methodID attribute must be an integer
     * @ambrosia publish=true, methodID=Hello
     */
    export function MyFn2(): void
    {
    }

    /** 
     * Can't have a methodID less than 0
     * @ambrosia publish=true, methodID=-1
     */
    export function MyFn(): void
    {
    }
}

export namespace HandlerNegativeTests
{
    // Handler with incorrect parameters
    // Note: This only produces a warning, not an error
    export function onRecoveryComplete(name: string): void
    {
    }

    // Handler with incorrect return type
    // Note: This only produces a warning, not an error
    export function onBecomingPrimary(): number
    {
        return (123);
    }
}