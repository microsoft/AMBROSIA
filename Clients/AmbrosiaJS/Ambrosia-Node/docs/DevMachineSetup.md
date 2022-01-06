<!-- Note: If using VS Code, install the "bierner.markdown-emoji" extension in order to see emoji's in the built-in MarkDown preview window. -->
## :computer: Developer Machine Setup Guide
----
If you want to work on _developing_ the Node.js Language Binding (LB) itself, then the following setup steps are provided to help you get your developer workstation up and running.<br/>
If you want to simply _use_ the the Node.js LB to build a new Ambrosia application/service, then only the **bold** numbered steps apply (1b, 2, 3, 4, 9, 10, 11).<br/>
In either case, if you're already familiar with Node.js development using TypeScript, VS Code, and Git, then you likely already have your development environment setup and can skip the software installation steps.<br/>

> **Note:** Windows 10 is assumed to be the target OS in all steps of this guide.

1a&#41; If you want to be able to build/debug the Immortal Coordinator (IC), which is written in C#, you'll need to install Visual Studio 2019 (which **[requires](https://docs.microsoft.com/en-us/visualstudio/releases/2019/system-requirements)** Windows):

- Install **[Visual Studio 2019](https://visualstudio.microsoft.com/vs/)**.
- During the installation, select these workloads:
  - ASP&#46;NET and web development
  - Azure development
  - Node.js development
  - .Net desktop development
  - Desktop development with C++
  - .Net Core cross-platform development
- See **[here](https://github.com/Microsoft/AMBROSIA/tree/master/CONTRIBUTING)** for more information about building Ambrosia.

**1b&#41;** If you don't want to build/debug the IC, you can just install the pre-built Ambrosia binaries:<br/>
 - Create a folder C:\ambrosia-win.
 - Visit https://github.com/microsoft/AMBROSIA/releases.
 - Click on Ambrosia-win-x64.zip and extract it to C:\ambrosia-win.

**2&#41;** Install VS Code from https://code.visualstudio.com/download. Accept all the defaults.

**3&#41;** Install Node.js (which includes npm) from https://nodejs.org/en/download/. Accept all the defaults.
  > Note: We used Node version **[14.17.5](https://nodejs.org/download/release/v14.17.5/)** (npm version 6.14.14), which will go end-of-life on 2023-04-30 (see **[previous releases](https://nodejs.org/en/download/releases/)** list).<br/>
    Using this exact version of Node (and npm) is recommended.

**4&#41;** Install Git
  - Visit https://git-scm.com/download/win and select the "64-bit Git for Windows setup".
  - During setup, use the following values. If an option is presented that's not specified below, accept the default.
    - Choose the default editor used by Git &#x2192; VSCode
    - Adjusting the name of the initial branch in new repositories &#x2192; Let Git decide
    - Adjusting your PATH environment &#x2192; Git from the command line and also from 3rd-party software
    - Choosing the SSH executable &#x2192; Use bundled OpenSSH
    - Choosing HTTPS transport backend &#x2192; Use the OpenSSL library
    - Configuring the line endings conversions &#x2192; Checkout as-is, commit as-is
    - Configuring the terminal emulator to use with Git Bash &#x2192; Using Windows' default console window
    - Choose the default behavior of 'git pull' &#x2192; Default (fast-forward or merge)
    - Choose a credential helper &#x2192; Git Credential Manager Core
    - Configuring extra options &#x2192; Enable file system caching
    - Configure experimental options &#x2192; (None)

5&#41; Clone Ambrosia from GitHub (if you haven't created an **[account](https://github.com/join)** on GitHub, you should do this first)
- Visit https://github.com/microsoft/AMBROSIA.
- Click the green "Code” button, then click the "Copy" button to get the URL (https://github.com/microsoft/AMBROSIA.git).
- From the command line (opened AFTER installing Git), create a folder, eg. C:\src\Git, and CD to that folder. Then run this:<br/>
````git clone https://github.com/microsoft/AMBROSIA.git````
- This only takes a few seconds and will create the AMBROSIA folder.

6&#41; Setup your Git identity

- From the C:\src\Git\AMBROSIA folder, run these commands (the first two set the values in C:\Users\(YourUserName)\.gitconfig, whereas the second two set the value for the current repo [in .git/config]):<br/>
`git config --global user.email [YourGitHubEmailAddress]`<br/>
`git config --global user.name "[YourGitHubUserName]"`<br/>
`git config user.email [YourGitHubEmailAddress]`<br/>
`git config user.name "[YourGitHubUserName]"`<br/>

- Check the results with this command:<br/>
````git config --list --show-origin````

7&#41; Start VS Code and open the \AMBROSIA\Clients\AmbrosiaJS\Ambrosia-Node folder. The IDE will report errors in the source code.

- From the PowerShell console window in VS Code, ensure you are in the Ambrosia-Node folder, then run this command:<br/>
  `npm install --production=false`

- The errors should be gone, and you should now be able to build (Ctrl+Shift+B) – but it won't yet run.
- Further, running the command `npm list --depth=0` should yield this output (without any errors), although the TypeScript package version number may be higher:
    ````
    +-- @types/node@16.11.7
    +-- azure-storage@2.10.5
    +-- source-map-support@0.5.20
    `-- typescript@4.4.4    
    ````
8&#41; Customize VS Code (these make it easier to develop, but are not essential to building or running the Node.js LB):
- Install the 'GitLens &ndash; Git supercharged' extension
  - In the 'Quick Setup' uncheck 'Current Line Blame', 'Git Code Lens' and 'Status Bar Blame'.
  - From the Source Control tab:
    - Drag 'File History' to the Explorer icon sidebar.
    - Go to 'Commits', click '…' then 'Show Repositories View'; drag the 'Repositories' to the Explorer icon sidebar.
- Install the 'vscode-js-profile-flame' extension.
- Install the 'Markdown Emoji' extension (by Matt Bierner).
- From Explorer, click '…' and select 'Open Editors'.

**9&#41;** Create the `AZURE_STORAGE_CONN_STRING` user environment variable (you will need to create an **[Azure storage account](https://docs.microsoft.com/en-us/azure/storage/common/storage-account-create?tabs=azure-portal)** first):

- Visit https://portal.azure.com/.
- Click on the resource group you previously created, then click on the storage account you previously created.
- Under 'Security + networking', select Access keys.
- Click on the 'Show keys' button, and the click the 'Copy' icon next to the Primary 'Connection string'.
- In Windows, search for 'Edit the system environment variables' and add the copied connection string as `AZURE_STORAGE_CONN_STRING`.

**10&#41;** Create an `AMBROSIATOOLS` user environment variable that points to C:\ambrosia-win\x64\Release (created in step 1b). Afterwards, restart VS Code and any command prompts.
> If you used step 1a instead of 1b, then set `AMBROSIATOOLS` to [YourRepoPath]\AMBROSIA\ImmortalCoordinator\bin\x64\Release (which will exist only _after_ you do a 'Release' build of Ambrosia.sln). However, this will not be sufficient to run the Node.js LB, so you will need to set `"icBinFolder"` (see step 12).

**11&#41;** Install Azure Storage Explorer. This is optional, but is extremely useful for debugging instance registration issues.
- Install the tool from https://azure.microsoft.com/en-us/features/storage-explorer/#overview
- Connect the explorer to your storage account:
  - In the Explorer under 'Local and Attached', select  'Storage Accounts'.
  - Right-click and select 'Connect to Azure Storage...'.
  - Select 'Storage account or service'.
  - Choose 'Connection string (Key or SAS)' and click 'Next'.
  - Enter the `AZURE_STORAGE_CONN_STRING` value in the 'Connection string' box (the 'Display name' will then auto-populate).

12&#41; Run the Node.js LB (smoke test)
- In VS Code, open \Ambrosia-Node\ambrosiaConfig.json and set the `"icBinFolder"` setting to `""` and `"autoRegister"` to `true`. Also, edit the `"icLogFolder"` to point to suitable folder on your local machine (the folder will be created if it doesn't exist).
  > If you used step 1a instead of 1b, then set `"icBinFolder"` to `"[YourRepoPath]\AMBROSIA\ImmortalCoordinator\bin\x64\Release;[YourRepoPath]\AMBROSIA\Ambrosia\Ambrosia\bin\x64\Release"` (these folders will only exists after you do a 'Release' build of Ambrosia.sln).<br/>
  Note that The `"icBinFolder"` overrides the `AMBROSIATOOLS` environment variable, and can contain multiple paths (separated by a semicolon).
- You should now be able to build and run (F5 in VS Code) the Node.js LB, although – on the first run – you will be prompted to let the app through the Windows Firewall (the IC opens 3 ports).
  - If you're not using VS Code (or just want a scriptable build)...
    - Build the Node.js LB by running `"npx tsc -p .\tsconfig.json '--incremental false'"` from the \AmbrosiaJS\Ambrosia-Node folder.
    - Alternatively, you can run `"./build.ps1"` (also from the \AmbrosiaJS\Ambrosia-Node folder) which will build the LB as part of building the ambrosia-node npm package (for example, `ambrosia-node-2.0.0.tgz`).
    - You can then run the Node.js LB with `"node .\lib\Demo.js"` (which runs the built-in 'smoke test' app).

&nbsp;

---
<div>
<!-- PNG converted from SVG (from https://iconcloud.design/browse/Azure%20Icons/Networking/e8b50c8ac-de64dd68d) using Paint3D -->
<!-- Slighty convoluted to make it work in both VSCode and ADO -->
<div style="width: 70px; height: 70px; float: left; padding-right: 10px">

![Ambrosia logo](images/ambrosia_logo.png)

</div>
    <div style="font-size:20px; padding-top:5px">
        <a style="color:inherit; text-decoration: none" href="https://github.com/microsoft/AMBROSIA#ambrosia-robust-distributed-programming-made-easy-and-efficient">AMBROSIA</a>
    </div>
    <div style="font-size:10px; margin-top:-5px;">An Application Platform for Virtual Resiliency</div>
    <div style="font-size:10px; margin-top:-2px;">from Microsoft Research</div>
</div>