Your first Ambrosia Application - Hello World
=======================================================================

Ambrosia applications consist of a collection of deployed, virtualized, "fat" objects, called instances, where the logic for these instances is defined by programmer authored Immortals. Specifically, each deployed Ambrosia Immortal is an instance with a public interface, whose methods may be invoked by other instances. Instances are fat in the sense that thay are expected to consume at least one core, which means that they may own many finer grained objects. As we'll see, Ambrosia instances are automatically recoverable, relocatable, time travel debuggable, upgadable, and can be made highly available at deployment time with no code changes. 

Server
-----------
In this application, we author a server Immortal, called Server, and 3 Client Immortals, each of which demonstrate different interesting features of the Ambrosia programming model. When we deploy, we will create one Server instance, paired with one of our Client instances. Server simply eats messages through a ReceiveMessage method, which returns the number of messages received by the Server instance so far. First, let's take a look at the code for Server. Here is the public declaration of the interface to Server, called IServer:

```
    public interface IServer
    {
        int ReceiveMessage(string Message);
    }
```
Note that this interface is declared in the IServer project in Hello World. Once this project is compiled, the Ambrosia code generation tool (AmbrosiaCS) may be run to generate all necessary base Immortal code for the Server implementation. In particular, this generated code will contain a base class for Server, as well as proxy classes for making method calls on any Server instance.

Next, let's look at the code for the Server Immortal implementation, found in the Server project:
```
        [DataContract]
        sealed class Server : Immortal<IServerProxy>, IServer
        {
            [DataMember]
            int _messagesReceived = 0;

            public Server()
            {
            }

            public async Task<int> ReceiveMessageAsync(string message)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n!! SERVER Received message from a client: " + message);
                Console.ResetColor();

                _messagesReceived++;
                return _messagesReceived;
            }

            protected override async Task<bool> OnFirstStart()
            {
                return true;
            }
        }
```
First, note the use of IServer, which is in the project generated from the IServer interface project, which specifies the public interfaces that must be implemented by the Immortal. Note that the generated version of IServer uses async methods, which requires an implementation that is async. While this is not important for Server, we will see how this is exploited in Client3. 

Also, observe that Server inherits Immortal<IServerProxy>. This base class and template parameter indicate that we are implementing an Immortal, whose self reference proxy is of type IServerProxy. IServerProxy is also in the generated project. While the self reference proxy isn't important for Server, we will see how this can be important for other Immortals (e.g. Client2). 

Note the lack of any logging code, reconnection or retry code, or explicit instructions to push or pull state to storage. Nevertheless, instances of Server will never lose data or messages, can be migrated transparently from one machine/vm/pod to another, can be deployed in a highly available fashion through active/active replication, and can be debugged by going back in time and rolling forward, called time travel debugging. There are only two requirements Ambrosia makes of C# programmers:

* Given the same input method calls in the same order, the Immortal must arrive at an equivalent state, and generate the same outgoing instance method calls in the same order.
* The Immortal author must provide a way to serialize Immortal instances, through DataContract, so that deserialization will create an Immortal with equivalent state.

The first requirement is trivially accomplished through Ambrosia's threading and dispatch model. In Ambrosia, all method calls are executed serially, unless an async call is made, as in Client3. Since there are no outgoing method calls in Server, all ReceiveMessageAsync calls are executed one by one to completion, guaranteeing requirement the first requirement.

The second requirement is met by labeling the class as DataContract, and labeling the _messagesReceived member as DataMember. This ensures that _messagesReceived is protected, and in the case of failure, will be reconstituted to the correct value when recovery occurs. Note that not all members must be labled in this way, as they may be computed from other members, or may not be important to serialize (e.g. rendering state).

Also note the existence of the method OnFirstStart, which is an initialization method called once when an instance is created. Like other methods, OnFirstStart is executed serially with respect to other method calls. OnFirstStart returns a bool because of limitations in our abilitiy to serialize tasks in C#. As a result, OnFirstStart, which could make an async call to another instance, must have a return value, although its not used. We will discuss this issue further in our section on Client3.

Note that the Immortal class defined above must be instantiated in a running C# program. For this reason, we've embedded this Immortal in a console program, with the following Main:
```
        static void Main(string[] args)
        {
            int receivePort = 2001;
            int sendPort = 2000;
            string serviceName = "server";

            if (args.Length == 1)
            {
                serviceName = args[0];
            }

            using (var c = AmbrosiaFactory.Deploy<IServer>(serviceName, new Server(), receivePort, sendPort))
            {
                Thread.Sleep(14 * 24 * 3600 * 1000);
            }
        }
```
Each time the Immortal is recovered or relocated, or an active replica created, the above Main will execute. The only really relevant line from Ambrosia's point of view, is the using statement, which stands up the Ambrosia instance inside the process which is running Main. Note the Thread.Sleep, which puts the main thread to sleep for 14 days. This is just a simple convenience to keep the program from exiting early. In practice, something like an AsyncQueue can be used to put the main thread to sleep until the Immortal decides to complete and end. Client1 shows how this can be done. In this sense, Ambrosia can be used for both "jobs" and "services", where jobs logically terminate and services logically run forever.

