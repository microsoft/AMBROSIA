// Tests to handle  Event Handlers. 
// Event handler function in the input source file for any AppEvent will automatically get wired-up at code gen (publisher side)
// Even if have the @ambrosia tag, it should NOT code gen to consumer. 

// have couple inside namespace
export namespace Test
{

    /**  Fake Event Handler due to case in the name so this will be generated
    * @ambrosia publish=true 
    */
    export function onbecomingprimary(): void
    {
        console.log(`Fake Event Handler due to name case so just seen as typical function`);
    }

    export function onRecoveryComplete(/** Bar! */): /** Foo! */ void
    {
        console.log(`On Recovery`);
    }
 
    ///** @ambrosia publish=true */   Putting an Ambrosia tag on Event Handler will cause error
    export function onBecomingPrimary(): void
    {
        console.log(`Becoming primary`);
    }
}

// have some outside namespace

    export function onICStopped(exitCode: number): void
    {
        console.log(`The IC stopped with exit code ${exitCode}`);
    }

    export function onICStarted(): void
    {
        console.log(`The IC Started`);
    }

    export function onICStarting(): void
    {
        console.log(`The IC is starting`);
    }

    export function onICReadyForSelfCallRpc(): void
    {
        console.log(`The IC Ready`);
    }
    
    export function onUpgradeStateAndCode(): void
    {
        console.log(`The onUpGrade`);
    }
   
    export function onIncomingCheckpointStreamSize(): void
    {
        console.log(`The incoming checkpoint`);
    }

    //** This is valid EventHandler but do not add code for event to make sure publisher handles an event that isn't defined */
    //** Should put a "TODO" comment in publisher generated code */
    //export function onFirstStart(): void
    //{
        //console.log(`on First Start`);
    //}

 
