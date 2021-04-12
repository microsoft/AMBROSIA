class MyClassWithPrivateMember
{
    /** 
     * Can't publish a private static method
     * @ambrosia publish=true 
     */
    private static privateMethod(): void
    {
    }
}

