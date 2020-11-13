// Exports all [non-internal] Ambrosia modules (files) so that they can be imported as a single "namespace", eg:
// import Ambrosia = require("./Ambrosia");
// We use this technique because of the difficulty in building a module that spans multiple files.
// Note: This 2-step import/re-export approach is because there is no "export * as Foo from" syntax, which we need to create "sub-namespaces" (eg. Ambrosia.Messages)
import * as Configuration from "./Configuration";
export { Configuration };

import * as DataFormat from "./DataFormat";
export { DataFormat };

import * as IC from "./ICProcess";
export { IC };

import * as ICTest from "./ICTest";
export { ICTest };

import * as Messages from "./Messages";
export { Messages };

import * as Meta from "./Meta";
export { Meta };

import * as Streams from "./Streams";
export { Streams };

import * as StringEncoding from "./StringEncoding";
export { StringEncoding };

import * as Utils from "./Utils/Utils-Index";
export { Utils };

// Note: We don't create a "sub-namespace" for the AmbrosiaRoot module, because its members will simply be accessed via 'Ambrosia.xxx'
export * from "./AmbrosiaRoot";