Ambrosia: Highly Robust Distributed Programming Made Easy and Efficient
=======================================================================

[![Linux Build status](https://msrfranklin.visualstudio.com/Franklin/_apis/build/status/Ambrosia-CI-LinuxDocker-github?branchName=master)](https://msrfranklin.visualstudio.com/Franklin/_build/latest?definitionId=21)

[![Windows Build Status](https://msrfranklin.visualstudio.com/Franklin/_apis/build/status/Ambrosia-CI?branchName=master)](https://msrfranklin.visualstudio.com/Franklin/_build/latest?definitionId=7)

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

Quick Start
-----------

Build the PerformanceTestInterruptible example, using this one-line
powershell command:

    .\CmdLine-FreshBuild.ps1

Overview of directories
-----------------------

 * LocalAmbrosiaRuntime: the core reliable messaging and coordination engine.

 * InternalImmortals: example programs and services built on Ambrosia.

 * Tools: additional console tools for interacting with the Azure
           metadata that supports an Ambrosia service.

 * AmbrosiaTest: integration tests.

 * Ambrosia : Language binding for C#
 
Out of place things that should move:

 * Franklin_TestApp/
   - this contained the first kubernetes prototype, obsolete [2018.09.13]





