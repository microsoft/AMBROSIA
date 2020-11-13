// This is a test file used in testing of AST.ts
namespace TestFile
{
    export interface Greeter
    {
        SayHello(name: string): void;
    }

    /** 
     * The 'Test' class
     * @mycustomtag (p1, p2, p3)
     */
    export class Test implements Greeter
    {
        /** @DataMember */
        private _name: string;

        public SayHello(name: string): void
        {
            this._name = name;
            console.log(`Hi there ${this._name}!`);
        }
    }
}
 