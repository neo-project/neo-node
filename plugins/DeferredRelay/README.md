# DeferredRelay

Queues transactions that are **valid but not yet in the relay window** (`VerifyResult.NotYetValid`) and relays them automatically once they enter the allowed `ValidUntilBlock` window.

On a standard Neo node, transactions whose `ValidUntilBlock` is too far in the future are rejected at relay time. DeferredRelay keeps those transactions in a **local plugin store** and retries relay on a schedule.

---

## ⚠️ Internal nodes only

**This plugin should be used on internal / private nodes only — not on public RPC endpoints exposed to the open internet.**

Public-facing nodes with DeferredRelay enabled become an **additional transaction ingress path** that ordinary nodes do not offer. Even with queue limits and fee checks, attackers can:

- **Fill the local queue** with far-future transactions, consuming disk and verification CPU.
- **Probe validation behavior** and queue state via RPC (`getpendingvaliduntilrelay`, `getrawpendingtx`).
- **Bypass the normal “reject early” behavior** that protects mempools on the public network.

Typical mitigations (`MaxTransactions`, `MaxTransactionsPerSender`, `MinNetworkFee`) reduce abuse but **do not eliminate** the extra attack surface. Operators who enable this plugin on a public RPC node often do not account for this risk.

**Recommended deployment:** private infrastructure nodes (wallets, indexers, dApp backends) that you control, behind firewalls, with RPC access restricted to trusted clients.

---

## How it works

```
Relay attempt (RPC / P2P / CLI)
        │
        ▼
Blockchain returns NotYetValid
        │
        ▼
DeferredRelayActor ──► TryOffer (validate + persist to local store)
        │
        ▼
On each Nth block (CheckFrequency)
        │
        ▼
ProcessQueuedAsync ──► evict expired / invalid entries
        │                relay txs that entered the window
        ▼
Blockchain.Relay (normal mempool path)
```

### Offer (enqueue)

When the node relays a transaction and the result is `NotYetValid`, `DeferredRelayActor` calls `TryOffer`:

1. Skip if the plugin is disabled, the tx is already in the mempool, or it is already queued.
2. Enforce **queue capacity** (`MaxTransactions`), **per-sender cap** (`MaxTransactionsPerSender`), and **minimum network fee** (`MinNetworkFee`).
3. Run **state-dependent verification** (`VerifyForOffer`) — same checks as `Transaction.VerifyStateDependent`, but **without** the `Expired` / `NotYetValid` time gates, so far-future transactions can still be validated for policy, balance, and witnesses.
4. Persist the serialized transaction in the plugin’s dedicated store (`DeferredRelay_{networkId}`).

### Process (dequeue & relay)

After every `CheckFrequency` blocks:

1. **Classify** all queued entries — remove expired, corrupt, below-min-fee, or state-invalid transactions.
2. **Relay** transactions whose `ValidUntilBlock` is now within the allowed forward window.
3. Cap relays per cycle (`MaxRelayPerCycle`, default 256) to avoid flooding the Blockchain actor mailbox.
4. Remove successfully relayed entries (or entries that failed with a definitive error).

On startup, the store is **compacted** so stale entries from a previous run do not occupy queue slots.

---

## Configuration

Edit `DeferredRelay.json`. The plugin is **disabled by default** (`MaxTransactions` or `CheckFrequency` set to `0`).

| Setting | Description |
|---------|-------------|
| `Path` | Local store path template (`DeferredRelay_{0}` → network id). |
| `MaxTransactions` | Maximum queued transactions. Must be &gt; 0 to enable. |
| `MaxTransactionsPerSender` | Per-sender queue cap (must be &lt; `MaxTransactions` when both are non-zero). `0` = unlimited per sender. |
| `MinNetworkFee` | Minimum `NetworkFee` in GAS; txs below this are rejected / evicted. |
| `CheckFrequency` | Process the queue every N blocks. Must be &gt; 0 to enable. |

**Requires:** [RpcServer](https://github.com/neo-project/neo-node/tree/master/plugins/RpcServer) plugin (RPC methods below).

---

## RPC & CLI

| Method / command | Description |
|------------------|-------------|
| `getpendingvaliduntilrelay` | Queue snapshot: height, settings, pending tx list. |
| `getrawpendingtx` | Fetch a queued transaction by hash (base64 or verbose JSON). |
| `list pending` | CLI equivalent of `getpendingvaliduntilrelay`. |
