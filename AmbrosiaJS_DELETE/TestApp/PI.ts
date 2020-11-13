import Ambrosia = require("ambrosia-node"); 
import Utils = Ambrosia.Utils;
import * as Self from "./ConsumerInterface.g"; // This a generated file

export namespace Published
{
    export namespace PI
    {
        /** 
         * Parameter type for the 'ComputePI' method.
         * @ambrosia publish=true
         */
        export type Digits = { count: number };
        
        /**
         * Returns pi computed to the specified number of digits.
         * @ambrosia publish=true, version=1, doRuntimeTypeChecking=true
         */
        export function ComputePI(digits?: Digits): number
        {
            let pi: number = Number.parseFloat(Math.PI.toFixed(digits?.count ?? 10));
            return (pi);
        }
    }
}

export namespace AppEventHandlers
{
    export async function onRecoveryComplete(): Promise<void>
    {
        let pi: number = await Self.Published.PI.ComputePIAsync(new Self.Published.PI.Digits(5));
        Utils.log(`pi = ${pi}`);
    }
}