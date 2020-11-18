// Generated consumer-side API for the 'server' Ambrosia Node instance.
// Note: This file was generated
// Note: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import Ambrosia = require("ambrosia-node");
import IC = Ambrosia.IC;

let DESTINATION_INSTANCE_NAME: string = "server";
let POST_TIMEOUT_IN_MS: number = 8000; // -1 = Infinite

export namespace Test
{
    /**
     * Correctly handle line-breaks and comments
     */
    export async function myComplexReturnFunctionAsync(): Promise<{ r1: string, r2: string }>
    {
        let postResult: { r1: string, r2: string } = await IC.postAsync(DESTINATION_INSTANCE_NAME, "myComplexReturnFunction", 1, null, POST_TIMEOUT_IN_MS);
        return (postResult);
    }

    /**
     * Correctly handle line-breaks and comments
     */
    export function myComplexReturnFunction(resultHandler: IC.PostResultHandler<{ r1: string, r2: string }>): void
    {
        IC.post(DESTINATION_INSTANCE_NAME, "myComplexReturnFunction", 1, resultHandler, POST_TIMEOUT_IN_MS);
    }
}