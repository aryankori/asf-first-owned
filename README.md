# ASF FirstOwned

An [ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm) plugin that tells you **when your account first got a game**, down to the second.

Steam does not show "first played" anywhere, but it does record when each license was added to your account. For most people that is the moment they first owned (and usually first launched) a game. This plugin reads that straight from the license list ASF already has, so there is no browser, no scraping and no extra login.

## Command

```
FIRSTOWNED <appID or game name>            run on the bot you message
FIRSTOWNED <bot1,bot2> <appID or name>     query several bots (needs Master access)
```

Example:

```
!firstowned 1422450
<mybot> app 1422450: first owned 2024-06-01 15:05:13 UTC via ActivationCode (package 499466)

!firstowned Deadlock
<mybot> Deadlock: first owned 2024-06-01 15:05:13 UTC via ActivationCode (package 499466)
```

- If several packages grant the same game, the earliest one is shown.
- Game names are resolved with the Steam store search (first result). Use the appID when the name is ambiguous.
- Requires `Operator` access or higher. Querying other bots requires `Master`.
- Times are UTC.

## Install

1. Download `ASFFirstOwned.dll` from the [latest release](../../releases/latest).
2. Put it in the `plugins` folder next to `ArchiSteamFarm.exe`.
3. Restart ASF. The log should show `FirstOwned loaded`.

Tested with ASF **6.3.11.7**. Other versions may work but are untested, and ASF plugins are generally built per ASF version.

## Build

Needs the .NET 10 SDK.

```
git clone --recurse-submodules https://github.com/aryankori/asf-first-owned
cd asf-first-owned/ASFFirstOwned
dotnet build -c Release
```

The output is `bin/Release/net10.0/ASFFirstOwned.dll`. The `ArchiSteamFarm` submodule is pinned to tag `6.3.11.7`.

## Limits

- It reports when the **license was acquired**, not the first time you launched the game. For free-to-play games and invites those are usually minutes or hours apart.
- It can only see games on the account the bot is logged into.
- ASF ships as a trimmed single-file build, so this plugin sticks to well-trodden framework APIs and ASF's own web client. Keep that in mind if you extend it.

## License

MIT
