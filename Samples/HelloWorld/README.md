
AMBROSIA Sample Application: Hello World
========================================

This sample shows two immortals communicating, a client and a server. You can build and run it locally to get a quick idea of how Ambrosia operates. The solution contains two alternate versions of the client (Client1 and Client2), only one of which is used at a time.  Client1 demonstrates basic communication, while Client2 demonstrates nondeterministic input using an impulse handler.

To run it yourself, refer to the version of the tutorial that matches
your tooling environment:

 * [HOWTO-WINDOWS.md](./HOWTO-WINDOWS.md): Build and run using
   Windows-native tooling, e.g. Visual Studio and `cmd.exe`.

 * [HOWTO-BASH.md](./HOWTO-BASH.md): Build and run on your local
   machine (Mac, Windows, Linux) using Bash scripts.

 * [HOWTO-DOCKER-K8S.md](./HOWTO-DOCKER-K8S.md): Build and run inside
   containers using Docker.

[HelloWorldExplained.md](./HelloWorldExplained.md) Explains the actual code in this Hello World sample.

[TimeTravel-Windows.md](./TimeTravel-Windows.md) Explains how to use Ambrosia's time travel debugging feature with HelloWorld.

[ActiveActive-Windows.md](./ActiveActive-Windows.md) Explains how to make the server in HelloWorld highly available by using active standbys.

