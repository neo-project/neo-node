<p align="center">
<a href="https://neo.org/">
      <img
      src="https://neo3.azureedge.net/images/logo%20files-dark.svg"
      width="250px" alt="neo-logo">
  </a>
</p>

<p align="center">      
  <a href="https://travis-ci.org/neo-project/neo-node">
    <img src="https://travis-ci.org/neo-project/neo-node.svg?branch=master" alt="Current TravisCI build status.">
  </a>
  <a href="https://github.com/neo-project/neo-node/blob/master/LICENSE">
    <img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License">
  </a>
  <a href="https://github.com/neo-project/neo-node/releases">
    <img src="https://badge.fury.io/gh/neo-project%2Fneo-node.svg" alt="Current neo-cli version.">
  </a>    
</p>

## Prerequisites

You will need Window, Linux or macOS. Ubuntu 14 and 16 are supported. Ubuntu 17 is not supported.

Install [.NET Core](https://www.microsoft.com/net/download/core).

On Linux, install the LevelDB and SQLite3 dev packages. E.g. on Ubuntu:

```sh
sudo apt-get install libleveldb-dev sqlite3 libsqlite3-dev libunwind8-dev
```

On macOS, install the LevelDB package. E.g. install via Homebrew:

```
brew install --ignore-dependencies --build-from-source leveldb
```

On Windows, use the [Neo version of LevelDB](https://github.com/neo-project/leveldb).

## Download pre-compiled binaries

See also [official docs](https://docs.neo.org/docs/en-us/node/introduction.html). Download and unzip [latest release](https://github.com/neo-project/neo-cli/releases).

```sh
dotnet neo-cli.dll
```

## Compile from source

Clone the neo-cli repository.

```sh
cd neo-cli
dotnet restore
dotnet publish -c Release
```
In order to run, you need .NET Core. Download the SDK [binary](https://www.microsoft.com/net/download/linux).

Assuming you extracted .NET in the parent folder:

```sh
../dotnet bin/Release/netcoreapp1.0/neo-cli.dll .
```

## Logging
To get event logs in neo-cli, you need to add the ApplicationLogs plugin. Please check [here](https://github.com/neo-project/neo-modules) for more information.


## Bootstrapping the network.
In order to synchronize the network faster, please check [here](https://docs.neo.org/docs/en-us/node/syncblocks.html).


## Usage
See [documentation](https://docs.neo.org/docs/en-us/node/introduction.html). E.g. try `show state` or `create wallet wallet.json`.
