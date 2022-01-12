## Folder Descriptions

### Ambrosia-Node

Contains the source code for the Node.js language binding, including [documentation](Ambrosia-Node/docs/Introduction.md).

### PTI-Node &#x00B9;

Contains the source code for the Performance Test Interruptible app for the Node.js language binding, including [documentation](PTI-Node/ReadMe.md).

### TestApp-Node &#x00B9;

Contains the source code for a simple example app that uses the Node.js language binding. 

> &#x00B9; If you have not built the `ambrosia-node` npm package locally (by running `build.ps1` in the Ambrosia-Node folder), then running `npm install --production=false` from this folder will fail.<br/>
Instead, you can install the pre-built package by running `npm install https://github.com/microsoft/AMBROSIA/releases/download/v2.0.0.0/ambrosia-node-2.0.0.tgz` in the app folder (eg. TestApp-Node).