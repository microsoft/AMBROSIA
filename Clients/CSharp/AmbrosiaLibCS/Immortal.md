# Immortal

In this document we will deep-dive into the main logic of the Immortal, the building block of AMBROSIA.

## The Dispatch Loop

The Dispatch method is the main method in the Immortal class - it receives and executes incoming RPCs. This method consists of one big loop which continues processing incoming messages until either another Dispatch loop takes over or the current loop is awaiting an asynchronous operation to finish.

Only one Dispatch loop is allowed to run at a time (with the exception of one loop finishing its handling of an awaited RPC call - resulting in one of the running loops committing suicide at the next iteration). This logic is controlled by the DispatchTaskIdQueue which keeps track of all active Dispatch loops running:

```c#
lock (DispatchTaskIdQueueLock)
{
    if (this.DispatchTaskIdQueue.Data.Count > 1)
    {
        int x;
        while (!this.DispatchTaskIdQueue.Data.TryDequeue(out x)) { }
        break; // some other dispatch loop will take over, so just die.
    }
}
```

Since we don't allow real concurrency when handling RPC calls, and there is still the chance for 2 Dispatch loops to be active before one dies (in the case mentioned above), we use a single-thread scheduler when starting a new Dispatch loop:

```c#
public void StartDispatchLoop()
{
    lock (DispatchTaskIdQueueLock)
    {
        var t = this.DispatchWrapper();
        this.DispatchTaskIdQueue.Data.Enqueue(t.Id);
        t.Start(this.DispatchTaskScheduler);
    }
}
```
After assuring that only one thread is running the Dispatch loop, we are ready to start reading incoming bytes from the wire.

## Handling RPC Calls

Handling each RPC call depends on its type, which is always defined in the first byte of the incoming RPC message. The RPC type constants are defined in the AmbrosiaRuntime class.

### InitialMessage

This type of message arrives only once at the beginning of the first run of the Immortal, and it triggers the Immortal to start running the OnFirstStart() method.

#### Message format:

| Field Name | R    | messageLength | messageBuffer |
| ---------- | ---- | ------------- | ------------- |
| Field Type | byte | int           | byte[]        |

* **R** - Determines the type of the RPC call ( = AmbrosiaRuntime.InitalMessageByte)
* **messageLength** - Determines the length of the message contained in messageBuffer
* **messageBuffer** - Contains an encoded message string (currently ignored in the code)

### Checkpoint

This type of message contains a checkpoint for the Immortal to recover from. The checkpoint consists of a serialized Immortal (serialized with the generated ImmortalSerializer class), which the current Immortal then copies all fields and properties from using the CopyFromDeserializedImmortal method.

**Note:** Fields and properties which are not defined in a subclass of Immortal (e.g Immortal itself) should be decorated with the [CopyFromDeserializedImmortal] attribute if they should be copied during recovery.

#### Message format:

| Field Name | R    | checkpointSize | checkpoint |
| ---------- | ---- | -------------- | ---------- |
| Field Type | byte | long           | -          |

- **R** - Determines the type of the RPC call ( = AmbrosiaRuntime.checkpointByte)
- **checkpointSize** - Determines the size of the checkpoint contained in checkpoint
- **checkpoint** - Contains the serialized checkpoint. Since checkpointSize is a long, checkpoint is being passed to deserialization as a stream rather than a byte[] (in order to support large-sized checkpoints)

### TakeCheckpoint

This type of message signals the immortal to take a checkpoint at its current state. We will delve deeper into this logic later in this document.

#### Message format:

| Field Name | R    |
| ---------- | ---- |
| Field Type | byte |

- **R** - Determines the type of the RPC call ( = AmbrosiaRuntime.takeCheckpointByte)

### TakeBecomingPrimaryCheckpoint

This type of message signals the Immortal to take a checkpoint at its current state, as with TakeCheckpoint above. This message also signals the Immortal to call BecomingPrimary().

#### Message format:

| Field Name | R    |
| ---------- | ---- |
| Field Type | byte |

- **R** - Determines the type of the RPC call ( = AmbrosiaRuntime.takeBecomingPrimaryCheckpointByte)

### UpgradeService

