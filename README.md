# KoGaMa Offline Patcher

**WIP / As-Is Release**

**Author:** Marshal (Marshze)
**Target Game:** KoGaMa 2026 native launcher, IL2CPP (Latest release | Multiverse ApS)
**Modloader:** MelonLoader 0.7.3
**Version:** 3.5.3 (Unfinished)
**License:** GPL-3.0
**Status:** Development discontinued

---

## About this release

This is where I leave it.

I started this project because I wanted to see if KoGaMa could actually be made playable without relying on its online infrastructure. I spent a lot of time digging through the client, figuring out what it was doing, patching things, breaking things, and trying different approaches until something worked.

I didn't get all the way there.

The project can get surprisingly far, but it never reached the point where I could call it a proper offline version of KoGaMa. There are still parts of the game's initialization and runtime that depend on systems I never managed to reproduce properly.

At this point, continuing would require more time and energy than I'm willing to put into it, so I'm releasing what I have instead of leaving it on my drive.

**This is an as-is release.** This is simply the state the project was in when I stopped working on it. It is unfinished, unmaintained, and may break depending on the version of KoGaMa you're using.

I'm not promising updates or fixes (For now :) )

If someone wants to take the code, figure out what I screwed up, improve it, or continue where I stopped, go for it. Granted, AI-generated code will never be acknowledged or endorsed by me. That's what the GPL is there for.

---

## What's actually in here?

### `BypassMVGameControllerInit (Compressed)`

This is one of the main pieces of the patch.

**Note** : [The original file was too messy to be included and went through heavy compressing in both size and lines of code, in which it had almost 10k lines of code.]

It hooks into the game's core initialization and tries to give it enough fake session and game state to get past the parts that normally require the online environment.

It also deals with loading screens that refuse to disappear when initialization doesn't happen normally.

There are also fallback routines that create basic geometry when map data isn't available. This was mostly useful during development to determine whether the game had actually progressed instead of leaving an empty scene.

### `PhotonInProcessStub`

This was my attempt at dealing with Photon without actually having a Photon server running.

The idea is to provide enough of what the client expects from Photon directly inside the process so that it believes the connection exists.

It's not a complete Photon implementation. It was enough to experiment with getting the client past parts of its networking initialization, but there was still a lot left to figure out.

### `PhotonRedirect`

Works alongside the Photon stub.

Instead of letting the game follow its normal networking path, this patch redirects the relevant behavior toward the local implementation.

### `MapLoaderPatch`

This is the local map-loading side of the project.

The goal was to let the game load `.kgm` maps from the local filesystem instead of requiring them to come through the normal online pipeline.

The project expects local maps under:

```text
%LOCALAPPDATA%\KogamaMaps
```

So, for example:

```text
C:\Users\<YourUser>\AppData\Local\KogamaMaps
```

Put the `.kgm` files there and the patch will attempt to load them.

### `UnityWebRequestSpy`

This catches HTTP requests made by the game and redirects requests intended for KoGaMa's servers toward a local server.

The default address used during development was:

```text
http://127.0.0.1:8080
```

This was useful for supplying fake API responses and local assets without having to rewrite every system making HTTP requests.

A basic HTTP server obviously won't provide the KoGaMa API by itself. You still need to provide whatever responses the game expects.

### `RegionConfigPatch`

This modifies the game's region configuration so it can operate against the local environment instead of trying to discover or contact the normal online region infrastructure.

---

## Getting it running

Don't expect to just throw the DLL into the Plugins folder and have a working offline KoGaMa installation.

There are still external pieces involved.

### 1. MelonLoader

You'll need an IL2CPP-compatible installation of MelonLoader on the KoGaMa client.

The appropriate IL2CPP interop assemblies are also required by the project.

### 2. Local HTTP server

The patch redirects certain HTTP requests to:

```text
127.0.0.1:8080
```

You'll need something listening there and serving whatever responses or assets the game expects.

A basic Python server can be started with:

```bash
python -m http.server 8080
```

That alone won't provide the KoGaMa API. It's just a local HTTP endpoint to work from.

**Additional Information** : Modifying the hosts file inside the windows folder to include the local host IP is also do-able

```txt
# Local Game Environment Redirection
127.0.0.1       api.kogama.com
127.0.0.1       www.kogama.com
```

### 3. Local maps

Create:

```text
%LOCALAPPDATA%\KogamaMaps
```

and put your `.kgm` files inside.

### 4. Install the mod

Put:

```text
KogamaOfflinePatch.dll
```

into the MelonLoader `Mods` folder and launch the game.

The current implementation may fail at different stages depending on the KoGaMa version and the available local data.

---

## Developer notes

### IL2CPP

A lot of this project exists because normal C# types don't always play nicely with the IL2CPP side of the game.

There are places where I had to deal with `Il2CppSystem` collections, `Il2CppObjectBase`, `Il2CppArrayBase`, `Il2CppReferenceArray`, pointers, boxing, and other IL2CPP-specific types.

There are also places where I used memory allocation and pointer manipulation because I couldn't get the normal managed approach to behave correctly.

### Reflection

I couldn't reliably hardcode everything I needed because of how the game's types are exposed and obfuscated at runtime.

You'll see things like `FindTypeInAnyAssembly` being used to locate types dynamically.

It's not ideal, but it was useful while reverse-engineering the client.

### Debugging code

I left quite a bit of the debugging code I used while investigating the client.

You'll find things like:

* `ProbeAvatarFields`
* `ProbeChunkInstances` (Not included)
* `DriveLog`
* Various other probes and logging routines

If you're trying to figure out what the client is doing, some of this code may still be useful.

There may also be useful information in `drive.log` under the relevant local application-data directories.

### Brute-force workarounds

You'll eventually run into methods such as:

```text
TryBruteForceHideLoadingScreen
```

These exist because sometimes I couldn't find the proper way to make the game do something, so I forced it into the state I needed.

Some of these were temporary approaches that were never replaced before development stopped.

---

## Where I left it

The project **does not currently provide a complete playable offline KoGaMa experience**.

The client can be pushed through parts of its normal online initialization, local resources can be introduced, HTTP requests can be redirected, and there was progress toward replacing parts of the networking layer.

There are still major pieces missing.

The biggest problems were getting the game's complete state initialized correctly and getting the systems normally provided by the online environment to behave locally.

Getting the game to load was one thing.

Getting it to actually become a playable game was another.

That's where I stopped.

I would also recommend using Unity explorer in case you want to view your ported map with the free cam and explore objects in the scenery.

Provided here : https://github.com/yukieiji/UnityExplorer (deserves a ⭐ in my opinion)

---

## License

This project is licensed under the **GNU General Public License v3.0**.

You are free to study it, modify it, fork it, and redistribute your modifications under the terms of the license.

See `LICENSE` for the full license text.

---

**Marshal (Marshze)**
