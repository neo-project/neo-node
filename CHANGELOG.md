# Changelog
All notable changes to this project will be documented in this file.

## [3.0.3]

### Changed
- ([#812](https://github.com/neo-project/neo-node/pull/812/)) User friendly cli console write system
- ([#815](https://github.com/neo-project/neo-node/pull/815/)) Enable private key import in initial wallet creation
- ([#823](https://github.com/neo-project/neo-node/pull/823/)) Show gas before send

### Added
- ([#813](https://github.com/neo-project/neo-node/pull/813/)) Add Copyright
- ([#817](https://github.com/neo-project/neo-node/pull/817/)) Add delete address command
- ([#825](https://github.com/neo-project/neo-node/pull/825/)) Add connections config



## [3.0.2]

### Changed
- ([#805](https://github.com/neo-project/neo-node/pull/805/)) Improve plugins installation

## [3.0.0-rc4]

### Fixed
- ([#787](https://github.com/neo-project/neo-node/pull/787/)) Fix incomplete signature make invoke failed
- ([#788](https://github.com/neo-project/neo-node/pull/788/)) fix and improve
- ([#796](https://github.com/neo-project/neo-node/pull/796/)) Fix logger path

## [3.0.0-rc3]

### Added
- ([#776](https://github.com/neo-project/neo-node/pull/776)) add unvote function
- ([#781](https://github.com/neo-project/neo-node/pull/781)) add getAccountState

### Changed
- ([#779](https://github.com/neo-project/neo-node/pull/779)) Reorder transfer arguments
- ([#780](https://github.com/neo-project/neo-node/pull/780)) reorder send args

## [3.0.0-rc2]

### Added
- ([#771](https://github.com/neo-project/neo-node/pull/771/)) Add update

### Changed
- ([#766](https://github.com/neo-project/neo-node/pull/766)) Add network to ContractParametersContext
- ([#722](https://github.com/neo-project/neo-node/pull/772)) Optimize code

### Fixed
- ([#769](https://github.com/neo-project/neo-node/pull/769/)) Fix default signer in OnInvokeCommand

## [3.0.0-rc1]

### Changed
- ([#753](https://github.com/neo-project/neo-node/pull/753)) Combine config.json and protocol.json
- ([#752](https://github.com/neo-project/neo-node/pull/752)) update neo to v3.0.0-CI01229
- ([#748](https://github.com/neo-project/neo-node/pull/748)) sync neo changes
- ([#743](https://github.com/neo-project/neo-node/pull/743)) sync neo
- ([#740](https://github.com/neo-project/neo-node/pull/740)) remove singletons

### Fixed
- ([#750](https://github.com/neo-project/neo-node/pull/750)) Fix autostart
- ([#749](https://github.com/neo-project/neo-node/pull/749)) fix log path

## [3.0.0-preview5]
### Added
- ([#737](https://github.com/neo-project/neo-node/pull/737)) Show header height when show state
- ([#714](https://github.com/neo-project/neo-node/pull/714)) add total supply

### Changed
- ([#733](https://github.com/neo-project/neo-node/pull/733)) sync block height
- ([#726](https://github.com/neo-project/neo-node/pull/726)) Sync to CI01171
- ([#724](https://github.com/neo-project/neo-node/pull/724)) Neo 3.0.0-CI01168
- ([#722](https://github.com/neo-project/neo-node/pull/722)) sync ondeploycommand
- ([#719](https://github.com/neo-project/neo-node/pull/719)) Sync neo 1161
- ([#712](https://github.com/neo-project/neo-node/pull/712)) Neo 3.0.0-CI01152
- ([#709](https://github.com/neo-project/neo-node/pull/709)) Sync to Neo 3.0.0-CI01148
- ([#707](https://github.com/neo-project/neo-node/pull/707)) Sync to CI01133
- ([#706](https://github.com/neo-project/neo-node/pull/706)) 3.0.0-CI01125
- ([#702](https://github.com/neo-project/neo-node/pull/702)) CI01123
- ([#681](https://github.com/neo-project/neo-node/pull/681)) dotnet 5.0

### Fixed
- ([#735](https://github.com/neo-project/neo-node/pull/735)) fix "show state" auto refresh
- ([#730](https://github.com/neo-project/neo-node/pull/730)) fix broadcast getheaders
- ([#727](https://github.com/neo-project/neo-node/pull/727)) Add test mode gas when invoking
- ([#716](https://github.com/neo-project/neo-node/pull/716)) More alignment
- ([#715](https://github.com/neo-project/neo-node/pull/715)) Fix Dockerfile
- ([#713](https://github.com/neo-project/neo-node/pull/713)) Update MainService.Plugins.cs
- ([#704](https://github.com/neo-project/neo-node/pull/704)) Avoid register candidate for others

## [3.0.0-preview4]
### Added
- ([#679](https://github.com/neo-project/neo-node/pull/679)) Add services to plugin system

### Changed
- ([#695](https://github.com/neo-project/neo-node/pull/695)) Update name nep17
- ([#689](https://github.com/neo-project/neo-node/pull/689)) Sync to management SC
- ([#687](https://github.com/neo-project/neo-node/pull/687)) Change nep5 to nep17
- ([#686](https://github.com/neo-project/neo-node/pull/686)) Add data
- ([#682](https://github.com/neo-project/neo-node/pull/682)) Max traceable blocks
- ([#676](https://github.com/neo-project/neo-node/pull/676)) Sync neo changes
- ([#673](https://github.com/neo-project/neo-node/pull/673)) invoke* use base64 script
- ([#654](https://github.com/neo-project/neo-node/pull/654)) Remove Get validators
- ([#643](https://github.com/neo-project/neo-node/pull/643)) Unify ApplicationEngine output
- ([#639](https://github.com/neo-project/neo-node/pull/639)) Unify encoding to be Strict UTF8
- ([#628](https://github.com/neo-project/neo-node/pull/628)) Allow smart contract verification

### Fixed
- ([#674](https://github.com/neo-project/neo-node/pull/674)) Fix to avoid duplicate error message
- ([#664](https://github.com/neo-project/neo-node/pull/664)) Fix invokecommand
- ([#654](https://github.com/neo-project/neo-node/pull/654)) Fix applicationengine.run
- ([#647](https://github.com/neo-project/neo-node/pull/647)) Fix script check

## [3.0.0-preview3]
### Added
- ([#608](https://github.com/neo-project/neo-node/pull/608)) Ensure json extension in wallet
- ([#607](https://github.com/neo-project/neo-node/pull/607)) Add 'nativecontract' command
- ([#599](https://github.com/neo-project/neo-node/pull/599)) Add plugins description field
- ([#575](https://github.com/neo-project/neo-node/pull/575)) Add NEP5 commands
- ([#568](https://github.com/neo-project/neo-node/pull/568)) Add vote commands
- ([#564](https://github.com/neo-project/neo-node/pull/564)) Add StackItem ToJson

### Changed
- ([#634](https://github.com/neo-project/neo-node/pull/634)) Improve Show pool command
- ([#633](https://github.com/neo-project/neo-node/pull/633)) Included optional "from" in send and transfer commands
- ([#630](https://github.com/neo-project/neo-node/pull/630)) Get innerException message Recursively
- ([#626](https://github.com/neo-project/neo-node/pull/626)) Workflows: use checkout action v2
- ([#625](https://github.com/neo-project/neo-node/pull/625)) Update protocol.json
- ([#622](https://github.com/neo-project/neo-node/pull/622)) Apply signers
- ([#621](https://github.com/neo-project/neo-node/pull/621)) Show invocation error
- ([#604](https://github.com/neo-project/neo-node/pull/604)) Add description and uninstall restriction for “SystemLog”
- ([#602](https://github.com/neo-project/neo-node/pull/602)) Remove StackItem.ToParameter()
- ([#593](https://github.com/neo-project/neo-node/pull/593)) Add fields to protocol.json
- ([#585](https://github.com/neo-project/neo-node/pull/585)) Show address in list key command
- ([#584](https://github.com/neo-project/neo-node/pull/584)) Fill default settings
- ([#582](https://github.com/neo-project/neo-node/pull/582)) Move SystemLog plugin into neo-cli as a native logger with on/off functionalities
- ([#581](https://github.com/neo-project/neo-node/pull/581)) Parse vote commands' result
- ([#579](https://github.com/neo-project/neo-node/pull/579)) Update cosigner
- ([#578](https://github.com/neo-project/neo-node/pull/578)) Backup Wallet on change password
- ([#577](https://github.com/neo-project/neo-node/pull/577)) Remove log logic
- ([#567](https://github.com/neo-project/neo-node/pull/567)) Add plugins description field
- ([#566](https://github.com/neo-project/neo-node/pull/566)) Show ScriptHash in `list address`
- ([#536](https://github.com/neo-project/neo-node/pull/536)) Refactor node commands

### Fixed
- ([#613](https://github.com/neo-project/neo-node/pull/613)) Fix invoke command
- ([#610](https://github.com/neo-project/neo-node/pull/610)) Fix engine.ResultStack.Pop()
- ([#594](https://github.com/neo-project/neo-node/pull/594)) Fix relay tx

