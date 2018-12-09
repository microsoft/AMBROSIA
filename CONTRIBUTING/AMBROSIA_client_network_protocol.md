
Client Protocol for AMBROSIA network participants
=================================================

This document covers how a network endpoint should communicate with
the AMBROSIA reliability coordinator assigned to it (typically located
within the same physical machine).

Overview and Terminology
------------------------

FINISHME

 * Commit ID
 * Sequence ID

 * "Async/await" RPC - a classic notion of a *future*.  These RPCs return a value back to the caller.  Because AMBROSIA ensures reliability, they are semantically identical to function calls, without introducing new failure modes such as timeouts or disconnections.

 * "Fire and Forget" RPC - launch a remote computation, but provide no information back to the caller.  Note that even an async/await
RPC with "void" return value exposes more than this, because the caller can ascertain when the remote RPC has been completely processed.


Required Helper Functions
-------------------------

FINISHME:

Assumes TCP + 

 * WriteZigZagInt
 * WriteFixedInt
 * WriteZigZagLong
 * WriteFixedLong 

 * CheckSum

Message Formats
---------------

 * LogRecords - *log header* followed by zero or more messages.
 * Message - all regular AMBROSIA messages

All information received from the reliability coordinator is in the form of a sequence of log records.
Each log record has a 24 byte header, followed by the actual record contents. The header is as follows:

 * Bytes [0-3]: The commit ID for the service, this should be constant for all records for the lifetime of the service, format IntFixed.
 * Bytes [4-7]: The size of the whole log record, in bytes, including the header. The format is IntFixed
 * Bytes [8-15]: The check bytes to check the integrity of the log record. The format is LongFixed.
 * Bytes [16-23]: The log record sequence ID. Excluding records labeled with sequence ID “-1”, these should be in order. The format is LongFixed

The rest of the record is a sequence of messages, packed tightly, each with the following format:

 * Size : Number of bytes taken by Type and Data – 1 to 5 bytes, depending on value (format ZigZagInt)
 * Type : A byte which indicates the type of message
 * Data : A variable length sequence of bytes which depends on the message type


All information sent to the reliability coordinator is in the form of a sequence of messages with the format specified above.
Message types and associated data which may be sent to or received by services:

 * 14 - TrimTo (RRN: INTERNAL!??!)
 * 13 - CountReplayableRPCBatchByte (RRN: INTERNAL!??!)

 * 12 – `UpgradeService` (Received): No data

 * 11 – `TakeBecomingPrimaryCheckpoint` (Received): No data

 * 10 – `UpgradeTakeCheckpoint` (Received): No data

 * 9 – `InitialMessage` (Sent/Received): Data is a complete (incoming rpc) message which is given back to the service as the very first RPC message it ever receives. Used to bootstrap service start behavior.

 * 8 – `Checkpoint` (Sent/Received): Data are the bytes corresponding to the serialized state of the service.

 * 5 – `RPCBatch` (Sent/Received): Data are a count of the number of RPC messages in the batch, followed by the corresponding number of RPC messages. Note that the count is in variable sized WriteInt format

 * 2 – `TakeCheckpoint` (Received): No data

 * 1 – `AttachTo` (Sent): Data are the destination bytes. Note that these must match the names used when services are logically created using localambrosiaruntime

 * 0 - Incoming RPC (Received):

  - Byte 0 of data is reserved (RPC or return value), and is currently always set to 0 (RPC).
  - Next is a variable length int (ZigZagInt) which is a method ID.
  - The next byte is a reserved byte (Fire and forget or Async/Await) and is currently always set to 1 (Fare and Forget).
  - The remaining bytes are the serialized arguments packed tightly.

 * 0 - Outgoing RPC (Sent):

  - First is a variable length int (ZigZagInt) which is the length of the destination service.
  - Next are the actual bytes for the name of the destination service.
  - Next follow all four fields listed above under "Incoming RPC".

That is, an Outgoing RPC is just an incoming RPC with two extra fields on the front.


Communication Protocols
-----------------------

### Starting up:

If starting up for the first time:

 * Receive a `TakeBecomingPrimaryCheckpoint` message
 * Send an `InitialMessage`
 * Send a checkpoint message
 * Normal processing

If recovering but not upgrading, or starting as a non-upgrading secondary, or running a repro or what-if test:

 * Receive a checkpoint message
 * Receive logged replay messages
 * Receive takeBecomingPrimaryCheckpoint message
 * Send a checkpoint message
 * Normal processing

If recovering and upgrading, or starting as an upgrading secondary:

 * Receive a checkpoint message
 * Receive logged replay messages
 * Receive UpgradeTakeCheckpoint message
 * Upgrade state
 * Send a checkpoint message for upgraded state
 * Normal processing

If performing an upgrade what-if test:

 * Receive a checkpoint message
 * Receive upgradeService message
 * Upgrade state
 * Receive logged replay messages

### Normal operation:

 * Receive an arbitrary mix of RPCs, RPC batches, and TakeCheckpoint messages.

When a TakeCheckpoint message is received, no further messages may be processed until the state is serialized and sent in a checkpoint message. Note that the serialized state must include any unsent output messages which resulted from previous incoming calls. Those serialized unsent messages must follow the checkpoint message.

(RRN: What are the rules for SENDING!!)

(RRN: What is the ATTACH protocol??)
