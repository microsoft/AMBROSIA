// Note: The "ambrosia-node" package was installed using "npm install ..\..\Ambrosia-Node\ambrosia-node-0.0.80.tgz", 
//       which also installed all the required [production] package dependencies (eg. azure-storage).
import Ambrosia = require("ambrosia-node"); 
import Utils = Ambrosia.Utils;
import Meta = Ambrosia.Meta;
import IC = Ambrosia.IC;
import Configuration = Ambrosia.Configuration;
import StringEncoding = Ambrosia.StringEncoding;
import Process = require("process");

import * as PTI from "./PTI";
import * as Framework from "./PublisherFramework.g"; // This is a generated file
import * as PublishedAPI from "./ConsumerInterface.g"; // This is a generated file

main();
// codeGen();

// A "bootstrap" program that code-gen's the publisher/consumer TypeScript files.
async function codeGen()
{
    try
    {
        await Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen);
        const sourceFile: string = "./src/PTI.ts";
        const fileKind: Meta.GeneratedFileKind = Meta.GeneratedFileKind.All;
        const mergeType: Meta.FileMergeType = Meta.FileMergeType.None;
        Meta.emitTypeScriptFileFromSource(sourceFile, { apiName: "PTI", fileKind: fileKind, mergeType: mergeType, outputPath: "./src" });
    }
    catch (error: unknown)
    {
        Utils.tryLog(Utils.makeError(error));
    }
}

let _config: Configuration.AmbrosiaConfig | null = null;

