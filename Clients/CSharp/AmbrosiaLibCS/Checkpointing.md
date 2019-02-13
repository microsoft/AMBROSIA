# Checkpointing the Immortal State

Every time a Take Checkpoint message is received by the Immortal, the Immortal saves and serializes its current state and sends it over to the ImmortalCoordinator, which logs the checkpoint for later recovery. Since the Immortal does not process any other messages while handling the Take Checkpoint message, its state is guaranteed not to change whilst saving its state.

The checkpointing differs between two scenarios - an Immortal which contains no async calls to other Immortals, and an Immortal which contains async calls to other Immortals. The difference stems from the fact that with async calls, the Immortal needs to keep track of their current state and continuation. For the latter scenario we are making use of a 3rd party open-source code to serialize a state of a C# Task (the original code repository can be found on GitHub here: https://github.com/ljw1004/blog/tree/master/Async/AsyncWorkflow).

## Checkpointing - w/o async calls

The simplest form of checkpointing is when the Immortal is only making Fork calls to other Immortals. In this scenario the Immortal does not care about the state of each call, only if it was made or not.

In this case, the immortal instance is simply being serialized at its current state using the generated ImmortalSerializer and the serialized state is then sent over to the ImmortalCoordinator. As no other messages are being handled concurrently, this is a deterministic process.

## Checkpointing - w/ async calls

Checkpointing with async calls is a more elaborate process, as the current state of the Immortal instance must consider the state of each async call, and upon recovery should be able to continue handling responses to previously sent async calls.

In order to serialize the current state machine we are utilizing external open-source code, contained in the TaskCheckpoint class (link to GitHub repository mentioned above). The Task which state is serialized is defined in OnFirstStartWrapper(), upon calling:

```c#
 await this.OnFirstStart().RunWithCheckpointing(ref this.SerializedTask);
```

The Task which state is serialized is the one returned by the async method *this.OnFirstStart()*, and the state  is being serialized and saved into the StringBuilder object *this.SerializedTask*.

**Note:** In order for this serialization to work, every variable on the call stack must be serializable.

The actual state of the task will be saved by one of the tasks awaiting a response (there must always be at least one pending Task, otherwise execution wouldn't have been handed over to processing a new incoming message - the TakeCheckpoint message).

For this purpose, the Immortal contains two main data structures:

```c#
public SerializableCallCache CallCache = new SerializableCallCache();
```

The *CallCache* is matching between RPC sequence numbers and a TaskCompletionSource object which is set to contain the result of the async call once a response arrives. This structure is serialized upon checkpointing and copied over to a recovering Immortal instance.

and: 

```c#
public SerializableCache<int, long> TaskIdToSequenceNumber = new SerializableCache<int, long>();
```

The *TaskIdToSequenceNumber* is matching between the TaskId associated with the current async call and the RPC sequence number. This structure is of course not copied over upon recovery.

Once a TakeCheckpoint message gets in, we start by going over each TaskCompletionSource in the *CallCache* and set its result, signaling it to save its current sequence number and TaskId into the *TaskIdToSequenceNumber* cache. We will also pick the first **awaited** TaskCompletionSource to actually take the checkpoint. After each Task completes saving its context or taking a checkpoint, we create a new TaskCompletionSource for it to await.

*TaskIdToSequenceNumber* is used in the process of the state serialization, when serializing Task objects returned by async calls. This allows the following scenario:

```c#
var task1 = this._server.MAsync(buffer);
...
var result1 = await task1;
```

Notice that in this case, if a TakeCheckpoint message was received anywhere between the *async* call and the *await*, *task1* should be serialized as part of the call stack. Since Task is not a serializable type, we switch the Task object with the matching TaskCompletionSource (for the same sequence number) upon serialization, which is completed only if a result had been obtained prior to taking the checkpoint. 