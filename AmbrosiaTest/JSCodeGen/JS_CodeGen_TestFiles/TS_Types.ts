/** 
   Test File to test all the Types for typescripts
   Has the basic types
*/

export namespace Test
{

    /************* Primitives - bool, string, number, array ********
    * @ambrosia publish=true 
    * 
    */
    export function BasicTypes(isFalse: boolean, height: number,mystring: string = "doublequote",mystring2: string = 'singlequote',my_array:number[] = [1, 2, 3],notSure: any = 4)
    {
        console.log(isFalse);
        console.log(height);
        console.log(mystring);
        console.log(mystring2);
        console.log(my_array);
        console.log(notSure);
    }

    //**** String Enums are not supported scenario */

    /***********  Enum type (numeric enum - strings as number) as return    *************
    * @ambrosia publish=true 
    */
   export enum PrintMedia {
        Newspaper = 1,
        Newsletter,
        Magazine,
        Book
    }

    /********* Function using / returning Numeric Enum ****
    * @ambrosia publish=true 
    */
   export function getMedia(mediaName: string): PrintMedia {
        if (  mediaName === 'Forbes' || mediaName === 'Outlook') {
            return PrintMedia.Magazine;
        }
     }

    /**********  Enum type (Reverse Mapped enum - can access the value of a member and also a member name from its value) *************
    * @ambrosia publish=true 
    */
    export enum PrintMediaReverse {
        NewspaperReverse = 1,
        NewsletterReverse,
        MagazineReverse,
        BookReverse
    }
  
    PrintMediaReverse.MagazineReverse;   // returns  3
    PrintMediaReverse["MagazineReverse"];// returns  3
    PrintMediaReverse[3];         // returns  MagazineReverse


    /*************  Void type *************
    * @ambrosia publish=true 
    */
    export function warnUser(): void 
    {
        alert("This is my warning message");
    }


    /*************** Complex Type ************* 
     * @ambrosia publish=true
     */
    export type Name = 
    {
        // Test 1
        first: string, // Test 2
        /** Test 3 */
        last: string, /* Test 4 */
        priorNames: Names[]
    }

    /************** Example of a type that references another type *************.
     * @ambrosia publish=true
     */
    export type Names = Name[];

    
    /************** Example of a nested complex type.*************
     * @ambrosia publish=true
     */
    export type Nested = 
    {
        abc:
        { 
            a: Uint8Array, 
            b: 
            { 
                c: Names 
            } 
        }
    }

    /************** Example of a [post] method that uses custom types. *************
     * @ambrosia publish=true, version=1
     */
    export function makeName(firstName: string = "John", lastName: string /** Foo */ = "Doe"): Names
    {
        let names: Names;
        let name: Name = { first: firstName, last: lastName, priorNames: [] };
        names.push(name);
        return (names);
    }
  

    /********* Function returning number ****
    * @ambrosia publish=true 
    */
    export function return_number(strvalue: string): number
    {
        if (strvalue == "99") 
        {
            return 99;
        }
    }  

    /********* Function returning string ****
    * @ambrosia publish=true 
    */
   export function returnstring(numvalue: number): string
   {
       if (numvalue == 9999) 
       {
           return '99';
       }
   }  

}
 