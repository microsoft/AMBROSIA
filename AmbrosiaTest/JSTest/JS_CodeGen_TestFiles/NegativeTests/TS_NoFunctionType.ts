export module Test
{
    /**********  Negative Test *************

    /** 
     * Function types are not supported
     * @ambrosia publish=true 
     */
    export type fnType = (p1: number) => string;

}

