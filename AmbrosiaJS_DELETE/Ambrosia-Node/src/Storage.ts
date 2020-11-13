// Module for reading from central storage (typically IC settings).
import AzureStorage = require("azure-storage");
import Process = require("process");
// Note: The 'import * as Foo from "./Foo' syntax avoids the "compiler re-write problem" that breaks debugger hover inspection
import * as Configuration from "./Configuration"
import * as StringEncoding from "./StringEncoding";
import * as Streams from "./Streams";
import * as Utils from "./Utils/Utils-Index";
import { RejectedPromise } from "./ICProcess"; // There is no re-write issue here as this is just a type

/** Returns the AZURE_STORAGE_CONN_STRING environment variable, or throws if it's missing or empty. */
function getConnString(): string
{
    let connStr: string = Process.env["AZURE_STORAGE_CONN_STRING"];

    if (!connStr || (connStr.trim().length === 0))
    {
        throw new Error("The 'AZURE_STORAGE_CONN_STRING' environment variable is missing or empty");
    }
    return (connStr.trim());
}

/** 
 * Asynchronously reads the IC registration settings (icReceivePort, icSendPort, icLogFolder) by directly querying CRA's blob storage in Azure.\
 * TODO: This is brittle [to CRA internal data structure changes] so we need a better way to do this.
 */
export async function getRegistrationSettingsAsync(instanceName: string, appVersion: number): Promise<Configuration.RegistrationSettings>
{
    let promise: Promise<Configuration.RegistrationSettings> = new Promise<Configuration.RegistrationSettings>((resolve, reject: RejectedPromise) =>
    {
        // See https://azure.github.io/azure-storage-node/ and https://docs.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-nodejs 
        let blobSvc: AzureStorage.BlobService = AzureStorage.createBlobService(getConnString());
        let containerName: string = "cra";
        // Note: 'blobName' is a "pathed" name and is found by right-clicking on a blob and selecting 'Properties...' in Azure Storage Explorer
        let blobName: string = `AmbrosiaBinaries/${instanceName}-${instanceName}${appVersion}`; 
        let memStream: Streams.MemoryStream = new Streams.MemoryStream();

        // Note: We can't use blobSvc.getBlobToText() due to https://stackoverflow.com/questions/30609691/hash-mismatch-integrity-check-failed-with-azure-blob-storage 
        blobSvc.getBlobToStream(containerName, blobName, memStream, (error: Error, result: AzureStorage.BlobService.BlobResult, response: AzureStorage.ServiceResponse): void =>
        {
            try
            {
                if (error)
                {
                    reject(new Error(`Failed to query registration blob '${blobName}' in container '${containerName}' (reason: ${response ? `[Code ${response.statusCode}] ` : ""}${(error as Error).message})`));
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
                    // let logTriggerSizeMB: number = parseInt(readElementText(xml, "logTriggerSizeMB"));
                
                    // let formattedXml: string = Utils.formatXml(Utils.decodeXml(xml));
                    // Utils.log(formattedXml);

                    registrationSettings.icReceivePort = parseInt(readElementText(xml, "serviceReceiveFromPort"));
                    registrationSettings.icSendPort = parseInt(readElementText(xml, "serviceSendToPort"));
                    registrationSettings.icLogFolder = Utils.ensurePathEndsWithSeparator(readElementText(xml, "serviceLogPath"));

                    Utils.log("Registration settings read (from Azure)");
                    resolve(registrationSettings);
                
                    // Local helper function
                    function readElementText(xml: string, elementName: string): string
                    {
                        let startIndex: number = xml.indexOf(`<${elementName}>`) + elementName.length + 2;
                        let endIndex: number = xml.indexOf(`</${elementName}>`);

                        if ((startIndex >= 0) && (endIndex > startIndex))
                        {
                            let elementText: string = xml.substring(startIndex, endIndex);
                            return (elementText);
                        }
                        throw new Error(`Unable to read element '${elementName}' from registration XML`)
                    }
                }
            }
            catch (innerError)
            {
                reject(new Error(`Unable to parse registration blob '${blobName}' in container '${containerName}' (reason: ${(innerError as Error).message})`));
            }
        });
    });
    return (promise);
}

/** 
 * Asynchronously returns a list of remote instance names that the supplied 'localInstanceName' connects to, by directly querying the CRAConnectionTable in Azure.
 * Optionally (based on deleteRemoteCRAConnections), deletes entries for remote instances from the CRAConnectionTable.\
 * TODO: Ideally We would get/do this using either ImmortalCoordinator.exe or Ambrosia.exe, but no such mechanism currently exists.
 */
