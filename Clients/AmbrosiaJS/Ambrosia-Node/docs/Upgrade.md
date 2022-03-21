<!-- Note: If using VS Code, install the "bierner.markdown-emoji" extension in order to see emoji's in the built-in MarkDown preview window. -->
## :arrows_counterclockwise: Upgrading a Node.js Ambrosia App/Service
----
Once an app/service has been built using the Node.js LB (and placed into production), it may later become necessary to upgrade it. This document descibes how to upgrade a standalone Node.js LB instance. Upgrade of an **[active/active](https://github.com/microsoft/AMBROSIA/blob/master/CONTRIBUTING/AMBROSIA_client_network_protocol.md#activeactive)** configuration is not (yet) covered. You can read more about upgrades **[here](https://github.com/microsoft/AMBROSIA/blob/master/CONTRIBUTING/AMBROSIA_client_network_protocol.md#app-upgrade)**.

There are 3 types of upgrade:

- Upgrades that don't care about preserving the existing saved state (checkpoints) or state changes (logs). This type of upgrade will rarely occur [in production].
- Upgrades that don't effect either app state or deterministic message pathways. This type of upgrade will sometimes occur [in production].
- Upgrades that need to preserve the existing saved state/changes for continuity. This type of upgrade will often occur [in production].

The first two (and less common [in production]) types of upgrade are the most simple to perform:

1. Stop the app.
2. Replace the app code with the upgraded app code.
3. Delete the logs and checkpoints for the current version (for example, delete from C:\logs\server_0 where 0 is the current app version, or simply set the `deleteLogs` setting in ambrosiaConfig.json to `true`). This step can be skipped if the changes to the app are known not to effect either the app state or the deterministic message pathways.<br/>
  :warning: _When the checkpoints and logs are deleted, all app state is lost._<br/>
  :warning: _If the logs are manually deleted but not the checkpoints, the IC will error at startup with (for example)_ `FATAL ERROR 3: Missing log 1`
4. Restart the app.

Upgrades that preserve the latest saved state/changes are significantly more involved, and can be undertaken in two different ways:
- Using only the ambrosiaConfig.json file (recommended, because it requires fewer steps and is less error prone due to being mostly automated).
- "Manually" using Ambrosia.exe ("manually" is a slight misnomer since both approaches involve some manual steps).

Regardless of which approach you take, your app will require code changes to support a state-preserving upgrade.
<br/><br/>

### :desktop_computer: Programming to Handle Upgrades

The goal of a state-preserving upgrade is to not lose data during the upgrade process. To achieve this requires that your app includes both the current _and_ next (upgraded) versions of the code (referred to as "VCurrent" and "VNext" respectively). It must also include code that can convert the existing application state into the upgraded application state. Having such code enables a lossless upgrade, which involved these steps:

- App starts and loads the last checkpoint (app state) and recovers the last log file using VCurrent code.
- App converts the VCurrent app state to the VNext app state.
- App continues running using VNext code and state.

The following describes how to code these capabilities into your app.

After running code-gen, in your generated `PublisherFramework.g.ts`, a class for application state will have been created like this:
````TypeScript
class AppState extends Ambrosia.AmbrosiaAppState
````
In this class (which you may have moved [as part of the entire `State` namespace] to your code-gen source input file, as recommended) you must override the `convert()` method, which should convert the current `AppState` into an "AppStateVNext" (which you must also declare) and return it, for example:

````TypeScript
override convert(): AppStateVNext
{
    return (AppStateVNext.fromPriorAppState(this));
}
````

In your generated `PublisherFramework.g.ts`, follow the instructions in the comments for the `Messages.AppEventType.UpgradeState` and `Messages.AppEventType.UpgradeCode` events:

````TypeScript
case Messages.AppEventType.UpgradeState:
    // TODO: Add an exported [non-async] function 'onUpgradeState(upgradeMode: Messages.AppUpgradeMode): void' to ./[YourInputSourceFile].ts, then (after the next code-gen) a call to it will be generated here
    // Note: You will need to import Ambrosia to ./[YourInputSourceFile].ts in order to reference the 'Messages' namespace.
    //       Upgrading is performed by calling _appState.upgrade(), for example:
    //       _appState = _appState.upgrade<AppStateVNext>(AppStateVNext);
    break;

case Messages.AppEventType.UpgradeCode:
    // TODO: Add an exported [non-async] function 'onUpgradeCode(upgradeMode: Messages.AppUpgradeMode): void' to ./[YourInputSourceFile].ts, then (after the next code-gen) a call to it will be generated here
    // Note: You will need to import Ambrosia to ./[YourInputSourceFile].ts in order to reference the 'Messages' namespace.
    //       Upgrading is performed by calling IC.upgrade(), passing the new handlers from the "upgraded" PublisherFramework.g.ts,
    //       which should be part of your app (alongside your original PublisherFramework.g.ts).
    break;
````

For example, add these functions to the file where your created your published types and methods:

````TypeScript
export function onUpgradeState(upgradeMode: Messages.AppUpgradeMode): void
{
    State._appState = State._appState.upgrade<State.AppStateVNext>(State.AppStateVNext); 
}

export function onUpgradeCode(upgradeMode: Messages.AppUpgradeMode): void
{
    IC.upgrade(UpgradedFramework.messageDispatcher,
        UpgradedFramework.checkpointProducer,
        UpgradedFramework.checkpointConsumer,
        UpgradedFramework.postResultDispatcher);
}
````
> **Note:** In the example above, the handlers provided in the `IC.upgrade()` call are from the `PublisherFramework.g.ts` file generated for the upgraded app (VNext). Typically, to create the VNext code you would copy your existing (VCurrent) code-gen source input file, modify it as needed, re-run code-gen on it (specifying a different `FileGenOptions.generatedFileName`, eg. `"UpgradedPublisherFramework.g.ts"`), then import that into your app (alongside your VCurrent code):
>
>````TypeScript
>import * as UpgradedFramework from "./UpgradedPublisherFramework.g"; // This is a generated file
>````
><div style="height:1px"></div>
<br/>

After re-running code-gen, the `dispatcher` in `PublisherFramework.g.ts` will have been updated:

````TypeScript
case Messages.AppEventType.UpgradeState:
    {
        const upgradeMode: Messages.AppUpgradeMode = appEvent.args[0];
        PTM.onUpgradeState(upgradeMode); 
        break; 
    }
case Messages.AppEventType.UpgradeCode: 
    {
        const upgradeMode: Messages.AppUpgradeMode = appEvent.args[0];
        PTM.onUpgradeCode(upgradeMode);
        break; 
    }
````
> **Note:** "PTM" is the alias for the source input file used during code-gen, and is an acronym for "Published Types and Methods".

These handlers can be tested by changing the `debugTestUpgrade` config setting to "true" and using TTD (time-travel debugging) by setting `debugStartCheckpoint` to a non-zero value.

> **Note:** In addition to testing your upgrade handlers, the `debugTestUpgrade` config setting allow you to run a "what-if" test. This test allows existing messages to be replayed against a test instance of an upgraded app/service to verify that the changes don't introduce bugs. This helps catch regressions in the changes before actually upgrading the "live" app/service.<br/>
Logs and checkpoints are only read (never written) during a "what-if" test, so it's fully repeatable. Note also that recovery will never reach completion in this mode.

<br/>

### :rocket: Upgrade Using Only ambrosiaConfig.json

After the necessary code changes have been made, the upgrade itself can largely be accomplished purely by making changes to the ambrosiaConfig.json file.
No additional command-line tools are explicitly used (they are simply run under the covers as needed), which is made possible by specifying the path(s) to the tools (ambrosia.exe) using either the `AMBROSIATOOLS` environment variable or the `icBinFolder` setting in ambrosiaConfig.json (which can include a semi-colon separated list of paths).

**Preparing to test a "live" upgrade**

1. If doing the upgrade in a dev/test environment, it's often best to start from a "clean slate". You can do this by erasing (ie. removing all Azure data and log/checkpoint files) the instance you want to work with. To do this, first <u>ensure</u> that your ambrosiaConfig.json refers your target instance, then simply add `eraseInstance` to the `node.exe` command-line for your Ambrosia app, for example:

````PowerShell
node.exe .\out\Main.js eraseInstance
````
Rather than running your app, the presence of `eraseInstance` will cause the app to prompt you to confirm the erase operation:
````
2021/09/14 21:34:20.188: Warning: Are you sure you want to completely erase instance 'serverAA' (y/n)?
````
If you press 'y', the app will exit after producing output similar to this:
````
2021/09/14 21:34:24.437: Erasing instance 'serverAA'...
2021/09/14 21:34:24.686: Blob 'AmbrosiaBinaries/serverAA-serverAA0' was deleted
2021/09/14 21:34:24.699: Table 'serverAA' was deleted
2021/09/14 21:34:24.838: 2 row(s) deleted from 'craconnectiontable'
2021/09/14 21:34:25.062: 4 row(s) deleted from 'craendpointtable'
2021/09/14 21:34:25.063: 2 row(s) deleted from 'cravertextable'
2021/09/14 21:34:25.067: Removed C:\logs\serverAA_0 (4 files deleted)
2021/09/14 21:34:25.067: Instance 'serverAA' successfully erased
2021/09/14 21:34:25.067: Warning: Please wait at least 30 seconds before re-registering / starting the instance, otherwise you may encounter HTTP error 409 (Conflict) from Azure
````

2. Since we just erased the instance, edit the ambrosiaConfig.json file to request automatic registration the next time the app starts:<br/>
`"autoRegister": true`<br/>
The version numbers are left at their defaults:<br/>
`"appVersion": 0`<br/>
`"upgradeVersion": 0`<br/>
`"activeCode": "VCurrent"`<br/>

    > Note: For any initial registration, both `appVersion` and `upgradeVersion` must be the same. So, to start with a non-zero `appVersion` (for example 3), just set both
     `appVersion` and `upgradeVersion` to 3. Alternatively, `upgradeVersion` can simply be omitted.

3. Now start the app, let it do some work (so that we have some messages to recover during the upgrade), then stop the app.<br/>
Note that the `autoRegister` setting will automatically reset itself to "false" after the registration.

**Performing a "live" upgrade**
1. Stop the app.
2. Replace the app with new app code that contains VCurrent and VNext code, can handle the `UpgradeState` and `UpgradeCode` app-events, and has had its upgrade pathway tested using the `debugTestUpgrade` setting (in ambrosiaConfig.json).

3. Edit the ambrosiaConfig.json to specify that a "live" upgrade is required by setting `upgradeVersion` to a value larger than `appVersion`, for example:<br/>
`"upgradeVersion": 1`<br/>
Also, ensure that `deleteLogs` is false.

4. Now re-start the app. Note these key message sequences in the output (assuming that the `outputLoggingLevel` in ambrosiaConfig.json is set to "Verbose"):

````
2021/03/26 11:34:05.147: [Re]registering instance 'serverAA' 
2021/03/26 11:34:10.978: Instance successfully [re]registered (for upgrade) 
````
````
2021/03/26 11:34:15.904: Received 'UpgradeTakeCheckpoint' (2 bytes) 
2021/03/26 11:34:15.904: Recovery complete (Received 3 messages [2 Fork messages], sent 2 Fork messages) 
2021/03/26 11:34:15.905: Upgrading app (state and code) [in 'Live' mode] 
2021/03/26 11:34:15.907: Upgrade of state and code complete 
````
````
2021/03/26 11:34.16.489: Received 'TakeCheckpoint' (2 bytes)
2021/03/26 11:34.16.490: Sending 'Checkpoint' to local IC (4 bytes)
2021/03/26 11:34.16.491: Streaming 'CheckpointDataStream (233 bytes)' to local IC...
2021/03/26 11:34.16.494: Stream 'CheckpointDataStream (233 bytes)' finished
2021/03/26 11:34.16.494: checkpointProducer: Checkpoint saved
2021/03/26 11:34.16.499: Upgrade complete
````
After you see the "Upgrade complete" message, observe that these ambrosiaConfig.json settings have been updated:<br>
`"autoRegister": true`<br/>
`"appVersion": 1`<br/>
`"upgradeVersion": 0`<br/>
`"activeCode": "VNext"`<br/>

5. The next time you restart the app, it will complete the final step in the upgrade which is to re-register the new `appVersion`.

When you’re ready for the _next_ upgrade (or at anytime leading up to it), stop the instance, change `activeCode` to "VCurrent" and update the `IC.start()` call to use the handlers being assigned [as part of the previous upgrade] by `IC.upgrade()`.<br/><br/>

### :raised_hand_with_fingers_splayed: "Manual" Upgrade

While the Node.js LB provides additional assistance to perform an upgrade of a standalone instance, you can still chose to upgrade "manually" if you prefer. The steps are essentially the same as those documented **[here](https://github.com/microsoft/AMBROSIA/blob/master/CONTRIBUTING/AMBROSIA_client_network_protocol.md#app-upgrade)**.

To manually do a upgrade of a standalone Node.js app instance:

1) Stop the app.
2) Replace the app with new app code that contains VCurrent and VNext code, can handle the `UpgradeState` and `UpgradeCode` app-events, and has had its upgrade pathway tested using the `debugTestUpgrade` setting (in ambrosiaConfig.json).
3) Optionally, remove the following settings from ambrosiaConfig.json: `appVersion`, `deleteLogs`.
4) Set `deleteLogs` to "false" (not needed if `deleteLogs` is omitted from ambrosiaConfig.json – see step 3).
5) Manually run "Ambrosia.exe RegisterInstance" to set the upgradeVersion, for example:<br/>
````PowerShell
Ambrosia.exe RegisterInstance --instanceName=server --receivePort=2000 -sendPort=2001 --log=C:/logs/ --currentVersion=0 --upgradeVersion=1
````
6) Start the app, and wait for "Upgrade complete" message.
7) Stop the app [when next convenient].
8) Manually run "Ambrosia.exe RegisterInstance" to update the currentVersion, for example:<br/>
````PowerShell
Ambrosia.exe RegisterInstance --instanceName=server --receivePort=2000 -sendPort=2001 --log=C:/logs/ --currentVersion=1
````
9) Set `appVersion` in ambrosiaConfig.json to the upgradeVersion (not needed if `appVersion` is omitted from ambrosiaConfig.json – see step 3).
10) Set `activeCode` to "VNext" in ambrosiaConfig.json, or replace the app code with new app code that has the VNext code as the [now] VCurrent code.
11) Start the app.

