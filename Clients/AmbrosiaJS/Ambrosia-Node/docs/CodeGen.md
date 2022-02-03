<!-- Note: If using VS Code, install the "bierner.markdown-emoji" extension in order to see emoji's in the built-in MarkDown preview window. -->
## :wrench: Code Generation for an Ambrosia Node.js App/Service
----
Programming with any Ambriosia language binding involves code generation, which is at the heart of creating an Ambrosia app/service. Code generation is the process of taking code flagged as participating in the exposed API surface of an Ambrosia app/service (an Immortal) and creating both the public API and private infrastructure behind it. The generated code is then included in the app/service code, and the code of any Immortal(s) that use the app/service. This document describes code generation using the Node.js language binding (aka. "JS LB"), which uses TypeScript.

### Code Generation: A Quick Walkthrough

> _**Note:** The following example is contrived purely to demonstrate code generation; it is not representative of production quality code._

Suppose we want to create an Immortal that exposes a `computePI` post method in its API. Such a method might have the following implementation in a file called `PI.ts`:

````TypeScript
export namespace Published
{
    export namespace PI
    {
        /** 
         * Parameter type for the 'computePI' method.
         */
        type Digits = { count: number };

        /**
         * Returns pi computed to the specified number of digits.
         */
        function computePI(digits: Digits): number
        {
            const pi: number = Number.parseFloat(Math.PI.toFixed(digits?.count ?? 10));
            return (pi);
        }
    }
}
````

The first step in code generation is to mark these entities as being available ("published") in the Immortal's API.  This is done by adding the `export` keyword, and by annotating the entities with the `@ambrosia` JSDoc tag:

````TypeScript
/** 
 * Parameter type for the 'computePI' method.
 * @ambrosia publish=true
 */
export type Digits = { count: number };

/**
 * Returns pi computed to the specified number of digits.
 * @ambrosia publish=true
 */
export function computePI(digits: Digits): number
{
    const pi: number = Number.parseFloat(Math.PI.toFixed(digits?.count ?? 10));
    return (pi);
}
````
The `@ambrosia` tag can include one or more comma-separated attributes depending on the entity being published:

| TypeScript Entity | @ambrosia Attribute(s) |
| - | - |
| function, static method | `publish` (boolean), `version` (integer), `methodID`&#x00B9; (integer), `doRuntimeTypeChecking`&#x00B2; (boolean) |
| type alias, enum | `publish` (boolean) |

&#x00B9; If no `methodID` is specified the method will be considered to be a **[post](ImpulseExplained.md#post:-methods-(rpc-calls)-that-return-values)** method.<br/>
&#x00B2; See [Type Checking in the Ambrosia Node.js Language Binding](TypeChecking.md).

The second step is to create a small code-gen program (in TypeScript) to do code generation:

````TypeScript
import Ambrosia = require("ambrosia-node"); 
import Meta = Ambrosia.Meta;
import Utils = Ambrosia.Utils;

codeGen();

async function codeGen()
{
    try
    {
        await Ambrosia.initializeAsync(Ambrosia.LBInitMode.CodeGen);
        const fileGenOptions: Meta.FileGenOptions = { apiName: "PI", mergeType: Meta.FileMergeType.None };
        Meta.emitTypeScriptFileFromSource("./PI.ts", fileGenOptions);
    }
    catch (error: unknown)
    {
        Utils.tryLog(Utils.makeError(error));
    }
}
````

This program will generate 2 files: `PublisherFramework.g.ts` and `ConsumerInterface.g.ts`. The former should be included by the Immortal that implements the API, and the latter should be included by an Immortal that wishes to use the `computePI` method.  For simplicity, let's assume that our Immortal wants to call its own methods, so our Immortal is both the publisher _and_ the consumer of the API.

On the publisher side, `PublisherFramework.g.ts` would be used like this:

````TypeScript
import Ambrosia = require("ambrosia-node"); 
import Configuration = Ambrosia.Configuration;
import IC = Ambrosia.IC;
import Utils = Ambrosia.Utils;
import * as Framework from "./PublisherFramework.g"; // This is a generated file
import * as Self from "./ConsumerInterface.g"; // This a generated file