This type of message signals the Immortal to upgrade. The current Immortal creates an instance of the upgraded Immortal type. All the fields from the current Immortal instance are copied into the new Immortal instance. The new Immortal instance is then being started by starting a new Dispatch loop with the same single-threaded task scheduler and the same Task Id queue. The current Immortal's Dispatch loop would then commit suicide once the new Dispatch loop takes over. The new Dispatch loop will have the current Immortal's remaining number of bytes to read handed over to it, in order to continue processing messages from the same point.

**Note:** The upgraded Immortal type is defined upon deploying the service when using the following AmbrosiaFactory.Deploy Method:

```c#
public static IDisposable Deploy<T, T2, Z2>(string serviceName, Immortal instance, int receivePort, int sendPort)
```

#### Message format:

| Field Name | R    |
| ---------- | ---- |
| Field Type | byte |

- **R** - Determines the type of the RPC call ( = AmbrosiaRuntime.upgradeServiceByte)

### UpgradeServiceTakeCheckpoint

This type of message signals the Immortal to upgrade, similarly to UpgradeService. It also signals the upgraded immortal to take a checkpoint at the time of creation (after the pervious Immortal's state has been copied over).

#### Message format:

| Field Name | R    |
| ---------- | ---- |
| Field Type | byte |

- **R** - Determines the type of the RPC call ( = AmbrosiaRuntime.upgradeServiceTakeCheckpointByte)

### RPC

RPCs are divided into Request RPCs, which trigger the Immortal to run a method defined in the user-defined interface (and returning a Response RPC if the method is not a Fire-and-Forget method) and Response RPCs which return a return value for a previously sent Request RPC. Method Ids are assigned upon code-generation and are used to encode the method for the Immortal to run. The matching between requests and responses is done by matching sequence numbers.

* #### Request RPCs:

  In the case of a request RPC, the Immortal will call its dispatcher instance (of the auto-generated Dispatcher implementation) which will handle the method call (and return a response, if one is required). 

  ##### Message format:

  | Field Name     | R    | ret  | m    | b    | lFR    | n    | args   |
  | -------------- | ---- | ---- | ---- | ---- | ------ | ---- | ------ |
  | **Field Type** | byte | byte | int  | int  | byte[] | long | byte[] |

  * **R** - Determines the type of the RPC call (= RPCByte)
  * **ret** - Determines the type of the return value (None = 0, in the case of a request RPC)
  * **m** - The method ID for the method to call
  * **b** - Size of the sender name (Required only if RPC is not fire and forget - defined in RpcType.IsFireAndForget())
  * **lFR** - Encoded name of sender (Only if RPC is not fire and forget - defined in RpcType.IsFireAndForget())
  * **n** - Contains the sequence number of the request matching the response
  * **args** - Contains serialized arguments, number and size baked into the generated code

* #### Response RPCs:

  In the case of an RPC containing a response, the Immortal will "wake up" the task awaiting this response and perform a context switch to this task (the context switch would always result in switching to the woken-up task, as it is the only task running in parallel to the current task).

  In the case of an RPC containing an exception as its return value, the exception would be set in the awaiting task, and the same context switch will be made to that task. 

  ##### Message format:

  | Field Name     | R    | ret  | n    | returnValue |
  | -------------- | :--- | :--- | :--- | :---------- |
  | **Field Type** | byte | byte | long | T           |

  * **R** - Determines the type of the RPC call (= RPCByte)  
  * **ret** - Determines the type of the return value (values defined by enum ReturnValueTypes)  
  * **n** - Contains the sequence number of the request matching the response  
  * **returnValue** - Contains a return value of type T (defined in the signature of the method called in the RPC request)

### RPCBatch

In this case, we are receiving a batch of RPC messages.

#### Message format:

| Field Name | R    | nRPC | RPC  | RPC  | ...  |
| ---------- | ---- | ---- | ---- | ---- | ---- |
| Field Type | byte | int  |      |      |      |

* **R** - Determines the type of the RPC call (= RPCBatchByte)
* **nRPC** - Number of RPCs in the batch
* **RPCs** - Batch of RPC messages in the format described above

### CountReplayableRPCBatch

#### Message format:

| Field Name | R    | nRPC | nRRPC | RPC  | RPC  | ...  |
| ---------- | ---- | ---- | ----- | ---- | ---- | ---- |
| Field Type | byte | int  | int   |      |      |      |

- **R** - Determines the type of the RPC call (= RPCBatchByte)
- **nRPC** - Number of RPCs in the batch
- **nRRPC** - Number of replayable RPCs in the batch
- **RPCs** - Batch of RPC messages in the format described above