The additional upgrade assistance provided by the Node.js LB, is exactly that: additional. As shown above, you can still manually request the upgrade via RegisterInstance, and manually re-register the instance to update currentVersion. The only difference is that you also have to edit the ambrosiaConfig.json file to match the manual changes, although even this can be mitigated by simply omitting the affected settings from the json file.

The motivation for providing the additional support for upgrade in the Node.js LB was to have a more automated end-to-end upgrade solution (for the standalone case at least) "out of the box". This can then be built upon with additional automation should you wish to write your own "upgrade orchestration service".
<br/><br/>
### :vertical_traffic_light: Upgrade Orchestration
As has been shown, upgrade is a multi-step process, and managing it at any kind of scale or frequency will require automating the process through the creation of an "upgrade orchestration manager". While there is limited support for authoring such a tool today (January 2022), we hope to deliver an "Immortal Lifecycle API" at some point in the future that will address this gap.

Any upgrade orchestration service will need to be aware of LB differences. For example, the Node.js LB uses a .json file for configuration, but other LB's may also use the config file format (or config techniques) most appropriate for their target language/technology. While we don't restrict LB authors from deciding how to best serve their user base, we do expect each LB author to document the specifics of how upgrade is performed/managed using their LB (as we have done here for the Node.js LB).

_Aside..._

