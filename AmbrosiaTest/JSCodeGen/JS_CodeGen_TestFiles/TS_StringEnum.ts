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

   PrintMediaString.NewspaperStringEnum; //returns NEWSPAPER
   PrintMediaString['Magazine'];//returns MAGAZINE

}

