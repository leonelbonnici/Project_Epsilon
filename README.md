# 3-Player Co-op Boss-Rush — Netcode for GameObjects + PlayMaker + Rewired

A 2D top-down pixel-art co-op game (up to 3 players) built on a reusable Unity template. Players connect over the internet via Unity Relay — no port forwarding. Gameplay is authored in visual scripting (PlayMaker); a thin C# "bridge" handles the things that genuinely need code (network lifecycle, synced state, RPCs, framework substrates).

## Stack

- **Unity 6.x**
- **Netcode for GameObjects (NGO 2.x)** — core networking, multi-scene management
- **Multiplayer Services SDK** (Sessions + Relay + Lobby) — online connection / matchmaking
- **Multiplayer Sessions Building Block** — drop-in session UI (Create / Quick Join / Join by Code)
- **Multiplayer Play Mode (MPPM)** — in-editor multi-client testing
- **PlayMaker** — visual scripting (all gameplay logic in FSMs)
- **Rewired** — input
- **EasySave 3** — cross-session persistence
- **Feel (MoreMountains MMFeedbacks)** — game-feel polish (hit reactions, screen shake, telegraphs)
- **Dialogue System for Unity (Pixel Crushers)** — NPC dialogue

## First-time setup

1. Open the project in Unity 6.x.
2. Paid assets may not be committed to the repo — install from the Asset Store / Package Manager if missing: PlayMaker, Rewired, Feel, EasySave 3, Dialogue System.
3. **Link to Unity Gaming Services** (REQUIRED for online play). A duplicated copy of this project is NOT automatically linked — each project needs its own link:
   - In the Editor: click the cloud icon (Services) → create or link a cloud project.
   - In the Unity Cloud Dashboard (cloud.unity.com): enable Relay and Lobby for that project.
   Without this, online sessions will not connect. (MPPM local testing still works.)
4. Install the Multiplayer Play Mode package to test multiple players in one editor.

## Running / testing

1. Open the **Bootstrap** scene (never open Hub or an Area scene directly — Bootstrap owns Persistent NetworkObjects).
2. Window > Multiplayer > Multiplayer Play Mode → enable 2 virtual players (3 total).
3. Press Play.
4. In each window, click Quick Join — the first window creates a Relay session, the others join it. The session UI hides once you're in.
5. Once in the Hub: WASD = move, Q = interact, K = melee, E = ranged.

**MPPM rule**: virtual players load scenes/prefabs from disk, not your unsaved editor state. Always Save (and Save Project) before pressing Play, or changes appear only in the main editor.

## Multi-scene architecture

The world is split into three layers of scenes:

- **Bootstrap** — the entry scene. Contains the NetworkManager, the Persistent scene loader, and the session UI. Never unloaded.
- **Persistent** — additively loaded from Bootstrap. Holds objects that must survive scene changes: `SceneFlowController`, `AreaStateManager`, `GameStateBridge`, party HUD canvas, boss HUD canvas, low-health vignette. Also never unloaded.
- **Gameplay scenes** — `Hub`, `Area_01`, `Area_02`, etc. Exactly one is loaded at a time. Contains the arenas, rooms, portals, NPCs, pickups — everything spatial.

Scene transitions run through `SceneFlowController.ServerTransitionToScene(sceneName, spawnPointName)` on the server. It unloads the current gameplay scene (after snapshotting persistable state), loads the target, teleports all players to the named spawn point via owner-authoritative `NetworkTransform.Teleport`, then restores any previously-snapshotted state for the newly-loaded scene.

**Key NGO configuration** (already set in the NetworkManager): `ClientSynchronizationMode = Additive` and `PostSynchronizationSceneUnloading = false`. Without these, connecting clients unload their Bootstrap scene and lose the Persistent contents.

## Persistence — `IPersistable` + `AreaStateManager`

Anything that survives a scene unload/reload implements `IPersistable`:

```csharp
public interface IPersistable
{
    string PersistenceId { get; }
    string CaptureState();
    void RestoreState(string state);
}
```

- **`PersistenceId`** — unique per object across the whole game (e.g. `"pickup:pickup_01a"`, `"arena:arena_01"`).
- **`CaptureState()`** — return a compact string (`"1"` / `"0"` or JSON for anything richer). Called on the server just before the scene unloads.
- **`RestoreState(string)`** — called on the server just after the scene reloads.

