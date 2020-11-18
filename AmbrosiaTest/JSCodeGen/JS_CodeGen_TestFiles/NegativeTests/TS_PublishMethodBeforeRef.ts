export module Test
{
    /**********  Negative Test *************

    /**
     * Can't publish any method while references to unpublished types exist
     * @ambrosia publish=true
     */
    export type MyType = Name[];
    export type Name = { first: string, last: string };
    /** @ambrosia publish=true */
    export function fn(): void {
    }


}

