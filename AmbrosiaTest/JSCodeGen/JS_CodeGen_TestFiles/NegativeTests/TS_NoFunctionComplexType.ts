export module Test
{
    /**********  Negative Test *************

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

}

