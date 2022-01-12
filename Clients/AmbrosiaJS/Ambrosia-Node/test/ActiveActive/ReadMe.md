## :recycle: Demonstration of Using Active/Active with the Ambrosia Node.js Language Binding
---

The following describes how to setup, run, and test a basic **[active/active](https://github.com/microsoft/AMBROSIA/blob/master/CONTRIBUTING/AMBROSIA_client_network_protocol.md#activeactive)** (failover) configuration for an app/service using the Ambrosia Node language binding (LB). For simplicity, the 'app' being used in this demonstration is the LB's built-in demo/test app (`/lib/Demo.js`).

1. Start from the \AmbrosiaJS\Ambrosia-Node folder.
2. Ensure that the Ambrosia `AZURE_STORAGE_CONN_STRING` environment variable (see **[here](https://github.com/microsoft/AMBROSIA/blob/master/Samples/HelloWorld/HOWTO-WINDOWS-TwoProc.md#storage-connection-string)**) has been set.
3. If not already done, build the language binding by running `"npx tsc -p .\tsconfig.json '--incremental false'"` or by running `"build.ps1"`. &#x00B9;
4. Reset any previous test runs (if this is the first test run then you can skip this step):<br/>
   :warning:**Warning:** This command will erase the 'serverAA' instance and all its replicas. If you are already using the 'serverAA' name for a real Ambrosia app/service, then you will need to change the instance name in all the .\test\ActiveActive\ambrosiaConfig.json_replica<b>X</b>.json files.<br/>
    `node .\lib\Demo.js ambrosiaConfigFile=./test/ActiveActive/ambrosiaConfig_replica0.json eraseInstanceAndReplicas`<br/>
    You will need to press the 'y' key when prompted, and then wait at least 30 seconds as instructed.
5. Change `"autoRegister"` to `true` in ambrosiaConfig_replica<b>0</b>.json, ambrosiaConfig_replica<b>1</b>.json &#x00B2;, and ambrosiaConfig_replica<b>2</b>.json &#x00B2; in ./test/ActiveActive.<br/>
   Be sure to save the files.
6. Start 3 command prompts (for example, cmd.exe on Windows):<br/>
   From prompt 1 run: `node .\lib\Demo.js ambrosiaConfigFile=./test/ActiveActive/ambrosiaConfig_replica0.json` &#x00B3;<br/>
   From prompt 2 run: `node .\lib\Demo.js ambrosiaConfigFile=./test/ActiveActive/ambrosiaConfig_replica1.json` &#x00B3;<br/>
   From prompt 3 run: `node .\lib\Demo.js ambrosiaConfigFile=./test/ActiveActive/ambrosiaConfig_replica2.json` &#x00B3;<br/>

_The following assumes that `"outputLoggingLevel"` is set to `"Verbose"` or higher (in ambrosiaConfig.json), and that the test was reset (see step #4)..._

- When instance 0 starts, it will report `"Local instance is now primary"` (and handles the initial 'TakeBecomingPrimaryCheckpoint' message).
- When instance 1 starts, it will report `"[IC] I'm a checkpointer"`.
- When instance 2 starts, it will report `"[IC] I'm a secondary"`.
- Any activity (eg. pressing 'P') at instance 0 is immediately (within a second) reflected at instance 1 and 2, since they are in continuous recovery (ie. they are always reading from the current log [which is being written by the primary (instance 0 for now)] as it accumulates messages).
- When instance 0 is stopped (by pressing CTRL+C):
    - Instance 1 will take a checkpoint.
    - Instance 2 will report `"[IC] NOW I'm Primary"` (because failover has occurred) and it exits recovery.
- When instance 0 is restarted, it will report `"[IC] I'm a secondary"`, load the latest checkpoint (taken by instance 1), and enter continuous recovery.

To end the test, press CTRL+C in each console window where an instance is still running.

<u>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</u><br/>
&#x00B9; Visual Studio Code is the recommended way to build and edit the Ambrosia Node language binding.<br/>
&#x00B2; When `"replicaNumber"` is greater than 0, the instance will be registered as a replica (aka. a secondary).<br/>
&#x00B3; Note that in the config file `"isActiveActive"` is `true`, and a `"replicaNumber"` has been specified.<br/>

&nbsp;

---
<table align="left">
  <tr>
    <td>
      <img alt="Ambrosia logo" src="../../docs/images/ambrosia_logo.png"/>
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