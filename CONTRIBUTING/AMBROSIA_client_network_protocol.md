
Client Protocol for AMBROSIA network participants
=================================================

Each application has an AMBROSIA reliability coordinator assigned to it. 
The coordinator is located within the same physical machine/container, and 
must survive or fail with the application process. This process separation
is designed to minimize assumptions about the application and maximize 
language-agnosticity. 
The coordinator (also known as an Immortal Coordinator) communicates
via TCP/IP over 2 local sockets with the application through a language-specific
binding. This document covers how a language binding should communicate with
it's Immortal Coordinator, providing a high-level spec for a language binding author.

Overview and Terminology
------------------------

In AMBROSIA a set of application processes (services) serve as communication
endpoints, communicating *exclusively* through the network of Immortal
Coordinators, which collectively serve as the message bus.  The individual
processes (or objects contained therein) are the *Immortals* which survive the
failure of individual machines.

Below we use the following terminology:

 * Committer ID - an arbitrary (32 bit) identifier for a communication endpoint
   (a service) in the network of running "immortals".  This is typically
   generated automatically the first time each application process starts.
   It is distinct from the destination *name*.

 * Destination name - the string identifying a communication endpoint, often
   human readable.

 * Sequence ID - the (monotonically increasing) number of a log entry. Note that
   each logical immortal has its own log.

 * "Async/await" RPCs - are *futures*; they return a value back to the
   caller.  Because AMBROSIA ensures reliability, they are semantically
   identical to function calls, without introducing new failure modes such as
   timeouts or disconnections.

 * "Fire and Forget" RPCs - launch a remote computation, but provide no
   information back to the caller.  Note that even an async/await RPC with
   "void" return value indicates more to the caller (namely, that the remote
   computation has completed).

 * "Language Binding" (LB) - the language-specific AMBROSIA binding that 
   exposes the programming interfaces and handles all communication with
   the associated Immortal Coordinator (IC).

Required Helper Functions
-------------------------

In order to build the binary message formats described below, we assume that the
new client software can access TCP sockets and additionally implements the
following serialized datatypes.

 * ZigZagInt  - a zig-zag encoded 32-bit signed integer
 * ZigZagLong - a zig-zag encoded 64-bit signed integer
 * IntFixed  - a 32-bit little endian number 
 * LongFixed - a 64-bit little endian number 

