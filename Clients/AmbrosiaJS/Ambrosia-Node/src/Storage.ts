// Module for reading from central storage (typically IC settings).
import AzureStorage = require("azure-storage");
import Process = require("process");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "./Configuration"
import * as StringEncoding from "./StringEncoding";
import * as Streams from "./Streams";
import * as Utils from "./Utils/Utils-Index";
import { RejectedPromise } from "./ICProcess"; // There is no re-write issue here as this is just a type

/** Needed as a workaround for when 'strictNullChecks' is true in tsconfig.json. */
// @ts-tactical-any-cast: Suppress error "Type 'null' is not assignable to type 'TableContinuationToken'. ts(2322)" [because we use 'strictNullChecks', but the azure-storage npm package does not]
const _nullTableSvcContinuationToken: AzureStorage.TableService.TableContinuationToken = null as any;
// @ts-tactical-any-cast: Suppress error "Type 'null' is not assignable to type 'ContinuationToken'. ts(2322)" [because we use 'strictNullChecks', but the azure-storage npm package does not]
const _nullCommonContinuationToken: AzureStorage.common.ContinuationToken = null as any;

/** Returns the AZURE_STORAGE_CONN_STRING environment variable, or throws if it's missing or empty. */
function getConnString(): string
{
    const connStr: string | undefined = Process.env["AZURE_STORAGE_CONN_STRING"];

    if (!connStr || (connStr.trim().length === 0))
    {
        throw new Error("The 'AZURE_STORAGE_CONN_STRING' environment variable is missing or empty");
    }
    return (connStr.trim());
}

/** Returns either "[(statusCode)] " or "" from the specified error. */
function getStatusCode(error: Error): string
{
    const statusCode: number = ((error as any).statusCode !== undefined) ? (error as any).statusCode : undefined;
    return ((statusCode !== undefined) ? `[${statusCode}] ` : "");
}

/**
 * [Internal] Asynchronously deletes all Azure data related to the specified instance.\
 * TODO: This is brittle [to CRA internal data structure changes] so we need a better way to do this (eg. UnsafeDeregisterInstance.exe [which, as of 3/22/21, does only partial cleanup of Azure data]).
 */