Note that currently, each process can only have one Immortal, due to global data structures which aren't currenly sharable. This limitation could, however, change eventually.

Another thing to note in main, is the existence of receivePort and sendPort. The two variables are passed into the Deploy method, and are the two ports which this process uses to communicate with its local ImmortalCoordinator. Specifically, each instance consists of two running processes which run in the same VM/machine/container, and which fail and recover together. When logically creating instances with the Ambrosia RegisterInstance command, these ports are specified in reverse, which is to say that the ImmortalCoordinator's receive port is the application process's send port, and visa-versa.

Finally, obseerve serviceName in the Deploy call, which is the name of this particular Ambrosia instance that is being initialized or recovered. This name is stored in an Azure Table which contains a directory of Ambrosia instances, and the logical connections between instances. Note that a single instance can have many replicas, all with the same service name.

Client1 - The Basics
-----------
Client1 is a simple example of an Amborsia job, in the sense that it is a distributed component which completes. In particular, it sends 3 messages to Server, reading a line from the user after the first message is sent. Reading this line gives us an opportunity to break Client1 and restart it, initiating recovery. Even though the job sends a message and is restarted, only 3 messages will arrive at Server.

In this case, Client1 has no public methods, so the interface is empty:
```
    public interface IClient1
    {
    }
```

Nevertheless, codegen is needed, because each Immortal runs a codegen step which includes its own interface, and all the interfaces of all the other Immortals it will make calls on. In this case, that includes IServer. We are now ready to look at the code for the Client1 Immortal:

```
    [DataContract]
    class Client1 : Immortal<IClient1Proxy>, IClient1
    {
        [DataMember]
        private string _serverName;

        [DataMember]
        private IServerProxy _server;

        public Client1(string serverName)
        {
            _serverName = serverName;
        }

        protected override async Task<bool> OnFirstStart()
        {
            _server = GetProxy<IServerProxy>(_serverName);


            _server.ReceiveMessageFork("\n!! Client: Hello World 1!");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n!! Client: Sent message 1.");
            Console.WriteLine("\n!! Client: Press enter to continue (will send 2&3)");
            Console.ResetColor();

            Console.ReadLine();
            _server.ReceiveMessageFork("\n!! Client: Hello World 2!");
            _server.ReceiveMessageFork("\n!! Client: Hello World 3!");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n!! Client: Press enter to shutdown.");

            Console.ReadLine();
            Program.finishedTokenQ.Enqueue(0);
            return true;
        }
    }
```
In addition to the aspects already familiar from Server, note that our constructor is no longer empty. The construstor is used to pass information from the hosting program to the Immortal instance, prior to initialization or recovery. In this case, that includes the name of the Server instance, called "server" by default. This is passed into the constructor because we will need to establish a connection to server before making method calls on it.

As a consequence, the first line of OnFirstStart makes the connection to the specified Server instance using GetProxy. The return value of this call is an object which contains all public method calls on the specified instance.

The next line makes such a call to ReceiveMessage. Each call has two forms: the fork form is a form of message passing, where the call is made asynchronously, is not awaitable, and whose return value is ignored. The second from is async, which is awaitable (see Client3). The fork version of the call is by far the most performant and is generally encouraged. The async version has the advantage of being more flexible in its use, but is considered experimental due to important current limitations, which will hopefully be relaxed over time. In this case, we simply send the ReceiveMessage call and continue, immediately writing a couple lines and waiting on user input.

This is a good time to break the execution of Client1, Server, or both. If either or both is restarted, they will correctly recover to the state prior to breaking.

After pressing Enter, the program continues, sending two more messages. The last action of OnFirstStart is to enqueue a token into a global AsyncQueue called finishedTokenQ, which up receipt, will exit the program (see below):

```
        public static AsyncQueue<int> finishedTokenQ;

        static void Main(string[] args)
        {
            finishedTokenQ = new AsyncQueue<int>();

            int receivePort = 1001;
            int sendPort = 1000;
            string clientInstanceName = "client";
            string serverInstanceName = "server";

            if (args.Length >= 1)
            {
                clientInstanceName = args[0];
            }

            if (args.Length == 2)
            {
                serverInstanceName = args[1];
            }

            using (var c = AmbrosiaFactory.Deploy<IClient1>(clientInstanceName, new Client1(serverInstanceName), receivePort, sendPort))
            {
                finishedTokenQ.DequeueAsync().Wait();
            }
        }
```
Also, note that the server instance name, which is "server" by default, is passed into the constructor for Client1 in the Deploy call.

Client2 - Handling Non-Determinism
-----------
Under Construction

Client3 - Async calls (Experimental)
-----------
Under Construction