// Note: For optimal performance, run this outside the debugger in a PowerShell (not CMD) window.
// TODO: Performance is worse if main() is not async: but why? It almost seems like additional thread(s) get spun up by node.exe, which then allows CPU usage to exceed 50%.
async function main()
{
    const ONE_KB: number = 1024;
    const ONE_MB: number = ONE_KB * ONE_KB;
    const CLIENT_ROLE_NAME = PTI.InstanceRoles[PTI.InstanceRoles.Client];
    const SERVER_ROLE_NAME = PTI.InstanceRoles[PTI.InstanceRoles.Server];
    const COMBINED_ROLE_NAME = PTI.InstanceRoles[PTI.InstanceRoles.Combined];
    const CLIENT_PARAMS: string[] = ["-sin", "--serverInstanceName", "-bpr", "--bytesPerRound", "-bsc", "--batchSizeCutoff", "-mms", "--maxMessageSize", "-n", "--numOfRounds", "-nds", "--noDescendingSize", "-fms", "--fixedMessageSize", "-eeb", "--expectedEchoedBytes", "-ipm", "--includePostMethod"];
    const SERVER_PARAMS: string[] = ["-nhc", "--noHealthCheck", "-bd", "--bidirectional", "-efb", "--expectedFinalBytes"];
    let instanceRole: string;
    let serverInstanceName: string;
    let bytesPerRound: number = 1024 * ONE_MB; // 1 GB
    let batchSizeCutoff: number = 10 * ONE_MB; // 10 MB
    let maxMessageSize: number = 64 * ONE_KB; // 64 KB
    let numberOfRounds: number = 1;
    let useDescendingSize: boolean;
    let useFixedMessageSize: boolean;
    let checkpointPadding: number = 0; // In bytes
    let noHealthCheck: boolean;
    let expectedFinalBytes: number = 0;
    let expectedEchoedBytes: number = 0;
    // Note: 'autoContinue' defaults to false in C# PTI. However, if we did the same we'd want to set --autoContinue in runClient.ps1/runServer.ps1 so that the user
    //       wouldn't have to explicitly add it as a parameter each time. But if we did that then the user would have no way to way to turn off --autoContinue.
    //       So instead we default it to true, and consume it as "=true|false" parameter. An alternative would be to rename it to --waitAtStart and default it to false.
    let autoContinue: boolean = true;
    let bidirectional: boolean = false;
    let verifyPayload: boolean = false;
    let includePostMethod: boolean = false;

    /** [Local function] Returns the first command-line arg (if any) found in the supplied 'paramList', otherwise returns null. */
    function getCommandLineArgIn(paramList: string[]): string | null
    {
        const args: string[] = Process.argv;
        for (let i = 2; i < args.length; i++)
        {
            const paramName: string = args[i].split("=")[0];
            if (paramList.indexOf(paramName) !== -1)
            {
                return (paramName);
            }
        }
        return (null);
    }

    try
    {
        // Parse command-line parameters
        try
        {
            if (Utils.hasCommandLineArg("-h|help"))
            {
                throw new Error("ShowHelp");
            }
            instanceRole = Utils.getCommandLineArg("-ir|instanceRole", COMBINED_ROLE_NAME); // JS-only
            serverInstanceName = Utils.getCommandLineArg("-sin|serverInstanceName", ""); // JS-only
            bytesPerRound = parseInt(Utils.getCommandLineArg("-bpr|bytesPerRound", bytesPerRound.toString())); // JS-only
            batchSizeCutoff = parseInt(Utils.getCommandLineArg("-bsc|batchSizeCutoff", batchSizeCutoff.toString())); // JS-only
            maxMessageSize = parseInt(Utils.getCommandLineArg("-mms|maxMessageSize", maxMessageSize.toString()));
            numberOfRounds = parseInt(Utils.getCommandLineArg("-n|numOfRounds", numberOfRounds.toString()));
            useFixedMessageSize = Utils.hasCommandLineArg("-fms|fixedMessageSize"); // JS-only
            useDescendingSize = !Utils.hasCommandLineArg("-nds|noDescendingSize") && !useFixedMessageSize;
            checkpointPadding = parseInt(Utils.getCommandLineArg("-m|memoryUsed", checkpointPadding.toString()));
            noHealthCheck = Utils.hasCommandLineArg("-nhc|noHealthCheck"); // JS-only
            expectedFinalBytes = parseInt(Utils.getCommandLineArg("-efb|expectedFinalBytes", expectedFinalBytes.toString())); // JS-only
            autoContinue = Utils.equalIgnoringCase(Utils.getCommandLineArg("-c|autoContinue", autoContinue.toString()), "true");
            bidirectional = Utils.hasCommandLineArg("-bd|bidirectional");
            expectedEchoedBytes = parseInt(Utils.getCommandLineArg("-eeb|expectedEchoedBytes", expectedEchoedBytes.toString())); // JS-only
            verifyPayload = Utils.hasCommandLineArg("-vp|verifyPayload"); // JS-only
            includePostMethod = Utils.hasCommandLineArg("-ipm|includePostMethod"); // JS-only

            const unknownArgName: string | null = Utils.getUnknownCommandLineArg();
            if (unknownArgName)
            {
                throw new Error(`Invalid parameter: The supplied '${unknownArgName}' parameter is unknown; specify '--help' to see all possible parameters`);
            }

            // Validate parameters
            const availableRoles: string[] = Utils.getEnumKeys("InstanceRoles", PTI.InstanceRoles);
            if (availableRoles.indexOf(instanceRole) === -1)
            {
                throw new Error(`Invalid parameter: The supplied --instanceRole ('${instanceRole}') must be '${availableRoles.join("' or '")}'`);
            }

            // Check that all the supplied parameters are valid for the role
            if ((instanceRole === CLIENT_ROLE_NAME) && getCommandLineArgIn(SERVER_PARAMS))
            {
                throw new Error(`Invalid parameter: The ${getCommandLineArgIn(SERVER_PARAMS)} parameter is only valid when --instanceRole is '${SERVER_ROLE_NAME}' (or '${COMBINED_ROLE_NAME}')`);
            }
            if ((instanceRole === SERVER_ROLE_NAME) && getCommandLineArgIn(CLIENT_PARAMS))
            {
                throw new Error(`Invalid parameter: The ${getCommandLineArgIn(CLIENT_PARAMS)} parameter is only valid when --instanceRole is '${CLIENT_ROLE_NAME}' (or '${COMBINED_ROLE_NAME}')`);
            }

            if ((instanceRole === CLIENT_ROLE_NAME) && !serverInstanceName)
            {
                throw new Error(`Missing parameter: The --serverInstanceName is required when --instanceRole is '${CLIENT_ROLE_NAME}'`);
            }

            const bytesPerRoundPower2: number = Math.log2(bytesPerRound);
            if (!Number.isInteger(bytesPerRoundPower2) || (bytesPerRoundPower2 <= 6))
            {
                throw new Error(`Invalid parameter: The supplied --bytesPerRound (${bytesPerRound}) must be an exact power of 2 greater than 6 (128+)`);
            }

            const maxMessageSizePower2 = Math.log2(maxMessageSize);
            if (!Number.isInteger(maxMessageSizePower2) || (maxMessageSizePower2 < 6) || (maxMessageSizePower2 > bytesPerRoundPower2))
            {
                throw new Error(`Invalid parameter: --maxMessageSize (${maxMessageSize}) must be set to an exact power of 2 between 6 (64) and ${bytesPerRoundPower2} (${Math.pow(2, bytesPerRoundPower2)}) [the --bytesPerRound value]`);
            }

            if (batchSizeCutoff > bytesPerRound)
            {
                throw new Error(`Invalid parameter: --batchSizeCutoff (${batchSizeCutoff}) must be less than or equal to --bytesPerRound (${bytesPerRound})`);
            }

            if (useDescendingSize && (numberOfRounds > 1) && ((numberOfRounds - 1) > (maxMessageSizePower2 - 6)))
            {
                // This is not an error condition, but the result may not be what the user expected so we emit a warning
                console.log(`WARNING: The supplied --numOfRounds (${numberOfRounds}) is larger than needed to reach the 64 byte minimum message size when using "descending size"; the final ${numberOfRounds - (maxMessageSizePower2 - 6) - 1} rounds will use a message size of 64`);
            }

            const MAX_UINT32: number = Math.pow(2, 32) - 1; // The message ID is sent as a Uint32, so we can only send MAX_UINT32 messages in total (in all rounds)
            const maxMessagesPerRound: number = (bytesPerRound / 64); // For simplicity, we assume the "worst" case (ie. all messages being 64 bytes)
            const maxRounds: number = Math.floor(MAX_UINT32 / maxMessagesPerRound);
            if ((numberOfRounds < 1) || (numberOfRounds > maxRounds))
            {
                // For example, if bytesPerRound is 1GB then maxRounds will be 255
                throw new Error(`Invalid parameter: The supplied --numOfRounds (${numberOfRounds}) must be between 1 and ${maxRounds}`);
            }

            if (expectedFinalBytes > 0)
            {
                if ((instanceRole === COMBINED_ROLE_NAME) && (expectedFinalBytes != numberOfRounds * bytesPerRound))
                {
                    throw new Error(`Invalid parameter: The supplied --expectedFinalBytes (${expectedFinalBytes}) should either be 0 or ${numberOfRounds * bytesPerRound}`);
                }

                // Validate that expectedFinalBytes is a multiple of some number that's a power of 2
                // (eg. 256MB [Client #1] + 128MB [Client #2] = 384MB, which is a multiple of a 128MB so it's valid).
                const expectedFinalBytesPower2: number = Math.log2(expectedFinalBytes);
                if (!Number.isInteger(expectedFinalBytesPower2))
                {
                    let isMultipleOfPowerOf2: boolean = false;
                    for (let powerOf2 = Math.floor(expectedFinalBytesPower2) - 1; powerOf2 > 0; powerOf2--)
                    {
                        if (expectedFinalBytes % Math.pow(2, powerOf2) === 0)
                        {
                            isMultipleOfPowerOf2 = true;
                            break;
                        }
                    }
                    if (!isMultipleOfPowerOf2)
                    {
                        throw new Error(`Invalid parameter: The supplied --expectedFinalBytes (${expectedFinalBytes}) must be a multiple of a number that is an exact power of 2`);
                    }
                }
            }
            else
            {
                // If possible, set a default for expectedFinalBytes. Note that for the explicit 'Server' role there can be
                // multiple clients, each with its own bytesPerRound and numberOfRounds, so we can't compute a default value.
                if (instanceRole === COMBINED_ROLE_NAME)
                {
                    expectedFinalBytes = numberOfRounds * bytesPerRound; // This will always be multiple of some number that's a power of 2
                }
            }

            if ((instanceRole === CLIENT_ROLE_NAME) && (expectedEchoedBytes > 0) && (expectedEchoedBytes !== (numberOfRounds * bytesPerRound)))
            {
                throw new Error(`Invalid parameter: The supplied --expectedEchoedBytes (${expectedEchoedBytes}) should either be 0 or ${numberOfRounds * bytesPerRound}`);
            }

            if ((instanceRole === COMBINED_ROLE_NAME) && bidirectional)
            {
                if (expectedEchoedBytes === 0)
                {
                    expectedEchoedBytes = expectedFinalBytes;
                }
                else
                {
                    if (expectedEchoedBytes !== expectedFinalBytes)
                    {
                        throw new Error(`Invalid parameter: The supplied --expectedEchoedBytes (${expectedEchoedBytes}) must be the same as --expectedFinalBytes (${expectedFinalBytes}) when --bidirectional is specified in the '${COMBINED_ROLE_NAME}' role`);
                    }
                }
            }

            const nodeMaxOldGenerationSize: number = Utils.getNodeLongTermHeapSizeInBytes(); // _appState will end up in the long-term ("old") GC heap
            const maxCheckpointPadding: number = Math.floor((nodeMaxOldGenerationSize * 0.8) / ONE_MB) * ONE_MB; // Largest whole MB <= 80% of nodeMaxOldGenerationSize
            if ((checkpointPadding < 0) || (checkpointPadding > maxCheckpointPadding))
            {
                throw new Error(`Invalid parameter: The supplied memoryUsed (${checkpointPadding}) must be between 0 and ${maxCheckpointPadding} (${maxCheckpointPadding / ONE_MB} MB); set the node.js V8 parameter '--max-old-space-size' to raise the upper limit (see https://nodejs.org/api/cli.html)`);
            }
        }
        catch (e: unknown)
        {
            const error: Error = Utils.makeError(e);
 
            console.log("");
            if (error.message === "ShowHelp")
            {
                console.log("  PTI Parameters:");
                console.log("  ===============");
                console.log("  -h|--help                    : [Common] Displays this help message");
                console.log("  -ir|--instanceRole=          : [Common] The role of this instance in the test ('Server', 'Client', or 'Combined'); defaults to 'Combined'");
                console.log("  -m|--memoryUsed=             : [Common] Optional \"padding\" (in bytes) used to simulate large checkpoints by being included in app state; defaults to 0");
                console.log("  -c|--autoContinue=           : [Common] Whether to continue automatically at startup (if true), or wait for the 'Enter' key (if false); defaults to true");
                console.log("  -vp|--verifyPayload          : [Common] Enables verifying the message payload bytes (for 'doWork' on the server, and 'doWorkEcho' on the client); enabling this will decrease performance");
                console.log("  -sin|--serverInstanceName=   : [Client] The name of the instance that's acting in the 'Server' role for the test; only required when --role is 'Client'");
                console.log("  -bpr|--bytesPerRound=        : [Client] The total number of message payload bytes that will be sent in a single round; defaults to 1 GB");
                console.log("  -bsc|--batchSizeCutoff=      : [Client] Once the total number of message payload bytes queued reaches (or exceeds) this limit, then the batch will be sent; defaults to 10 MB");
                console.log("  -mms|--maxMessageSize=       : [Client] The maximum size (in bytes) of the message payload; must be a power of 2 (eg. 65536), and be at least 64; defaults to 64KB");
                console.log("  -n|--numOfRounds=            : [Client] The number of rounds (of size bytesPerRound) to work through; each round will use a [potentially] different message size; defaults to 1");
                console.log("  -nds|--noDescendingSize      : [Client] Disables descending (halving) the message size after each round; instead, a random size [power of 2] between 64 and --maxMessageSize will be used");
                console.log("  -fms|--fixedMessageSize      : [Client] All messages (in all rounds) will be of size --maxMessageSize; --noDescendingSize (if also supplied) will be ignored");
                console.log("  -eeb|--expectedEchoedBytes=  : [Client] The total number of \"echoed\" bytes expected to be received from the server when --bidirectional is specified; the client will report a \"success\" message when this number of bytes have been received");
                console.log("  -ipm|--includePostMethod     : [Client] Includes a 'post' method call in the test");
                console.log("  -nhc|--noHealthCheck         : [Server] Disables the periodic server health check (requested via an Impulse message)");
                console.log("  -bd|--bidirectional          : [Server] Enables echoing the 'doWork' method call back to the client(s)");
                console.log("  -efb|--expectedFinalBytes=   : [Server] The total number of bytes expected to be received from all clients; the server will report a \"success\" message when this number of bytes have been received");
            }
            else
            {
                console.log(error.message);
            }
            console.log("");
            return;
        }

        // Run the app
        await Ambrosia.initializeAsync();
        const outputLoggingLevel: Utils.LoggingLevel = Configuration.loadedConfig().lbOptions.outputLoggingLevel;

        if (outputLoggingLevel !== Utils.LoggingLevel.Minimal)
        {
            PTI.log(`Warning: Set the 'outputLoggingLevel' in ${Configuration.loadedConfigFileName()} to 'Minimal' (not '${Utils.LoggingLevel[outputLoggingLevel]}') for optimal performance`);
        }

        // Check if the effective batch size exceeds 50% of the "maxMessageQueueSizeInMB" config setting.
        // Note: We use 50% (not 100%) because we only know the message payload size (maxMessageSize), not the actual on-the-wire message size, but at 50% we are guaranteed to be able to queue 1 batch.
        const effectiveBatchSize: number = (maxMessageSize >= batchSizeCutoff) ? maxMessageSize : (Math.ceil(batchSizeCutoff / maxMessageSize) * maxMessageSize);
        const maxMessageQueueSizeInBytes: number = Configuration.loadedConfig().lbOptions.maxMessageQueueSizeInMB * ONE_MB;
        const maxEffectiveBatchSize: number = maxMessageQueueSizeInBytes / 2;
        if (effectiveBatchSize > maxEffectiveBatchSize)
        {
            let maxBatchSizeCutoff: number = 0;
            if (batchSizeCutoff > maxMessageSize)
            {
                if (maxEffectiveBatchSize % maxMessageSize === 0)
                {
                    maxBatchSizeCutoff = maxEffectiveBatchSize;
                }
                else
                {
                    maxBatchSizeCutoff = Math.floor(maxEffectiveBatchSize / maxMessageSize) * maxMessageSize;
                }
            }
            console.log(`\nInvalid parameter: The effective batch size (${effectiveBatchSize}) cannot be larger than ${maxEffectiveBatchSize}; ` +
                        `reduce ${(maxMessageSize > batchSizeCutoff) ? `--maxMessageSize (to ${maxEffectiveBatchSize}` : `--batchSizeCutoff (to ${maxBatchSizeCutoff}`} or less) to resolve this\n`);
            return;
        }

        // For the 'Server' or 'Combined' role, set serverInstanceName to the local instance name (for the 'Client' role, we've already checked that a --serverInstanceName was supplied)
        if (!serverInstanceName)
        {
            serverInstanceName = IC.instanceName();
        }
        // Prevent a client instance from targeting itself as the server
        if ((instanceRole === CLIENT_ROLE_NAME) && Utils.equalIgnoringCase(serverInstanceName, IC.instanceName()))
        {
            console.log(`\nInvalid parameter: When --instanceRole is '${CLIENT_ROLE_NAME}' the --serverInstanceName cannot reference the local instance ('${IC.instanceName()}'); instead, set --instanceRole to '${COMBINED_ROLE_NAME}'\n`);
            return;
        }

        // The client sends its name in the message payload, which can be as small as 64 bytes, so the client name is limited to 59 bytes (+1 byte for name length, +4 bytes for call number = 64 bytes)
        if (((instanceRole === CLIENT_ROLE_NAME) || (instanceRole === COMBINED_ROLE_NAME)) && (StringEncoding.toUTF8Bytes(IC.instanceName()).length > 59))
        {
            console.log(`\nThe client instance name ('${IC.instanceName()}') is too long; the maximum allowed length is 59 bytes`);
            return;
        }

        PTI.log(`Local instance is running in the '${instanceRole}' PTI role`);
        if (!autoContinue)
        {
            // For debugging we don't want to auto-continue, but for test automation we do
            PTI.log(`Pausing execution of '${IC.instanceName()}'. Press 'Enter' to continue...`);
            await Utils.consoleReadKeyAsync([Utils.ENTER_KEY]);
        }

        _config = new Configuration.AmbrosiaConfig(Framework.messageDispatcher, Framework.checkpointProducer, Framework.checkpointConsumer, PublishedAPI.postResultDispatcher);
        PTI.State._appState = IC.start(_config, PTI.State.AppState);
        
        // Preserve command-line parameters in app-state [so that they're available upon re-start, in which case these
        // command-line parameter values will be ignored since they'll be overwritten when the checkpoint is restored]
        // Note: 'autoContinue' is not included in app-state because it's used for debugging.
        PTI.State._appState.instanceRole = PTI.InstanceRoles[instanceRole as keyof typeof PTI.InstanceRoles];
        PTI.State._appState.serverInstanceName = serverInstanceName;
        PTI.State._appState.bytesPerRound = bytesPerRound;
        PTI.State._appState.batchSizeCutoff = batchSizeCutoff;
        PTI.State._appState.maxMessageSize = maxMessageSize;
        PTI.State._appState.numRounds = numberOfRounds;
        PTI.State._appState.numRoundsLeft = numberOfRounds;
        PTI.State._appState.useDescendingSize = useDescendingSize;
        PTI.State._appState.useFixedMessageSize = useFixedMessageSize;
        PTI.State._appState.noHealthCheck = noHealthCheck;
        PTI.State._appState.bidirectional = bidirectional;
        PTI.State._appState.expectedFinalBytesTotal = expectedFinalBytes;
        PTI.State._appState.expectedEchoedBytesTotal = expectedEchoedBytes;

        if (checkpointPadding > 0)
        {
            const ONE_HUNDRED_MB: number = 100 * ONE_MB;
            PTI.State._appState.checkpointPadding = new Array<Uint8Array>();

            let padding: Uint8Array = new Uint8Array(checkpointPadding % ONE_HUNDRED_MB);
            PTI.State._appState.checkpointPadding.push(padding);

            for (let i = 0; i < Math.floor(checkpointPadding / ONE_HUNDRED_MB); i++)
            {
                padding = new Uint8Array(ONE_HUNDRED_MB).fill(i + 1);
                PTI.State._appState.checkpointPadding.push(padding);
            }
        }

        PTI.State._appState.verifyPayload = verifyPayload;
        PTI.State._appState.includePostMethod = includePostMethod;
    }
    catch (error: unknown)
    {
        Utils.tryLog(Utils.makeError(error));
    }
}