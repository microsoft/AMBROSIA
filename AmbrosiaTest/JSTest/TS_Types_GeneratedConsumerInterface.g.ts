// Generated consumer-side API for the 'TS_Types_Generated' Ambrosia Node app/service.
// Publisher: (Not specified).
// Note: This file was generated
// Note [to publisher]: You can edit this file, but to avoid losing your changes be sure to specify a 'mergeType' other than 'None' (the default is 'Annotate') when re-running emitTypeScriptFile[FromSource]().
import Ambrosia = require("ambrosia-node");
import IC = Ambrosia.IC;
import Utils = Ambrosia.Utils;

const _knownDestinations: string[] = []; // All previously used destination instances (the 'TS_Types_Generated' Ambrosia app/service can be running on multiple instances, potentially simultaneously)
let _destinationInstanceName: string = ""; // The current destination instance
let _postTimeoutInMs: number = 8000; // -1 = Infinite

/** 
 * Sets the destination instance name that the API targets.\
 * Must be called at least once (with the name of a registered Ambrosia instance that implements the 'TS_Types_Generated' API) before any other method in the API is used.
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
        throw new Error("setDestinationInstance() must be called to specify the target destination before the 'TS_Types_Generated' API can be used.");
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

/**
Test File to test all the Types for typescripts
Has the basic types
 */
export namespace Test
{
    /*********** Enum type (numeric enum - strings as number) as return */
    export enum PrintMedia { Newspaper = 1, Newsletter = 2, Magazine = 3, Book = 4 }

    /********** Enum type (Reverse Mapped enum - can access the value of a member and also a member name from its value) */
    export enum PrintMediaReverse { NewspaperReverse = 1, NewsletterReverse = 2, MagazineReverse = 3, BookReverse = 4 }

    export enum MyEnumAA { aa = -1, bb = -123, cc = 123, dd = 0 }

    export enum MyEnumBBB { aaa = -1, bbb = 0 }

    /*************** Complex Type */
    export class Name
    {
        first: string;
        last: string;

        constructor(first: string, last: string)
        {
            this.first = first;
            this.last = last;
        }
    }

    /************** Example of a type that references another type *************.
     */
    export type Names = Name[];

    /************** Example of a nested complex type.*************
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
     * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
     * 
     * *********** Primitives - bool, string, number, array
     */
    export function BasicTypes_Post(callContextData: any, isFalse: boolean, height: number, mystring?: string, mystring2?: string, my_array?: number[], notSure?: any): number
    {
        checkDestinationSet();
        const callID = IC.postFork(_destinationInstanceName, "BasicTypes", 1, _postTimeoutInMs, callContextData, 
            IC.arg("isFalse", isFalse), 
            IC.arg("height", height), 
            IC.arg("mystring?", mystring), 
            IC.arg("mystring2?", mystring2), 
            IC.arg("my_array?", my_array), 
            IC.arg("notSure?", notSure));
        return (callID);
    }

    /**
     * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
     * 
     * *********** Primitives - bool, string, number, array
     */
    export function BasicTypes_PostByImpulse(callContextData: any, isFalse: boolean, height: number, mystring?: string, mystring2?: string, my_array?: number[], notSure?: any): void
    {
        checkDestinationSet();
        IC.postByImpulse(_destinationInstanceName, "BasicTypes", 1, _postTimeoutInMs, callContextData, 
            IC.arg("isFalse", isFalse), 
            IC.arg("height", height), 
            IC.arg("mystring?", mystring), 
            IC.arg("mystring2?", mystring2), 
            IC.arg("my_array?", my_array), 
            IC.arg("notSure?", notSure));
    }

    /**
     * *Note: The result (PrintMedia) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
     * 
     * ******* Function using / returning Numeric Enum
     */
    export function getMedia_Post(callContextData: any, mediaName: string): number
    {
        checkDestinationSet();
        const callID = IC.postFork(_destinationInstanceName, "getMedia", 1, _postTimeoutInMs, callContextData, IC.arg("mediaName", mediaName));
        return (callID);
    }

    /**
     * *Note: The result (PrintMedia) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
     * 
     * ******* Function using / returning Numeric Enum
     */
    export function getMedia_PostByImpulse(callContextData: any, mediaName: string): void
    {
        checkDestinationSet();
        IC.postByImpulse(_destinationInstanceName, "getMedia", 1, _postTimeoutInMs, callContextData, IC.arg("mediaName", mediaName));
    }

