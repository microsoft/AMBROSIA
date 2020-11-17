export module Test
{
    /**********  Negative Test *************

    /** 
     * Union types are not supported
     * @ambrosia publish=true
     */
    export type MyUnionType = string | number;

}

