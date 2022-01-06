// Generated consumer-side API for the 'TestApp' Ambrosia Node app/service.
// Publisher: Rich (MSR) [richardh@microsoft.com].
// Note: This file was generated on 2021/07/18 at 23:21:14.774.
// Note [to publisher]: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import Ambrosia = require("ambrosia-node");
import IC = Ambrosia.IC;
import Utils = Ambrosia.Utils;

const _knownDestinations: string[] = []; // All previously used destination instances (the 'TestApp' Ambrosia app/service can be running on multiple instances, potentially simultaneously)
let _destinationInstanceName: string = ""; // The current destination instance
let _postTimeoutInMs: number = 8000; // -1 = Infinite

/** 
 * Sets the destination instance name that the API targets.\
 * Must be called at least once (with the name of a registered Ambrosia instance that implements the 'TestApp' API) before any other method in the API is used.
 */
export function setDestinationInstance(instanceName: string): void
{
    _destinationInstanceName = instanceName.trim();
    if (_destinationInstanceName && (_knownDestinations.indexOf(_destinationInstanceName) === -1))
    {
        _knownDestinations.push(_destinationInstanceName);
    }
}

/** Returns the destination instance name that the API currently targets. */
export function getDestinationInstance(): string
{
    return (_destinationInstanceName);
}

/** Throws if _destinationInstanceName has not been set. */
function checkDestinationSet(): void
{
    if (!_destinationInstanceName)
    {
        throw new Error("setDestinationInstance() must be called to specify the target destination before the 'TestApp' API can be used.");
    }
}

/**
 * Sets the post method timeout interval (in milliseconds), which is how long to wait for a post result from the destination instance before raising an error.\
 * All post methods will use this timeout value. Specify -1 for no timeout. 
 */
export function setPostTimeoutInMs(timeoutInMs: number): void
{
    _postTimeoutInMs = Math.max(-1, timeoutInMs);
}

/**
 * Returns the post method timeout interval (in milliseconds), which is how long to wait for a post result from the destination instance before raising an error.\
 * A value of -1 means there is no timeout.
 */
export function getPostTimeoutInMs(): number
{
    return (_postTimeoutInMs);
}

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
         * *Note: The result (number) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
         * 
         * Returns pi computed to the specified number of digits.
         */
        export function ComputePI_Post(callContextData: any, digits?: Digits): number
        {
            checkDestinationSet();
            const callID = IC.postFork(_destinationInstanceName, "ComputePI", 1, _postTimeoutInMs, callContextData, IC.arg("digits?", digits));
            return (callID);
        }

        /**
         * *Note: The result (number) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
         * 
         * Returns pi computed to the specified number of digits.
         */
        export function ComputePI_PostByImpulse(callContextData: any, digits?: Digits): void
        {
            checkDestinationSet();
            IC.postByImpulse(_destinationInstanceName, "ComputePI", 1, _postTimeoutInMs, callContextData, IC.arg("digits?", digits));
        }

        /**
         * *Note: The result (number) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
         * 
         * Returns the sum of the specified numbers [tests passing rest parameters].
         */
        export function Sum_Post(callContextData: any, ...numbers: number[]): number
        {
            checkDestinationSet();
            const callID = IC.postFork(_destinationInstanceName, "Sum", 1, _postTimeoutInMs, callContextData, IC.arg("numbers", numbers));
            return (callID);
        }

        /**
         * *Note: The result (number) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
         * 
         * Returns the sum of the specified numbers [tests passing rest parameters].
         */
        export function Sum_PostByImpulse(callContextData: any, ...numbers: number[]): void
        {
            checkDestinationSet();
            IC.postByImpulse(_destinationInstanceName, "Sum", 1, _postTimeoutInMs, callContextData, IC.arg("numbers", numbers));
        }

        /**
         * *Note: The result (number | string) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
         * 
         * "Sets" the apartment number of an address (eg. 127 or "C314") [tests passing a union type].
         */
        export function SetApartmentNumber_Post(callContextData: any, aptNumber: number | string): number
        {
            checkDestinationSet();
            const callID = IC.postFork(_destinationInstanceName, "SetApartmentNumber", 1, _postTimeoutInMs, callContextData, IC.arg("aptNumber", aptNumber));
            return (callID);
        }

        /**
         * *Note: The result (number | string) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
         * 
         * "Sets" the apartment number of an address (eg. 127 or "C314") [tests passing a union type].
         */
        export function SetApartmentNumber_PostByImpulse(callContextData: any, aptNumber: number | string): void
        {
            checkDestinationSet();
            IC.postByImpulse(_destinationInstanceName, "SetApartmentNumber", 1, _postTimeoutInMs, callContextData, IC.arg("aptNumber", aptNumber));
        }

        /**
         * *Note: The result (\`Hello ${"World" | "Universe"}\`) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
         * 
         * Tests a string-template type.
         */
        export function TemplateStringTest_Post(callContextData: any, template: `Hello ${"World" | "Universe"}`): number
        {
            checkDestinationSet();
            const callID = IC.postFork(_destinationInstanceName, "TemplateStringTest", 1, _postTimeoutInMs, callContextData, IC.arg("template", template));
            return (callID);
        }

        /**
         * *Note: The result (\`Hello ${"World" | "Universe"}\`) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
         * 
         * Tests a string-template type.
         */
        export function TemplateStringTest_PostByImpulse(callContextData: any, template: `Hello ${"World" | "Universe"}`): void
        {
            checkDestinationSet();
            IC.postByImpulse(_destinationInstanceName, "TemplateStringTest", 1, _postTimeoutInMs, callContextData, IC.arg("template", template));
        }
    }
}

/**
 * Handler for the results of previously called post methods (in Ambrosia, only 'post' methods return values). See Messages.PostResultDispatcher.\
 * Must return true only if the result (or error) was handled.
 */
export function postResultDispatcher(senderInstanceName: string, methodName: string, methodVersion: number, callID: number, callContextData: any, result: any, errorMsg: string): boolean
{
    const sender: string = IC.isSelf(senderInstanceName) ? "local" : `'${senderInstanceName}'`;
    let handled: boolean = true;

    if (_knownDestinations.indexOf(senderInstanceName) === -1)
    {
        return (false); // Not handled: this post result is from a different instance than the destination instance currently (or previously) targeted by the 'TestApp' API
    }

    if (errorMsg)
    {
        switch (methodName)
        {
            case "ComputePI":
            case "Sum":
            case "SetApartmentNumber":
            case "TemplateStringTest":
                Utils.log(`Error: ${errorMsg}`);
                break;
            default:
                handled = false;
                break;
        }
    }
    else
    {
        switch (methodName)
        {
            case "ComputePI":
                const ComputePI_Result: number = result;
                Utils.log(`pi = ${ComputePI_Result}`);
                break;
            case "Sum":
                const Sum_Result: number = result;
                Utils.log(`sum = ${Sum_Result}`);
            case "SetApartmentNumber":
                const SetApartmentNumber_Result: number | string = result;
                const type: string = typeof SetApartmentNumber_Result;
                const delimiter: string = (type === "string") ? "\"" : "";
                Utils.log(`apartmentNumber is a ${type} (${delimiter}${SetApartmentNumber_Result}${delimiter})`);
                break;
            case "TemplateStringTest":
                const TemplateStringTest_Result: `Hello ${"World" | "Universe"}` = result;
                Utils.log(`template = "${TemplateStringTest_Result}"`);
                break;
            default:
                handled = false;
                break;
        }
    }
    return (handled);
}