export async function deleteRegisteredInstanceAsync(instanceName: string, replicaNumber: number = 0, verboseOutput: boolean = false): Promise<void>
{
    let tableSvc: AzureStorage.TableService;

    /** [Local function] Logs a debug message. */
    function log(msg: string): void
    {
        if (verboseOutput)
        {
            Utils.log(msg, null, Utils.LoggingLevel.Minimal);
        }
    }

    /** [Local function] Handles the success/failure of a Promise&lt;void>. */
    function handlePromiseOutcome(error: Error, errorMsgPrefix: string, resolve: (value: void | PromiseLike<void>) => void, reject: RejectedPromise): void
    {
        if (error)
        {
            reject(new Error(`${errorMsgPrefix} (reason: ${getStatusCode(error)}${error.message})`));
        }
        else
        {
            resolve();
        }
    }

    /** [Local function] Excutes a task (a step) in the unregistering sequence. */
    function executeTask(reject: RejectedPromise, unregisterTask: () => void): void
    {
        try
        {
            unregisterTask();
        }
        catch (error: unknown)
        {
            reject(new Error(`Unable to unregister instance '${instanceName}' (reason: ${Utils.makeError(error).message})`));
        }
    }

    /** [Local function] Deletes all rows from the specified 'tableName' where the PartitionKey equals the specified 'key'. */
    function deleteFromTable(tableName: string, key: string, resolve: (value: void | PromiseLike<void>) => void, reject: RejectedPromise)
    {
        executeTask(reject, () =>
        {
            tableSvc = tableSvc || AzureStorage.createTableService(getConnString()); // Note: This establishes a connection
            const query: AzureStorage.TableQuery = new AzureStorage.TableQuery().where("PartitionKey eq ?", key).select(["RowKey"]);
            // const entityGenerator = AzureStorage.TableUtilities.entityGenerator;
            let deletedRowCount: number = 0;

            // We cannot delete by PartitionKey alone, so we have to query for all the RowKey's in the partition
            tableSvc.queryEntities(tableName, query, _nullTableSvcContinuationToken, (error: Error, result: AzureStorage.TableService.QueryEntitiesResult<any>, response?: AzureStorage.ServiceResponse) =>
            {
                try
                {
                    if (error)
                    {
                        reject(new Error(`Failed to query table '${tableName}' (reason: ${getStatusCode(error)}${error.message})`));
                    }
                    else
                    {
                        const numRows: number = result.entries.length;
                        if (numRows === 0)
                        {
                            log(`No rows to delete from '${tableName}'`);
                            resolve();
                        }
                        else
                        {
                            for (let i = 0; i < numRows; i++)
                            {
                                const rowKey: string = result.entries[i].RowKey._;
                                const entityDescriptor =
                                {
                                    PartitionKey: { '_': key }, // PartitionKey is a required property of an entity
                                    RowKey: { '_': rowKey }     // RowKey is a required property of an entity
                                };
                                // Effectively: DELETE * FROM (tableName) WHERE PartitionKey = "(key)" AND RowKey = "(rowKey)"
                                tableSvc.deleteEntity(tableName, entityDescriptor, (error: Error, response: AzureStorage.ServiceResponse) =>
                                {
                                    if (error)
                                    {
                                        reject(new Error(`Failed to delete row from '${tableName}' (reason: ${getStatusCode(error)}${error.message})`));
                                        return; // Stop on the first failure
                                    }
                                    else
                                    {
                                        if (++deletedRowCount === numRows)
                                        {
                                            log(`${deletedRowCount} row(s) deleted from '${tableName}'`);
                                            resolve();
                                        }
                                    }
                                });
                            }
                        }
                    }
                }
                catch (error: unknown)
                {
                    reject(new Error(`Failed to delete from table '${tableName}' (reason: ${Utils.makeError(error).message})`));
                }
            });
        });
    }

    // 1) Delete the 'Block Blob' for the instance
    // Note: The 'executor' callback passed to the Promise() constructor executes immediately, so to defer execution we wrap the Promise in a function
    function deleteBlob(): Promise<void>
    {
        const promise: Promise<void> = new Promise<void>((resolve, reject: RejectedPromise) =>
        {
            executeTask(reject, () =>
            {
                // See https://azure.github.io/azure-storage-node/ and https://docs.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-nodejs 
                const blobSvc: AzureStorage.BlobService = AzureStorage.createBlobService(getConnString()); // Note: This establishes a connection
                const containerName: string = "cra";
                // Note: 'blobName' is a "pathed" name and is found by right-clicking on a blob and selecting 'Properties...' in Azure Storage Explorer
                const blobName: string = `AmbrosiaBinaries/${instanceName}-${instanceName}${replicaNumber}`; 

                blobSvc.deleteBlobIfExists(containerName, blobName, (error: Error, result: boolean) =>
                {
                    if (!error) 
                    { 
                        log(`${result ? "" : "Warning: "}Blob '${blobName}' was ${result ? "deleted" : "not found"}`);
                    }
                    handlePromiseOutcome(error, `Failed to delete registration blob '${blobName}' in container '${containerName}'`, resolve, reject);
                });
            });
        });
        return (promise);
    }

    // 2) Delete the table for the instance
    // Note: The 'executor' callback passed to the Promise() constructor executes immediately, so to defer execution we wrap the Promise in a function
    function deleteTable(): Promise<void>
    {
        const promise = new Promise<void>((resolve, reject: RejectedPromise) =>
        {
            executeTask(reject, () =>
            {
                tableSvc = tableSvc || AzureStorage.createTableService(getConnString()); // Note: This establishes a connection
                // Note: Unlike string data values (eg. a RowKey string), table names are case-insensitive [see https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model].
                tableSvc.deleteTableIfExists(instanceName, (error: Error, result: boolean) =>
                {
                    if (!error) 
                    { 
                        log(`${result ? "" : "Warning: "}Table '${instanceName}' was ${result ? "deleted" : "not found"}`);
                    }
                    handlePromiseOutcome(error, `Failed to delete instance table '${instanceName}'`, resolve, reject);
                });
            });
        });
        return (promise);
    }

    // 3) Delete inbound rows from the CraConnectionTable (these are connections from remote instances [excluding itself] to the instance).
    //    Note that in order to find connections from remote instances that the instance has never connected to (ie. unidirectional
    //    connections), ALL RowKeys of CraConnectionTable have to be examined exhaustively.
    // Note: The 'executor' callback passed to the Promise() constructor executes immediately, so to defer execution we wrap the Promise in a function
    function deleteConnectionTableInboundRows(): Promise<void>
    {
        const promise = new Promise<void>(async (resolve, reject: RejectedPromise) =>
        {
            try
            {
                const tableName: string = "craconnectiontable";
                const rows: { partitionKey: string, rowKey: string }[] = await getAllRowsAsync(tableName);
                const rowsToDelete: { partitionKey: string, rowKey: string }[] = [];

                // Find the rows to delete
                for (let i = 0; i < rows.length; i++)
                {
                    const originatingInstanceName: string = rows[i].partitionKey;
                    if (originatingInstanceName === instanceName)
                    {
                        // We're only looking for connections [to us] from other instances
                        continue;
                    }
                    const toInstanceName: string = rows[i].rowKey.split(":")[1]; // This identifies the instance to connect to
                    if (toInstanceName === instanceName)
                    {
                        // We found a connection from a remote instance to our instance, so flag it for deletion
                        rowsToDelete.push(rows[i]);
                    }
                }

                if (rowsToDelete.length === 0)
                {
                    resolve();
                    return;
                }

                // Asynchronously delete the found rows
                for (let i = 0; i < rowsToDelete.length; i++)
                {
                    const originatingInstanceName: string = rowsToDelete[i].partitionKey;
                    const toInstanceName: string = rowsToDelete[i].rowKey.split(":")[1]; // This identifies the instance to connect to
                    const entityDescriptor = 
                    {
                        PartitionKey: { '_': rowsToDelete[i].partitionKey },
                        RowKey: { '_': rowsToDelete[i].rowKey }
                    };
                    
                    tableSvc.deleteEntity(tableName, entityDescriptor, (error: Error, response: AzureStorage.ServiceResponse) =>
                    {
                        log(error ? `Error: Failed to delete CRA connection from IC '${originatingInstanceName}' to '${toInstanceName}' (${rowsToDelete[i].rowKey}) (reason: ${getStatusCode(error)}${error.message})` :
                                    `Deleted CRA connection from IC '${originatingInstanceName}' to '${toInstanceName}' (${rowsToDelete[i].rowKey})`);
                        
                        if (i === rowsToDelete.length - 1)
                        {
                            // We're reached the last row to delete, so deleteConnectionTableInboundRows() is done
                            resolve();
                        }
                    });
                }
            }
            catch (error: unknown)
            {
                reject(new Error(`Error: deleteConnectionTableInboundRows() failed (reason: ${Utils.makeError(error).message})`));
            }
        });
        return (promise);
    }

    // 4) Delete outbound rows from the CraConnectionTable (these are connections from the instance to other instances [including itself])
    // Note: The 'executor' callback passed to the Promise() constructor executes immediately, so to defer execution we wrap the Promise in a function
    function deleteConnectionTableOutboundRows(): Promise<void>
    {
        const promise = new Promise<void>((resolve, reject: RejectedPromise) =>
        {
            deleteFromTable("craconnectiontable", instanceName, resolve, reject);
        });
        return (promise);
    }

    // 5) Delete rows from the CraEndpointTable
    // Note: The 'executor' callback passed to the Promise() constructor executes immediately, so to defer execution we wrap the Promise in a function
    function deleteEndpointTableRows(): Promise<void>
    {
        const promise = new Promise<void>((resolve, reject: RejectedPromise) =>
        {
            deleteFromTable("craendpointtable", instanceName, resolve, reject);
        });
        return (promise);
    }

    // 6) Delete rows from the CraVertexTable
    // Note: The 'executor' callback passed to the Promise() constructor executes immediately, so to defer execution we wrap the Promise in a function
    function deleteVertexTableRows(): Promise<void>
    {
        const promise = new Promise<void>((resolve, reject: RejectedPromise) =>
        {
            deleteFromTable("cravertextable", `${instanceName}${replicaNumber}`, resolve, reject);
        });
        return (promise);
    }

    // 7) TODO: Delete rows from the CraShardedVertexTable

    // Note: The 'executor' callback passed to the Promise() constructor executes immediately, so to defer execution we wrap the Promise in a function
    // await deleteBlob();
    // await deleteTable();
    // await deleteConnectionTableInboundRows();
    // await deleteConnectionTableOutboundRows();
    // await deleteEndpointTableRows();
    // await deleteVertexTableRows();

    // In parallel (for performance):
    const replicaCount: number = (await getReplicaNumbersAsync(instanceName)).length;
    await Promise.all<any>([deleteBlob(), ...((replicaCount === 1) ? [deleteTable(), deleteConnectionTableInboundRows(), deleteConnectionTableOutboundRows(), deleteEndpointTableRows()] : []), deleteVertexTableRows()]);
}

