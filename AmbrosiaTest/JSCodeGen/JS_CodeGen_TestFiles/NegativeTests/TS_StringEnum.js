"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.Test = void 0;
var Test;
(function (Test) {
    /**********  Enum type (string enum - initialize strings with strings and not as number) *************
    * @ambrosia publish=true
    */
    var PrintMediaString;
    (function (PrintMediaString) {
        PrintMediaString["NewspaperStringEnum"] = "NEWSPAPER";
        PrintMediaString["NewsletterStringEnum"] = "NEWSLETTER";
        PrintMediaString["MagazineStringEnum"] = "MAGAZINE";
        PrintMediaString["BookStringEnum"] = "BOOK";
    })(PrintMediaString = Test.PrintMediaString || (Test.PrintMediaString = {}));
    var enumValue1 = PrintMediaString.NewspaperStringEnum; // returns NEWSPAPER
    var enumValue2 = PrintMediaString["MagazineStringEnum"]; // returns MAGAZINE
})(Test = exports.Test || (exports.Test = {}));
//# sourceMappingURL=TS_StringEnum.js.map