export module Test
{
    //** Negative test

    /** 
     * The ambrosia tag must be on the implementation of an overloaded function
     * @ambrosia publish=true 
     */
    export function fnOverload(): void;
    export function fnOverload(name?: string): void {
    }

}

