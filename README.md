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

Currently, neo-cli and neo-gui are integrated into one repository. You can enter the corresponding folder and follow the instructions to run each node.

## Prerequisites

You will need Window, Linux or macOS. Ubuntu LTS 14, 16 and 18 are supported.

Install [.NET Core](https://www.microsoft.com/net/download/core).

On Linux, install the LevelDB and SQLite3 dev packages. E.g. on Ubuntu or Fedora:

```sh
sudo apt-get install libleveldb-dev sqlite3 libsqlite3-dev libunwind8-dev # Ubuntu
sudo dnf install leveldb-devel sqlite sqlite-devel libunwind-devel # Fedora
```

On macOS, install the LevelDB package. E.g. install via Homebrew:

```
brew install --ignore-dependencies --build-from-source leveldb
```

On Windows, use the [Neo version of LevelDB](https://github.com/neo-project/leveldb).

## Download pre-compiled binaries

See also [official docs](https://docs.neo.org/docs/en-us/node/introduction.html). Download and unzip the [latest release](https://github.com/neo-project/neo-node/releases).

On Linux, you can type the command:

```sh
./neo-cli
```

On Windows, you can just double click the exe to run the node.

## Compile from source

Clone the neo-node repository.

For neo-cli, you can type the following commands:

```sh
cd neo-node/neo-cli
dotnet restore
dotnet publish -c Release
```
Next, you should enter the working directory (i.e. /bin/Debug, /bin/Release) and paste the `libleveldb.dll` here. In addition, you need to create `Plugins` folder and put the `LevelDBStore` or `RocksDBStore` or other supported storage engine, as well as the configuration file, in the Plugins folder.

Assuming you are in the working directory:

```sh
dotnet neo-cli.dll 
```

For neo-gui, you just need to enter the `neo-node/neo-gui` folder and follow the above steps to run the node.

## Build into Docker

Clone the neo-node repository.

```sh
cd neo-node
docker build -t neo-cli .
docker run -p 10332:10332 -p 10333:10333 --name=neo-cli-mainnet neo-cli
```

After start the container successfully, use the following scripts to open neo-cli interactive window:

```sh
docker exec -it neo-cli-mainnet /bin/bash
screen -r node
```

## Logging

To enable logs in neo-cli, you need to add the ApplicationLogs plugin. Please check [here](https://github.com/neo-project/neo-modules.git) for more information.


## Bootstrapping the network.
In order to synchronize the network faster, please check [here](https://docs.neo.org/docs/en-us/node/syncblocks.html).


## Usage

For more information about these two nodes, you can refer to [documentation](https://docs.neo.org/docs/en-us/node/introduction.html) to try out more features. 

