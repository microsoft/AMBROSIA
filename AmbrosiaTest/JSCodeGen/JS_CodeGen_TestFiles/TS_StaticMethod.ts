/**
 * We now allow publishing static methods.  This gives developers additional code organization choices
 * @ambrosia publish = true 
 */
export class StaticStuff {
    /** @ambrosia publish=true */
    static hello(name: string): void {
        console.log(`Hello ${name}!`);
    }
}

