 export class SomeClass   {
    // Cannot publish a local (nested) function in a static method
    static someStaticMethod(): void
    {
       /** @ambrosia publish=true */
       function localFn(): void
       {
       }
    }
}
