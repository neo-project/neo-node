# Native SmartAccount fee simulation

Bounded callbacks need enough remaining transaction gas to admit their declared
child budget. Actual consumed gas alone can therefore underquote an otherwise
successful invocation. On a capable core, `invokescript` and `invokefunction`
return `minimumrequiredfee` as a decimal string in datoshi, alongside the unchanged
`gasconsumed`. It includes the bounded admission peak and is always at least
the consumed fee. RPC clients preserve this optional field; transaction builders
use it when supplied and retain legacy consumed-fee behavior for older nodes.

`invoketransaction` accepts one base64 serialized, fully signed transaction.
It verifies the actual witnesses and fees against a disposable ledger snapshot,
then prepares a hypothetical next block containing only that transaction. It runs
actual native `OnPersist` with that block before executing the exact script and
container with the submitted system fee. This applies native fee burns, primary
network-fee rewards, activation and ledger preparation in their real order; a
preparation failure returns an RPC error and never an Application result.
It never relays, signs, reserves fees or commits state. The response identifies
the transaction and snapshot, reports the verification result, and includes
application state, fee measurements and stack only after verification succeeds.
The configured RPC invocation gas maximum also limits this endpoint.

Clients must require successful verification and application HALT before
broadcast, keep independent system/network/total fee caps, and never mutate a
transaction after signing. This preview does not reserve a nonce or balance and
does not promise mempool acceptance: pending competing transactions, a new block,
time or policy changes can invalidate the result. An admitted transaction that
later faults still charges the external fee payer.

The `snapshot` identity remains the original persisted ledger. After successful
preparation the response additionally declares:

```json
{
  "simulation": {
    "mode": "single-transaction-next-block",
    "height": 101,
    "timestamp": "1800000000000",
    "primaryIndex": 3,
    "view": 0,
    "transactionCount": 1,
    "onPersist": "HALT",
    "nextConsensus": "0x0000000000000000000000000000000000000000"
  }
}
```

These values are illustrative. `height` is the snapshot height plus one. The
primary follows DBFT's view-zero calculation over the current validator roster;
`nextConsensus` uses the newly computed validators at committee-refresh heights.
The timestamp assumes the previous timestamp plus the snapshot's block interval,
and is a canonical unsigned decimal UInt64 string to avoid JSON number precision
loss. The hypothetical header has nonce zero and the single transaction hash as
its Merkle root. Empty validator rosters and height/timestamp overflow fail closed.
Both native `OnPersist` and Application use this same block. `PostPersist` does
not run, and the root snapshot is never committed, on either HALT or FAULT.
`gasconsumed`, `minimumrequiredfee` and notifications describe Application only;
fee burn/reward notifications from preparation are excluded.

Actual consensus can select another view, primary, timestamp and nonce and
include other transactions. Those differences, current mempool conflicts and
subsequent state changes can change execution; this is a declared single-transaction
simulation, not an inclusion or outcome guarantee. Clients requiring fee-aware
preflight must validate the simulation declaration and reject older responses
without it rather than treating a pre-fee HALT as an equivalent result. No live
state is modified.

For source integration before the matching core package is released, build with
`-p:NativeCoreRoot=/absolute/path/to/the/reviewed/core/checkout`. The optional
project reference replaces the published Neo package; keep the core source
identity alongside the node build receipt. The server also builds against the published Neo core. It caches an optional
public instance `long MinimumRequiredFee` getter once, rather than requiring that
member at compile time. A core without the property omits `minimumrequiredfee`;
for example, a legacy response contains `"gasconsumed":"30"` without a minimum
field. A capable core may return `"gasconsumed":"30", "minimumrequiredfee":"100"`.
The adapter never substitutes consumed gas or zero for an absent capability. A
malformed property, failing getter, negative result or result below consumed gas
fails the request instead of silently degrading. Existing clients may retain
legacy behavior when the field is absent; native-account clients must require
the capability before claiming a bounded-callback admission quote.

CI keeps the normal published-package jobs and adds a separate source-core job
pinned to commit `ff422d940a4722c361c32398c6f28c00fb7f0693`. The source lane
asserts the capability exists and the published lane asserts it is absent, so
passing compatibility tests cannot be mistaken for native runtime validation.

The native account is never its transaction's fee payer. Its operation signature
authorizes its exact asset operation, while the external payer signs the entire
transaction, including both fees, expiry, script, signers and witness scopes.

The .NET transaction manager includes custom verification scripts and their
invocation parameters when requesting a network fee quote. After assembling
signatures it quotes the complete witnesses again; a larger required fee fails
closed and requires rebuilding the transaction instead of changing signed bytes.
