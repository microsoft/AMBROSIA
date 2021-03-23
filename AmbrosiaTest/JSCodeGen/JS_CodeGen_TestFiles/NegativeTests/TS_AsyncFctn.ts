/** 
   Invalid test case - Async is not supported
*/

export namespace Test
{

    /** 
     * Parameter type for the 'ComputePI' method.
     * @ambrosia publish = true
     */
    export type Digits = { count: number };

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
 