# Changelog
All notable changes to this project will be documented in this file.

## [Unreleased]

## [2.7.6.1] - 2018-07-09
### Fixed
- Fix a bug that crashes when the non-consensus node runs the "Start consensus" command.
- Fix a bug that do not load plugins when the node is started.

## [2.7.6] - 2018-06-19
### Added
- New CLI command: `import multisigaddress`.
- New CLI commands: `sign` and `relay`.
- New RPC command: `getvalidators`.
- New smart contract APIs: `Neo.Enumerator.*`.
- New smart contract API: `System.Blockchain.GetTransactionHeight`.
- New smart contract API: `System.Storage.GetReadOnlyContext` and `Neo.StorageContext.AsReadOnly`.

### Changed
- Support for NeoContract Standary Namespace.
- Improved Plugins System: filter transactions in plugin.
- Improve the speed of creating addresses.

## [2.7.5] - 2018-05-18
### Added
- Importing/exporting blocks with sharding.
- Daemonizing the neo process.
- Support for Neo Plugins System.
- New smart contract API: `Neo.Contract.IsPayable`.

### Changed
- Optimize RPC command `getbalance` for NEP-5.
- Optimize config.json
- Improve the performance of p2p network.
- Improve the performance of block synchronization.

### Fixed
- Prevents blocking when the second instance is started.

## [2.7.4] - 2018-03-29
### Added
- New smart contract feature: Maps.

### Changed
- Optimize protocol.json

### Fixed
- Fix the issue of `Neo.Storage.Find`.(smart contract)
- Record application logs when importing blocks.

## [2.7.3] - 2018-03-14
### Added
- New CLI command: `broadcast`.
- GzipCompression over RPC.
- New smart contract APIs: `Neo.Iterator.*`, `Neo.Storage.Find`.
- New smart contract APIs: `Neo.Runtime.Serialize`, `Neo.Runtime.Deserialize`.
- New smart contract API: `Neo.TransactionInvocation.GetScript`.

### Changed
- Improve the performance of importing blocks.
- Improve the performance of p2p network.
- Optimize CLI commands: `show node`, `show pool`.

### Fixed
- Fix crash on exiting.

## [2.7.1] - 2018-01-31
### Added
- Allow user to create db3 wallet.

## [2.7.0] - 2018-01-26
### Added
- New RPC command: `listaddress`.
- New RPC command: `getapplicationlog`.
- New opcode `REMOVE`.(smart contract)

### Removed
- Remove option `--record-notifications`.

## [2.6.0] - 2018-01-15
### Added
- New RPC command: `sendfrom`.

### Changed
- Improve the performance of rebuilding wallet index.
- Prevent the creation of wallet files with blank password.
- Add `time` to the outputs of `Blockchain_Notify`.

### Fixed
- Save wallet file when creating address by calling RPC command `getnewaddress`.
- Fix the issue of RPC commands `invoke*`.

### Removed
- Remove `Neo.Account.SetVotes` and `Neo.Validator.Register`.(smart contract)

## [2.5.2] - 2017-12-14
### Added
- New smart contract API: `Neo.Runtime.GetTime`.
- New opcodes `APPEND`, `REVERSE`.(smart contract)

### Changed
- Add fields `tx` and `script` to RPC commands `invoke*`.
- Improve the performance of p2p network.
- Optimize protocol.json

### Fixed
- Fix the network issue when restart the client.

## [2.5.0] - 2017-12-12
### Added
- Support for NEP-6 wallets.
- Add startup parameter: `--nopeers`.

## [2.4.1] - 2017-11-24
### Added
- New smart contract feature: Dynamic Invocation.(NEP-4)
- New smart contract APIs: `Neo.Transaction.GetUnspentCoins`, `Neo.Header.GetIndex`.

### Changed
- Optimize CLI command: `show state`.
- Optimize config.json
- Improve the performance of p2p network.

## [2.3.5] - 2017-10-27
### Changed
- Optimize RPC commands `sendtoaddress` and `sendmany` for NEP-5 transfer.
- Optimize CLI command `send` for NEP-5 transfer.

## [2.3.4] - 2017-10-12
### Added
- Add startup parameter: `--record-notifications`.
- New RPC commands: `invoke`, `invokefunction`, `invokescript`.
- New RPC command: `getversion`.
- Console colors.

### Fixed
- Improve stability.

## [2.3.2] - 2017-09-06
### Added
- New CLI command: `send all`.
- New opcodes `THROW`, `THROWIFNOT`.(smart contract)

### Changed
- Optimize opcode `CHECKMULTISIG`.

### Fixed
- Fix the issue of `Neo.Runtime.CheckWitness`.(smart contract)

## [2.1.0] - 2017-08-15
### Added
- New RPC command: `sendmany`.
- New CLI command: `show utxo`.
- New smart contract feature: Triggers.

## [2.0.2] - 2017-08-14
### Changed
- Improve the performance of p2p network.

## [2.0.1] - 2017-07-20
### Added
- New RPC commands: `getpeers`, `getblocksysfee`.
- New RPC commands: `getaccountstate`, `getassetstate`, `getcontractstate`, `getstorage`.
- Add default config files for MAINNET and TESTNET.

### Changed
- Improve the performance of p2p network.

## [2.0.0] - 2017-07-13
### Changed
- Rebrand from AntShares to NEO.
