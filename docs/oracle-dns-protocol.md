# Oracle DNS Protocol

The Oracle plugin resolves RFC 4501 `dns:` URIs through a DNS-over-HTTPS (DoH) gateway. This lets oracle nodes read authoritative DNS data (TXT for DKIM/SPF/DIDs, CERT/TLSA, etc.) without sending plaintext DNS queries.

> **When should I use it?**  
> Whenever you need DNS data on-chain and want the request to stay encrypted end-to-end.

## Enable and configure

1. Install or build the `OracleService` plugin and copy `OracleService.json` next to the plugin binary.
2. Add the `Dns` section (defaults shown):

```jsonc
{
  "PluginConfiguration": {
    // ...
    "Dns": {
      "EndPoint": "https://cloudflare-dns.com/dns-query",
      "TimeoutMilliseconds": 5000
    }
  }
}
```

- `EndPoint` must point to a DoH resolver that supports [RFC 8484](https://www.rfc-editor.org/rfc/rfc8484.html) with `application/dns-message` format.
- `Timeout` is the maximum milliseconds the oracle will wait for a DoH response before returning `OracleResponseCode.Timeout`.

> You can run your own DoH gateway and point the oracle to it if you need custom trust anchors or strict egress controls.

### RFC 8484 Compliance

This implementation uses the standard `application/dns-message` content type as defined in RFC 8484. DNS queries are sent as POST requests with binary DNS wire format (RFC 1035). Compatible DoH endpoints include:

| Provider | Endpoint |
|----------|----------|
| Cloudflare | `https://cloudflare-dns.com/dns-query` |
| Google | `https://dns.google/dns-query` |
| Quad9 | `https://dns.quad9.net/dns-query` |

Any RFC 8484-compliant DoH server should work with this oracle protocol.

## RFC 4501 URI format

```
dns:[//authority/]domain[?CLASS=class;TYPE=type]
```

- `domain` is the DNS owner name (relative or absolute). Percent-encoding and escaped dots (`%5c.`) follow RFC 4501 rules.
- `domain` must not include additional path segments; only the owner name belongs here.
- `authority` is the optional DoH server to use for this query (RFC 4501). When specified, the oracle connects to `https://{authority}/dns-query`. If omitted, the configured `EndPoint` is used.
- `CLASS` is optional and case-insensitive. Only `IN` (`1`) is supported; other classes are rejected.
- `TYPE` is optional and case-insensitive. Use mnemonics (`TXT`, `TLSA`, `CERT`, `A`, `AAAA`, …) or numeric values. Defaults to `A` per RFC 4501.
- `name` is an oracle extension; if present, it overrides `domain` entirely (useful for percent-encoding complex owner names).

Query parameters can be separated by `;` (RFC style) or `&`.

Examples:

- `dns:1alhai._domainkey.icloud.com?TYPE=TXT` — DKIM TXT record.
- `dns:simon.example.org?TYPE=CERT` — CERT RDATA is returned as-is (type, key tag, algorithm, base64).
- `dns://dns.google/ftp.example.org?TYPE=A` — uses Google's DoH server (`https://dns.google/dns-query`) instead of the configured endpoint.
- `dns://cloudflare-dns.com/example.org?TYPE=TXT` — uses Cloudflare's DoH server for this specific query.
- `dns:ignored?name=weird%5c.label.example&type=TXT` — uses the `name` override (decoded to `weird.label.example`).

## Response schema

Successful queries return UTF-8 JSON. Attributes correspond to the `ResultEnvelope` produced by the oracle:

```jsonc
{
  "Name": "1alhai._domainkey.icloud.com",
  "Type": "TXT",
  "Answers": [
    {
      "Name": "1alhai._domainkey.icloud.com",
      "Type": "TXT",
      "Ttl": 299,
      "Data": "\"k=rsa; p=...IDAQAB\""
    }
  ]
}
```

- `Answers` mirrors the DoH response but normalizes record types and names.
- CERT records are returned verbatim in `Answers[].Data` (type, key tag, algorithm, base64 payload). Contracts can parse the certificate themselves if needed.
- If the DoH server responds with NXDOMAIN, the oracle returns `OracleResponseCode.NotFound`.
- Responses exceeding `OracleResponse.MaxResultSize` yield `OracleResponseCode.ResponseTooLarge`.

## Contract usage example

```csharp
public static void RequestAppleDkim()
{
    const string url = "dns:1alhai._domainkey.icloud.com?TYPE=TXT";
    Oracle.Request(url, "", nameof(OnOracleCallback), Runtime.CallingScriptHash, 5_00000000);
}

public static void OnOracleCallback(string url, byte[] userData, int code, byte[] result)
{
    if (code != (int)OracleResponseCode.Success) throw new Exception("Oracle query failed");

    var envelope = (Neo.SmartContract.Framework.Services.Neo.Json.JsonObject)StdLib.JsonDeserialize(result);
    var answers = (Neo.SmartContract.Framework.Services.Neo.Json.JsonArray)envelope["Answers"];
    var txt = (Neo.SmartContract.Framework.Services.Neo.Json.JsonObject)answers[0];
    Storage.Put(Storage.CurrentContext, "dkim", txt["Data"].AsString());
}
```

Tips:

1. Always set `TYPE` when you need anything other than an A record.
2. Budget enough `gasForResponse` to cover JSON payload size (TXT records are often kilobytes).
3. Validate TTL or fingerprint data before trusting it.
4. Combine oracle DNS data with existing filters (e.g., `Helper.JsonPath`/`OracleService.Filter`) if you only need a slice of the result.

## Manual testing

Use the same resolver the oracle will contact to inspect responses:

```bash
curl -s \
  -H 'accept: application/dns-json' \
  'https://cloudflare-dns.com/dns-query?name=1alhai._domainkey.icloud.com&type=TXT'
```

Compare the JSON payload with the data returned by your contract callback to ensure parity.