/** Asynchronously returns all PartitionKey and RowKey values from the specified table. Can handle tables of any size. */
// Helpful links:
//   https://stackoverflow.com/questions/47786874/azure-table-storage-continuation-tokens-in-node-js
//   https://azure.github.io/azure-storage-node/TableService.html
//   https://stackoverflow.com/questions/53385166/retrieving-more-than-1000-records-from-azure-storage-table-js
//   https://docs.microsoft.com/en-us/rest/api/storageservices/query-timeout-and-pagination
export async function getAllRowsAsync(tableName: string): Promise<{ partitionKey: string, rowKey: string }[]>
{
    const tableSvc: AzureStorage.TableService = AzureStorage.createTableService(getConnString());
    const query: AzureStorage.TableQuery = new AzureStorage.TableQuery().select(["PartitionKey", "RowKey"]);
    // @ts-tactical-any-cast: Suppress error "Type 'null' is not assignable to type 'TableContinuationToken'. ts(2322)" [because we use 'strictNullChecks', but the azure-storage npm package does not]
    let continuationToken: AzureStorage.TableService.TableContinuationToken = null as any;
    const rows: { partitionKey: string, rowKey: string }[] = [];

    /** [Local function] Returns the next segment (chunk) of rows from the table. */
    async function getRowsSegmented(tableName: string, tableQuery: AzureStorage.TableQuery, continuationToken: AzureStorage.TableService.TableContinuationToken): Promise<AzureStorage.TableService.QueryEntitiesResult<any>>
    {
        let promise: Promise<AzureStorage.TableService.QueryEntitiesResult<any>> = new Promise<AzureStorage.TableService.QueryEntitiesResult<any>>((resolve, reject: RejectedPromise) =>
        {
            tableSvc.queryEntities(tableName, query, continuationToken, (error: Error, result: AzureStorage.TableService.QueryEntitiesResult<any>, response?: AzureStorage.ServiceResponse) =>
            {
                try
                {
                    if (error)
                    {
                        reject(new Error(`Failed to query table '${tableName}' (reason: ${getStatusCode(error)}${error.message})`));
                    }
                    else
                    {
                        resolve(result);
                    }
                }
                catch (error: unknown)
                {
                    reject(new Error(`Error: getAllRows() failed (reason: ${Utils.makeError(error).message})`));
                }
            });
        });
        return (promise);
    }
  
    do
    {
        const result: AzureStorage.TableService.QueryEntitiesResult<any> = await getRowsSegmented(tableName, query, continuationToken);
        // @ts-tactical-any-cast: Suppress error "Type 'null' is not assignable to type 'TableContinuationToken'. ts(2322)" [because we use 'strictNullChecks', but the azure-storage npm package does not]
        continuationToken = !result.continuationToken ? null as any : continuationToken;
        if (result.entries)
        {
            for (let i = 0; i < result.entries.length; i++)
            {
                let partitionKey: string = result.entries[i].PartitionKey._; // Eg: "PTIClient"
                let rowKey: string = result.entries[i].RowKey._; // Eg: "Ambrosiacontrolout:PTIServer:Ambrosiacontrolin"
                rows.push({ partitionKey, rowKey });
            }
        }
    }
    while (continuationToken !== null);

    return (rows);
}

/** 
 * Asynchronously returns the list of replica numbers for the specified instance. 
 * Note that replica 0 (ie. an instance registered with RegisterInstance, not AddReplica) counts as a replica.
 */
export async function getReplicaNumbersAsync(instanceName: string): Promise<number[]>
{
    let promise: Promise<number[]> = new Promise<number[]>((resolve, reject: RejectedPromise) =>
    {
        let tableSvc: AzureStorage.TableService = AzureStorage.createTableService(getConnString());
        let query: AzureStorage.TableQuery = new AzureStorage.TableQuery().where("(PartitionKey ge ?) and (PartitionKey le ?) and (RowKey eq ?)", instanceName + "0", instanceName + "999", instanceName).select(["PartitionKey"]);
        let tableName: string = "cravertextable";
        tableSvc.queryEntities(tableName, query, _nullTableSvcContinuationToken, async (error: Error, result: AzureStorage.TableService.QueryEntitiesResult<any>, response?: AzureStorage.ServiceResponse) =>
        {
            try
            {
                if (error)
                {
                    reject(new Error(`Failed to query table '${tableName}' (reason: ${getStatusCode(error)}${error.message})`));
                }
                else
                {
                    const countReplicas: number = result.entries.length;
                    const replicaNumbers: number[] = [];

                    for (let i = 0; i < countReplicas; i++)
                    {
                        const instanceNameWithReplica: string = result.entries[i].PartitionKey._;
                        const replicaNumber: number = parseInt(instanceNameWithReplica.replace(instanceName, ""));
                        replicaNumbers.push(replicaNumber);
                    }
                    resolve(replicaNumbers);
                }
            }
            catch (error: unknown)
            {
                reject(new Error(`Error: getReplicaNumbersAsync() failed (reason: ${Utils.makeError(error).message})`));
            }
        });
    });
    return (promise);
}

