export module Test
{
    /**********  Negative Test *************

    /**
     * Cannot publish a local (nested) function
     * @ambrosia publish=true
     */
    export function parentFn(): void {
        /** @ambrosia publish=true */
        function localFn(): void {
        }
    }

}

