export module Test
{
    /**********  Negative Test *************

    /**
     * Intersection types are not supported
     * @ambrosia publish=true
     */
    export type IntersectionType = FullName[] & ShortName[];
    export type ShortName = { first: string };
    export type FullName = { first: string, last: string};


}

