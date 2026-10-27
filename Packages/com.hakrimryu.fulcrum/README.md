# Fulcrum Balancing Toolkit

Curve-driven game balance authoring and export for the Unity Editor.

Define stage- and level-based game numbers as formulas, shape them into values you would
actually ship, and export them as runtime data — without leaving the editor.

> **Status: 0.3.0.** Authoring, evaluation, simulation, validation and export work end to
> end, all from one window. See [CHANGELOG.md](CHANGELOG.md) for what ships today.

## Why

Balance passes usually loop through a spreadsheet, an importer, a build, and a playthrough
— half a day per iteration. Worse, the reasoning behind each number lives in someone's head
and evaporates when they move on.

Fulcrum keeps the formula, the adjustments applied to it, and the note explaining why, all
in one asset, and closes the loop to a few seconds.

## Requirements

- Unity 6000.0 or newer
- No external dependencies (Unity standard APIs only)

## Installation

**Window → Package Manager → + → Install package from git URL…**

```
https://github.com/hakrimryu/fulcrum-unity.git?path=/Packages/com.hakrimryu.fulcrum
```

## Five-minute tour

1. **Create a profile** — `Assets → Create → Fulcrum → Balance Profile`.
2. **Open the editor** — `Window → Fulcrum → Balance Editor`, then drop the profile into
   the *Profile* field.
3. **Press "Add a typical stage setup"**. That gives you a stage axis and six tracks that
   already simulate, so there is something to react to. Every number in it is a starting
   point.
4. **Pick a track** on the left and change its model. The dropdown says what each one does
   — "Gets steeper", "Compounds", "Changes at a boundary" — and every field has a tooltip
   and, where it helps, a slider.
5. **Watch the graph and the Issues pane.** Drag a parameter and both follow. Each finding
   carries a suggestion; clicking one frames that part of the curve.
6. **Pin the values that are decisions.** Double-click a point on the graph — or
   right-click it — to fix that position to a number you chose. Pinned points are marked
   on the curve, so an overruled formula never passes for one that produced the shape.
7. **Turn on *Compare*** to overlay the three player presets and see which one stalls
   first, or ***Table*** to read the numbers instead of the shape.
8. **Write down why** in the profile's *Notes*. That is the part a spreadsheet loses.
9. **Export and import** — *Export Table* bakes a `BalanceTable` asset for your game to
   read; *Export CSV* writes a spreadsheet for the design team, and *Import CSV* reads a
   corrected one back. Columns are matched by track id; a matched column replaces that
   track's curve with the numbers from the file.

Read it back at runtime:

```csharp
[SerializeField] private BalanceTable _balance;

var hp = _balance.Get("monster_hp", stage, fallback: 1d);
```

## Concepts

| Concept | What it is |
|---|---|
| **Profile** | One complete set of balance settings, plus the notes explaining it |
| **Axis** | A named input dimension — stage, character level, equipment tier |
| **Track** | One curve: an axis, a range, a model, and the steps applied after it |
| **Model** | The formula producing raw values |
| **Post-processor** | An ordered step turning raw output into shippable numbers |
| **Table** | The baked result your game reads. No formulas, no editor dependency |
| **Binding** | Which track fills each role a simulator needs |

### Models

| Model | Formula | Typical use |
|---|---|---|
| `Linear` | `slope · x + intercept` | A fixed increase per step |
| `Power` | `scale · x^exponent + offset` | Monster stats — accelerates gently |
| `Exponential` | `scale · ratio^x` | Upgrade costs — a fixed percentage per step |
| `Table` | Explicit values | Hand-tuned early stages |
| `Curve` | An `AnimationCurve` | When the shape is easier to draw than to write |
| `Piecewise` | Different curves per span | Chapter boundaries and difficulty tiers |
| `Track Reference` | Another track, shifted and scaled | Deriving one value from another |
| `Composite` | Several models combined | Expected gold as count × drop × rate |

### Post-processors

Balance values are never the bare output of a formula. **Order is configuration** — rounding
before a minimum-step rule produces different numbers than after it.

| Step | What it does |
|---|---|
| `Round` | Significant digits, decimals, floor or ceiling. `12847 → 12800` |
| `Clamp` | A floor, a ceiling, or both |
| `Min Delta` | Guarantees each step exceeds the last, so levelling up is visible |
| `Multiplier` | A constant, optionally a profile-wide one such as difficulty |
| `Smooth` | Averages neighbours to soften a seam |

## Simulation

Fulcrum does not only draw curves; it reports what they do.

- **Combat** — deterministic Monte Carlo. The same profile, iteration count and seed always
  produce the same numbers, so a difference between two runs is always your edit.
- **Progression** — levels, upgrades and predicted playtime, retries included.
- **Economy** — income, spending, and the points where a surplus or shortfall sets in.
- **Rules** — seven built-in checks, each carrying a suggestion rather than only a finding.

Three player presets are simulated together, because the same curve is comfortable for
someone who upgrades optimally and a wall for someone who does not.

```csharp
var analysis = new BalanceAnalyzer().Analyze(
    BalanceSnapshot.From(profile), profile.SimulationBinding,
    SimulationRun.Default(1, 200));

foreach (var issue in analysis.Issues)
{
    Debug.Log($"{issue.Severity}: {issue.Message} — {issue.Suggestion}");
}
```

## Assemblies

| Assembly | Kind | Contents |
|---|---|---|
| `Fulcrum.Core` | Runtime | Data model, evaluation, export |
| `Fulcrum.Simulation` | Runtime | Combat, progression and economy simulators; validation rules |
| `Fulcrum.Editor` | Editor | Authoring window, graph, exporters |
| `Fulcrum.Tests` | Editor | Unit tests |

Dependencies flow one way: `Editor → Simulation → Core`. `Fulcrum.Simulation` declares
`noEngineReferences`, so the compiler — not a convention — keeps it free of `UnityEngine`.

## Extending

Derive from these in your own project; the editor picks new types up through `TypeCache`,
so they appear in the dropdowns without touching this package.

| To add | Derive from |
|---|---|
| A growth curve | `GrowthModel` |
| A post-processing step | `PostProcessor` |
| A validation rule | `IValidationRule` |

```csharp
using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(false, sourceNamespace: "MyGame", sourceAssembly: "MyGame.Runtime",
           sourceClassName: nameof(LogisticModel))]
public sealed class LogisticModel : GrowthModel
{
    [SerializeField] private double _ceiling = 1d;
    [SerializeField] private double _steepness = 1d;
    [SerializeField] private double _midpoint;

    public override double Evaluate(double x, EvalContext context)
        => _ceiling / (1d + Math.Exp(-_steepness * (x - _midpoint)));

    public override string Describe() => $"logistic({_ceiling}, {_steepness}, {_midpoint})";
}
```

> **Add `[MovedFrom]` when you write the class, not when you want to rename it.** Models are
> stored by class name; renaming one empties that field in every asset that referenced it,
> and Unity reports nothing. The attribute records where the type used to live so the old
> name still resolves. Filling it in with the current location up front means a later move
> needs no edit.

Game-specific logic belongs in your project, never in `Runtime`.

## License

MIT. See [LICENSE.md](LICENSE.md).