/** Asynchronously determines if the instance has been registered. Will return false if UnsafeDeregisterInstance.exe has been called. */
export async function isRegisteredAsync(instanceName: string, replicaNumber: number): Promise<boolean>
{
    let endpointExists: boolean = false;
    let registrationBlobExists: boolean = false;

    try
    {
        // Step 1:
        // Check for any rows for the instance in CraEndpointTable. Immediately after running "Ambrosia.exe RegisterInstance" (but
        // before starting the IC) there will be 4 rows. Critically, these rows are removed by BOTH UnsafeDeregisterInstance.exe and
        // Configuration.eraseInstanceAsync(), so their absence is a "universal tell" that the instance is not registered.
        const endpointCount: number = await getRowCountAsync("craendpointtable", instanceName);
        endpointExists = (endpointCount >= 4);

        // Step 2:
        // Check that the registration blob exists for the specified replicaNumber
        if (endpointExists)
        {
            await getRegistrationSettingsAsync(instanceName, replicaNumber);
            registrationBlobExists = true;
        }

        return (endpointExists && registrationBlobExists);
    }
    catch (error: unknown)
    {
        if (Utils.makeError(error).message.indexOf("[404] NotFound") !== -1)
        {
            return (false);
        }
        else
        {
            throw error;
        }
    }
}

/** 
 * Asynchronously reads the IC registration settings (eg. icReceivePort, icSendPort, icLogFolder) by directly querying CRA's blob storage in Azure.\
 * TODO: This is brittle [to CRA internal data structure changes] so we need a better way to do this.
 */
export async function getRegistrationSettingsAsync(instanceName: string, replicaNumber: number): Promise<Configuration.RegistrationSettings>
{
    let promise: Promise<Configuration.RegistrationSettings> = new Promise<Configuration.RegistrationSettings>((resolve, reject: RejectedPromise) =>
    {
        // See https://azure.github.io/azure-storage-node/ and https://docs.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-nodejs 
        let blobSvc: AzureStorage.BlobService = AzureStorage.createBlobService(getConnString());
        let containerName: string = "cra";
        // Note: 'blobName' is a "pathed" name and is found by right-clicking on a blob and selecting 'Properties...' in Azure Storage Explorer
        let blobName: string = `AmbrosiaBinaries/${instanceName}-${instanceName}${replicaNumber}`; 
        let memStream: Streams.MemoryStream = new Streams.MemoryStream();

        // Note: We can't use blobSvc.getBlobToText() due to https://stackoverflow.com/questions/30609691/hash-mismatch-integrity-check-failed-with-azure-blob-storage 
        blobSvc.getBlobToStream(containerName, blobName, memStream, (error: Error, result: AzureStorage.BlobService.BlobResult, response: AzureStorage.ServiceResponse) =>
        {
            try
            {
                if (error)
                {
                    let errorMsg: string = `Failed to query registration blob '${blobName}' in container '${containerName}' (reason: ${getStatusCode(error)}${error.message})`;
                    if ((error as any).statusCode === 404)
                    {
                        errorMsg += `; check that the instanceName ('${instanceName}') and/or replicaNumber (${replicaNumber}) are correct (do you need to auto-register the instance?)`;
                    }
                    reject(new Error(errorMsg));
                }
                else
                {
                    let buf: Buffer = memStream.readAll();
                    let contents: string = StringEncoding.fromUTF8Bytes(buf);
                    let startIndex: number = contents.indexOf("<AmbrosiaRuntimeParams");
                    let endIndex: number = contents.indexOf("</AmbrosiaRuntimeParams>") + 24;
                    let xml: string = contents.substring(startIndex, endIndex).replace(/\\\\r\\\\n/g, "").replace(/\\\\\\/g, "");
                    let registrationSettings: Configuration.RegistrationSettings = new Configuration.RegistrationSettings();
                
                    // Redact the <storageConnectionString> [for security] to prevent accidental logging
                    startIndex = xml.indexOf("<storageConnectionString>");
                    endIndex = xml.indexOf("</storageConnectionString>") + 26;
                    xml = xml.substring(0, startIndex) + xml.substr(endIndex);
                
                    // let formattedXml: string = Utils.formatXml(Utils.decodeXml(xml));
                    // Utils.log(formattedXml);

                    registrationSettings.icReceivePort = parseInt(readElementText(xml, "serviceReceiveFromPort"));
                    registrationSettings.icSendPort = parseInt(readElementText(xml, "serviceSendToPort"));
                    registrationSettings.icLogFolder = Utils.ensurePathEndsWithSeparator(readElementText(xml, "serviceLogPath"));
                    registrationSettings.appVersion = parseInt(readElementText(xml, "currentVersion")); // Note: This is not updated by the IC after an upgrade - we have to update it by re-registering
                    registrationSettings.upgradeVersion = parseInt(readElementText(xml, "upgradeToVersion"));
                    registrationSettings.activeActive = Utils.equalIgnoringCase(readElementText(xml, "activeActive"), "true");

                    Utils.log("Registration settings read (from Azure)");
                    resolve(registrationSettings);
                
                    /** 
                     * [Local function] Returns the text of the specified XML element (eg. returns "Foo" from &lt;someElement>Foo&lt;/someElement>). 
                     * Throws if an element with 'elementName' does not exist in the specified XML.
                     */
                    function readElementText(xml: string, elementName: string): string
                    {
                        const isEmptyElement: boolean = (xml.indexOf(`<${elementName} />`) !== -1);
                        if (isEmptyElement)
                        {
                            return ("");
                        }
                        const startIndex: number = xml.indexOf(`<${elementName}>`) + elementName.length + 2;
                        const endIndex: number = xml.indexOf(`</${elementName}>`);

                        if ((startIndex >= 0) && (endIndex > startIndex))
                        {
                            const elementText: string = xml.substring(startIndex, endIndex);
                            return (elementText);
                        }
                        throw new Error(`Unable to read element '${elementName}' from registration XML`)
                    }
                }
            }
            catch (innerError: unknown)
            {
                reject(new Error(`Unable to parse registration blob '${blobName}' in container '${containerName}' (reason: ${Utils.makeError(innerError).message})`));
            }
        });
    });
    return (promise);
}

