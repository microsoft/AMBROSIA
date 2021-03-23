/** The Fooiest Foo ever! This comment not generated because no direct published entities - Baz will though */
export namespace Foo {
    export namespace Bar {
        /** 
        * The Baziest Baz...
        * ...ever! 
        */
        export namespace Baz {
            /**
             * Generic built-in types can be used, but only with concrete types (not type placeholders, eg. "T"): Example #1
             * @ambrosia publish = true 
             */
            export type NameToNumberDictionary = Map<string, number>;
        }
    }
    export namespace Woo {
        /** */
        export namespace Hoo {
            /** @ambrosia publish = true */
            export type NumberToNameDictionary = Map<number, string>;
        }
    }
}