The advantage of using a .json file for configuration are:
-	It can be placed under source control (so its versionable/diffable).
-	It can be easily programmatically modified (JSON is a platform-agnostic industry standard).
-	It has rich support in IDEs (eg. IntelliSense, defaults) via JSON schema.
-	It's unified: a single configuration file for an Immortal (IC and LB) that can live alongside it.
-	It's structured/standardized, so it's preferrable to creating loose batch files to persist various sets of command-line parameters (eg. for TTD vs. “normal” running vs. upgrade).
<br/><br/>
### :repeat: Migration vs. Upgrade

Migration refers to starting an instance while it's already running elsewhere. The already running instance will detect that the new instance has started and will self-terminate, allowing the new instance to take over. This enables an instance to seamlessly "migrate" from machine to machine, allowing the app to pick up exactly where it left off on whatever device it runs on. This capability is one of the many advantages of using an Ambrosia-enabled app.

For an app to migrate across machines it must either be using an `icLogStorageType` of `"Blobs"`, or &ndash; when `icLogStorageType` is `"Files"` &ndash; specify an `icLogFolder` that's on a file share. This is required so that the checkpoint and logs files are stored in a location that's accessible to both machines, and so that the already running instance can detect that a migration has been requested.

Although it's of no practical use (other than to make testing easier), an app can also "migrate" on the same machine. However, for this to work it's necessary for the new instances to specify a different `icCraPort`, `icRecievePort` and `icSendPort` than the already running instance.