/** [Internal] Returns the value of 'columnName' for the row specified by 'partitionKey' and 'rowKey in table 'tableName'. */
export async function readAzureTableColumn(tableName: string, columnName: string, partitionKey: string, rowKey: string): Promise<unknown>
{
    const promise: Promise<unknown> = new Promise<unknown>((resolve, reject: RejectedPromise) =>
    {
        const tableSvc: AzureStorage.TableService = AzureStorage.createTableService(getConnString());
        const query: AzureStorage.TableQuery = new AzureStorage.TableQuery().where("(PartitionKey eq ?) and (RowKey eq ?)", partitionKey, rowKey).select([columnName]);
        tableSvc.queryEntities(tableName, query, _nullTableSvcContinuationToken, (error: Error, result: AzureStorage.TableService.QueryEntitiesResult<Utils.SimpleObject>, response?: AzureStorage.ServiceResponse) =>
        {
            if (error)
            {
                reject(new Error(`Failed to query table '${tableName}' (reason: ${getStatusCode(error)}${error.message})`));
            }
            else
            {
                if (result.entries && (result.entries.length === 1))
                {
                    // An incorrect column name will always be included in the results, but the value (._) of the column will be null.
                    // Note: The TableService does not persist null values (see https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model#property-types)
                    //       so there is no possibility of confusion between a bad column name and a correct column that has a null value.
                    if (result.entries[0][columnName]._ !== null)
                    {
                        const columnValue: unknown = result.entries[0][columnName]._;
                        resolve(columnValue);
                    }
                    else
                    {
                        reject(new Error(`Failed to query table '${tableName}' (reason: unknown column name '${columnName}')`));
                    }
                }
                else
                {
                    reject(new Error(`Failed to query table '${tableName}' (reason: no row found for PartitionKey '${partitionKey}' and RowKey '${rowKey}')`));
                }
            }
        });
    });
    return (promise);
}

/** 
 * [Internal] Returns true if this is the first start of the specified 'instanceName' after being initially registered,
 * where "initially registered" means one of:
 * 1) The first ever registration.
 * 2) The first registration after running UnsafeDeregisterInstance.
 * 3) The first registration after calling Configuration.eraseInstanceAsync().
 */
export async function isFirstStartAfterInitialRegistration(instanceName: string, replicaNumber: number = 0): Promise<boolean>
{
    let promise: Promise<boolean> = new Promise<boolean>((resolve, reject: RejectedPromise) =>
    {
        let tableSvc: AzureStorage.TableService = AzureStorage.createTableService(getConnString());
        let query: AzureStorage.TableQuery = new AzureStorage.TableQuery().where("(PartitionKey eq ?) and (RowKey eq ?) and (IsActive eq false)", `${instanceName}${replicaNumber}`, instanceName);
        let tableName: string = "cravertextable";
        tableSvc.queryEntities(tableName, query, _nullTableSvcContinuationToken, async (error: Error, result: AzureStorage.TableService.QueryEntitiesResult<any>, response?: AzureStorage.ServiceResponse) =>
        {
            try
            {
                if (error)
                {
                    reject(new Error(`Failed to query table '${tableName}' (reason: ${getStatusCode(error)}${error.message})`));
                }
                else
                {
                    let countDisabledVerticies: number = result.entries.length;
                    let countConnections: number = 0;

                    // The query will return 1 (or more?) rows even in the case of a simple re-registration (which is not an initial registration),
                    // so we also have to check the CraConnectionTable for rows
                    if (countDisabledVerticies > 0)
                    {
                        countConnections = await getRowCountAsync("craconnectiontable", instanceName);
                    }
                    resolve((countDisabledVerticies > 0) && (countConnections === 0));
                }
            }
            catch (error: unknown)
            {
                reject(new Error(`Error: isFirstStartAfterInitialRegistration() failed (reason: ${Utils.makeError(error).message})`));
            }
        });
    });
    return (promise);
}

/** Asynchronously returns the number of rows in the specified table where the PartitionKey matches the supplied 'partitionKey'. */
async function getRowCountAsync(tableName: string, partitionKey: string): Promise<number>
{
    let promise: Promise<number> = new Promise<number>((resolve, reject: RejectedPromise) =>
    {
        let tableSvc: AzureStorage.TableService = AzureStorage.createTableService(getConnString());
        let query: AzureStorage.TableQuery = new AzureStorage.TableQuery().where("PartitionKey eq ?", partitionKey);
        tableSvc.queryEntities(tableName, query, _nullTableSvcContinuationToken, (error: Error, result: AzureStorage.TableService.QueryEntitiesResult<any>, response?: AzureStorage.ServiceResponse) =>
        {
            try
            {
                if (error)
                {
                    reject(new Error(`Failed to query table '${tableName}' (reason: ${getStatusCode(error)}${error.message})`));
                }
                else
                {
                    resolve(result.entries.length);
                }
            }
            catch (error: unknown)
            {
                reject(new Error(`Error: getRowCount() failed (reason: ${Utils.makeError(error).message})`));
            }
        });
    });
    return (promise);
}

