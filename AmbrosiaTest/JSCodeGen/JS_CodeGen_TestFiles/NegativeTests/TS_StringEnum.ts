export module Test
{
    /**********  Enum type (string enum - initialize strings with strings and not as number) *************
    * @ambrosia publish=true 
    */
    export enum PrintMediaString {
        NewspaperStringEnum = "NEWSPAPER",
        NewsletterStringEnum = "NEWSLETTER",
        MagazineStringEnum = "MAGAZINE",
        BookStringEnum = "BOOK"
   }

    let enumValue1: PrintMediaString = PrintMediaString.NewspaperStringEnum; // returns NEWSPAPER
    let enumValue2: PrintMediaString = PrintMediaString["MagazineStringEnum"]; // returns MAGAZINE

}