The conceptual connection between upgrade and migration is that an upgrade can also occur when an app migrates (although most migrations will not involve an upgrade). Typically, this happens when upgrading an app running in an **[active/active](https://github.com/microsoft/AMBROSIA/blob/master/CONTRIBUTING/AMBROSIA_client_network_protocol.md#activeactive)** configuration (see **[here](https://github.com/microsoft/AMBROSIA/blob/master/CONTRIBUTING/AMBROSIA_client_network_protocol.md#app-upgrade)**). Basically, when a secondary (aka. replica) is being upgraded it starts on another machine (making it a migration) which then triggers all other secondaries - along with the current primary - to self-terminate.

> **How migration is detected by the IC:** The log file has an exclusive write lock and shared read lock, with the primary always taking the exclusive write lock when it becomes the primary. When the migrating (second) instance is started, it takes a shared read lock on the log file and runs recovery.  When recovery is complete, it slowly spins (200ms) waiting to lock the "kill" file, then slowly spins again (200ms) waiting for the original instance to self-terminate (which it does when it discovers that it can no longer lock the kill file, which all primaries periodically try to do). After the original instance terminates, the second instance takes the exclusive write lock on the log file, releases the lock on the kill file, and becomes the primary. Note that it will take the original instance at least 4500ms (3 attempts at 1500ms each) to detect it needs to self-terminate, so it takes at least that long for the second instance to become the primary <i>after</i> it completes recovery.

&nbsp;

---
<table align="left">
  <tr>
    <td>
      <img alt="Ambrosia logo" src="images/ambrosia_logo.png"/>
    </td>
    <td>
      <div>
        <a href="https://github.com/microsoft/AMBROSIA#ambrosia-robust-distributed-programming-made-easy-and-efficient">AMBROSIA</a>
      </div>
      <sub>An Application Platform for Virtual Resiliency</sub>
      <br/>
      <sub>from Microsoft Research</sub>
    </td>
  </tr>
</table>