/** 
 * Asynchronously returns a list of remote instance names that the supplied 'localInstanceName' connects to, by directly querying the CRAConnectionTable in Azure.\
 * Optionally (based on deleteRemoteCRAConnections), deletes entries in CRAConnectionTable for connections (in both directions) between the local instance and 
 * remote instances (but only if the local instance has a connection to the remote instance).\
 * TODO: Ideally We would get/do this using either ImmortalCoordinator.exe or Ambrosia.exe, but no such mechanism currently exists.
 */
export async function getRemoteInstancesAsync(localInstanceName: string, deleteRemoteCRAConnections: boolean): Promise<string[]>
{
    let promise: Promise<string[]> = new Promise<string[]>((resolve, reject: RejectedPromise) =>
    {
        let tableSvc: AzureStorage.TableService = AzureStorage.createTableService(getConnString());
        let query: AzureStorage.TableQuery = new AzureStorage.TableQuery().where("PartitionKey eq ?", localInstanceName).select(["RowKey"]);
        let tableName: string = "craconnectiontable";
        let remoteInstanceNames: string[] = [];
        let deletedRemoteInstanceNames: string[] = [];

        // Effectively: SELECT RowKey FROM craconnectiontable WHERE PartitionKey = "(localInstanceName)"
        tableSvc.queryEntities(tableName, query, _nullTableSvcContinuationToken, (error: Error, result: AzureStorage.TableService.QueryEntitiesResult<any>, response?: AzureStorage.ServiceResponse) =>
        {
            try
            {
                if (error)
                {
                    reject(new Error(`Failed to query table '${tableName}' (reason: ${getStatusCode(error)}${error.message})`));
                }
                else
                {
                    for (let i = 0; i < result.entries.length; i++)
                    {
                        let rowKey: string = result.entries[i].RowKey._; // Eg: "Ambrosiacontrolout:server:Ambrosiacontrolin"
                        let remoteInstanceName: string = rowKey.split(":")[1]; // This identifies the instance to connect to

                        if (remoteInstanceName === localInstanceName)
                        {
                            // Don't include/delete the rows for self-connections
                            continue;
                        }

                        // TODO: This is just temporary until we have a 'DeleteConnection' message implemented in the IC.
                        //       If we don't delete connection entries which refer to instances that are [currently] down
                        //       the CRA will hang (indefinitely) trying to re-establish those connections. Depending on the 
                        //       ordering of the rows, this can prevent the local instance from being able to connect to itself.
                        if (deleteRemoteCRAConnections)
                        {
                            let entityDescriptor = 
                            {
                                PartitionKey: { '_': localInstanceName },
                                RowKey: { '_': rowKey }
                            };
                            
                            // Effectively: DELETE * FROM craconnectiontable WHERE PartitionKey = "(localInstanceName)" AND RowKey CONTAINS "(remoteInstanceName)"
                            tableSvc.deleteEntity(tableName, entityDescriptor, (error: Error, response: AzureStorage.ServiceResponse) =>
                            {
                                Utils.log(error ? `Error: Failed to delete CRA connection '${localInstanceName}': '${rowKey}' (reason: ${getStatusCode(error)}${error.message})` :
                                                  `Deleted CRA connection for IC '${localInstanceName}' to '${remoteInstanceName}' (${rowKey})`);
                            });

                            // Keep track of which instances we've deleted connections to
                            if (deletedRemoteInstanceNames.indexOf(remoteInstanceName) === -1)
                            {
                                deletedRemoteInstanceNames.push(remoteInstanceName);
                            }
                        }
                        else
                        {
                            if (remoteInstanceNames.indexOf(remoteInstanceName) === -1)
                            {
                                remoteInstanceNames.push(remoteInstanceName);
                            }
                        }
                    }

                    // TODO: Again, this is just temporary until we have a 'DeleteConnection' message implemented in the IC.
                    if (deletedRemoteInstanceNames.length > 0)
                    {
                        removeConnectionsFromRemoteInstances(deletedRemoteInstanceNames, localInstanceName);
                    }
  
                    resolve(remoteInstanceNames);
                }
            }
            catch (innerError: unknown)
            {
                reject(new Error(`Error: getRemoteInstancesAsync() failed (reason: ${Utils.makeError(innerError).message})`));
            }
        });
    });
    return (promise);
}

/** Asynchronously deletes connections from the specified remote instance(s) to the local instance. */
function removeConnectionsFromRemoteInstances(remoteInstanceNames: string[], localInstanceName: string): void
{
    try
    {
        for (let remoteInstanceName of remoteInstanceNames)
        {
            let tableSvc: AzureStorage.TableService = AzureStorage.createTableService(getConnString());
            let query: AzureStorage.TableQuery = new AzureStorage.TableQuery().where("PartitionKey eq ?", remoteInstanceName).select(["RowKey"]);
            let tableName: string = "craconnectiontable";

            // Effectively: SELECT RowKey FROM craconnectiontable WHERE PartitionKey = "(remoteInstanceName)"
            tableSvc.queryEntities(tableName, query, _nullTableSvcContinuationToken, (error: Error, result: AzureStorage.TableService.QueryEntitiesResult<any>, response?: AzureStorage.ServiceResponse) =>
            {
                if (error)
                {
                    Utils.log(`Error: Failed to query table '${tableName}' (reason: ${getStatusCode(error)}${error.message})`);
                }
                else
                {
                    for (let i = 0; i < result.entries.length; i++)
                    {
                        let rowKey: string = result.entries[i].RowKey._; // Eg: "Ambrosiacontrolout:server:Ambrosiacontrolin"
                        let instanceName: string = rowKey.split(":")[1] // This identifies the instance to connect to

                        if (instanceName === localInstanceName)
                        {
                            let entityDescriptor = 
                            {
                                PartitionKey: { '_': remoteInstanceName },
                                RowKey: { '_': rowKey }
                            };
                            
                            // Effectively: DELETE * FROM craconnectiontable WHERE PartitionKey = "(remoteInstanceName)" AND RowKey CONTAINS "(localInstanceName)"
                            tableSvc.deleteEntity(tableName, entityDescriptor, (error: Error, response: AzureStorage.ServiceResponse) =>
                            {
                                Utils.log(error ? `Error: Failed to delete CRA connection '${remoteInstanceName}': '${rowKey}' (reason: ${getStatusCode(error)}${error.message})` :
                                                  `Deleted CRA connection for IC '${remoteInstanceName}' to '${localInstanceName}' (${rowKey})`);
                            });
                        }
                    }
                }
            });
        }
    }
    catch (innerError: unknown)
    {
        Utils.log(`Error: removeConnectionsFromRemoteInstances() failed (reason: ${Utils.makeError(innerError).message})`);
    }
}

