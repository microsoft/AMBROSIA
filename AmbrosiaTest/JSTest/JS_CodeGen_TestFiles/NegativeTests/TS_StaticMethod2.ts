export class StaticStuff {
    /** 
     * A method must have the 'static' modifier to be published.
     * @ambrosia publish=true 
     */
    hello(name: string): void {
        console.log(`Hello ${name}!`);
    }
}


