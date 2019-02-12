# Checkpointing the Immortal State

Every time a Take Checkpoint message is received by the Immortal, the Immortal saves and serializes its current state and sends it over to the ImmortalCoordinator, which logs the checkpoint for later recovery. Since the Immortal does not process any other messages while handling the Take Checkpoint message, its state is guaranteed not to change whilst saving its state.

The checkpointing differs between two scenarios - an Immortal which contains no async calls to other Immortals, and an Immortal which contains async calls to other Immortals. The difference stems from the fact that with async calls, the Immortal needs to keep track of their current state and continuation. For the latter scenario we are making use of a 3rd party open-source code to serialize a state of a C# Task (the original code repository can be found on GitHub here: https://github.com/ljw1004/blog/tree/master/Async/AsyncWorkflow).

## Checkpointing - w/o async calls

The simplest form of checkpointing is when the Immortal is only making Fork calls to other Immortals. In this scenario the Immortal does not care about the state of each call, only if it was made or not.