`AreaStateManager` (in Persistent) walks the loaded scene, calls `CaptureState` on every `IPersistable`, and writes to a single EasySave 3 dictionary entry. On scene reload it restores in the mirror direction. Only the *meaningful* state persists — e.g. `ArenaBridge.CaptureState` only saves the `Cleared` state; anything mid-encounter resets to `Idle`, so players can re-attempt.

## Rooms and arenas — `IRoom`

An area (e.g. Area_01) is made up of rooms. Every room implements `IRoom`:

```csharp
public interface IRoom
{
    string RoomId { get; }
    bool IsCompleted { get; }
    event Action<IRoom> RoomCompleted;
}
```

Doors reference other rooms by `roomId` as prerequisites (walk into the next room once the boss arena's `IsCompleted` is true). `AreaBridge` orchestrates the area-level flow, listens to `RoomCompleted` events, and drives the `IsAreaCleared` derived property + `EndAreaAltar` drop.

`ArenaBridge` is the boss-arena implementation. It owns the arena's `NetworkVariable<int> status` (values `Idle`, `InProgress`, `Cleared`, `Failed`), the boss prefab reference, and the encounter lifecycle (`ServerStartEncounter → SpawnBoss → OnBossDied → drop altar + revive downed`).

Filler-room types (`PickupRoom`, `PuzzleRoom`, `WalkthroughRoom`, `NpcRoom`) each implement `IRoom` with their own completion rules.

## Player bridge — `Scripts/Network/NetworkPlayMakerBridge.cs`

The single per-player networking substrate. Exposes:

- **Lifecycle events**: `NETWORK_SPAWNED`, `NETWORK_DESPAWNED`, `PLAYER_REVIVED`.
- **PlayMaker Get Property**: `IsLocalOwner`, `OwnerId`, `HealthValue`, `HealthNormalized`, `IsDowned`, `MaxHealth`.
- **Synced state**: `NetworkVariable<float> health`, `NetworkVariable<bool> isDowned`.
- **Damage entry**: `RequestDamageRpc` (any client), `ServerApplyDamage` (server-side direct), with damage immunity when downed.
- **Revive entry**: `ServerRevive(float hp)` — flips `isDowned` false, sets health, fires `DownedChanged` event and `PLAYER_REVIVED` PlayMaker event to all this player's FSMs.
- **Impulse entry**: `ServerApplyImpulse(Vector2 dir, float distance, float duration)` — server-side push/pull for boss knockback attacks.
- **Owner-authoritative teleport**: `ServerTeleportPlayer(Vector3 pos)` → routes through `[Rpc(SendTo.Owner)]` → `NetworkTransform.Teleport(pos, rot, scale)` + resets `Rigidbody2D.linearVelocity`. Direct `transform.position = pos` from the server doesn't work — the owner immediately overrides. This pattern is required for spawn placement, scene transitions, respawns, and cutscene movement.
- **C# events**: `HealthChanged`, `DownedChanged` — for other server systems to subscribe.
- **Team**: `Team.Player` (see `IDamageable` below).

Also on the Player prefab:

- **`DamageFlash`** — hit-feedback tint. Lazy color capture on first Flash() call (PlayerColor FSM applies per-player tint after Awake, so caching at Awake would grab the wrong color). Skipped on killing blow so `DownedTint` takes over cleanly.
- **`DownedTint`** — greys the sprite when downed. Caches the base color at first-down moment, restores on revive.
- **`DownedStateController`** — disables named PlayMaker FSMs and MonoBehaviour components on downed, switches Rigidbody2D to Kinematic, re-enables on revive and sends `PLAYER_REVIVED` to the re-enabled FSMs so they jump to their polling state.
- **`PlayerColor`** FSM — reads `OwnerId`, indexes a color array, tints the sprite (deterministic-from-OwnerId pattern).
- **`LowHealthVignette`** — client-side fullscreen UI Image alpha driven by local `HealthNormalized`, with optional Feel MMFeedback hooks for heartbeat audio.

## Death, revive, and party wipe

The full death/respawn flow lives across `NetworkPlayMakerBridge`, `DownedTint`, `DownedStateController`, and `ArenaBridge`:

- **Player HP hits 0** → `isDowned` flips true → damage immunity kicks in, downed tint applies, disabled FSMs stop, player can't act.
- **Boss dies with downed players in the arena** → `ArenaBridge.OnBossDied` calls `ReviveAllDownedPlayers(reviveOnBossDeathFraction)`, restoring downed players to a configurable HP fraction (default 25%). They stay at their fallen position — no teleport.
- **All players downed simultaneously (party wipe)** → `ArenaBridge` subscribes to each player's `DownedChanged` event on encounter start; the callback triggers `AreAllConnectedPlayersDowned()` after any player goes down. On wipe: broadcasts `ARENA_WIPED` FSM event (for cutscene/effect hooks), waits `wipeToHubDelay` seconds (default 2s), revives everyone at `reviveOnWipeFraction` (default 50%), resets *only that arena's* `status` to `Idle`, unsubscribes, and calls `SceneFlowController.ServerTransitionToScene` back to the Hub. All other persistable state (other arenas' cleared status, pickups, switches, NPCs) is untouched.

The revive-on-wipe fraction and delay are per-arena serialized fields, so different arenas can tune the penalty independently.

## Ready-up gating — `ReadyZone.prefab`

A drag-and-drop component that enforces "all players present" gating on any triggered action — boss gate entry, scene portals, locked doors, puzzle pressure plates.

`ReadyZone` is a NetworkBehaviour + IInteractable with:

- A trigger `Collider2D` that tracks players via a `NetworkList<ulong>` of client IDs currently inside.
- A `NetworkVariable<bool> currentlyAllReady` — flips true when every connected player is inside, false when anyone leaves or a new player joins.
- A `NetworkVariable<bool> hasFired` — for `oneShot = true` mode, prevents re-firing after the gate has been activated.
- A `NetworkVariable<int> totalConnected` — replicated player count so non-host clients see correct X/Y labels.
- `IsAvailable` — armed and unfired. Central `IsThisInteractableActive` in `InteractPrompt` reads this to show/hide the Q prompt.
- `UnityEvent onActivate` — invoked server-side when any player presses Q while armed. Wire this in the inspector to whatever should fire.

Package (`Prefabs/Gameplay/ReadyZone.prefab`): the trigger, NetworkObject, ReadyZone, ReadyZoneLabel (world-space X/Y count), InteractPrompt, and a prompt visual, all self-wired. Drop the prefab in, resize the collider, wire `onActivate` to your target method.

Existing wiring:

- Arena gate → `ArenaBridge.ServerStartEncounter`.
- Hub scene portal → `ScenePortal.ServerInitiateTransition`.

## Boss framework

Bosses are composed at the prefab level from three C# layers plus PlayMaker FSMs for presentation. Adding a new boss is a prefab-authoring job, not a coding job.

### Layer 1 — `BossBridge` (per-boss substrate)

Networked HP, phase, damage entry, death broadcast. Owns:

- `NetworkVariable<float> health` and `effectiveMaxHealth` (scaled by connected player count on spawn).
- `NetworkVariable<int> phase` — driven by an ordered `List<BossPhase>`:
```csharp
  [Serializable]
  public class BossPhase
  {
      public float enterAtHpFraction;  // ignored for phases[0]; HP threshold for later entries
      public float attackCooldown;      // seconds between attacks in this phase
  }
```
  `CheckPhase()` runs on every damage and advances by as many entries as HP has crossed, so a single massive hit can skip a phase cleanly.
- `AttackCooldown` property — reads the current phase's cooldown; the brain FSM's Cooldown state reads this via Get Property, so phase transitions automatically speed the boss up without any FSM changes.
- Broadcast events: `BOSS_SPAWNED`, `BOSS_HEALTH_CHANGED`, `BOSS_PHASE_CHANGED`, `BOSS_DIED`, plus `ATTACK_DONE` fired at the end of each attack cycle by `BossAttackBase`.
- `DiedRaised` C# event — `ArenaBridge` subscribes to detect encounter completion.
- `ServerBroadcastEvent(string)` — server-side entry point for firing arbitrary PlayMaker events to every client's copy of the boss FSMs.

### Layer 2 — `BossAttackSelector`

The picker. On every cooldown tick the brain FSM calls `ServerChooseAndFire`, which:

1. Filters attacks by `minPhase <= currentPhase`.
2. Weighted-random rolls among eligible attacks.
3. Calls `ServerExecute()` on the chosen attack component.

```csharp
[Serializable]
public class BossAttackOption
{
    public BossAttackBase attack;  // component on the same GameObject
    public float weight = 1f;
    public int minPhase = 0;
}
```

Weights are relative — one entry at 2, one at 1 → the first is picked twice as often as the second. Setting `minPhase = 1` gates that attack to phase 1+ (i.e. it becomes eligible only after the first HP-fraction threshold is crossed).

### Layer 3 — `BossAttackBase` + concrete attacks

Every attack is a `MonoBehaviour` extending `BossAttackBase`. Base class owns:

- Telegraph timing (`telegraphDuration`) and broadcast events (`telegraphStartedEvent`, `executedEvent`).
- `ServerExecute()` — the entry point selector calls. Runs `ExecuteRoutine` as a coroutine on the boss GameObject.
- `ExecuteRoutine` shape: broadcast telegraph event → wait telegraph duration → run subclass `DoExecute()` → broadcast executed event → broadcast `ATTACK_DONE`.
- Shared helpers: `IsValidTarget(IDamageable)` (filters out downed players and non-player teams), `GetNearestPlayer()`, `SpawnBossProjectile(dir, prefab, speed, damage)`, `Rotate(v, degrees)`.

Concrete attacks (in `Scripts/Network/Boss/Attacks/`):

- `BossAttack_Slam` — radial melee AoE
- `BossAttack_Shoot` — single projectile at nearest player
- `BossAttack_SpreadShot` — fan of projectiles at nearest player
- `BossAttack_BulletRing` — 360° radial projectile burst
- `BossAttack_Dash` — lerp toward nearest player, damaging on collision
- `BossAttack_Hazard` — instant hazard prefab at nearest player
- `BossAttack_LingeringHazard` — persistent hazard prefab at nearest player
- `BossAttack_PullPlayers` / `BossAttack_PushPlayers` — impulse all players toward/away from boss

Each attack's parameters live on its own component in the inspector — no shared "BossAttacks" component with fifty fields anymore.

### Presentation FSMs on the boss prefab

Four FSMs, each listening for events broadcast from the C# side:

- **BossBrain** — the combat loop. `Waiting → CheckServer` (server branch only, gated on `BossBridge.IsServerBrain`) `→ ChooseAttack` (Call Method → `BossAttackSelector.ServerChooseAndFire`) `→ Cooldown` (Get Property → `BossBridge.AttackCooldown`, Wait, loop back). No per-attack states.
- **BossHealth** — reads `BOSS_HEALTH_CHANGED` and `BOSS_DIED`. Where death animation, victory sting, and HUD hooks live.
- **BossPhase** — reads `BOSS_PHASE_CHANGED`, routes to per-phase states (Phase1, Phase2, Phase3). Where phase-transition visuals live (recolor, roar, particle burst).
- **BossTelegraph** — reads `BOSS_TELEGRAPH`. Where the wind-up visual lives (sprite color flash, warning indicator, telegraph SFX). Every attack broadcasts the same string, so this one FSM handles all wind-ups.

## Damage, teams, and hitboxes — `IDamageable`

Any object that can take damage implements:

```csharp
public interface IDamageable
{
    Team Team { get; }
    void ServerApplyDamage(float amount);
}
```

`Team` is `Player`, `Enemy`, or `Neutral`. Filters projectiles, AoE, and boss targeting cleanly — a boss doesn't damage other bosses, a player doesn't damage other players, etc.

`Projectile` (used by player ranged and by all boss projectiles) is configured with `direction`, `speed`, `damage`, and `targetTeam` — only hits colliders whose `IDamageable.Team` matches the target.

## Networking patterns established (reuse these)

- **Deterministic-from-OwnerId** — for values that never change at runtime but must be consistent across clients (player color, spawn position). Compute from the synced `OwnerId`; no NetworkVariable needed, because every client derives the same answer.
- **NetworkVariable** — for persistent synced state (health, score, phase). Declare in a bridge, expose a read property + a `...Changed` event, react in PlayMaker or C#. Write permission: `Server` for trusted state, `Owner` for simple/trusted-client values. **Anything a non-host client needs to read must be a NetworkVariable, not a plain field** — plain fields aren't replicated and non-host clients see stale defaults (this bit us on `ReadyZone`).
- **ServerRpc** (`[Rpc(SendTo.Server)]`) — client requests an action; the server validates and applies. Anti-cheat. Method name must end in `Rpc`. Add `RequireOwnership = false` when the client isn't the owner of the target object (e.g. GameManager or Boss, which are server-owned).
- **ClientRpc** (`[Rpc(SendTo.ClientsAndHost)]`) — server announces a one-shot event to all clients (effects, PlayMaker events, sounds). Method name must end in `Rpc`.
- **Owner-authoritative teleport** — for any forced position change (spawn, respawn, scene transition, knockback, cutscene), route through `[Rpc(SendTo.Owner)]` → `NetworkTransform.Teleport(pos, rot, scale)` + `Rigidbody2D.linearVelocity = zero`. Direct `transform.position` from the server is instantly overridden by the owner. Interpolation is bypassed cleanly.
- **Spawning networked objects** — only the server may spawn. The server `Instantiate`s a registered prefab and calls `NetworkObject.Spawn()`; clients ask via a ServerRpc. Removing one is the mirror: `NetworkObject.Despawn()`. Register every networked prefab in the NetworkPrefabsList — unregistered = spawn silently fails.
- **In-scene NetworkObjects in persistent scenes** cause sync failures for clients that connect late — spawn them dynamically instead. This is why the boss is instantiated by `ArenaBridge` rather than sitting in the Area scene.
- **Cross-FSM PlayMaker events from C#** — must be declared as **Global** events in the target FSM's Events tab, or the broadcast is silently dropped.
- **Lazy color capture** — don't read sprite colors in Awake if a PlayMaker FSM tints them on spawn (PlayerColor runs after Awake). Defer to first-use (first Flash call, first downed moment).
- **NetworkVariable OnValueChanged serves as a C# → FSM bridge** — subscribe in the bridge's `OnNetworkSpawn`, fire a PlayMaker event on change. Unsubscribe in `OnNetworkDespawn`.

## Adding a new boss attack

Concrete walkthrough. Say you want to add a "Ground Slam Ripple" — a slam that also spawns a ring of hazards outward.

### 1. Write the attack script

`Assets/_Project/Scripts/Network/Boss/Attacks/BossAttack_SlamRipple.cs`:

```csharp
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BossAttack_SlamRipple : BossAttackBase
{
    [UnityEngine.Tooltip("Central slam radius (immediate melee AoE).")]
    public float slamRadius = 3f;
    [UnityEngine.Tooltip("Central slam damage.")]
    public float slamDamage = 20f;

    [UnityEngine.Tooltip("Number of hazard prefabs in the outward ring.")]
    public int rippleCount = 8;
    [UnityEngine.Tooltip("Distance from boss to each ring hazard.")]
    public float rippleRadius = 5f;
    [UnityEngine.Tooltip("Hazard prefab (networked). Must be in NetworkPrefabsList.")]
    public GameObject rippleHazardPrefab;

    protected override IEnumerator DoExecute()
    {
        // Central slam
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, slamRadius);
        foreach (Collider2D hit in hits)
        {
            IDamageable d = hit.GetComponentInParent<IDamageable>();
            if (IsValidTarget(d)) d.ServerApplyDamage(slamDamage);
        }

        // Ripple ring
        if (rippleHazardPrefab != null && rippleCount > 0)
        {
            float step = 360f / rippleCount;
            for (int i = 0; i < rippleCount; i++)
            {
                float r = i * step * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f) * rippleRadius;
                GameObject obj = Instantiate(rippleHazardPrefab, transform.position + offset, Quaternion.identity);
                obj.GetComponent<NetworkObject>().Spawn();
            }
        }

        yield break;  // instant attack — no per-frame loop needed
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rippleRadius);
    }
}
```

Notes:
- Extends `BossAttackBase`. Never extends `NetworkBehaviour` directly — the base doesn't need to be networked, all networking goes through the boss's `BossBridge`.
- `DoExecute` returns `IEnumerator`. For instant attacks, `yield break` at the end. For attacks that need per-frame logic over time (like `BossAttack_Dash`), yield `null` inside a while loop.
- Server-only — the base class's `ServerExecute` already gates on `boss.IsServer` before starting the coroutine, so `DoExecute` runs server-side by definition.
- Use the base's `IsValidTarget`, `GetNearestPlayer`, `SpawnBossProjectile`, `Rotate` where possible.
- If you spawn networked objects, they must be registered in NetworkPrefabsList — otherwise `.Spawn()` silently fails.

### 2. Attach to a boss prefab

Open the boss prefab in prefab-edit mode:

1. Select the boss root.
2. **Add Component → Boss Attack_Slam Ripple**.
3. Fill in the inspector fields: `slamRadius`, `slamDamage`, `rippleCount`, `rippleRadius`, `rippleHazardPrefab`.

### 3. Configure telegraph

On the same component, in the Telegraph section:

- **Telegraph Duration**: how long the wind-up is (e.g. `0.8f` for a heavy attack).
- **Telegraph Started Event**: `BOSS_TELEGRAPH` (routes through the shared telegraph FSM, giving you the default color-flash visual). If this attack needs its own unique visual, pick a distinct string like `SLAMRIPPLE_TELEGRAPH` and add a matching transition in `BossTelegraph`'s Waiting state.
- **Executed Event**: leave empty, or fill with something like `SLAMRIPPLE_EXECUTED` if you want to hook a post-impact effect (camera shake, ground-crack sprite spawn).

### 4. Register with the selector

On the same prefab, on `BossAttackSelector`:

1. Expand `Attacks` list.
2. Click `+` to add an entry.
3. **Attack**: drag `BossAttack_SlamRipple` from the same prefab's inspector into the slot (drag the component header row, not the whole GameObject).
4. **Weight**: relative frequency. `1` = same as other 1-weight attacks. `0.5` = half as often. `2` = twice as often.
5. **Min Phase**: `0` = always eligible. `1` = phase 1 or later only. `2` = phase 2 only.

### 5. Save prefab and test

Save. MPPM in. When the boss's cooldown expires and the selector rolls this attack, the shared telegraph fires, the wind-up plays, and after the telegraph duration the slam + ripple executes.

### 6. (Optional) Author custom presentation

If `BOSS_TELEGRAPH` isn't distinctive enough for this attack, extend `BossTelegraph`:

1. In `BossTelegraph`'s Events tab, add `SLAMRIPPLE_TELEGRAPH`. Mark **Global**.
2. In the `Waiting` state, add a new transition on `SLAMRIPPLE_TELEGRAPH` → a new state `SlamRippleWarn`.
3. In `SlamRippleWarn`, author the specific visual (concentric circle indicator, red glow at boss feet, whatever).
4. Change the attack's `Telegraph Started Event` field to `SLAMRIPPLE_TELEGRAPH`.

Same pattern for `Executed Event` if you want post-impact effects.

## Adding a new boss

The framework is designed so a new boss is prefab work + FSM authoring, zero framework changes.

1. **Duplicate an existing boss prefab**. Rename (e.g. `Boss_02`, `Boss_FinalArea1`).
2. **Change the sprite**. Change the base color if you want a different tint.
3. **Adjust the attack loadout**:
   - Remove `BossAttack_*` components this boss won't use.
   - Add `BossAttack_*` components this boss will use.
   - Update `BossAttackSelector.attacks` list to match. Every component in the list must be on the prefab; every attack the boss should be able to do must be in the list.
   - Tune weights and `minPhase` gates per boss.
4. **Adjust the phase list on `BossBridge`**:
   - Number of phases matches boss complexity — 2 for a straightforward boss, 3 for a difficult one, 1 for a mini-boss.
   - Set `enterAtHpFraction` thresholds (descending: e.g. 1, 0.66, 0.33 for 3 phases).
   - Set `attackCooldown` per phase — later phases usually shorter for pacing.
5. **Adjust `maxHealth` on `BossBridge`**. This scales by player count at spawn.
6. **Customize presentation FSMs** as needed:
   - `BossPhase` — add or edit per-phase states for phase-transition visuals.
   - `BossHealth` — edit the `OnDeath` state for death animation.
   - `BossTelegraph` — usually fine as-is; only edit if this boss needs unique telegraph visuals.
   - `BossBrain` — usually never edit; it's generic. Only touch if you need a boss-specific quirk (e.g. a special "enraged" state on low HP that changes selection logic).
7. **Register the new boss prefab in NetworkPrefabsList**.
8. **Assign the prefab** to whichever `ArenaBridge` should spawn it (drag into `bossPrefab` field).

The whole flow, from duplication to a fightable boss, is usually under an hour once the attack scripts you need already exist.

## Adding a new interactable

`IInteractable` implementations follow one pattern:

```csharp
public interface IInteractable
{
    void ServerOnInteract(NetworkPlayMakerBridge interactor);
}
```

- Add the component to a GameObject with a Collider2D (trigger or solid — the player's interact scan uses OverlapCircle).
- Implement `ServerOnInteract`. Guard with `if (!IsServer) return;` if the class isn't already a NetworkBehaviour that only writes state server-side.
- Optionally add an `IsAvailable` property, and add a matching case to `InteractPrompt.IsThisInteractableActive` — the Q prompt will auto-show only when available.
- Optionally implement `IPersistable` if the state must survive scene transitions.

Existing examples: `PickupCollectible`, `PuzzleSwitch`, `NpcInteractable`, `EndAreaAltar`, `HealInteractable`, `ReadyZone`.

## Input (Rewired)

Single game player: Player 0.

Actions:
- `MoveHorizontal` (A/D), `MoveVertical` (W/S)
- `Interact` (Q)
- `Melee` (K), `RangedFire` (E)
- `TestScore` (P) — scaffolding, remove for release

PlayMaker reads these via the Rewired PlayMaker integration actions.

## Folder structure

Assets/
_Project/
Audio/            (Music, SFX)
FSMs/             (reusable FSM templates)
Prefabs/
Players/        (networked Player prefab)
Bosses/         (Boss_Base and per-boss variants)
Enemies/
Pickups/        (networked, server-spawned)
Projectiles/    (player + boss projectile prefabs)
Hazards/        (instant + lingering hazard prefabs)
Gameplay/       (ReadyZone.prefab, other reusable gameplay prefabs)
UI/
Scenes/
Bootstrap/      (entry — session UI, NetworkManager)
Persistent/     (SceneFlowController, AreaStateManager, HUDs)
Hub/
Area_01/, Area_02/, ...
ScriptableObjects/
Scripts/
Network/
NetworkPlayMakerBridge.cs
NetworkObjectSpawner.cs
GameStateBridge.cs
SceneFlowController.cs
AreaStateManager.cs
AreaBridge.cs
ArenaBridge.cs
ScenePortal.cs
ReadyZone.cs
Boss/
BossBridge.cs
BossAttackSelector.cs
BossAttackBase.cs
Attacks/
BossAttack_Slam.cs
BossAttack_Shoot.cs
BossAttack_Dash.cs
BossAttack_SpreadShot.cs
BossAttack_BulletRing.cs
BossAttack_Hazard.cs
BossAttack_LingeringHazard.cs
BossAttack_PullPlayers.cs
BossAttack_PushPlayers.cs
Interfaces/
IPersistable.cs
IRoom.cs
IInteractable.cs
IDamageable.cs
Interactables/
PickupCollectible.cs
PuzzleSwitch.cs
NpcInteractable.cs
EndAreaAltar.cs
HealInteractable.cs
UI/
PartyHealthHUD.cs
PartyMemberSlot.cs
BossHealthHUD.cs
LowHealthVignette.cs
ReadyZoneLabel.cs
InteractPrompt.cs
FX/
DamageFlash.cs
DownedTint.cs
DownedStateController.cs
Utility/
Sprites/
Blocks/
MultiplayerSession/
PlayMaker/  Rewired/  Feel/  EasySave3/  PixelCrushers/

## Current phase / known TODOs

- **Content build-out** — six themed areas, more bosses using the boss framework, filler-room variety.
- **Pause menu + leave-session button** — the Building Block's generic session UI is hidden on join; needs a proper in-game menu.
- **Final boss / win condition** — undecided.
- **Mid-fight revive mechanic** (healer ability or pickup) — planned, out of scope for framework phase.
- **Per-area checkpoints** — currently a wipe always returns to Hub; late-game areas may want arena-only reset.
- **Combo attacks in the boss framework** — currently one-shot only. A `BossAttack_Combo` component that runs multiple sub-attacks in sequence is the natural extension.