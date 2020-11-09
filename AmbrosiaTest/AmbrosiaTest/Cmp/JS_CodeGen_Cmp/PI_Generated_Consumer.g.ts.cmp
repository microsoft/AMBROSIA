// Generated consumer-side API for the 'server' Ambrosia Node instance.
// Note: This file was generated
// Note: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import Ambrosia = require("ambrosia-node");
import IC = Ambrosia.IC;

let DESTINATION_INSTANCE_NAME: string = "server";
let POST_TIMEOUT_IN_MS: number = 8000; // -1 = Infinite

/**
 * Parameter type for the 'ComputePI' method.
 */
export class Digit3
{
    count: number;

    constructor(count: number)
    {
        this.count = count;
    }
}

export namespace Test
{
    /**
     * Parameter type for the 'Today' method.
     */
    export enum DayOfWeek { Sunday = 0, Monday = 1, Tuesday = 2, Wednesday = 3, Thursday = 4, Friday = 5, Saturday = 6 }

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
     * Parameter type for the 'ComputePI' method.
     */
    export class Digit2
    {
        count: number;

        constructor(count: number)
        {
            this.count = count;
        }
    }

    /**
     * Parameter type for the 'ComputePI' method.
     */
    export class Digit3
    {
        count: number;

        constructor(count: number)
        {
            this.count = count;
        }
    }

    /**
     * Some new test.
     * @param person Datails of a person.
     */
    export async function NewTestAsync(person: { age: number }): Promise<{ age: number }>
    {
        let postResult: { age: number } = await IC.postAsync(DESTINATION_INSTANCE_NAME, "NewTest", 1, null, POST_TIMEOUT_IN_MS, IC.arg("person", person));
        return (postResult);
    }

    /**
     * Some new test.
     * @param person Datails of a person.
     */
    export function NewTest(resultHandler: IC.PostResultHandler<{ age: number }>, person: { age: number }): void
    {
        IC.post(DESTINATION_INSTANCE_NAME, "NewTest", 1, resultHandler, POST_TIMEOUT_IN_MS, IC.arg("person", person));
    }

    export function DoItFork(dow: DayOfWeek): void
    {
        IC.callFork(DESTINATION_INSTANCE_NAME, 1, { dow: dow });
    }

    export function DoItImpulse(dow: DayOfWeek): void
    {
        IC.callImpulse(DESTINATION_INSTANCE_NAME, 1, { dow: dow });
    }

    export namespace TestInner
    {
        /**
         * Parameter type for the 'ComputePI' method.
         */
        export class Digit3
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