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

- `EndPoint` must point to a DoH resolver that understands the [application/dns-json](https://developers.cloudflare.com/api/operations/dns-over-https) format.
- `Timeout` is the maximum milliseconds the oracle will wait for a DoH response before returning `OracleResponseCode.Timeout`.

> You can run your own DoH gateway and point the oracle to it if you need custom trust anchors or strict egress controls.

## RFC 4501 URI format

```
dns:[//authority/]domain[?CLASS=class;TYPE=type][;FORMAT=x509]
```

- `domain` is the DNS owner name (relative or absolute). Percent-encoding and escaped dots (`%5c.`) follow RFC 4501 rules.
- `domain` must not include additional path segments; only the owner name belongs here.
- `authority` is the optional DNS server hint from RFC 4501. The oracle still uses the DoH resolver configured in `OracleService.json`.
- `CLASS` is optional and case-insensitive. Only `IN` (`1`) is supported; other classes are rejected.
- `TYPE` is optional and case-insensitive. Use mnemonics (`TXT`, `TLSA`, `CERT`, `A`, `AAAA`, …) or numeric values. Defaults to `A` per RFC 4501.
- `FORMAT` is an oracle extension; use `format=x509` (or `cert`) to parse TXT/CERT payloads into the `Certificate` field.
- `name` is an oracle extension; if present, it overrides `domain` entirely (useful for percent-encoding complex owner names).

Query parameters can be separated by `;` (RFC style) or `&`.

Examples:

- `dns:1alhai._domainkey.icloud.com?TYPE=TXT` — DKIM TXT record.
- `dns:simon.example.org?TYPE=CERT;FORMAT=x509` — extract the X.509 payload into `Certificate`.
- `dns://192.168.1.1/ftp.example.org?TYPE=A` — RFC-compliant authority form (authority is ignored; the configured DoH endpoint is used).
- `dns:ignored?name=weird%5c.label.example&type=TXT` — uses the `name` override (decoded to `weird\.label.example`).

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
  ],
  "Certificate": {
    "Subject": "CN=example.com",
    "Issuer": "CN=Example Root",
    "Thumbprint": "ABCD1234...",
    "NotBefore": "2024-01-16T00:00:00Z",
    "NotAfter": "2025-01-16T00:00:00Z",
    "Der": "MIIC...",
    "PublicKey": {
      "Algorithm": "RSA",
      "Encoded": "MIIBIjANBg...",
      "Modulus": "B968DE...",
      "Exponent": "010001"
    }
  }
}
```

- `Answers` mirrors the DoH response but normalizes record types and names.
- `Certificate` is present only when `TYPE=CERT` or `FORMAT=x509`. `Der` is the base64-encoded certificate, while `PublicKey` provides both the encoded SubjectPublicKeyInfo (`Encoded`) and algorithm-specific fields (`Modulus`/`Exponent` for RSA, `Curve`/`X`/`Y` for EC).
- For RSA keys the modulus/exponent strings are big-endian hex. For EC keys the X/Y coordinates are hex-encoded affine coordinates on the reported `Curve`.
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
