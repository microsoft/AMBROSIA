export module Test
{
    /**********  Negative Test *************

    /**
     * Can't have a methodID less than -1
     * @ambrosia publish=true, methodID=-2
     */
    export function MyFn(): void {
    }
}

