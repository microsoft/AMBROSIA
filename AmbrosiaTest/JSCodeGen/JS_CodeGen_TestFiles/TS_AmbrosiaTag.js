"use strict";
/**
   Test File to test all the the ways that the ambrosia tag can be set and still work
*/
Object.defineProperty(exports, "__esModule", { value: true });
exports.Test = void 0;
var Test;
(function (Test) {
    /** @ambrosia publish=true */
    function OneLineNoComment() {
        console.log("One Line with no extra comment");
    }
    Test.OneLineNoComment = OneLineNoComment;
    /** Multi Line with Comment before Tag
     * but still before tag
     * @ambrosia publish=true
     */
    function MultiLineCommentBeforeTag() {
        console.log("Multi Line before tag");
    }
    Test.MultiLineCommentBeforeTag = MultiLineCommentBeforeTag;
    /** Multi Line with Comment before Tag */
    /** but still before tag -- since separate comment, these will not show in .g.ts*/
    /** @ambrosia publish=true
    */
    function MultiSeparateLinesCommentBeforeTag() {
        console.log("Multi Separate Comment Line before tag");
    }
    Test.MultiSeparateLinesCommentBeforeTag = MultiSeparateLinesCommentBeforeTag;
    /** Multi Line with Comment after Tag */
    /** @ambrosia publish=true
     */
    /** Separate Comment after tag -- causes a warning that Skipping Function*/
    function SeparateLinesCommentAfterTag() {
        console.log("Separate Comment Line after tag");
    }
    Test.SeparateLinesCommentAfterTag = SeparateLinesCommentAfterTag;
    /************** Have a space after the tag before function declaration
     * @ambrosia publish=true
     */
    function EmptyLineBetweenTagAndFctn() {
        console.log("Empty line between tag and fctn");
    }
    Test.EmptyLineBetweenTagAndFctn = EmptyLineBetweenTagAndFctn;
    /****** Spacing around the tag
    *                    @ambrosia publish=true
    */
    function SpacingAroundTag() {
        console.log("Spacing in front and behind tag");
    }
    Test.SpacingAroundTag = SpacingAroundTag;
    /** JS Doc
    *  @ambrosia publish=true
    */
    function JSDOcTag() {
        console.log("JSDOcTag");
    }
    Test.JSDOcTag = JSDOcTag;
    /*  This will NOT generate code - causes a warning that Skipping Function */
    /******** @ambrosia publish=true          */
    function NotJSDOcTag() {
        console.log("NotJSDOcTag");
    }
    Test.NotJSDOcTag = NotJSDOcTag;
    /**
     * The ambrosia tag must be on the implementation of an overloaded function
     * @ambrosia publish=true
     */
    function fnOverload(name) {
    }
    Test.fnOverload = fnOverload;
})(Test = exports.Test || (exports.Test = {}));
//# sourceMappingURL=TS_AmbrosiaTag.js.map