/** The name of the Azure blob container for Ambrosia logs. */
const _logsContainerName: string = "ambrosialogs";

/** 
 * Returns a modified version of the supplied 'folderPath' that's Azure-compatible.\
 * For example, "sub1\sub2" will become "sub1/sub2".\
 * A leading separator is always removed. A trailing separator is either added or removed based on 'addTrailingSeparator' (which defaults to false).
 */
export function convertToAzurePath(folderPath: string, addTrailingSeparator: boolean = false): string
{
    const pathStartsWithDriveLetter: boolean = /^[A-Za-z]:[\/\\]?/.test(folderPath);
    if (pathStartsWithDriveLetter)
    {
        throw new Error(`Unable to convert path (reason: The specified 'folderPath' ("${folderPath}") refers to a file system path)`);
    }

    let modifiedPath: string = folderPath.replace(/\/+|[\\]+/g, "/");
    if (modifiedPath.startsWith("/"))
    {
        modifiedPath = modifiedPath.substr(1);
    }
    if (addTrailingSeparator)
    {
        if ((modifiedPath.length > 0) && !modifiedPath.endsWith("/"))
        {
            modifiedPath += "/";
        }
    }
    else
    {
        if ((modifiedPath.length > 0) && modifiedPath.endsWith("/"))
        {
            modifiedPath = modifiedPath.slice(0, -1);
        }
    }
    return (modifiedPath);
}

/** [Internal] Deletes Ambrosia log/checkpoint files from the specified Azure blob folder, and removes the folder. Returns the number of files deleted. */
export async function deleteBlobLogsAsync(folderName: string): Promise<number>
{
    folderName = convertToAzurePath(folderName);
    const debug: boolean = false;
    const blobSvc: AzureStorage.BlobService = AzureStorage.createBlobService(getConnString()); // Note: This establishes a connection
    // An array of functions that return a Promise (we do it this way because the 'executor' callback passed to
    // the Promise() constructor executes immediately, so to defer execution we wrap the Promise in a function)   
    const taskList: (() => Promise<boolean | void>)[] = [];
    let deletedFileCount: number = 0;

    try
    {
        await queueBlobDeletionTasksAsync(_logsContainerName, folderName);

        // Execute promises serially [because we want the tasks to execute in the order we queued them]
        if (debug) { Utils.log("DEBUG: Starting blob deletions..."); }
        for (let i = 0; i < taskList.length; i++)
        {
            const taskResult: boolean | void = await taskList[i]();
            if ((typeof taskResult === "boolean") && taskResult) // Only deleteBlobAsync() tasks return a boolean
            {
                deletedFileCount++;
            }
        }
        if (debug) { Utils.log("DEBUG: Blob deletions complete"); }
        return (deletedFileCount > 0 ? deletedFileCount - 1: 0); // The "folder shadow blob" (used by the IC for lease coordination) gets counted as a deleted file, so we adjust accordingly
    }
    catch (error: unknown)
    {
        throw new Error(`deleteBlobLogsAsync() failed (reason: ${Utils.makeError(error).message})`);
    }

    /** [Local function] Reads the available log blobs in the specified folderName and populates 'taskList' with delete tasks for each of them (including the folder itself). */
    function queueBlobDeletionTasksAsync(containerName: string, folderName: string): Promise<void>
    {
        const promise: Promise<void> = new Promise<void>((resolve, reject: RejectedPromise) =>
        {
            blobSvc.listBlobsSegmentedWithPrefix(containerName, folderName, _nullCommonContinuationToken, (error: Error, result: AzureStorage.BlobService.ListBlobsResult) =>
            {
                if (error)
                {
                    reject(new Error(`Failed to enumerate files (blobs) in folder '${folderName}' of container '${containerName}' (reason: ${getStatusCode(error)}${error.message})`));
                }
                else
                {
                    try
                    {
                        result.entries.forEach((blob) =>
                        {
                            let blobName: string = blob.name;

                            // Note: The folderName itself will be included in the result entries, but we must delete that last
                            if (/\/serverlog[\d]+$/.test(blobName) || /\/serverchkpt[\d]+$/.test(blobName) || blobName.endsWith("serverkillFile"))
                            {
                                // Note: An "infinite" lease (which is the duration of the lease on the active log file) breaks immediately 
                                //       (see https://docs.microsoft.com/en-us/rest/api/storageservices/lease-container)
                                if (blob.lease && (blob.lease.state === "leased") && (blob.lease.duration === "infinite"))
                                {
                                    // We have to break the lease to allow the file to be deleted
                                    taskList.push(() => breakBlobLeaseAsync(containerName, blobName));
                                }
                                taskList.push(() => deleteBlobAsync(blobSvc, containerName, blobName));
                            }
                        });
                        // Finally, delete the "folder shadow blob" (used by the IC for lease coordination)
                        // Note: Azure deletes empty blob folders automatically (see https://docs.microsoft.com/en-us/answers/questions/466968/remove-azure-blob-storage-folders-with-sdk.html),
                        //       but the IC also creates [at the folder level] a blob file with the same name as the folder, which is what we delete here
                        taskList.push(() => deleteBlobAsync(blobSvc, containerName, folderName));
                        resolve();
                    }
                    catch (error: unknown)
                    {
                        const err: Error = Utils.makeError(error);
                        reject(new Error(`queueBlobDeletionTasksAsync() failed (reason: ${getStatusCode(err)}${err.message})`));
                    }
                }
            });
        });
        return (promise);
    }

    /** [Local function] Breaks the lease on the specified blobName in the specified containerName. */
    function breakBlobLeaseAsync(containerName: string, blobName: string): Promise<void>
    {
        const promise: Promise<void> = new Promise<void>((resolve, reject: RejectedPromise) =>
        {
            blobSvc.breakLease(containerName, blobName, (error: Error, result: AzureStorage.BlobService.LeaseResult) =>
            {
                if (error)
                {
                    reject(new Error(`Failed to break lease on blob '${blobName}' in container '${containerName}' (reason: ${getStatusCode(error)}${error.message})`));
                }
                else
                {
                    if (debug) { Utils.log(`DEBUG: Lease broken for blob '${blobName}'`); }
                    resolve();
                }
            });
        });
        return (promise);
    }
}

