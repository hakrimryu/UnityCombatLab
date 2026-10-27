# Unity Combat Lab

[日本語](README.md) | [한국어](README.ko.md) | [English](README.en.md)

**Contents:** [Overview](#project-overview) · [Environment](#environment-and-libraries) · [Fulcrum](#about-fulcrum) · [Code structure](#code-structure) · [Verification](#tests-and-verification-results) · [Implementation details](#implementation-and-design-decisions) · [Running the project](#running-the-project-and-restoring-assets)

## Project overview

**A Unity game client portfolio project where you position three heroes, choose upgrades between waves, and fight through five waves of automatic combat.**

The placement of the Knight, Ranger, and Mage in the front and back rows, together with your upgrade choices, changes the outcome. The game implements a complete play loop: title → formation → combat → upgrade selection → results → retry. The game UI is in Japanese. This README explains the code and the decisions behind its design. A **Run** is one attempt, from departure after formation selection through victory, defeat, or an early exit.

Purchased assets are excluded from the repository, so running the project in Unity requires [restoring the assets and references](#running-the-project-and-restoring-assets).

> **This portfolio project is still in development and is not a finished release.** The main game flow and Fulcrum balance data integration are implemented. Sound, stability checks, and further improvements to the game logic remain future work.

![Wave 5 combat at 2× speed, showing HP, shields, and damage numbers](Docs/Images/battle-wave5.png)

## Environment and libraries

These are the installed versions verified in this project. UPM configuration is in [manifest.json](Packages/manifest.json) and [packages-lock.json](Packages/packages-lock.json); NuGet configuration is in [packages.config](Assets/packages.config).

| Technology | Version | Usage and rationale |
|---|---|---|
| Unity / C# | **6000.3.8f1** | Scenes, prefabs, Animator, and game client implementation. [ProjectVersion](ProjectSettings/ProjectVersion.txt) |
| Universal Render Pipeline | **17.3.0** | Rendering the 3D combat stage and characters. [Rendering settings](Assets/UnityCombatLab/Settings) |
| uGUI / TextMeshPro | **Included in uGUI 2.0.0** | Explicit View connections, Japanese UI, and numeric displays. [UI code](Assets/UnityCombatLab/Scripts/Presentation/UI) |
| VContainer | **1.19.0** | Application and scene object composition, plus EntryPoint registration. Container access is concentrated in [Bootstrap](Assets/UnityCombatLab/Scripts/Bootstrap) |
| UniTask | **2.5.11** | Asset preparation, scene transitions, next-frame waits, and cancellation. [RunFlowCoordinator](Assets/UnityCombatLab/Scripts/Application/Run/RunFlowCoordinator.cs) |
| R3 | **1.3.1** — core + Unity integration | Subscribing to HP, shield, and projected HP changes, with explicit subscription cleanup. [BattleHudPresenter](Assets/UnityCombatLab/Scripts/Presentation/UI/BattleHudPresenter.cs) |
| DOTween Standard | **1.3.045** | UI popups, button feedback, movement, hit reactions, and damage numbers. [UiTweenUtility](Assets/UnityCombatLab/Scripts/Presentation/UI/UiTweenUtility.cs), [UnitView](Assets/UnityCombatLab/Scripts/Presentation/Units/UnitView.cs) |
| Addressables | **2.8.1** | Loading local unit definitions, prefabs, UI, and icons. [RunAssetScope](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/RunAssetScope.cs) |
| Input System | **1.18.0** | EventSystem UI input and Esc input. [RunBattleView](Assets/UnityCombatLab/Scripts/Presentation/UI/RunBattleView.cs) |
| Unity Test Framework / NUnit | **1.6.0 package** | EditMode tests for combat and progression; PlayMode tests for Unity objects and asset lifetimes. [Tests](Assets/UnityCombatLab/Tests) |
| NuGetForUnity | **4.5.0** | Restoring R3 core and its .NET dependencies |
| Fulcrum | **0.4.0 / MIT / embedded** | Authoring wave values, exporting Tables, and converting them into Run inputs. [Package source](Packages/FULCRUM_SOURCE.md) |

Development tools: Rider **2026.2.3.1** / Unity Rider Editor **3.0.39**, and Unity CLI **1.0.0-beta.14** / Pipeline **0.9.1-exp.1**. CLI and MCP were used for Editor operations and verification.

## Game structure

[Full gameplay video (MP4 · 62 s · 1.9 MB)](Docs/Videos/gameplay-demo.mp4)

- **Formation:** Place the Knight, Ranger, and Mage in the front slot and the two back slots (upper and lower). Select cards and slots to swap positions.
- **Combat:** Three allies automatically fight up to three enemies. The Knight reduces incoming damage, the Ranger periodically performs a stronger single-target attack, and the Mage periodically performs an area attack.
- **Upgrades:** After winning each intermediate wave, choose from up to three offers. There are six upgrade types: attack power, maximum HP, attack speed, shield, healing on a kill, and opening attack power. Each can stack up to three times.
- **Progression:** Allies revive and recover to their upgraded maximum HP at the start of the next wave. Upgrades last for one run and reset on a new attempt.
- **Controls:** Switch between 1× and 2× speed, pause and resume, or confirm an exit. Losing focus automatically pauses the game; resuming requires player input.

## Key problems and approaches

Even in a small auto-battler, **the moment combat is resolved and the moment it appears on screen** can differ, as can **the moment a screen closes and the moment its assets finish loading**. Reproducing the same battle also does not automatically reveal an unintended rule change. The implementation and tests focus on these four problems.

| Problem | Approach | Key code |
|---|---|---|
| HP drops before a projectile hits, and damage and healing appear out of order | Separate resolved combat state from display state, preserving the order of changes across healing events | [UnitPresentationReadModel](Assets/UnityCombatLab/Scripts/Presentation/ReadModels/UnitPresentationReadModel.cs), [BattlePresenter](Assets/UnityCombatLab/Scripts/Presentation/Units/BattlePresenter.cs) |
| A load completes after cancellation or screen closure, leaving asset cleanup without a clear owner | Transfer ownership of each successful load to a Lease, and immediately release results that arrive after their Scope has ended | [AddressableAssetScope](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/AddressableAssetScope.cs), [RunFlowCoordinator](Assets/UnityCombatLab/Scripts/Application/Run/RunFlowCoordinator.cs) |
| An incorrectly changed combat rule still produces matching results when replayed by the same code | Combine reproducibility checks for deterministic 20Hz combat with regression tests against independent, fixed expectations | [BattleRunner](Assets/UnityCombatLab/Scripts/Application/Battle/BattleRunner.cs), [RunReplayTests](Assets/UnityCombatLab/Tests/EditMode/RunReplayTests.cs) |
| Input from an old screen or previous Run state leaks into the next step, duplicating upgrades or preventing a fresh retry | Validate the Run ID, phase, and offers, and separate authoring data from each attempt's runtime state | [RunController](Assets/UnityCombatLab/Scripts/Application/Run/RunController.cs), [RunProgress](Assets/UnityCombatLab/Scripts/Domain/Run/RunProgress.cs) |

Responsibilities are separated so combat rules, presentation timing, and asset loading can each be changed and checked independently. Layer assemblies and DI connect implementations, presentation read models hold what the HUD displays, and asset Scopes define cleanup ownership. The aim is to make **the code to change and the tests to check explicit**. Smaller requirements, such as combat slots and state transitions, use fixed arrays and enums.

## About Fulcrum

**Fulcrum is a game balance authoring tool for the Unity Editor that I develop and maintain independently.** It lets you edit growth curves and numeric data while viewing them as graphs, then export the results as Tables or CSV files that a game can read. I built it to manage frequently adjusted balance data separately from game code and make changes in the numbers easy to see.

A Profile is the authoring source containing axes, tracks, and calculation models. A BalanceTable is the output obtained by evaluating that Profile. Fulcrum also has general simulation and validation features, while each game's combat core remains responsible for its actual rules and result verification.

## How this project uses Fulcrum

Unity Combat Lab uses Fulcrum to manage **enemy HP, attack power, and upgrade strength for each wave**. Instead of changing numbers inside the game's C# code, you edit the standard and easy difficulty Profiles, re-export the Tables, and apply them to the next Run. The following shows the actual Profile connected to this project and its workflow.

![The actual Fulcrum editor with UnityCombatLab's standard Profile: tracks on the left, enemy HP by wave in the center, and model and input settings on the right](Docs/Images/fulcrum-balance-editor.png)

The screenshot shows the `enemy_hp` track in the game's `BalanceProfile_Standard`. The horizontal axis covers **waves 1–5**, and the vertical axis shows **enemy HP before role coefficients**. The current model is **Hand-written values**, with five explicit entries. Expanding `Values` on the right and editing a wave's value also updates the graph. Increasing wave 1 HP, for example, increases the number of attacks needed to defeat its enemies.

### From the graph to actual combat

```mermaid
flowchart LR
    P[Edit Profile] --> E[Editor validation · Export Table]
    E --> T[Standard · Easy BalanceTable]
    T --> A[Start Run · Validate and round selected Table]
    A --> S[Fixed RunGameData snapshot]
    S --> B[Existing BattleSimulation]
    S --> R[Replay verification with the same inputs]
```

[RunBalanceExporter](Assets/UnityCombatLab/Editor/RunBalanceExporter.cs) validates the Profile's `wave` axis from 1–5, its required tracks, and the relationship between standard and easy difficulties. Tables do not store axis names, so [FulcrumBalanceAdapter](Assets/UnityCombatLab/Scripts/Infrastructure/Balance/FulcrumBalanceAdapter.cs) checks for missing tracks, insufficient ranges, NaN/Infinity, and unsafe input values when the game starts a Run. Invalid values reject startup instead of falling back to defaults, using the existing asset cleanup and error display path.

[RunDefinition](Assets/UnityCombatLab/Scripts/Infrastructure/Balance/RunDefinition.cs) selects the difficulty Table and combines it with the enemy slots in [WaveDefinition](Assets/UnityCombatLab/Scripts/Infrastructure/Balance/WaveDefinition.cs) to create pure C# [RunGameData](Assets/UnityCombatLab/Scripts/Domain/Run/RunGameData.cs). Fulcrum types do not enter Domain/Application, and the game's direct runtime dependency is limited to Infrastructure referencing `Fulcrum.Core`. Active Runs and replay records use this fixed snapshot, so later Profile edits cannot change them.

**This graph shows input values; it does not predict the game's win or loss.** The `Simulation not configured` message at the bottom means this Profile does not configure Fulcrum's general simulator. The existing `BattleSimulation` runs game combat, and the game's `RunReplayVerifier` checks reproducibility. The two simulators are not synchronized, and there is no feature that overwrites active game values in real time.

### Values adjusted in this game

| Track | Value applied in the game | Initial standard Profile values for waves 1–5 |
|---|---|---|
| `enemy_hp` | Base enemy HP before role coefficients | 420 / 500 / 600 / 700 / 800 |
| `enemy_attack` | Base enemy attack before role coefficients | 12 / 14 / 17 / 20 / 24 |
| `reward_strength` | Multiplier applied to an upgrade's base effect when selected | 1.00 / 1.10 / 1.20 / 1.30 / 1.40 |

`reward_strength` is **the strength multiplier for a selected upgrade**, rather than a currency reward for defeating an enemy. If the base attack upgrade grants +15% and the wave multiplier is 1.10, that choice is fixed at +16.5% and stored in the Run. The wave 5 value is also present in the Table, although there is no upgrade selection after the final victory.

### Applying edited values to the game

1. Open **Window → Fulcrum → Balance Editor** in Unity.
2. Assign [BalanceProfile_Standard](Assets/UnityCombatLab/Data/Balance/BalanceProfile_Standard.asset) to the top `Profile` field, select a track on the left, and edit the model's `Values` on the right. The entries correspond to waves 1–5 in order.
3. Update [BalanceProfile_Easy](Assets/UnityCombatLab/Data/Balance/BalanceProfile_Easy.asset) so enemy HP and attack are 80% of standard and upgrade strength is identical. The two Profiles do not synchronize automatically.
4. **Exit Play mode, then run Tools → UnityCombatLab → Export Run Balance Tables.** This validates both Profiles and updates the existing standard and easy Tables. Use this game-specific menu instead of the graph window's general `Export Table` button to check the axis, input ranges, and difficulty ratio as well.
5. Enter Play mode, select a difficulty on the formation screen, and **start a new Run**. It reads the latest Table for the selected difficulty.

**Editing and saving a Profile alone does not apply the change to the game.** You must explicitly export the Table, and an active Run retains the values it read at startup. `BalanceTable_Standard` and `BalanceTable_Easy` are generated outputs; edit the Profiles instead.

For example, changing standard wave 1 HP from `420 → 500` requires matching easy from `336 → 400` before re-exporting. Melee enemies in the next Run start with 500 / 400 HP respectively. Changes to outcomes and clear times are then checked with the actual game core.

### Input conversion and package layout

The easy difficulty Profile uses **standard enemy HP and attack ×0.8**, with identical upgrade strength. HP and attack are converted to integers with `MidpointRounding.AwayFromZero` when reading the Table, giving easy base attack values of `10 / 11 / 14 / 16 / 19`. Existing integer flooring still applies to ranged/captain role coefficients and damage calculations afterward. Easy wave 1 attack, for example, is `12 × 0.8 = 9.6 → 10`; the game does not apply another 80% multiplier.

Fulcrum **0.4.0** is embedded under `Packages/com.hakrimryu.fulcrum`, containing only the package from the verified source revision. It does not depend on a personal machine's absolute path or access to a private repository. The revision and source are recorded in [FULCRUM_SOURCE](Packages/FULCRUM_SOURCE.md), and the MIT copyright and permission notice are preserved in [LICENSE](Packages/com.hakrimryu.fulcrum/LICENSE.md). Integration regression tests are in [FulcrumBalanceAdapterTests](Assets/UnityCombatLab/Tests/EditMode/FulcrumBalanceAdapterTests.cs).

## Code structure

Combat rule changes, impact timing changes, and changes to asset loading each have distinct places in the code. These responsibilities are organized using **Clean Architecture Lite + MVP/Passive View**, with R3 for frequently changing HUD values. Clicks and screen transitions use C# events and explicit method calls.

The arrows below show the main code dependencies. References to Common enums and fixed conventions are omitted.

```mermaid
flowchart TD
    B[Bootstrap / VContainer] --> P[Presentation / View · Presenter]
    B --> A[Application / Run · BattleRunner · Ports]
    B --> I[Infrastructure / Assets · Scenes · Data]
    P --> A
    P --> D[Domain / Combat · Rewards · Replay]
    I --> A
    I --> D
    A --> D
```

The [Domain asmdef](Assets/UnityCombatLab/Scripts/Domain/UnityCombatLab.Domain.asmdef) sets `noEngineReferences: true` and references only Common. Combat rules and replay verification can run without a Unity view. [Application](Assets/UnityCombatLab/Scripts/Application/UnityCombatLab.Application.asmdef) also has no UnityEngine reference, but uses UniTask and R3 for progression, asynchronous flow, and read models.

Wave progression policy lives in [RunBattleCoordinator](Assets/UnityCombatLab/Scripts/Application/Run/RunBattleCoordinator.cs), while [RunBattleEntryPoint](Assets/UnityCombatLab/Scripts/Bootstrap/RunBattleEntryPoint.cs) connects Unity object creation, frame updates, and focus input. Conditions for advancing to the next wave can therefore be tested without setting up a scene or characters.

**VContainer is used where concrete implementations are assembled.** [AppLifetimeScope.Configure](Assets/UnityCombatLab/Scripts/Bootstrap/AppLifetimeScope.cs) connects the flow coordinator to scene transitions, an asset factory, frame waiting, and error reporting:

```csharp
builder.Register(resolver => new RunFlowCoordinator
(resolver.Resolve<SceneTransitionManager>(),
    loading,
    configuration => new RunAssetScope(resolver.Resolve<AddressableAssetManager>(), _uiAssets, _unitAssets, _runDefinition, configuration),
    () => UniTask.NextFrame(),
    Debug.LogException,
    value => AudioListener.volume = value), Lifetime.Singleton);
builder.RegisterEntryPoint<AppEntryPoint>().AsSelf();
```

Tests replace these connections with doubles to control when loads and frame waits complete. Domain does not query the container. VContainer's scene Scopes and Run asset lifetimes are managed separately: assets needed for an entire Run are distinguished from combat objects replaced each wave, [as shown in the lifetime table in the implementation details](#2-the-screen-has-closed-but-its-assets-finish-loading-later).

Data structures and state representations match the actual scale of the game. [BattleSimulation.FindTarget](Assets/UnityCombatLab/Scripts/Domain/Battle/BattleSimulation.cs) examines at most three enemy slots in a fixed order. Dictionaries are used where a Unit ID is needed to look up a View. `RunController` represents phases with an enum and explicit checks for allowed transitions.

## Tests and verification results

**These checks were executed in the Unity Editor on 2026-10-27, including the Fulcrum integration.** Actual combat advanced on Editor frames, with development input APIs selecting formation, speed, and upgrades. This was not a full automated mouse interaction test.

| Check | Result |
|---|---|
| EditMode | **177 / 177 passed** |
| PlayMode | **47 / 47 passed** |
| Actual Editor combat — standard | Victory across 5/5 waves, **1090 ticks / 455 events**, replay **MATCH** (`3F80450CE314E9DB`) |
| Actual Editor combat — easy | Victory across 5/5 waves, **872 ticks / 371 events**, replay **MATCH** (`E504B1BB12FD5095`) |
| Departure with an invalid Table | Missing required tracks, insufficient ranges, and abnormal values rejected; missing Table releases acquired assets and returns to the error screen |
| Runtime environment | Unity 6000.3.8f1, Addressables Asset Database / Fast Mode |

The actual play result of 1090 ticks/455 events and the test-specific fixed input result of 386 ticks/152 events come from different inputs.

Relevant test code is linked from each example in [Implementation and design decisions](#implementation-and-design-decisions) below. Besides numeric calculations, the checks cover presentation order, late async completion, duplicate input, and retries.

The following also confirms that Fulcrum output reaches actual combat. The formation is Mage/Ranger/Knight, the seed is `20261024`, and the upgrade order is attack → attack → healing on a kill → attack.

| Verification stage | Observed behavior |
|---|---|
| Re-export Profile after starting a standard Run | Change wave 1 HP from 420 to 1,000,000 for verification, matching easy at 80%, and export. The active Run retains HP 420 and wins with its original result |
| New standard Run after the change | Reads HP 1,000,000 from the new Table, changing the result to **timeout at 1800 ticks / 501 events**. Replay **MATCH** (`3646720722658195`) |
| Easy Run after restoring the base values | Starts wave 1 at HP 336 and attack 10, wins all five waves, and cleans up units after completion |

1,000,000 HP is a **verification value used to prove that an input change affects the actual result**, rather than a suggested balance setting. This re-export check used development Editor APIs; the normal authoring menu runs in Edit mode as described above. After verification, all Profiles, Tables, and their meta files were restored byte-for-byte to their previous contents.

The current submission scope is the GitHub repository. Android/WebGL builds, deployment, and device performance checks are on hold and are not replaced by these Editor results. Full restoration from a fresh clone without the purchased assets has not been verified either.

## Implementation and design decisions

The code blocks below are excerpts from the implementation. Each example links to the full source and relevant tests.

### 1. The hit is resolved, but the arrow has not arrived

**Problem:** Updating the HUD as soon as a combat tick calculates damage makes HP drop before the arrow hits. Moving damage calculation into an animation or Tween completion callback would instead make combat rules depend on animation duration and playback speed.

**Decision:** Damage, death, and victory or defeat are resolved in the pure C# combat core. The screen presents those results at impact time. Application's `BattleReadModel` holds the resolved state, while Presentation's `BattlePresentationReadModel` holds the state to display. [BattlePresentationTimeline](Assets/UnityCombatLab/Scripts/Presentation/Units/BattlePresentationTimeline.cs) schedules attack actions and impacts, and [BattlePresenter](Assets/UnityCombatLab/Scripts/Presentation/Units/BattlePresenter.cs) connects the units and HUD.

Simply displaying every event in receipt order would make an earlier melee hit wait for a slower magic projectile. Instead, **damage can be displayed in impact order, while changes before and after a heal preserve their resolved order.** [UnitPresentationReadModel.PresentChange](Assets/UnityCombatLab/Scripts/Presentation/ReadModels/UnitPresentationReadModel.cs) manages this ordering. It applies HP changes before raising `ChangePresented`, which triggers the corresponding damage number and hit reaction.

For example, a test starts with `100 HP → 16 damage → 8 healing → 16 damage`. Even if the final hit's presentation arrives first, HP is displayed as `100 → 84 → 92 → 76`. The test checks that later damage does not jump ahead of healing, and that the notification used to display damage numbers follows the HP update.

**R3 delivers changes in this display state to the HUD.** The subscription in [BattleHudPresenter](Assets/UnityCombatLab/Scripts/Presentation/UI/BattleHudPresenter.cs) is:

```csharp
unit.Health.Subscribe(value =>
{
    if (!_isDisposed && status != null)
    {
        status.SetHealth(value, unit.Input.MaxHealth);
    }
}).AddTo(_subscriptions);
```

The Presenter owns `_subscriptions` and releases them in `Dispose()`. Maintaining separate resolved and display states adds bookkeeping, but allows presentation timing to change without changing combat calculations or their tests. The game advances to the next wave or results screen after the final impacts and death presentations finish.

**DOTween handles the visual effects in this flow.** It is used for movement toward a target, return movement, and hit reactions in [UnitView](Assets/UnityCombatLab/Scripts/Presentation/Units/UnitView.cs), damage numbers in [DamageNumberView](Assets/UnityCombatLab/Scripts/Presentation/UI/DamageNumberView.cs), and UI popups. Shared UI uses independent timing so menus remain responsive while combat is paused. This is the return expression from [UiTweenUtility.PopIn](Assets/UnityCombatLab/Scripts/Presentation/UI/UiTweenUtility.cs):

```csharp
return DOTween.Sequence()
    .Append(content.DOScale(Vector3.one, seconds).SetEase(Ease.OutBack, 1.7f))
    .Join(DOTween.To(() => group.alpha, alpha => group.alpha = alpha, 1f,
        Mathf.Min(seconds, 0.18f)).SetEase(Ease.OutQuad))
    .SetUpdate(true)
    .SetRecyclable(false);
```

Combat Animators, Tweens, particles, and damage numbers receive a separate playback speed of 0×, 1×, or 2×. VFX reuse per-target-slot instances owned by [UnitEffectsView](Assets/UnityCombatLab/Scripts/Presentation/Units/UnitEffectsView.cs). Damage numbers start with three display slots and add another when all are busy, preserving numbers that are still visible. Returning a slot stops its Tween and changes its generation number so an old callback cannot affect the reused slot.

**Relevant tests:** [UnitPresentationReadModelTests](Assets/UnityCombatLab/Tests/EditMode/UnitPresentationReadModelTests.cs) checks damage arrival order, ordering across healing, and notification after HP application. [BattlePresentationTests](Assets/UnityCombatLab/Tests/PlayMode/BattlePresentationTests.cs) covers Unity display objects and the lifetimes of subscriptions and effects.

### 2. The screen has closed, but its assets finish loading later

**Problem:** Cancelling the Run that is waiting for a load, or destroying its screen, does not necessarily stop an operation already in progress. If the waiting code simply exits, responsibility for releasing the result becomes unclear. Releasing assets while their display objects still exist must also be avoided.

**Decision:** Treat completion of the load and cancellation by its caller as separate events. Each successful load returns an [AddressableAssetLease](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/AddressableAssetLease.cs) that owns its release responsibility. [AddressableAssetManager.LoadAsync](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/AddressableAssetManager.cs) uses UniTask to await the internal load before checking cancellation.

```csharp
var handle = Addressables.LoadAssetAsync<T>(reference.RuntimeKey);
try
{
    var asset = await handle;
    cancellationToken.ThrowIfCancellationRequested();
    if (asset == null)
        throw new InvalidOperationException($"Asset {reference.RuntimeKey} did not produce {typeof(T).Name}.");

    return new AddressableAssetLease<T>(handle);
}
catch
{
    if (handle.IsValid())
        Addressables.Release(handle);

    throw;
}
```

On success, the Lease owns the handle. On failure or cancellation, the loader releases it. [AddressableAssetScope](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/AddressableAssetScope.cs) collects the Leases used by a screen or Run. If the Scope has already ended, it immediately releases a late result instead of retaining it.

```csharp
var lease = await _assets.LoadAsync(reference, cancellationToken);
if (_isDisposed)
{
    lease.Dispose();
    throw new ObjectDisposedException(nameof(AddressableAssetScope));
}
```

This trades immediate cancellation responses for **retaining clear cleanup responsibility until an in-flight load finishes**. Addressables loads local unit definitions, prefabs, UI, and icons. Assets prepared by [RunAssetScope](Assets/UnityCombatLab/Scripts/Infrastructure/Assets/RunAssetScope.cs) are retained across all five waves. VFX are held as dependencies of unit prefabs, so they are not reloaded on every hit.

Run teardown also follows an explicit order. [RunSession](Assets/UnityCombatLab/Scripts/Application/Run/RunSession.cs) owns cancellation for the Run. After input is locked, [RunFlowCoordinator.EndRunAsync](Assets/UnityCombatLab/Scripts/Application/Run/RunFlowCoordinator.cs) performs **cancellation → subscription, effect, and View cleanup → wait for the next frame → asset release**.

```csharp
session.Cancel();
ShowTransitionSafely(Define.RunTransitionPhase.CleaningUp);
session.Battle?.Dispose();
await _nextFrame();
if (_isDisposed)
    return;

session.Resources.Dispose();
```

Waiting for the next frame allows Unity's `Destroy` processing to finish before releasing the assets. Required cleanup proceeds independently of the already-cancelled Run token. The flow then prepares Frontend and clears the Run state.

| Lifetime | Owned objects and when they are released |
|---|---|
| AppRoot | Shared scene transitions, loading UI, EventSystem, and other services used for the lifetime of the application |
| Frontend scene | Title, formation, results, and error UI; Presenters and screen assets. Released when the scene ends |
| Run | Cancellation token, unit and UI assets, progression data, and upgrade history. Released when the Run ends |
| Dungeon / wave | The scene owns the combat Canvas and controls. Runners, units, HUDs, subscriptions, and effects are replaced for each wave |

**Relevant tests:** [AddressableAssetTests](Assets/UnityCombatLab/Tests/PlayMode/AddressableAssetTests.cs) checks shared asset release by the final owner, load completion after cancellation, late completion after Scope disposal, and retries after loading fails. [RunFlowCoordinatorTests](Assets/UnityCombatLab/Tests/EditMode/RunFlowCoordinatorTests.cs) covers both completion orders when application disposal overlaps a cleanup frame wait, as well as release exceptions and late UI access.

### 3. Matching replays do not prove that the rules stayed correct

**Problem:** Bugs are harder to reproduce if frame intervals or 1×/2× speed change attack counts and battle outcomes. Even after solving that problem, recording and replaying with the current code can still pass verification when an incorrect rule change affects both sides in the same way.

**The first decision is to make combat timing and inputs reproducible.** One [BattleSimulation.Step](Assets/UnityCombatLab/Scripts/Domain/Battle/BattleSimulation.cs) represents 50ms, giving a 20Hz simulation. HP, shields, and damage use integers; ratios use basis points (`10000 = 100%`). [BattleRunner](Assets/UnityCombatLab/Scripts/Application/Battle/BattleRunner.cs) converts frame time into ticks, with playback speed applied to the time passed to the Runner.

After delivering a tick's events to the read model and consumers, the Runner acknowledges their consumption:

```csharp
_simulation.Step();
for (var index = 0; index < _simulation.EventCount; index++)
{
    var battleEvent = _simulation.GetEvent(index);
    ReadModel.Apply(battleEvent);
    EventOccurred?.Invoke(battleEvent);
    if (_isDisposed)
    {
        return;
    }
}

_simulation.AcknowledgeEvents();
```

The core reuses a fixed event array. Its unconsumed-event check and the Runner's reentrancy guard prevent the next tick from overwriting a buffer that is still being consumed. `BattleEvent` instances retained by asynchronous presentation code are passed by value.

The Runner processes at most **8 ticks** per frame and carries the remaining time forward. Under heavy load, combat may fall behind real time, but it does not skip simulation ticks. The action order is fixed: **ally front → enemy front → ally upper back → enemy upper back → ally lower back → enemy lower back**. A unit killed by an earlier attack does not act, even within the same tick, so this gives allies a first-action advantage. The game deliberately uses this simple, reproducible sequence of actions.

A completed Run's configuration, seed, upgrade choices, events, and result are stored in [RunReplayRecord](Assets/UnityCombatLab/Scripts/Domain/Run/RunReplayRecord.cs). [RunReplayVerifier](Assets/UnityCombatLab/Scripts/Domain/Run/RunReplayVerifier.cs) recalculates combat without a Unity view and reports the first differing event or result. Events are serialized in a fixed field order using little-endian encoding, then hashed with FNV-1a 64-bit for comparison. The Run ID, which changes on each execution, is excluded.

**The second decision is to keep a regression baseline independent of the current execution.** [RunReplayTests](Assets/UnityCombatLab/Tests/EditMode/RunReplayTests.cs) defines its own inputs, formation, seed, and upgrade choices, then checks fixed expectations for the outcome, per-wave ticks, event counts, choice history, and hash. The main expected values are:

```csharp
private const ulong GoldenEventHash = 0x135806989FA96900UL;
private const int GoldenTotalTicks = 386;
private const int GoldenEventCount = 152;
```

| Check | What it detects |
|---|---|
| Execute the same inputs with different frame intervals and 1×/2× speed | Combat events or outcomes that change with execution timing |
| Recalculate a recorded Run without a view | Differences between the recording and replay, or altered events and choice history |
| Compare fixed inputs against independent expectations | Changes to the established rules' results, even when recording and replay change together |

Fixed expectations require maintenance when the rules change. They are updated, with a documented reason, only when a balance or rule change is intentional.

**Relevant tests:** [BattleSimulationTests](Assets/UnityCombatLab/Tests/EditMode/BattleSimulationTests.cs) checks targeting, damage, death, and termination order. [BattleRunnerTests](Assets/UnityCombatLab/Tests/EditMode/BattleRunnerTests.cs) checks time accumulation, event consumption, and reentrancy. [RunReplayTests](Assets/UnityCombatLab/Tests/EditMode/RunReplayTests.cs) checks the fixed results under three combinations of frame interval and speed, and detects missing events and altered upgrade increments.

### 4. Keep old screen input and previous Run state out of the next step

**Problem:** Repeated upgrade clicks or callbacks arriving late from an old screen can apply the same upgrade twice. Storing runtime state in ScriptableObjects can also make a new attempt inherit HP or upgrades from the previous Run.

**Decision:** [RunController](Assets/UnityCombatLab/Scripts/Application/Run/RunController.cs) checks the current Run ID and phase, while [RunProgress](Assets/UnityCombatLab/Scripts/Domain/Run/RunProgress.cs) validates the wave, offered candidates, and whether a choice is still allowed. A successful choice advances the phase and wave, so a subsequent input cannot apply the same choice again.

```csharp
public bool TrySelectReward(Guid runId, int wave, Define.RewardKind? kind)
{
    if (!Matches(runId, Define.RunPhase.RewardSelection) || !Progress.TryChoose(wave, kind))
        return false;

    SetPhase(Define.RunPhase.WaveIntro);
    return true;
}
```

Upgrade offers use an explicit seed with [xorshift32 PRNG and partial Fisher–Yates sampling](Assets/UnityCombatLab/Scripts/Domain/Run/RewardRandom.cs). The increase granted at selection time is stored in [RewardChoice](Assets/UnityCombatLab/Scripts/Domain/Run/RewardChoice.cs), so the next wave's reward multiplier does not recalculate earlier upgrades.

Authoring data and runtime state are also separate. ScriptableObject settings in [RunDefinition](Assets/UnityCombatLab/Scripts/Infrastructure/Balance/RunDefinition.cs) are converted into validated C# data in [RunGameData](Assets/UnityCombatLab/Scripts/Domain/Run/RunGameData.cs). Run and combat objects own runtime HP and upgrade counts. [RunBattlePresenter](Assets/UnityCombatLab/Scripts/Presentation/UI/RunBattlePresenter.cs) prepares the values needed by upgrade cards in [RewardCardDisplay](Assets/UnityCombatLab/Scripts/Presentation/ReadModels/RewardCardDisplay.cs) and passes them to the View.

**Relevant tests:** [RunProgressTests](Assets/UnityCombatLab/Tests/EditMode/RunProgressTests.cs) checks offers and duplicate choices. [RunControllerTests](Assets/UnityCombatLab/Tests/EditMode/RunControllerTests.cs) checks state transitions and reset on retry. [RunBattleCoordinatorTests](Assets/UnityCombatLab/Tests/EditMode/RunBattleCoordinatorTests.cs) checks five-wave progression without a view, focus recovery, and waiting for presentation to finish.

## Running the project and restoring assets

Purchased assets are excluded from this repository in accordance with their licenses. Running the project in Unity requires restoring models, textures, animations, GUI assets, fonts, and the project's derived materials and VFX.

To set up a local development environment:

1. Open this repository's root in Unity **6000.3.8f1**. Restore UPM packages using the versions in manifest/lock.
2. Use `NuGet > Restore Packages` to restore R3 core and the dependencies in [packages.config](Assets/packages.config). The generated `Assets/Packages/` directory is excluded from Git.
3. Restore DOTween Standard **1.3.045** from its official distribution and configure it through `Tools > Demigiant > DOTween Utility Panel`. Retain the distribution and its notices; `Assets/Plugins/Demigiant/` is not included in the public repository.
4. Restore TMP resources using `Window > TextMeshPro > Import TMP Essential Resources`.
5. If you own the external assets listed below, import them yourself while preserving their original paths and GUIDs. Do not obtain them by copying another person's purchased assets.
6. After importing the asset packs, separately configure the project's derived URP materials and VFX wrappers, then assign references in the custom unit/UI prefabs and asset catalogs.
7. Once assets and references are restored, start from [Boot.unity](Assets/UnityCombatLab/Scenes/Boot.unity). Running Dungeon directly requires an active Run. Scene order must match [Define.SceneType](Assets/UnityCombatLab/Scripts/Common/Define.cs): `Boot=0 / Dungeon=1 / Frontend=2`.

Run `UnityCombatLab.Tests` in the Unity Test Runner's EditMode and PlayMode tabs. Tests that use asset references require the restoration steps above. After a battle ends, remain in Play mode and select `UnityCombatLab > Development > Verify Last Run` to see replay verification for the most recently completed Run in the Console.

## Implementation scope and external resources

The custom implementation covers combat, upgrades, progression rules, UI and effect coordination, asynchronous flow and asset lifetimes, tests, replay verification, and project-specific prefab and scene setup. The original character, environment, GUI, and VFX assets come from the following resources.

| Resource | Provider / purpose | Treatment in this repository |
|---|---|---|
| KayKit Adventurers / Skeletons / Dungeon Remastered | Kay Lousberg / characters, equipment, animations, and stage assets | Original assets under `Assets/KayKit/` excluded |
| GUI Pro - Minimal Game Light | LAYERLAB / UI graphics and icons; LTAvocado Bold and Afacad Flux ExtraBold font references | Original assets under `Assets/Layer Lab/` excluded |
| [Epic Toon FX](https://assetstore.unity.com/packages/vfx/particles/epic-toon-fx-57772) | Archanor VFX / combat effects | Original assets under `Assets/Epic Toon FX/` excluded |
| Project-derived materials and VFX wrappers | Local assets built around the purchased resources above | `Assets/UnityCombatLab/Art/Restricted/` excluded |
| Zen Maru Gothic Bold | Zen Maru Gothic Project Authors / Japanese UI | Original font, generated TMP atlas, [NOTICE](Assets/UnityCombatLab/Art/Fonts/ZenMaruGothic/NOTICE.txt), and [OFL](Assets/UnityCombatLab/Art/Fonts/ZenMaruGothic/OFL.txt) included |

Excluded paths are listed in [.gitignore](.gitignore). Custom wrappers reference external resources, so both the original assets and project-derived assets must be restored.

Development includes implementation drafts, Editor operations, and automated verification using Codex and Unity CLI/MCP. Changes have been incorporated through design and scope decisions, code review and revision, and play checks.

## Planned work

- **Sound:** Add background music and combat/UI sound effects. The current demo has no sound.
- **Logic improvements:** Review the existing logic against actual play flows and tests, then refactor and fix it where needed.