export async function getRemoteInstancesAsync(localInstanceName: string, deleteRemoteCRAConnections: boolean): Promise<string[]>
{
    let promise: Promise<string[]> = new Promise<string[]>((resolve, reject: RejectedPromise) =>
    {
        let tableSvc: AzureStorage.TableService = AzureStorage.createTableService(getConnString());
        let query: AzureStorage.TableQuery = new AzureStorage.TableQuery().where('PartitionKey eq ?', localInstanceName).select(["RowKey"]);        
        let tableName: string = "craconnectiontable";
        let remoteInstanceNames: string[] = [];

        // Effectively: SELECT RowKey FROM craconnectiontable WHERE PartitionKey = "(localInstanceName)"
        tableSvc.queryEntities(tableName, query, null, (error: Error, result: AzureStorage.TableService.QueryEntitiesResult<any>, response?: AzureStorage.ServiceResponse) =>
        {
            try
            {
                if (error)
                {
                    reject(new Error(`Failed to query table '${tableName}' (reason: ${response ? `[Code ${response.statusCode}] ` : ""}${(error as Error).message})`));
                }
                else
                {
                    for (let i = 0; i < result.entries.length; i++)
                    {
                        let rowKey: string = result.entries[i].RowKey._; // Eg: "Ambrosiacontrolout:server:Ambrosiacontrolin"
                        let remoteInstanceName: string = rowKey.split(":")[1] // This identifies the instance to connect to

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
                                Utils.log(error ? `Error: Failed to delete CRA connection '${localInstanceName}': '${rowKey}' (reason: [Code ${response.statusCode}] ${error.message})` :
                                                  `Deleted CRA connection for IC '${localInstanceName}' to '${remoteInstanceName}' (${rowKey})`);
                            });
                        }
                        else
                        {
                            if (remoteInstanceNames.indexOf(remoteInstanceName) !== -1)
                            {
                                remoteInstanceNames.push(remoteInstanceName);
                            }
                        }
                    }

                    // TODO: Again, this is just temporary until we have a 'DeleteConnection' message implemented in the IC.
                    if (deleteRemoteCRAConnections && (remoteInstanceNames.length > 0))
                    {
                        removeConnectionsFromRemoteInstances(remoteInstanceNames, localInstanceName);
                    }
  
                    Utils.log(`Local IC connects to ${remoteInstanceNames.length} remote ICs ${(remoteInstanceNames.length > 0) ? `'(${remoteInstanceNames.join("', '")}')` : ""}`);
                    resolve(remoteInstanceNames);
                }
            }
            catch (innerError)
            {
                reject(new Error(`Error: getRemoteInstancesAsync() failed (reason: ${(innerError as Error).message})`));
            }
        });
    });
    return (promise);
}

/** Asynchronously deletes connections from the specified remote instance(s) to the local instance. */
export function removeConnectionsFromRemoteInstances(remoteInstanceNames: string[], localInstanceName: string): void
{
    try
    {
        for (let remoteInstanceName of remoteInstanceNames)
        {
            let tableSvc: AzureStorage.TableService = AzureStorage.createTableService(getConnString());
            let query: AzureStorage.TableQuery = new AzureStorage.TableQuery().where('PartitionKey eq ?', remoteInstanceName).select(["RowKey"]);        
            let tableName: string = "craconnectiontable";

            // Effectively: SELECT RowKey FROM craconnectiontable WHERE PartitionKey = "(remoteInstanceName)"
            tableSvc.queryEntities(tableName, query, null, (error: Error, result: AzureStorage.TableService.QueryEntitiesResult<any>, response?: AzureStorage.ServiceResponse) =>
            {
                if (error)
                {
                    Utils.log(`Error: Failed to query table '${tableName}' (reason: ${response ? `[Code ${response.statusCode}] ` : ""}${(error as Error).message})`);
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
                                Utils.log(error ? `Error: Failed to delete CRA connection '${remoteInstanceName}': '${rowKey}' (reason: [Code ${response.statusCode}] ${error.message})` :
                                                  `Deleted CRA connection for IC '${remoteInstanceName}' to '${localInstanceName}' (${rowKey})`);
                            });
                        }
                    }
                }
            });
        }
    }
    catch (innerError)
    {
        Utils.log(`Error: removeConnectionsFromRemoteInstances() failed (reason: ${(innerError as Error).message})`);
    }
}