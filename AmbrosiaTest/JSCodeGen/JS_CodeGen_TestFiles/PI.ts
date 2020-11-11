export module Test
{
    /**
     * Some new  test. 
     * @ambrosia publish=true @param person Datails of a person.
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
        Sunday,
        Monday,
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
        export async function onFirstStart(): Promise<void>
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
        export async function ComputePI(/** Foo */ 
            digits /* Bar */ : 
            /** Baz */ Digits = 
            { 
                count: 12 /** a Dozen! */
            }): Promise<number>
        {
            function localfn(): void
            {
                console.log("foo!");
            }
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
