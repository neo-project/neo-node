
# Contributing to NEO
Neo is an open-source project and it depends on its contributors and constant community feedback to implement the features required for a smart economy. You are more than welcome to join us in the development of Neo.  

Read this document to understand how issues are organized and how you can start contributing.

*This document covers this repository only and does not include community repositories or repositories managed by NGD Shanghai and NGD Seattle.*

### Questions and Support
The issue list is reserved exclusively for bug reports and features discussions. If you have questions or need support, please visit us in our [Discord](https://discord.io/neo) server.  

### dApp Development
This document does not relate to dApp development. If you are looking to build a dApp using Neo, please [start here](https://neo.org/dev).

### Contributing to open source projects
If you are new to open-source development, please [read here](https://opensource.guide/how-to-contribute/#opening-a-pull-request) how to submit your code.

## Developer Guidance
We try to have as few rules as possible,  just enough to keep the project organized:


1.  **Discuss before coding**. Proposals must be discussed before being implemented.  
Avoid implementing issues with the discussion tag.
2. **Tests during code review**. We expect reviewers to test the issue before approving or requesting changes.

3. **Wait for at least 2 reviews before merging**. Changes can be merged after 2 approvals, for Neo 3.x branch, and 3 approvals for Neo 2.x branch.

3. **Give time to other developers review an issue**. Even if the code has been approved, you should leave at least 24 hours for others to review it before merging the code.

4. **Create unit tests**. It is important that the developer includes basic unit tests so reviewers can test it.

5. **Task assignment**. If a developer wants to work in a specific issue, he may ask the team to assign it to himself. The proposer of an issue has priority in task assignment.


### Issues for beginners
If you are looking to start contributing to NEO, we suggest you start working on issues with ![](./.github/images/cosmetic.png) or ![](./.github/images/house-keeping.png) tags since they usually do not depend on extensive NEO platform knowledge. 

### Tags for Project Modules 
These tags do not necessarily represent each module at code level. Modules consensus and compiler are not recommended for beginners.

![Compiler](./.github/images/compiler.png) Issues that are related or influence the behavior of our C# compiler. Note that the compiler itself is hosted in the [neo-devpack-dotnet](https://github.com/neo-project/neo-devpack-dotnet) repository.

![Consensus](./.github/images/consensus.png) Changes to consensus are usually harder to make and test. Avoid implementing issues in this module that are not yet decided.

![Ledger](./.github/images/ledger.png) The ledger is our 'database', any changes in the way we store information or the data-structures have this tag.

![House-keeping](./.github/images/house-keeping.png) 'Small' enhancements that need to be done in order to keep the project organised and ensure overall quality. These changes may be applied in any place in code, as long as they are small or do not alter current behavior.

![Network-policy](./.github/images/network-policy.png) Identify issues that affect the network-policy like fees, access list or other related issues. Voting may also be related to the network policy module.

![P2P](./.github/images/p2p.png) This module includes peer-to-peer message exchange and network optimisations, at TCP or UDP level (not HTTP).

![RPC](./.github/images/rpc.png) All HTTP communication is handled by the RPC module. This module usually provides support methods since the main communication protocol takes place at the p2p module.

![VM](./.github/images/vm.png) New features that affect the Neo Virtual Machine or the Interop layer.

![SDK](./.github/images/sdk.png) Neo provides an SDK to help developers to interact with the blockchain. Changes in this module must not impact other parts of the software. 

![Wallet](./.github/images/wallet.png) Wallets are used to track funds and interact with the blockchain. Note that this module depends on a full node implementation (data stored on local disk).