/** Deletes the specified blobName (which may include a path) in the specified containerName. */
function deleteBlobAsync(blobSvc: AzureStorage.BlobService, containerName: string, blobName: string): Promise<boolean>
{
    const promise: Promise<boolean> = new Promise<boolean>((resolve, reject: RejectedPromise) =>
    {
        blobSvc.deleteBlobIfExists(containerName, blobName, (error: Error, result: boolean) =>
        {
            if (error)
            {
                reject(new Error(`Failed to delete blob '${blobName}' in container '${containerName}' (reason: ${getStatusCode(error)}${error.message})`));
            }
            else
            {
                // Even though 'result' will be false if blobName doesn't exist, we always return true.
                // We do this because of timing issues in Azure: sometimes, when the last file is deleted, Azure will automatically delete the
                // folder so 'result' ends up being false for the final folder delete task, which then throws off our count of deleted "files".
                resolve(true);
            }
        });
    });
    return (promise);
}

/** Returns true if there's a log file in the specified Azure blob folder, returns false otherwise (and removes the "folder shadow blob" (used by the IC for lease coordination) if the log folder is empty). */
export async function folderContainsBlobLogAsync(folderName: string): Promise<boolean>
{
    folderName = convertToAzurePath(folderName);
    const blobSvc: AzureStorage.BlobService = AzureStorage.createBlobService(getConnString()); // Note: This establishes a connection
    const fileNames: string[] = await getFolderFileNamesAsync(_logsContainerName, folderName);
    let folderLockFileExists: boolean = false; // Whether the "folder shadow blob" exists
    let hasLog: boolean = false;

    for (let i = 0; i < fileNames.length; i++)
    {
        if (fileNames[i] === folderName)
        {
            folderLockFileExists = true;
        }
        if (/\/serverlog[\d]+$/.test(fileNames[i]))
        {
            hasLog = true;
        }
    }

    if (folderLockFileExists)
    {
        if (fileNames.length === 1)
        {
            // The folder is empty (the only "file" is the "folder shadow blob" (used by the IC for lease coordination)), so we [must] remove it
            // Note: Azure deletes empty blob folders automatically (see https://docs.microsoft.com/en-us/answers/questions/466968/remove-azure-blob-storage-folders-with-sdk.html),
            //       but the IC also creates [at the folder level] a blob file with the same name as the folder, which is what we delete here
            await deleteBlobAsync(blobSvc, _logsContainerName, folderName);
            return (false);
        }
        else
        {
            return (hasLog);
        }
    }
    else
    {
        return (false);
    }

    /** 
     * [Local function] Returns the names of all the files in the the specified folderName in the specified containerName. 
     * The list will also include the "folder shadow blob" (used by the IC for lease coordination) that has the same name as the folder.
     */
    function getFolderFileNamesAsync(containerName: string, folderName: string): Promise<string[]>
    {
        const promise: Promise<string[]> = new Promise<string[]>((resolve, reject: RejectedPromise) =>
        {
            const fileNames: string[] = [];
            blobSvc.listBlobsSegmentedWithPrefix(containerName, folderName, _nullCommonContinuationToken, (error: Error, result: AzureStorage.BlobService.ListBlobsResult) =>
            {
                if (error)
                {
                    reject(new Error(`Failed to enumerate files (blobs) in folder '${folderName}' of container '${containerName}' (reason: ${getStatusCode(error)}${error.message})`));
                }
                else
                {
                    result.entries.forEach((blob) =>
                    {
                        fileNames.push(blob.name);
                    });
                    resolve(fileNames);
                }
            });
        });
        return (promise);
    }
}

/** 
 * [Internal] Returns the list of "leaf" Azure log folder names (ie. the child folders of 'logFolder') for the specified instance.\
 * Leaf folder names are of the form "{instanceName}_{versionNumber}".
 */
export async function getChildLogFoldersAsync(logFolder: string, instanceName: string): Promise<string[]>
{
    logFolder = convertToAzurePath(logFolder, true);
    const blobSvc: AzureStorage.BlobService = AzureStorage.createBlobService(getConnString()); // Note: This establishes a connection
    const folderNames: string[] = [];

    const promise: Promise<string[]> = new Promise<string[]>((resolve, reject: RejectedPromise) =>
    {
        blobSvc.listBlobDirectoriesSegmentedWithPrefix(_logsContainerName, `${logFolder}${instanceName}_`, _nullCommonContinuationToken, (error: Error, result: AzureStorage.BlobService.ListBlobDirectoriesResult) =>
        {
            if (error)
            {
                reject(new Error(`Failed to enumerate log folders for '${instanceName}' in folder '${logFolder}' of container '${_logsContainerName}' (reason: ${getStatusCode(error)}${error.message})`));
            }
            else
            {
                result.entries.forEach((folder) =>
                {
                    const folderName: string = folder.name.endsWith("/") ? folder.name.slice(0, -1) : folder.name;
                    const parts: string[] = folderName.split("/");
                    const leafFolderName = parts[parts.length - 1];
                    if (RegExp(`${instanceName}_\\d+$`).test(leafFolderName))
                    {
                        folderNames.push(leafFolderName);
                    }
                });
                resolve(folderNames);
            }
        });
    });
    return (promise);
}