class StaticStuff {
    /** 
     * The parent class of a published static method must be exported.
     * @ambrosia publish=true 
     */
    static hello(name: string): void {
        console.log(`Hello ${name}!`);
    }
}