    /**
     * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
     * 
     * *********** Void type
     */
    export function warnUser_Post(callContextData: any): number
    {
        checkDestinationSet();
        const callID = IC.postFork(_destinationInstanceName, "warnUser", 1, _postTimeoutInMs, callContextData);
        return (callID);
    }

    /**
     * *Note: The result (void) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
     * 
     * *********** Void type
     */
    export function warnUser_PostByImpulse(callContextData: any): void
    {
        checkDestinationSet();
        IC.postByImpulse(_destinationInstanceName, "warnUser", 1, _postTimeoutInMs, callContextData);
    }

    /**
     * *Note: The result (Names) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
     * 
     * ************ Example of a [post] method that uses custom types.
     */
    export function makeName_Post(callContextData: any, firstName?: string, lastName?: string): number
    {
        checkDestinationSet();
        const callID = IC.postFork(_destinationInstanceName, "makeName", 1, _postTimeoutInMs, callContextData, 
            IC.arg("firstName?", firstName), 
            IC.arg("lastName?", lastName));
        return (callID);
    }

    /**
     * *Note: The result (Names) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
     * 
     * ************ Example of a [post] method that uses custom types.
     */
    export function makeName_PostByImpulse(callContextData: any, firstName?: string, lastName?: string): void
    {
        checkDestinationSet();
        IC.postByImpulse(_destinationInstanceName, "makeName", 1, _postTimeoutInMs, callContextData, 
            IC.arg("firstName?", firstName), 
            IC.arg("lastName?", lastName));
    }

    /**
     * *Note: The result (number) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
     * 
     * ******* Function returning number
     */
    export function return_number_Post(callContextData: any, strvalue: string): number
    {
        checkDestinationSet();
        const callID = IC.postFork(_destinationInstanceName, "return_number", 1, _postTimeoutInMs, callContextData, IC.arg("strvalue", strvalue));
        return (callID);
    }

    /**
     * *Note: The result (number) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
     * 
     * ******* Function returning number
     */
    export function return_number_PostByImpulse(callContextData: any, strvalue: string): void
    {
        checkDestinationSet();
        IC.postByImpulse(_destinationInstanceName, "return_number", 1, _postTimeoutInMs, callContextData, IC.arg("strvalue", strvalue));
    }

    /**
     * *Note: The result (string) produced by this post method is received via the PostResultDispatcher provided to IC.start(). Returns the post method callID.*
     * 
     * ******* Function returning string
     */
    export function returnstring_Post(callContextData: any, numvalue: number): number
    {
        checkDestinationSet();
        const callID = IC.postFork(_destinationInstanceName, "returnstring", 1, _postTimeoutInMs, callContextData, IC.arg("numvalue", numvalue));
        return (callID);
    }

    /**
     * *Note: The result (string) produced by this post method is received via the PostResultDispatcher provided to IC.start().*
     * 
     * ******* Function returning string
     */
    export function returnstring_PostByImpulse(callContextData: any, numvalue: number): void
    {
        checkDestinationSet();
        IC.postByImpulse(_destinationInstanceName, "returnstring", 1, _postTimeoutInMs, callContextData, IC.arg("numvalue", numvalue));
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
        return (false); // Not handled: this post result is from a different instance than the destination instance currently (or previously) targeted by the 'TS_Types_Generated' API
    }

    if (errorMsg)
    {
        switch (methodName)
        {
            case "BasicTypes":
            case "getMedia":
            case "warnUser":
            case "makeName":
            case "return_number":
            case "returnstring":
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
            case "BasicTypes":
                // TODO: Handle the method completion (it returns void), optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "getMedia":
                const getMedia_Result: Test.PrintMedia = result;
                // TODO: Handle the result, optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "warnUser":
                // TODO: Handle the method completion (it returns void), optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "makeName":
                const makeName_Result: Test.Names = result;
                // TODO: Handle the result, optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "return_number":
                const return_number_Result: number = result;
                // TODO: Handle the result, optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            case "returnstring":
                const returnstring_Result: string = result;
                // TODO: Handle the result, optionally using the callContextData passed in the call
                Utils.log(`Post method '${methodName}' from ${sender} IC succeeded`);
                break;
            default:
                handled = false;
                break;
        }
    }
    return (handled);
}