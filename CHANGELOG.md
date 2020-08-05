# Changelog
All notable changes to this project will be documented in this file.

## [3.0.0.preview2] - [3.0.0.preview3]
### Added
- ([#564](https://github.com/neo-project/neo-node/pull/564)) Add StackItem ToJson
- ([#575](https://github.com/neo-project/neo-node/pull/575)) Add NEP5 commands
- ([#568](https://github.com/neo-project/neo-node/pull/568)) Add vote commands
- ([#607](https://github.com/neo-project/neo-node/pull/607)) Add 'nativecontract' command
- ([#608](https://github.com/neo-project/neo-node/pull/608)) Ensure json extension in wallet
- ([#599](https://github.com/neo-project/neo-node/pull/599)) Add plugins description field

### Changed
- ([#567](https://github.com/neo-project/neo-node/pull/567)) Add plugins description field
- ([#536](https://github.com/neo-project/neo-node/pull/536)) Refactor node commands
- ([#585](https://github.com/neo-project/neo-node/pull/585)) Show address in list key command
- ([#582](https://github.com/neo-project/neo-node/pull/582)) Move SystemLog plugin into neo-cli as a native logger with on/off functionalities
- ([#584](https://github.com/neo-project/neo-node/pull/584)) Fill default settings
- ([#581](https://github.com/neo-project/neo-node/pull/581)) Parse vote commands' result
- ([#579](https://github.com/neo-project/neo-node/pull/579)) Update cosigner
- ([#578](https://github.com/neo-project/neo-node/pull/578)) Backup Wallet on change password
- ([#566](https://github.com/neo-project/neo-node/pull/566)) Show ScriptHash in `list address`
- ([#577](https://github.com/neo-project/neo-node/pull/577)) Remove log logic
- ([#604](https://github.com/neo-project/neo-node/pull/604)) Add description and uninstall restriction for “SystemLog”
- ([#602](https://github.com/neo-project/neo-node/pull/602)) Remove StackItem.ToParameter()
- ([#593](https://github.com/neo-project/neo-node/pull/593)) Add fields to protocol.json
- ([#621](https://github.com/neo-project/neo-node/pull/621)) Show invocation error
- ([#622](https://github.com/neo-project/neo-node/pull/622)) Apply signers
- ([#625](https://github.com/neo-project/neo-node/pull/625)) Update protocol.json
- ([#626](https://github.com/neo-project/neo-node/pull/626)) Workflows: use checkout action v2
- ([#630](https://github.com/neo-project/neo-node/pull/630)) Get innerException message Recursively
- ([#633](https://github.com/neo-project/neo-node/pull/633)) Included optional "from" in send and transfer commands
- ([#634](https://github.com/neo-project/neo-node/pull/634)) Improve Show pool command

### Fixed
- ([#610](https://github.com/neo-project/neo-node/pull/610)) Fix engine.ResultStack.Pop()
- ([#594](https://github.com/neo-project/neo-node/pull/594)) Fix relay tx
- ([#613](https://github.com/neo-project/neo-node/pull/613)) Fix invoke command