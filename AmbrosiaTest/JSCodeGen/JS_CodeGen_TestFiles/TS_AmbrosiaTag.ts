/** 
   Test File to test all the the ways that the ambrosia tag can be set and still work
*/

export namespace Test {


    /** @ambrosia publish=true */
    export function OneLineNoComment() {
        console.log("One Line with no extra comment");
    }

    /** Multi Line with Comment before Tag
     * but still before tag
     * @ambrosia publish=true 
     */
    export function MultiLineCommentBeforeTag() {
        console.log("Multi Line before tag");
    }

    /** Multi Line with Comment before Tag */
    /** but still before tag -- since separate comment, these will not show in .g.ts*/
    /** @ambrosia publish=true 
    */
    export function MultiSeparateLinesCommentBeforeTag() {
        console.log("Multi Separate Comment Line before tag");
    }

    /** Multi Line with Comment after Tag */
    /** @ambrosia publish=true 
     */
    /** Separate Comment after tag -- causes a warning that Skipping Function*/
    export function SeparateLinesCommentAfterTag() {
        console.log("Separate Comment Line after tag");
    }


    /************** Have a space after the tag before function declaration 
     * @ambrosia publish=true 
     */

    export function EmptyLineBetweenTagAndFctn() {
        console.log("Empty line between tag and fctn");
    }

    /****** Spacing around the tag 
    *                    @ambrosia publish=true          
    */
    export function SpacingAroundTag() {
        console.log("Spacing in front and behind tag");
    }

    /** JS Doc
    *  @ambrosia publish=true          
    */
    export function JSDOcTag() {
        console.log("JSDOcTag");
    }

    /*  This will NOT generate code - causes a warning that Skipping Function */
    /******** @ambrosia publish=true          */
    export function NotJSDOcTag() {
        console.log("NotJSDOcTag");
    }

    /* Proper way to tag Overloaded functions */
    export function fnOverload(): void;
    export function fnOverload(name: string): void;
    /** 
     * The ambrosia tag must be on the implementation of an overloaded function
     * @ambrosia publish=true 
     */
    export function fnOverload(name?: string): void {
    }


}