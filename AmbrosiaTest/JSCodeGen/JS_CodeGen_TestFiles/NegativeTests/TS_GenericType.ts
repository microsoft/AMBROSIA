export module Test
{
    /**
     * Invalid test - generic function not supported as published function
     * @ambrosia publish=true
     * 
     */
    export function generic<T>(p1: T): T
    {
        return (p1);
    }

}

