export namespace Test
{
    /**
     * Testing 1) a mix of ',' and ';' member separators, 2) A complex-type array
     * @ambrosia publish = true */
    export type MixedTest = 
    {
        p1: string[];
        p2: string[][],
        p3: { p4: number; p5: string }[];
    };

    /** 
     * Example of a complex type.
     * @ambrosia publish=true
     */
    export type Name = 
    {
        // Test 1
        first: string, // Test 2
        /** Test 3 */
        last: string /* Test 4 */
    }

    /** 
     * Example of a type that references another type.
     * @ambrosia publish=true
     */
    export type Names = Name[];

    /** 
     * Example of a nested complex type.
     * @ambrosia publish=true
     */
    export type Nested = 
    {
        abc:
        { 
            a: Uint8Array, 
            b: 
            { 
                c: Names 
            } 
        }
    }

    /** 
     * Example of an enum.
     * @ambrosia publish=true 
     */
    export enum Letters 
    {
        // The A
        A, 
        B = /** The B */ 3, 
        /* The C */
        C, // The C
        /** The D */ D = 9
    }

    /**
     * Example of a [post] method that uses custom types.
     * @ambrosia publish=true, version=1
     */
    export function makeName(firstName: string = "John", lastName: string /** Foo */ = "Doe"): Names
    {
        let names: Names;
        let name: Name = { first: firstName, last: lastName };
        names.push(name);
        return (names);
    }

    /**
     * Example of a [non-post] method
     * @ambrosia publish=true, methodID=123
     */
    export function DoIt(p1: Name[][]): void
    {
        console.log("Done!");
    }
}