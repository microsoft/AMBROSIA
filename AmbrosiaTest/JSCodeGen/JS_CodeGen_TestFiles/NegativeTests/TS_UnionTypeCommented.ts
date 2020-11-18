export module Test
{
    /**********  Negative Test *************

    /** 
     * Correctly handle line-breaks and comments in an unsupported return type
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
    }
    {
        return (null);
    }


}

