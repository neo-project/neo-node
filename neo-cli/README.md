## Compile

You will need Window or Linux. Use a virtual machine if you have a Mac.

Install [.NET Core](https://www.microsoft.com/net/download/core).

On Linux, install the LevelDB and SQLite3 dev packages. E.g. on Ubuntu:

```sh
sudo apt-get install libleveldb-dev sqlite3 libsqlite3-dev
```

On Windows, use the [Neo version of LevelDB](https://github.com/neo-project/leveldb).

Clone the neo-cli repository.

```sh
cd neo-cli
dotnet build
cd neo-cli # neo-cli/neo-cli
cp bin/Debug/netcoreapp1.0/neo-cli.dll .
```

## Run

```sh
dotnet neo-cli.dll
```

This will result in:
```
A fatal error was encountered. The library 'libhostpolicy.so' required to execute the application was not found in '/home/USER/neo-cli/neo-cli/'.
```
