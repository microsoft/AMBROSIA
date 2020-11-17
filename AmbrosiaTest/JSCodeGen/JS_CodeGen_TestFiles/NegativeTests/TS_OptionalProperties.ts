export module Test
{
    /**********  Negative Test *************

    /**
     * Types with optional properties are not supported
     * @ambrosia publish=true
     */
    export type MyTypeWithOptionalMembers = { foo: string, bar?: number };

}

