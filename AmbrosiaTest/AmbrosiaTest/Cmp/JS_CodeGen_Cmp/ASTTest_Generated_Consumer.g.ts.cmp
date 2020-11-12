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
     * Example of a complex type.
     */
    export class Name
    {
        first: string;
        last: string;
        priorNames: Names[];

        constructor(first: string, last: string, priorNames: Names[])
        {
            this.first = first;
            this.last = last;
            this.priorNames = priorNames;
        }
    }

    /**
     * Example of a type that references another type.
     */
    export type Names = Name[];

    /**
     * Example of a nested complex type.
     */
    export class Nested
    {
        abc: { a: Uint8Array, b: { c: Names } };

        constructor(abc: { a: Uint8Array, b: { c: Names } })
        {
            this.abc = abc;
        }
    }

    /**
     * Example of an enum.
     */
    export enum Letters { A = 0, B = 3, C = 4, D = 9 }

    /**
     * Example of a [post] method that uses custom types.
     */
    export async function makeNameAsync(firstName?: string, lastName?: string): Promise<Names>
    {
        let postResult: Names = await IC.postAsync(DESTINATION_INSTANCE_NAME, "makeName", 1, null, POST_TIMEOUT_IN_MS, 
            IC.arg("firstName?", firstName), 
            IC.arg("lastName?", lastName));
        return (postResult);
    }

    /**
     * Example of a [post] method that uses custom types.
     */
    export function makeName(resultHandler: IC.PostResultHandler<Names>, firstName?: string, lastName?: string): void
    {
        IC.post(DESTINATION_INSTANCE_NAME, "makeName", 1, resultHandler, POST_TIMEOUT_IN_MS, 
            IC.arg("firstName?", firstName), 
            IC.arg("lastName?", lastName));
    }

    /**
     * Example of a [non-post] method
     */
    export function DoItFork(p1: Name[][]): void
    {
        IC.callFork(DESTINATION_INSTANCE_NAME, 123, { p1: p1 });
    }

    /**
     * Example of a [non-post] method
     */
    export function DoItImpulse(p1: Name[][]): void
    {
        IC.callImpulse(DESTINATION_INSTANCE_NAME, 123, { p1: p1 });
    }
}