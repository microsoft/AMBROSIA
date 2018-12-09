Ambrosia: Highly Robust Distributed Programming Made Easy and Efficient
=======================================================================

 * Windows Build (net46/netcore) [![Windows Build Status](https://msrfranklin.visualstudio.com/Franklin/_apis/build/status/Ambrosia-CI-Win-Scripted?branchName=master)](https://msrfranklin.visualstudio.com/Franklin/_build/latest?definitionId=23)

 * Linux Build (netcore) [![Linux Build Status](https://msrfranklin.visualstudio.com/Franklin/_apis/build/status/Ambrosia-CI-Linux-Scripted?branchName=master)](https://msrfranklin.visualstudio.com/Franklin/_build/latest?definitionId=24)

 * Linux Docker Build: [![Linux Docker Build status](https://msrfranklin.visualstudio.com/Franklin/_apis/build/status/Ambrosia-CI-Linux-Docker?branchName=master)](https://msrfranklin.visualstudio.com/Franklin/_build/latest?definitionId=18) 


Ambrosia is a programming language independent approach for authoring
and deploying highly robust distributed applications. Ambrosia 
dramatically lowers development and deployment costs and time to
market by automatically providing recovery and high availability.

Today's datacenter oriented applications, which include most popular
services running in Azure today, are composed of highly complex,
distributed software stacks. For instance, they typically incorporate
Event Hub or Kafka to robustly journal input and interactions for
recoverability, log important information to stores like Azure blobs
for debuggability, and use extremely expensive mechanisms like
distributed transactions, and stateless functions with distributed
persistent back-ends, in order to ensure exactly once execution of
service code.

In contrast, Ambrosia automatically gives programmers recoverability,
high availability, debuggability, upgradability, and exactly once
execution, without requiring developers to weave together such complex
systems, or use overly expensive mechanisms. Check out the overview
deck linked to the left to learn more or email us.

Quick Start: Fetch a binary distribution
----------------------------------------

FINISHME - 


Quick Start: Build from Source
------------------------------

Build the Ambrosia Immortal coordinator and C# client code generator
with this Bash script:

    ./build_dotnetcore_bindist.sh

Given a .NET Core SDK, this will work on Windows, Mac OS, or Linux.
After that, you have an AMBROSIA binary distribution built inside the
`./bin` directory within your working copy.

Running a Sample
----------------

FINISHME - AmbrosiaDocs.md content will move here!!
