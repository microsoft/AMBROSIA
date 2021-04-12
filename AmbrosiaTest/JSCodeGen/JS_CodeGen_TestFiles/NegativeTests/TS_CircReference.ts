/** @ambrosia publish=true */
    export type CName = 
    {
        first: string,
        last: string,
        priorNames: CNames[]
    }
    /** 
     * Cannot publish a type that has a circular reference
     * @ambrosia publish=true
     */
    export type CNames = CName[];
