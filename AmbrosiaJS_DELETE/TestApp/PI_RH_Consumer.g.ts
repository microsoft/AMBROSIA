// Generated consumer-side API for the 'server' Ambrosia Node instance.
// Note: This file was generated
// Note: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import Ambrosia = require("ambrosia-node");
import IC = Ambrosia.IC;

let DESTINATION_INSTANCE_NAME: string = "server";
let POST_TIMEOUT_IN_MS: number = 8000; // -1 = Infinite

export namespace Test
{
    export async function DoItAsync(): Promise<void>
    {
        let postResult: void = await IC.postAsync(DESTINATION_INSTANCE_NAME, "DoIt", 1, null, POST_TIMEOUT_IN_MS);
        return (postResult);
    }

    export function DoIt(resultHandler: IC.PostResultHandler<void>): void
    {
        IC.post(DESTINATION_INSTANCE_NAME, "DoIt", 1, resultHandler, POST_TIMEOUT_IN_MS);
    }

    export async function DGonRecoveryCompleteAsync(): Promise<void>
    {
        let postResult: void = await IC.postAsync(DESTINATION_INSTANCE_NAME, "DGonRecoveryComplete", 1, null, POST_TIMEOUT_IN_MS);
        return (postResult);
    }

    export function DGonRecoveryComplete(resultHandler: IC.PostResultHandler<void>): void
    {
        IC.post(DESTINATION_INSTANCE_NAME, "DGonRecoveryComplete", 1, resultHandler, POST_TIMEOUT_IN_MS);
    }
}