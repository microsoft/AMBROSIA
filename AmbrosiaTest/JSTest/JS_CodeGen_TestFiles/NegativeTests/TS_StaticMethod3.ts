// Cannot publish a static method from a class expression
export class MoreStaticStuff {
    public utilities = new class Foo {
        constructor() {
        }

        /** @ambrosia publish=true */
        static helloAgain(name: string) {
            console.log(`Hello ${name}!`);
        }
    }();
}