async function main()
{
    try
    {
        await Ambrosia.initializeAsync();

        const config: Configuration.AmbrosiaConfig = new Configuration.AmbrosiaConfig(Framework.messageDispatcher,
            Framework.checkpointProducer,
            Framework.checkpointConsumer,
            Self.postResultDispatcher);
        Framework.State._appState = IC.start(config, Framework.State.AppState);
    }
    catch (error: unknown)
    {
        Utils.tryLog(Utils.makeError(error));
    }
}
````
> **Note:** `ConsumerInterface.g` is also being imported here _only_ because we are making self-calls, so our Immortal needs to handle the results of our own post methods.

And on the consumer side, `ConsumerInterface.g.ts` would be used like this

````TypeScript
import Ambrosia = require("ambrosia-node"); 
import IC = Ambrosia.IC;
import * as Self from "./ConsumerInterface.g"; // This a generated file

export namespace AppEventHandlers
{
    export function onBecomingPrimary(): void
    {
        Self.setDestinationInstance(IC.instanceName());
        Self.Published.PI.computePI_Post(null, new Self.Published.PI.Digits(5));
    }
}
````
> **Note:** After writing the above "consumer side" code, `codeGen()` would need to be run again in order to hook up the `onBecomingPrimary()` event handler in `PublisherFramework.g.ts`.

Finally, to handle the result of the `computePI` method, we need to add code to the `postResultDispatcher` in `ConsumerInterface.g.ts`:

````TypeScript
...
switch (methodName)
{
    case "computePI":
        const computePI_Result: number = result;
        Utils.log(`pi = ${computePI_Result}`); // <-- Added
        break;
}
...
````
> **Note:** The two generated ( `.g.ts`) files should be placed under source control (ideally using Git) so that any changes you make to either of them are not lost when re-running `codeGen()`. On the whole, the changes you need to make will largely be confined to the `postResultDispatcher` in `ConsumerInterface.g.ts`.

When the Immortal instance is started (for the first time), it will call the `computePI` method and log the result to the trace output of the Immortal:
````
...
2021/08/26 19:39:09.142: Intercepted [Fork] RPC call for post method 'computePI_Result' [resultType: normal]
2021/08/26 19:39:09.143: pi = 3.14159
````

That's it. Although we've glossed over many of the details, these are the broad strokes of how to do code generation. While it may seem a little complicated to begin with, you should find that because it's an iterative/repetitious process you quickly gain familiarity with it. 

To get into more of the details of code-gen:
- Review the `ConsumerInterface.g.ts` and `PublisherFramework.g.ts` files. In particular, look for the "`// TODO:`" comments in `PublisherFramework.g.ts`, since these call out places where you can/should take action.
- Explore the IntelliSense (if using VS Code) for the `Meta.FileGenOptions` class, which is passed into the `Meta.emitTypeScriptFileFromSource()` call to control the behavior of code-gen.

### Code Generation: A Deeper Look

