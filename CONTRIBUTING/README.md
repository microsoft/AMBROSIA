

CONTRIBUTING GUIDE
==================

For developers interested in adding to AMBROSIA, or developing new
[language-level or RPC-framework bindings to AMBROSIA](#new-client-bindings),
this document provides a few pointers.

We invite developers wishing to build on or contribute to AMBROSIA to join our [gitter community](https://gitter.im/AMBROSIA-resilient-systems/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link).

Overview of repository
----------------------

AMBROSIA is implemented in C# and built with Visual Studio or dotnet
CLI tooling. Within the top level of this source repository, you will
find.

(1) Core libraries and tools:

 * `./Ambrosia`: the core reliable messaging and runtime coordination engine.

 * `./ImmortalCoordinator`: the wrapper program around the core library that
   must be run as a daemon alongside each AMBROSIA application process.

 * `./DevTools`: additional console tools for interacting with the
   Azure metadata that supports an Ambrosia service.

 * `./AKS-scripts`: scripts to get a user started with AMBROSIA on
   Kubernetes on Azure.

 * `./Scripts`: scripts used when running automated tests (CI) as well
   as the runAmbrosiaService.sh script which provides an example means
   of executing an app+coordinator.

(2) Client libraries:

 * `./Clients`: these provide idiomatic bindings into different
   programming languages.

(3) Sample programs and tests:

 * `./Samples`: starting point examples for AMBROSIA users.
 
 * `./InternalImmortals`: internal test AMBROSIA programs, demos, and
   benchmarks.

 * `./AmbrosiaTest`: testing code


New Client Bindings
===================

AMBROSIA is designed to keep its runtime components in a separate
process (ImmortalCoordinator) than the running application process.
The coordinator and the application communicate over a pair of TCP
connections.

This separation makes the runtime component of AMBROSIA completely
language-agnostic.  All that is needed is for the application
processes to speak the low-level messaging protocol with the
coordinator.

For a new language or RPC framewrok, there are two ways to accomplish
this: (1) do the work yourself to implement the wire protocol, (2)
wrap the provided standalone native code library (which is small with
zero dependencies), to create a higher-level language binding.


Implement the low-level wire protocol
-------------------------------------

Refer to
[AMBROSIA_client_network_protocol.md](AMBROSIA_client_network_protocol.md)
for details on the specification applications must meet to communicate
with ImmortalCoordinator at runtime over TCP sockets.


Wrap the Native Client
----------------------

`Clients/C` contains a small library that handles the wire protocol.
That is it deals with decoding headers, variable width integer
encodings, and so on.  It provides a primitive messaging abstraction
for sending payloads of bytes with method IDs attached, but nothing more.

This native code client library is written in vanilla C code, free of
runtime dependencies.  Thus, it can be wrapped in any high-level
language that supports C calling conventions in its foreign function
interface.
