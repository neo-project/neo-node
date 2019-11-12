<p align="center">
  <a href="https://neo.org/">
      <img
      src="https://neo3.azureedge.net/images/logo%20files-dark.svg"
      width="250px" alt="neo-logo">
  </a>
</p>

<h3 align="center">Neo Blockchain</h3>

<p align="center">
   A modern distributed network for the Smart Economy.
  <br>
  <a href="https://docs.neo.org/docs/en-us/index.html"><strong>Documentation »</strong></a>
  <br>
  <br>
  <a href="https://github.com/neo-project/neo">Neo<</a>
  ·
  <a href="https://github.com/neo-project/neo-vm">Neo VM</a>
  ·
  <a href="https://github.com/neo-project/neo-plugins">Neo Plugins</a>
  ·
  <a href="https://github.com/neo-project/neo-devpack-dotnet">Neo DevPack</a>
  ·
  <a href="https://github.com/neo-project/neo-cli"><strong>Neo CLI</strong></a>
</p>
<p align="center">
  <a href="https://twitter.com/neo_blockchain">
      <img
      src=".github/images/twitter-logo.png"
      width="25px">
  </a>
  &nbsp;
  <a href="https://medium.com/neo-smart-economy">
      <img
      src=".github/images/medium-logo.png"
      width="23px">
  </a>
  &nbsp;
  <a href="https://neonewstoday.com">
      <img
      src=".github/images/nnt-logo.jpg"
      width="23px">
  </a>
  &nbsp;  
  <a href="https://t.me/NEO_EN">
      <img
      src=".github/images/telegram-logo.png"
      width="24px" >
  </a>
  &nbsp;
  <a href="https://www.reddit.com/r/NEO/">
      <img
      src=".github/images/reddit-logo.png"
      width="24px">
  </a>
  &nbsp;
  <a href="https://discord.io/neo">
      <img
      src=".github/images/discord-logo.png"
      width="25px">
  </a>
  &nbsp;
  <a href="https://www.youtube.com/channel/UCl1AwEDN0w5lTmfJEMsY5Vw/videos">
      <img
      src=".github/images/youtube-logo.png"
      width="32px">
  </a>
  &nbsp;
  <!--How to get a link? -->
  <a href="https://neo.org/">
      <img
      src=".github/images/we-chat-logo.png"
      width="25px">
  </a>
  &nbsp;
  <a href="https://weibo.com/neosmarteconomy">
      <img
      src=".github/images/weibo-logo.png"
      width="28px">
  </a>
</p>


