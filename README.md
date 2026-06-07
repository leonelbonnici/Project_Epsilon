# 3-Player Co-op Template — Netcode for GameObjects + PlayMaker + Rewired

A reusable Unity template for small-scale **online co-op (up to 3 players)**. Gameplay
is built with visual scripting (PlayMaker); a thin C# "bridge" handles the few things
that genuinely need code (network lifecycle, synced variables, RPCs). Players connect
over the internet via Unity Relay — no port forwarding.

---

## Stack

- **Unity 6.x**
- **Netcode for GameObjects (NGO)** — core networking
- **Multiplayer Services SDK (Sessions) + Relay + Lobby** — online connection / matchmaking
- **Multiplayer Sessions Building Block** — drop-in session UI (Create / Quick Join / Join by Code)
- **PlayMaker** — visual scripting (all gameplay logic lives in FSMs)
- **Rewired** — input
- **Feel** — game-feel polish (installed, ready to use)

---

## First-time setup

1. Open the project in **Unity 6.x**.
2. **Paid assets** (may not be committed to the repo): PlayMaker, Rewired, Feel.
   Install them from the Asset Store / Package Manager if the project reports them missing.
3. **Link to Unity Gaming Services (REQUIRED for online play).** A duplicated copy of
   this template is NOT automatically linked — each project needs its own link:
   - In the Editor: click the **cloud icon** (Services) → create or link a cloud project.
   - In the **Unity Cloud Dashboard** (cloud.unity.com): enable **Relay** and **Lobby**
     for that project.
   - Without this, online sessions will not connect. (Local Multiplayer Play Mode still
     works for testing.)
4. Install the **Multiplayer Play Mode** package to test multiple players in one editor.

---

## Running / testing

1. Open the **Game** scene.
2. `Window > Multiplayer > Multiplayer Play Mode` → enable **2 virtual players** (3 total).
3. Press **Play**.
4. In each window, click **Quick Join** — the first window creates a Relay session, the
   others join it. The session UI hides once you're in.
5. Click a window to focus it, then: **WASD** = move, **K** = test damage,
   **J** = spawn a pickup, **P** = add to the shared score.

> **Multiplayer Play Mode rule:** virtual players load scenes/prefabs from *disk*, not
> your unsaved editor state. Always **Save** (and Save Project) before pressing Play, or
> changes will appear for the main editor only.

---

## Architecture

### The player bridge — `Scripts/Network/NetworkPlayMakerBridge.cs`

A thin `NetworkBehaviour` on the Player prefab connecting NGO to PlayMaker. It is the
*only* per-player networking code you normally touch. It provides:

- **Lifecycle events** (broadcast to all FSMs on the object): `NETWORK_SPAWNED`,
  `NETWORK_DESPAWNED`.
- **Read-only values** for PlayMaker `Get Property`: `IsLocalOwner` (bool),
  `OwnerId` (int), `HealthValue` (float).
- **Synced state**: `health` — a server-write `NetworkVariable<float>`.
- **RPC entry points** for PlayMaker `Call Method`: `RequestDamage` / `RequestHeal`
  (plus parameterless `RequestDamage10` / `RequestHeal10` to avoid Call Method's
  parameter quirks).
- **Change/effect events**: `HEALTH_CHANGED` (fires when health changes),
  `HIT_EFFECT` (fires on all clients when hit).

### Player FSMs (PlayMaker, on the Player prefab)

- **Movement** — `Waiting` →(NETWORK_SPAWNED)→ `CheckOwnership` →(owner)→
  `SetSpawnPosition` → `RegisterAsLocal` → `Active`; non-owners go to `Remote`.
  Only the owner reads Rewired input and moves; `NetworkTransform` (Owner authority)
  syncs position to everyone. On join, the owner's `RegisterAsLocal` also points the
  camera at the local player and **hides the session UI** (deactivates the Quick Join
  canvas via `Find Game Object` + `Activate Game Object`).
- **PlayerColor** — on spawn, reads `OwnerId`, indexes a color array, sets the sprite
  color. (See "deterministic" pattern below.)
