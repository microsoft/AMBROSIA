
Client Protocol for AMBROSIA network participants
=================================================

This document covers how an application should communicate with the AMBROSIA
reliability coordinator assigned to it.  The coordinator is located within the
same physical machine/container and assumed to survive or fail with the
application process.  The coordinator communicates via TCP/IP over a local
socket with the application through a language-specific binding.  This process
separation is designed to minimize assumptions about the application and maximize
language-agnosticity.

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

 * CheckSum - FINISHME

The variable-length integers are in the same format used by, e.g.,
[Protobufs](https://developers.google.com/protocol-buffers/docs/encoding).


Message Formats
---------------

 * LogRecords - *log header* followed by zero or more messages.
 * Message - all regular AMBROSIA messages

All information received from the reliability coordinator is in the form of a sequence of log records.
Each log record has a 24 byte header, followed by the actual record contents. The header is as follows:

 * Bytes [0-3]: The committer ID for the service, this should be constant for all records for the lifetime of the service, format IntFixed.
 * Bytes [4-7]: The size of the whole log record, in bytes, including the header. The format is IntFixed
 * Bytes [8-15]: The check bytes to check the integrity of the log record. The format is LongFixed.
 * Bytes [16-23]: The log record sequence ID. Excluding records labeled with sequence ID “-1”, these should be in order. The format is LongFixed

The rest of the record is a sequence of messages, packed tightly, each with the following format:

 * Size : Number of bytes taken by Type and Data – 1 to 5 bytes, depending on value (format ZigZagInt)
 * Type : A byte which indicates the type of message
 * Data : A variable length sequence of bytes which depends on the message type


All information sent to the reliability coordinator is in the form of a sequence of messages with the format specified above.
Message types and associated data which may be sent to or received by services:

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

 * 2 – `TakeCheckpoint` (Received): No data

 * 1 – `AttachTo` (Sent): Data is the destination instance name length (ZigZagInt) followed by the name in UTF-8. Note that the name must match the
       name used when a service is logically created (registered). The `AttachTo` message must be sent (once) for each each outgoing RPC destination,
       exluding the local instance, prior to sending an RPC.

 * 0 - Incoming RPC (Received):

   - Byte 0 of data is reserved (RPC or return value)
   - Next is a variable length int (ZigZagInt) which is a method ID.
   - The next byte is a reserved byte (Fire and forget (1), Async/Await (0), or Impulse (2))
   - The remaining bytes are the serialized arguments packed tightly.

 * 0 - Outgoing RPC (Sent):

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

If recovering but not upgrading, or starting as a non-upgrading secondary, or running a repro or what-if test:

 * Receive a checkpoint message
 * Receive logged replay messages
 * Receive takeBecomingPrimaryCheckpoint message
 * Send a `Checkpoint` message
 * Normal processing

If recovering and upgrading, or starting as an upgrading secondary:

 * Receive a checkpoint message
 * Receive logged replay messages 
   <br/>*Note: MUST be processed by the old (pre-upgrade) code to prevent changing the generated sequence
   of messages that will be sent to the IC as a consequence of replay. Further, this requires that your
   service (application) is capable of dynamically switching (at runtime) from the old to the new version of its code.*
 * Receive `UpgradeTakeCheckpoint` message
 * Upgrade state and code
 * Send a checkpoint message for upgraded state
 * Normal processing

If performing an upgrade what-if test:

 * Receive a checkpoint message
 * Receive `UpgradeService` message
 * Upgrade state and code
 * Receive logged replay messages

The what-if testing allows messages to replayed against a (nominally) upgraded service to verify if the changes cause bugs.
This helps catch regressions before actually upgrading the live service. To receive `UpgradeTakeCheckpoint` or `UpgradeService`
messages requires special command line parameters to be passed to the IC.

### Normal operation:

 * Receive an arbitrary mix of RPCs, RPC batches, and TakeCheckpoint messages.

When a TakeCheckpoint message is received, no further messages may be processed until the state is serialized and sent in a checkpoint message. Note that the serialized state must include any unsent output messages which resulted from previous incoming calls. Those serialized unsent messages must follow the checkpoint message.

### Attach-before-send protocol

* Before an RPC is sent to an Immortal instance (other than to the local Immortal), the `AttachTo` message must be sent (once).
  This instructs the local IC to make the necessary TCP connections to the destination IC.

