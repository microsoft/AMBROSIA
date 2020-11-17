export module Test
{
    /**********  Negative Test *************

    /**
     * Cannot publish a method before all the types it uses are published
     * @ambrosia publish=true
     */
    export function showNames(names: NameList): void { }
    /** @ambrosia publish=true */
    export type NameList = string[];

}