- **PlayerHealth** — on `HEALTH_CHANGED` / `NETWORK_SPAWNED`, reads `HealthValue` into
  a variable. Hook a health bar here. Also logs on `HIT_EFFECT` (placeholder for a real
  hit reaction).
- **DamageTest** *(scaffolding)* — K → `RequestDamage10`. Remove or replace when you
  build real combat.
- **SpawnTest** *(scaffolding)* — J → `NetworkObjectSpawner.SpawnHere()` to spawn a
  Pickup at the player's position. Demonstration only.

### Camera — `CameraFollow` FSM (on Main Camera)

Waits until the local player registers itself (the player's `RegisterAsLocal` state uses
`Set Fsm GameObject` to set the camera's `target`), then follows that target every frame.
Each player's window follows its own local player.

### Spawning objects — `Scripts/Network/NetworkObjectSpawner.cs`

A reusable `NetworkBehaviour` you can drop on any object (player, GameManager, spawn
point) to spawn a configured networked prefab. Because **only the server may spawn**
NetworkObjects, client requests route through a ServerRpc.

- `prefabToSpawn` — the networked prefab to spawn (must be in the network prefabs list).
- `ownerOnly` — if true, only the owner of *this* object may trigger a spawn (use for
  player-attached spawners, like the SpawnTest demo).
- `SpawnHere()` / `SpawnAt(pos)` — client-callable request (via ServerRpc).
- `ServerSpawnAt(pos)` — server-side direct spawn (no RPC) for server logic such as a
  GameManager spawning enemies on a timer.

The **Pickup** prefab (`Prefabs/Pickups/`) is a minimal example: a sprite + `NetworkObject`,
registered in the network prefabs list, with no `NetworkTransform` because it doesn't move.

### Shared / global state — `Scripts/Network/GameStateBridge.cs` (on the GameManager)

An **in-scene `GameManager`** object (empty GameObject + `NetworkObject`) carrying
game-wide state that isn't per-player. Currently a server-write `NetworkVariable<int>`
score. Same bridge pattern as the player's health, just global:

- `ScoreValue` (int) — read for PlayMaker `Get Property`.
- `RequestAddScore(int)` / `AddOnePoint()` — client request via ServerRpc.
- `ServerAddScore(int)` — server-side direct (e.g., pickup collection).
- `SCORE_CHANGED` event — fires on all clients when the score changes; `NETWORK_SPAWNED`
  fires when the GameManager is network-ready.

**Two things make this work:** (1) the request RPC is marked
`[Rpc(SendTo.Server, RequireOwnership = false)]` — the GameManager is owned by the
*server*, so without this, non-host clients couldn't request changes; (2) in-scene
NetworkObjects synchronize to clients only when **Enable Scene Management** is on in the
NetworkManager (it is).

GameManager FSMs: **ScoreInput** (P key → `AddOnePoint`) and **ScoreDisplay**
(on `SCORE_CHANGED`, reads `ScoreValue` — hook your score UI here). The P-key input is
scaffolding; real games drive score from gameplay.

---

## Networking patterns established (reuse these)

1. **Deterministic-from-OwnerId** — for values that never change at runtime but must be
   consistent across clients (player color, spawn position). Compute from the synced
   `OwnerId`; no NetworkVariable needed, because every client derives the same answer.
2. **NetworkVariable** — for persistent synced *state* (health, score, ammo). Declare it
   in a bridge, expose a read property + a `...Changed` event, react in PlayMaker.
   Write permission: **Server** for trusted state, **Owner** for simple/trusted-client values.
3. **ServerRpc** (`[Rpc(SendTo.Server)]`) — client *requests* an action; the server
   validates and applies it. Server-authoritative (anti-cheat). Method name must end in `Rpc`.
4. **ClientRpc** (`[Rpc(SendTo.ClientsAndHost)]`) — server *announces* a one-shot event
   to all clients (effects, sounds, pings). Method name must end in `Rpc`.
5. **Spawning networked objects** — only the server may spawn. The server `Instantiate`s
   a *registered* prefab and calls `NetworkObject.Spawn()`; clients ask via a ServerRpc
   (`NetworkObjectSpawner`). Removing one is the mirror: the server calls
   `NetworkObject.Despawn()`.

**Common combo:** client ServerRpc → server validates → server updates a NetworkVariable
(state) and/or fires a ClientRpc (effect).

**State can be per-player or global** — health lives on the Player; score lives on a
single in-scene **GameManager**. Same `NetworkVariable` pattern, different host object.
For a client to change state on a *server-owned* object (like the GameManager), mark its
request RPC `RequireOwnership = false`.

---

## Recipe: adding a new networked behavior

1. **Synced state?** Add a `NetworkVariable<T>` to the bridge + a read property + a
   `...Changed` event (forwarded as a PlayMaker event via `OnValueChanged`).
2. **Client → server action?** Add `public void RequestX()` (with an `IsOwner` guard if
   it should be owner-only) that calls a `[Rpc(SendTo.Server)] XRpc()`.
3. **Server → clients announcement?** Add a `[Rpc(SendTo.ClientsAndHost)] YRpc()` that
   fires a PlayMaker event.
4. **In PlayMaker:** `Get Property` to read, `Call Method` to trigger, transitions on the
   events to react. Add `NetworkObject` (+ `NetworkTransform` if it moves) to any new
   networked prefab, and register it in the network prefabs list.
5. **Spawning a networked object?** Register the prefab (step 4), then have the *server*
   `Instantiate` + `.Spawn()` it — via `NetworkObjectSpawner`, or directly from server logic.
6. **Global (non-player) state?** Add a `NetworkVariable<T>` to `GameStateBridge` on the
   GameManager, with a read property + `...Changed` event, same as the player bridge. If
   clients trigger the change, mark the request RPC `RequireOwnership = false` (the
   GameManager is server-owned).

---

## Input (Rewired)

- Single game player: **Player 0**.
- Actions: `MoveHorizontal` (A/D), `MoveVertical` (W/S), `TestDamage` (K),
  `TestSpawn` (J), `TestScore` (P).
- PlayMaker reads these via the **Rewired PlayMaker integration** actions.

---

## Folder structure

```
Assets/
  _Project/
    Audio/            (Music, SFX)
    FSMs/             (reusable FSM templates)
    Prefabs/
      Players/        (the networked Player prefab)
      Enemies/
      Pickups/        (the networked Pickup prefab — server-spawned)
    Scenes/           (Game scene — also holds the in-scene GameManager;
                       session UI is hidden on join)
    ScriptableObjects/
    Scripts/
      Network/        NetworkPlayMakerBridge.cs  (player bridge)
                      NetworkObjectSpawner.cs    (spawn networked prefabs)
                      GameStateBridge.cs         (GameManager / shared state)
                      PickupCollectible.cs       (optional: collect -> score)
      Utility/
    Sprites/
  Blocks/
    MultiplayerSession/   (Multiplayer Sessions Building Block: session UI + settings)
  PlayMaker/   Rewired/   Feel/
```

---

## Known scaffolding / TODO when starting a real game

- **Test triggers are demonstration only:** the **DamageTest** (K), **SpawnTest** (J),
  and the GameManager's **ScoreInput** (P) FSMs/actions. Remove or replace them with
  real gameplay.
- The `HIT_EFFECT` Debug Log is a placeholder for a real hit reaction (flash/sound/particle).
- **Death/respawn** at 0 health is not implemented — that's gameplay to build on top.
- Damaging *other* objects (enemies, other players) uses the same RPC pattern but drops
  the `IsOwner` guard and lets the server validate who may damage whom.
- **Session UI / menu:** the Building Block's generic UI is hidden on join (the player's
  `RegisterAsLocal` deactivates it). Replace it with your own menu/HUD, and add a pause
  menu for leaving the session, since the Leave button is hidden in-game.
- **Scene flow:** online play runs in a single scene with the session UI hidden on join.
  A full **Menu → Game** multi-scene split (NGO `NetworkSceneManager`, server loads the
  game scene, clients sync to it) is the recommended next step for a real game — keep
  player auto-spawn on.
- **Pickup collection (optional bonus):** `PickupCollectible` adds score and despawns the
  pickup server-side when a player overlaps it. Needs a `Collider2D` on both the pickup
  (Is Trigger) and the player, a **Kinematic `Rigidbody2D`** on the player, and the player
  tagged `Player`.
