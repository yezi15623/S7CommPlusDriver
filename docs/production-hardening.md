# Production hardening notes

S7CommPlusDriver is useful for symbolic access to S7-1200/S7-1500 data, but it should not be treated like a stateless address-read driver. A single connection has protocol state and must be used carefully.

## Use one serialized access point per PLC

Use `S7CommPlusSafeClient` as the default entry point in UI applications and services:

```csharp
using S7CommPlusDriver.ClientApi;

using (var plc = new S7CommPlusSafeClient())
{
    int res = plc.Connect("192.168.0.10", password: "", username: "", timeoutMs: 5000);
    if (res != 0)
    {
        throw new Exception("PLC connect failed: " + S7Client.ErrorText(res));
    }

    var tag = plc.GetPlcTagBySymbol("\"DB_Process\".\"Speed\"");
    plc.ReadTags(new[] { tag });
}
```

Do not call `ReadValues`, `WriteValues`, `Browse`, `GetTypeInformation`, subscription APIs or raw `Connection` methods concurrently on the same connection. If a WPF program has timers, background polling and button-triggered writes, route them through the same `S7CommPlusSafeClient` instance or through a single producer/consumer queue.

## Cache resolved tags

`GetPlcTagBySymbol` may browse DB/type information. Resolve tags once at startup and cache the returned `PlcTag` objects. In polling loops, only call `ReadTags`/`WriteTags` with cached tags.

## Do not use this for microsecond or hard realtime work

Use this driver for configuration, diagnostics, status and engineering tools. For CT trigger timing, motion-card position compare, safety chain or detector synchronization, use deterministic hardware I/O or a PLC/HMI interface designed for the required timing.

## TLS key logs

The current low-level client writes TLS key log files named `key_yyyyMMdd_HHmmss.log` for Wireshark analysis. The repository ignores these files so they are not committed accidentally, but production deployments should remove or disable the keylog callback in `Net/S7Client.cs`.

## Long-run test checklist

Before using it inside an industrial workstation, run at least:

1. 24-hour single-client polling test.
2. Reconnect test after unplugging/replugging the PLC network cable.
3. Write test on non-critical variables only.
4. Memory and handle count observation.
5. Confirmation that no background thread calls the raw `S7CommPlusConnection` directly.
