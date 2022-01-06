import Ambrosia = require("ambrosia-node"); 
import IC = Ambrosia.IC;
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

        /**
         * Returns the sum of the specified numbers [tests passing rest parameters].
         * @ambrosia publish=true, version=1, doRuntimeTypeChecking=true
         */
        export function Sum(...numbers: number[]): number
        {
            const total: number = numbers.reduce((sum, curr) => sum + curr, 0);
            return (total);
        }

        /**
         * "Sets" the apartment number of an address (eg. 127 or "C314") [tests passing a union type].
         * @ambrosia publish=true, version=1
         */
        export function SetApartmentNumber(aptNumber: number | string): number | string
        {
            return (aptNumber);
        }

        /** Tests a string-template type.
         * @ambrosia publish=true
         */
        export function TemplateStringTest(template: `Hello ${"World" | "Universe"}`): `Hello ${"World" | "Universe"}`
        {
            return (template);
        }
    }
}

export namespace AppEventHandlers
{
    export function onBecomingPrimary(): void
    {
        Self.setDestinationInstance(IC.instanceName());
        Self.Published.PI.ComputePI_Post(null, new Self.Published.PI.Digits(5));
        Self.Published.PI.Sum_Post(null, 1, 2, 3);
        Self.Published.PI.SetApartmentNumber_Post(null, 311);
        Self.Published.PI.SetApartmentNumber_Post(null, "C311");
        Self.Published.PI.TemplateStringTest_Post(null, "Hello World");
    }
}