Code generation can be done in two ways: either using a source file as input (strongly recommended, and as shown in the **[Quick Walkthrough](#code-generation-a-quick-walkthrough)**), or by calling the publish API's (`publishType`, `publishMethod`, `publishPostMethod`) directly:

<div align="center">
  <img alt="Code generation diagram" src="images/CodeGen.png" width="720"/>
</div>

Automatically publishing types and methods from an annotated TypeScript source file using `emitTypeScriptFileFromSource()` provides 4 major benefits over explicitly hand-crafting `publishType()`, `publishPostMethod()`, and `publishMethod()` calls then calling `emitTypeScriptFile()`:

1. It's easier. Writing in free-form TypeScript is more familiar and faster than funneling code through the string parameters of the `publishX()` methods.
2. The developer gets rich design-time support from the TypeScript compiler and the editor (eg. VS Code). For example, comments can be inline with the input source and therefore contextual, which enables the developer to have IntelliSense for their published types and methods.
3. Because compiler support can now be leveraged, the types and methods can largely be verified before doing code-gen, speeding up the edit/compile/run cycle. Further, code-gen can leverage the TypeScript compiler to verify the generated .ts files (this second benefit also applies to the "manual" code-gen approach).
4. Because the majority of the developer-provided code is located separately from the generated `PublisherFramework.g.ts`, there is less time spent resolving merges conflicts.

&nbsp;
### Code Generation Restrictions

1. When using automatic (input source driven) code-gen, all published entities (functions / type aliases / enums) need to be in a single file. Namespaces can be used to help organize the code, although names must still be unique across namespaces.
2. Types to be published can only be expressed as type aliases, not interfaces or classes. This is because:
    - The nature/capability of a published type most closely matches TypeScript's Type Alias construct. 
    - Type Aliases and Interfaces are approximately equivalent in expressiveness (see **[here](https://www.typescriptlang.org/docs/handbook/advanced-types.html#type-aliases)**). For example, the intersection operator (`&`) in a Type Alias is the equivalent of the `extends` keyword in an Interface.
    - TypeScript does not provide a built-in way to rehydrate class instances from JSON.

    **Note:** The consumer-side file (`ConsumerInterface.g.ts`) actually generates a minimal "data-only" class for a published complex type to make it easier to create (by using constructor syntax) as a parameter for a published method call. You can see this being done in the generated code for the `Digits` type in the **[Quick Walkthrough](#code-generation-a-quick-walkthrough)**.
    
3. Type aliases cannot contain optional members (for example, `middleInitial?: string`), or contain generics (other than for built-in types, for example, `Map<string, number>`).
4. Enums cannot contain expressions or computed values, only assigned (or omitted) integer values.
5. Unlike in TypeScript, all type aliases must be declared before any function that references them (publishing from source occurs in the lexical order of the input file). Forward references between type aliases are allowed, subject to the prior restriction.

&nbsp;
### Comparison with Code Generation in the C# LB

Because C# and TypeScript are very different in both their available language facilities and in how they package their code, there are some significant differences in how the two LB's implement code generation:

| C# | TypeScript (Node.js LB) |
| - | - |
| Code-gen done by tool (**[AmbrosiaCS CodeGen](https://github.com/microsoft/AMBROSIA/blob/master/Clients/CSharp/AmbrosiaCS/CodeGeneration.md)**) | Code-gen done using API (part of the `ambrosia-node` package API) |
| Always requires input source code | Can be done either "manually" (without an input source file) or automatically (with an input source file) |
| C# **[attributes](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/attributes/)** used to annotate source | Source annotated with custom **[JSDoc](https://www.typescriptlang.org/docs/handbook/jsdoc-supported-types.html)** tag `@ambrosia` |
| Uses **[reflection](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/reflection)** | Uses TypeScript compiler API (**[AST](https://basarat.gitbook.io/typescript/overview/ast)** walking / dynamic compilation) |
| Tool uses **[T4](https://docs.microsoft.com/en-us/visualstudio/modeling/code-generation-and-t4-text-templates?view=vs-2019)** to template emitted code | Custom template implementation |
| Emits project/assembly to compile then reference in app | Emits source files (.ts) to be imported directly in app |
| **[DataContract](https://docs.microsoft.com/en-us/dotnet/framework/wcf/feature-details/using-data-contracts)** for managing app state | No tooling support; app state must be explicitly managed by the developer&#x00B9; |
| Annotated code can span multiple .cs files | Annotated code must be in a single .ts file, although namespaces can (and should) be used to help organize the code |
| No restrictions on code that can be annotated | Only type aliases, enums (that use integers), functions, and static methods can be annotated ("published") |

> &#x00B9; All app state <i>must</i> derive from the `Ambrosia.AmbrosiaAppState` class provided by the Node.js LB (which the LB uses to serialize internal LB state), otherwise `IC.start()` will throw an exception.

&nbsp;

---
<table align="left">
  <tr>
    <td>
      <img alt="Ambrosia logo" src="images/ambrosia_logo.png"/>
    </td>
    <td>
      <div>
        <a href="https://github.com/microsoft/AMBROSIA#ambrosia-robust-distributed-programming-made-easy-and-efficient">AMBROSIA</a>
      </div>
      <sub>An Application Platform for Virtual Resiliency</sub>
      <br/>
      <sub>from Microsoft Research</sub>
    </td>
  </tr>
</table>
