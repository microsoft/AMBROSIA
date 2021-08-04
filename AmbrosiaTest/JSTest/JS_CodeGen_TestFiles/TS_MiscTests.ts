/** 
   Test File of misc tests. If find a theme or grouping then move out of this file into separate file
*/

export namespace Test {


    /** 
     * Correctly handle line-breaks and comments
     * @ambrosia publish=true 
     */
    export function myComplexReturnFunction(): 
    {
        // TEST0
        r1: string,
        r2:  
        // TEST1
        /* 
        TEST2
        */
        string
    }

    {

        return ({ r1: " ", r2: " " });
    }

}