The variable-length integers are in the same format used by, e.g.,
[Protobufs](https://developers.google.com/protocol-buffers/docs/encoding).


Message Formats
---------------

 * LogRecords - *log header* followed by zero or more messages.
 * Message - all regular AMBROSIA messages

All information received from the reliability coordinator is in the form of a sequence of log records.
Each log record has a 24 byte header, followed by the actual record contents. The header is as follows:

 * Bytes [0-3]: The committer ID for the service, this should be constant for all records for the lifetime of the service, format IntFixed.
 * Bytes [4-7]: The size of the whole log record, in bytes, including the header. The format is IntFixed.
 * Bytes [8-15]: The check bytes to check the integrity of the log record. The format is LongFixed.
 * Bytes [16-23]: The log record sequence ID. Excluding records labeled with sequence ID “-1”, these should be in order. The format is LongFixed.

The rest of the record is a sequence of messages, packed tightly, each with the following format:

 * Size : Number of bytes taken by Type and Data; 1 to 5 bytes, depending on value (format ZigZagInt).
 * Type : A byte which indicates the type of message.
 * Data : A variable length sequence of bytes which depends on the message type.


All information sent to the reliability coordinator is in the form of a sequence of messages with the format specified above.
Message types and associated data which may be sent to or received by services:

 * 15 - `BecomingPrimary` (Received) : No data

 * 14 - `TrimTo`: Only used in IC to IC communication. The IC will never send this message type to the LB.

 * 13 - `CountReplayableRPCBatchByte` (Recieved): Similar to `RPCBatch`, but the data also includes a count (ZigZagInt)
   of non-Impulse (replayable) messages after the count of RPC messages.

 * 12 – `UpgradeService` (Received): No data

 * 11 – `TakeBecomingPrimaryCheckpoint` (Received): No data

 * 10 – `UpgradeTakeCheckpoint` (Received): No data

 * 9 – `InitialMessage` (Sent/Received): Data can be any arbitrary bytes. The `InitialMessage` message will simply be echoed back
   to the service which can use it to bootstrap service start behavior. In the C# language binding, the data is a complete incoming RPC
   message that will be the very first RPC message it receives. 

 * 8 – `Checkpoint` (Sent/Received): The data is a single 64 bit number (ZigZagLong).
   This message is immediately followed (no additional header) by checkpoint itself, 
   which is a binary blob.
   The reason that checkpoints are not sent in the message payload directly is
   so that they can have a 64-bit instead of 32-bit length, in order to support
   large checkpoints.

 * 5 – `RPCBatch` (Sent/Received): Data is a count (ZigZagInt) of the number of RPC messages in the batch, followed by the corresponding RPC messages.
   When sent by the LB, this message is essentially a performance hint to the IC that enables optimized processing of the RPCs, even for as few as 2 RPCs.

 * 2 – `TakeCheckpoint` (Sent/Received): No data. 
   When sent by the LB, this message requests the IC to take a checkpoint immediately rather than waiting until the log reaches the IC's `--logTriggerSize` (which defaults to 1024 MB).

 * 1 – `AttachTo` (Sent): Data is the destination instance name in UTF-8. The name must match the name used when the instance was logically created (registered).
       The `AttachTo` message must be sent (once) for each outgoing RPC destination, excluding the local instance, prior to sending an RPC.

 * 0 - Incoming `RPC` (Received):

   - Byte 0 of data is reserved (RPC or return value).
   - Next is a variable length int (ZigZagInt) which is a method ID. Negative method ID's are reserved for system use.
   - The next byte is the RPC type: 0 = Async/Await, 1 = Fire-and-Forget (aka. Fork), 2 = Impulse.
   - The remaining bytes are the serialized arguments packed tightly.

 * 0 - Outgoing `RPC` (Sent):

   - First is a variable length int (ZigZagInt) which is the length of the destination service.  For a self call, this should be set to 0 and the following field omitted.
   - Next are the actual bytes (in UTF-8) for the name of the destination service.
   - Next follow all four fields listed above under "Incoming RPC".

That is, an Outgoing RPC is just an incoming RPC with two extra fields on the front.


Communication Protocols
-----------------------

### Starting up:

If starting up for the first time:

 * Receive a `TakeBecomingPrimaryCheckpoint` message
 * Send an `InitialMessage`
 * Send a `Checkpoint` message
 * Normal processing

If recovering, but not upgrading, a standalone (non-active/active) immortal:

 * Receive a `Checkpoint` message
 * Receive logged replay messages
 * Receive `TakeBecomingPrimaryCheckpoint` message
 * Send a `Checkpoint` message
 * Normal processing

If recovering, but not upgrading, in active/active:

 * Receive a `Checkpoint` message
 * Receive logged replay messages
 * Receive `BecomingPrimary` message
 * Normal processing

If recovering and upgrading a standalone immortal, or starting as an upgrading secondary in active/active:

 * Receive a `Checkpoint` message
 * Receive logged replay messages 
   > Note: Replayed messages MUST be processed by the old (pre-upgrade) code to prevent changing the generated sequence
   of messages that will be sent to the IC as a consequence of replay. <br/>Further, this requires that your
   service (application) is capable of dynamically switching (at runtime) from the old to the new version of its code.
   See 'App Upgrade' below.
 * Receive `UpgradeTakeCheckpoint` message
 * Upgrade state and code
 * Send a `Checkpoint` message for upgraded state
 * Normal processing

If performing a repro test:

 * Receive a `Checkpoint` message
 * Receive logged replay messages

> Repro testing, also known as "Time-Travel Debugging", allows a given existing log to be replayed, for example to re-create 
the sequence of messages (and resulting state changes) that led to a bug. See 'App Upgrade' below.

If performing an upgrade test:

 * Receive a `Checkpoint` message
 * Receive `UpgradeService` message
 * Upgrade state and code
 * Receive logged replay messages

> Upgrade testing, in addition to testing the upgrade code path, allows messages to be replayed against an upgraded 
service to verify if the changes cause bugs. This helps catch regressions before actually upgrading the live service.
See 'App Upgrade' below.

### Normal processing:

 * Receive an arbitrary mix of `RPC`, `RPCBatch`, and `TakeCheckpoint` messages.
 * Persisted application state (the content of a checkpoint) should only ever be changed
   as a consequence of processing `RPC` and `RPCBatch` messages. This ensures that the 
   application state can always be deterministically re-created during replay (recovery).
 * The LB must never process messages [that modify application state] while it's in the process
   of either loading (receiving) or taking (sending) a checkpoint. This ensures the integrity of
   the checkpoint as a point-in-time snapshot of application state.

### Receive logged replay messages:

 * During recovery, it is a violation of the recovery protocol for the application to send an Impulse RPC. So while a replayed Impulse RPC can send 
   Fork RPCs, it cannot send Impulse RPCs. If it does, the language binding should throw an error.

### Attach-before-send protocol:

* Before an RPC is sent to an Immortal instance (other than to the local Immortal), the `AttachTo` message must be sent (once).
  This instructs the local IC to make the necessary TCP connections to the destination IC.

### Active/Active:

This is a high-availability configuration (used for server-side services only) involving at least 
3 immortal (service/LB + IC pair) instances: A **primary**, a **checkpointing secondary**, and one or more 
**standby secondaries**, which are continuously recovering until they become primary. A secondary is also 
sometimes referred to as a replica. Despite typically running on separate machines (and in separate racks
and/or datacenters), all instances "share" the log and checkpoint files.  Failover happens when the primary
loses its lock on the log file. The primary is the non-redundant instance. If it fails, one of the standby 
secondaries will become the primary, after completing recovery. The checkpointing secondary never becomes
the primary, and if it fails, the next started replica becomes the checkpointing secondary, even if it's the 
first started replica after all replicas fail.

The primary never takes checkpoints, except when it first starts (ie. before there are any logs).
Thereafter, all checkpointing is handled by the checkpointing secondary. This arrangement allows
the primary to never have to "pause" to take a checkpoint, increasing availability. A deep dive
into the theory behind active/active can be found in the [Shrink](https://www.vldb.org/pvldb/vol10/p505-goldstein.pdf)
paper, and how to configure an active/active setup is explained [here](https://github.com/microsoft/AMBROSIA/blob/3d86a6c140c823f594bf6e8daa9de14ed5ed6d80/Samples/HelloWorld/ActiveActive-Windows.md).

The language binding is oblivious as to whether it's in an active/active configuration or not.  However, it 
must be aware of whether it's a primary or not – primarily so that it can generate an error if an attempt is
made to send an Impulse before the instance has become the primary (it's a violation of the Ambrosia protocol to send an Impulse during recovery).
The LB must also notify the host service (app) when it has become the primary – for example, so that the service 
doesn't try to send the aforementioned Impulse before it's valid to do so.

There are 3 different messages that tell the LB it is becoming the primary, with each occurring under difference circumstances:
* `TakeBecomingPrimaryCheckpoint` – The instance is becoming the primary and **should** take a checkpoint (ie. this is the first start of the primary).
* `BecomingPrimary` – The instance is becoming the primary but **should not** take a checkpoint (ie. this is a non-first start of the primary).
* `UpgradeTakeCheckpoint` – The instance is a primary that is being upgraded and **should** take a checkpoint. Note that only a newly registered secondary
   can be upgraded, and it will cause all other secondaries – along with the existing primary – to die (see 'App Upgrade' below).

Finally, "non-active/active" (or "standalone") refers to a single immortal instance running by itself without any secondaries.

### App Upgrade:

Upgrade is the process of migrating an instance from one version of code and state to another version of code and
state ("state" in this context means the application state data). From the LB's perspective there are no version 
numbers involved: it simply has code/state for VCurrent and code/state for VNext. Both versions must be present so
that the app can recover using VCurrent, but then proceed using VNext. When the LB receives `UpgradeTakeCheckpoint` 
(or `UpgradeService` when doing an upgrade test) it switches over the state and code from VCurrent to VNext.
Note that the lack of version numbering from the LB's perspective is in contrast to the parameters supplied to 
`Ambrosia.exe RegisterInstance` (see below) which are specific integer version numbers. These numbers refer to "the version
of the running instance", not "the version of the state/code". This loose relationship is by design to offer maximum
flexibility to the deployment configuration of the service. 

Performing an upgrade of a standalone instance always involves stopping the app (or service), so it always involves downtime. The steps are:
* Stop the current instance.
* Run `Ambrosia.exe RegisterInstance --instanceName=xxxxx --currentVersion=n --upgradeVersion=m` where n and m are the integer version numbers with m > n.
* Start the new instance (that contains the VCurrent and VNext app code, and the VCurrent-to-VNext state conversion code).

To upgrade an active/active instance a new replica (secondary) is registered and started, which upgrades the current version, similar to
the previous example, but for a new replica. When the replica finishes recovering, it stops the primary, and holds a
lock on the log file which prevents other secondaries from becoming primary. Upon completion of state and code upgrades,
including taking the first checkpoint for the new version, execution continues and the suspended secondaries die.
If the upgrade fails, the upgrading secondary releases the lock on the log, and one of the suspended secondaries becomes
primary and continues with the old version of state/code.

Before doing a real (live) upgrade you can test the upgrade with this [abridged] example command:

`Ambrosia.exe DebugInstance --checkpoint=3 --currentVersion=0 --testingUpgrade=true`

> Note: Performing an upgrade test leads to a `UpgradeService` message being received as opposed to a `UpgradeTakeCheckpoint` message being 
received when doing a real (live) upgrade.

Doing a repro test (aka. "Time-Travel Debugging") is similar, just with `--testingUpgrade` set to false (or ommitted):

`Ambrosia.exe DebugInstance --checkpoint=1 --currentVersion=0 --testingUpgrade=false`

