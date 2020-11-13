// Generated consumer-side API for the 'server' Ambrosia Node instance.
// Note: This file was generated on 2020/11/11 at 10:53:09.065.
// Note: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import Ambrosia = require("ambrosia-node");
import IC = Ambrosia.IC;

let DESTINATION_INSTANCE_NAME: string = "server";
let POST_TIMEOUT_IN_MS: number = 8000; // -1 = Infinite

export namespace Published
{
    export namespace PI
    {
        /**
         * Parameter type for the 'ComputePI' method.
         */
        export class Digits
        {
            count: number;

            constructor(count: number)
            {
                this.count = count;
            }
        }

        /**
         * Returns pi computed to the specified number of digits.
         */
        export async function ComputePIAsync(digits?: Digits): Promise<number>
        {
            let postResult: number = await IC.postAsync(DESTINATION_INSTANCE_NAME, "ComputePI", 1, null, POST_TIMEOUT_IN_MS, IC.arg("digits?", digits));
            return (postResult);
        }

        /**
         * Returns pi computed to the specified number of digits.
         */
        export function ComputePI(resultHandler: IC.PostResultHandler<number>, digits?: Digits): void
        {
            IC.post(DESTINATION_INSTANCE_NAME, "ComputePI", 1, resultHandler, POST_TIMEOUT_IN_MS, IC.arg("digits?", digits));
        }
    }
}