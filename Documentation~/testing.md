# Test your integration

Test sessions and events sent to production stay there and cannot be removed. Point development builds at Almedia's testing backend instead. It serves the production API and shows your build's traffic in its own console.

## In the Editor

Play Mode uses the Editor mock and talks to no backend. Drive statuses, errors, notifications and progress from [`AlmediaEditorMock`](./integration-guide.md#driving-non-happy-paths). `BackendUrl` has no effect in the Editor beyond a warning when the value is invalid.

## On a device

Set the backend in code, under `DEVELOPMENT_BUILD`, so release builds keep talking to production:

```csharp
var config = new AlmediaConfig
{
    IosIntegrationKey = "YOUR_IOS_KEY",
    AndroidIntegrationKey = "YOUR_ANDROID_KEY",
};

#if DEVELOPMENT_BUILD
config.BackendUrl = "https://<testing host>";
config.AttBackendUrl = "https://<testing host>";   // iOS only, to test IDFA behaviour
#endif

Almedia.Initialize(config);
```

Keep your usual integration key. It also logs you in to the testing console.

1. Make a **Development Build** (File > Build Settings) and run it on a device.
2. Open the testing console at `<console URL>` and log in with your integration key.
3. Reset the device's session in the console for a fresh start.
4. Drive statuses, messages and failures from the console, and watch your UI react.
5. Check the **Traffic** tab for the requests your build sends, and the warning the SDK logs at `Initialize` naming the backend in use.

**URL rules.** An origin only: `https://host` or `https://host:port`, with no path, query or credentials. `http://` is accepted for `localhost`, `127.0.0.1` and private IPv4 addresses (`10.x`, `172.16.x` to `172.31.x`, `192.168.x`) only. A production host is rejected: leave `BackendUrl` empty for production. In the Editor, an invalid value logs a warning at `Initialize`. On a device it fails `Initialize` with `InvalidConfiguration`, and the SDK never falls back to production.

**In-app screens** open pages from the backend's own host. Pages on other hosts open in the browser.

**iOS and the IDFA.** The IDFA goes only to `AttBackendUrl`, and only once the player grants tracking. To test IDFA behaviour, set it to the same host as `BackendUrl`. Without it the IDFA is never sent. A store build that sends the IDFA to a separate host must declare that host as a tracking domain in its own privacy manifest.

**Switching backends** drops the message cursor and the queued tracking events. They belong to the backend that produced them.

## Before you release

- The backend lines sit under `DEVELOPMENT_BUILD`, so a release build does not carry them.
- A release build whose code still contains the testing backend URL does not build. The build error names the assembly and the URL.
- A release build that still sets a backend logs an error on every `Initialize`. Look for `Custom backend in a release build` in the device log.
- A store build that reaches players with the testing backend, or any `http://` backend, set talks to production instead, and `OnErrorOccurred` reports `InvalidConfiguration`.