## Table of Contents
1. [Overview](#overview)
1. [Features](#features)
2. [Quickstart](#quick-start)
    1. [Using for smart-contract development](#building-a-smart-contract)
    1. [Using neo library](#using-neo-library)
    3. [Using neo-cli releases](#using-neo-cli-releases)
4. [Status](#status)
5. [Reference implementations](#reference-implementations)
6. [Opening an issue](#opening-a-new-issue)  
7. [Bounty program](#bounty-program)
8. [How to contribute](#how-to-contribute)

## Overview
Neo-cli is a command line application that uses the [neo library](https://github.com/neo-project/neo).
You can use most of neo features using neo-cli.
Neo-cli is compatible with .NET Core 3.0.


*Note: This is Neo 3 branch, currently under development. For the current stable version, please [click here]()*

## Features
These are a few features Neo has:

- **[dBFT2.0](https://medium.com/neo-smart-economy/neos-dbft-2-0-single-block-finality-with-improved-availability-6a4aca7bd1c4)**
  - Single block finality consensus algorithm.
- **[Smart Contracts using C#](https://github.com/neo-ngd/NEO3-Development-Guide/tree/master/en/SmartContract)**
  - Build smart-contracts using C# sintax;
  - Python, Typescript and Go Smart Contracts provided by community projects.
- **[Unity support](https://github.com/neo-ngd/NEO3-Development-Guide/tree/master/en/SmartContract)**
  - Neo can be used to create your game in the blockchain.
- **[Neo Blockchain Toolkit for .NET]()**
  - Developer tools, supporting easy smart-contract development with debugging support using Visual Studio Code.
- **[Plugin system]()**
  - Used to extend Neo functinalities, the plugin system allow developers to easily add new features to their nodes.
- **[Native contracts](https://medium.com/neo-smart-economy/native-contracts-in-neo-3-0-e786100abf6e)**
  - Contracts running C# code.
- **[Smart Contract internet access](https://medium.com/neo-smart-economy/on-the-importance-of-oracles-neo-3-0-and-dbft-17c37ee35f32)**
  - Internet acess during a transaction.
- **[Voting Mechanism](https://medium.com/neo-smart-economy/how-to-become-a-consensus-node-27e5317722e6)**
  - Decentralizing control over the network by allowing NEO holders to vote for consensus nodes.
- **[Distributed file-system]()**
  - NeoFS is a scalable, decentralized object storage network integrated with NEO contracts to provide trustless data storage facilities.
- **[Digital identity]()**
  - Using trust, privacy and game theory models. (WIP)



## Quick Start

#### Run using Docker

1. Clone the neo-cli repository.
2. Replace `protocol.json` and `config.json` with the content from `protocol.testnet.json` and `config.testnet.json`. 
3. Build the docker image:
    ```sh
    cd neo-cli
    docker build -t neo-cli .
    docker run -p 20332:20332 -p 20333:20333 --name=neo-cli-testnet neo-cli
    ```

3. Run the container and access neo-cli:

    ```sh
    docker exec -it neo-cli-testnet /bin/bash
    screen -r node
    ```

#### Using neo-cli releases
Neo-cli is a full node with wallet capabilities. It also supports RPC endpoints allowing it to be managed remotely.  

1. Download neo-cli from the release page
2. Run neo-cli executable file
    1. (Optional) Start it with `--rpc` to enable RPC (HTTP endpoints)
4. Start it with `-t` to use testnet configuration  
    ```bash
    dotnet neo-cli.dll -t --rpc
    ```
5. (Optional) If you can't run it using the command line, replace the `protocol.json` file with the `protocol.testnet.json` content to access the testnet.


5. Use `help` to see the command list.

#### Logging

To enable logs in neo-cli, you need to add the ApplicationLogs plugin. Please check [here](https://github.com/neo-project/neo-plugins) for more information.


#### Bootstrapping the network.
In order to synchronize the network faster, please check [here](http://docs.neo.org/en-us/network/syncblocks.html).

## Status
<p>
  <a href="https://travis-ci.org/neo-project/neo">
    <img src="https://travis-ci.org/neo-project/neo.svg?branch=master" alt="Current TravisCI build status.">
  </a>
  <a href="https://github.com/neo-project/neo/releases">
    <img src="https://badge.fury.io/gh/neo-project%2Fneo.svg" alt="Current neo version.">
  </a>
  <a href="https://codecov.io/github/neo-project/neo/branch/master/graph/badge.svg">
    <img src="https://codecov.io/github/neo-project/neo/branch/master/graph/badge.svg" alt="Current Coverage Status." />
  </a>
  <a href="https://github.com/neo-project/neo/blob/master/LICENSE">
    <img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License.">
  </a>
</p>


#### Reference implementations
Code references are provided for all platform building blocks. Tha includes the base library, the VM, a command line application and the compiler. Plugins are also included to easily extend Neo functinalities.

* [neo:](https://github.com/neo-project/neo/tree/) Neo core library, contains base classes, including ledger, p2p and IO modules.
* [neo-vm:](https://github.com/neo-project/neo-vm/) Neo Virtual Machine is a decoupled VM that Neo uses to execute its scripts. It also uses the `InteropService` layer to extend its functionalities.
* [**neo-cli:**](https://github.com/neo-project/neo-cli/) Neo Command Line Interface is an executable that allows you to run a Neo node using the command line. 
* [neo-plugins:](https://github.com/neo-project/neo-plugins/) Neo plugin system is the default way to extend neo features. If a feature is not mandatory for Neo functionality, it will probably be implemented as a Plugin.
* [neo-devpack-dotnet:](https://github.com/neo-project/neo-devpack-dotnet/) These are the official tools used to convert a C# smart-contract into a *neo executable file*.

#### Opening a new issue
Please feel free to create new issues in our repository. We encourage you to use one of our issue templates when creating a new issue.  

- [Feature request](https://github.com/neo-project/neo/issues/new?assignees=&labels=&template=bug_report.md&title=)
- [Bug report](https://github.com/neo-project/neo/issues/new?assignees=&labels=&template=bug_report.md&title=)
- [Questions](https://github.com/neo-project/neo/issues/new?assignees=&labels=question&template=questions.md&title=)

If you found a security issue, please refer to our [security policy](https://github.com/neo-project/neo/security/policy).

#### Bounty program
You can be rewarded by finding security issues. Please refer to our [bounty program page](https://neo.org/bounty) for more information.

#### How to contribute
Please read our [contribution guide](.github/CONTRIBUTING.md).  
The best way to start contributing is by testing open PRs.

#### License
The NEO project is licensed under the [MIT license](LICENSE).

