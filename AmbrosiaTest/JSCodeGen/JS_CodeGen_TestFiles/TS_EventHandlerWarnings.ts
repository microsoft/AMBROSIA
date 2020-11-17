// Tests to handle  Event Handlers warnings
// Can't add to negative because not failing
// Can't add to TS_EventHandlers.ts because using functions already defined


// This is a bit of a rare case ... a warning will show success.  Will want to verify warning though.
export namespace HandlerNegativeTests {
    // Handler with incorrect parameters
    // Note: This only produces a warning, not an error
    export function onRecoveryComplete(name: string): void {
    }

    // Handler with incorrect return type
    // Note: This only produces a warning, not an error
    export function onBecomingPrimary(): number {
        return (123);
    }
}

/** @ambrosia publish=true */
export function unused(): void {
}



 
