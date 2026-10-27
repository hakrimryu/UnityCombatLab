# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- Table and CSV export reject cyclic or missing references, duplicate track ids and
  non-finite output before replacing an existing export.
- Source profile fingerprints include sampled output, detecting table values and curve
  edits that their display descriptions omit. Existing tables receive a new hash on re-export.
- CSV imports containing malformed numbers, NaN or Infinity are rejected before any track is changed.
- Replacing a simulation request immediately invalidates its previous result, including
  during debounce and when switching to an unconfigured profile. Cancellation sources
  are released on replacement and window disposal.

## [0.4.0] — 2026-08-04

Round trips and small fixes to the parts that got in the way.

### Added

- **CSV import.** Export was previously a one-way door: numbers went out to whoever did
  not have Unity, and a corrected sheet had to be retyped. Columns are matched by track
  id, rows are keyed by axis position so a re-sorted sheet still lands correctly, and a
  cell that cannot be read is reported rather than guessed at.
- **Pinning values from the graph.** Double-click or right-click a point to fix that
  position to a chosen number, and clear it the same way. Pinned positions are drawn as
  markers, which they were not before — a curve that had been overruled in four places
  read as one the formula produced.

### Changed

- Imported columns are final values, so applying one clears that track's post-processors
  and pinned values. Keeping them would round an already rounded column a second time.

### Fixed

- **Pane widths no longer strand the graph.** Widths are stored in pixels; a layout saved
  on a wide monitor restored into a 760px window left the graph 112px across. The side
  panes now give way so the graph keeps at least 220px.
- The toolbar wraps instead of squeezing the profile field when the window is narrow.
- The Issues pane is draggable rather than fixed at 132px.
- The formula line no longer wraps to three lines and takes the graph's height with it.

## [0.3.0] — 2026-08-04

Everything the tool does is now reachable from its own window. The previous releases
could show a profile someone else had built; this one lets a designer build it.

### Added

- **Track editing** — add, duplicate, reorder, remove and search, all from the window.
  New tracks arrive with an id, an axis, a range and a curve already set.
- **Profile settings in the window** — name, notes, axes, shared constants, simulation
  settings and the track binding. All of it previously lived only on the asset.
- **Simulation binding as pickers** rather than typed ids. A binding pointing at a
  deleted track shows as missing instead of quietly disappearing.
- **Plain-language names for models and steps** — "Gets steeper" rather than "Power" —
  carried on the type itself through `[FulcrumModel]`, so a model written elsewhere
  describes itself the same way. Every field has a tooltip.
- **Sliders** on parameters that declared a sensible range through `[FulcrumRange]`,
  because a bare number box gives no sense of what neighbourhood matters.
- **Value preview** under the model: what the track actually produces at a few positions.
- **Table view** — rows are axis positions, columns are tracks. The form a designer
  already reads numbers in.
- **Starter setup** for an empty profile: a stage axis and six bound tracks that simulate
  immediately.
- **Unsaved indicator**, since Unity shows nothing for an asset edited in memory.

### Fixed

- The inspector rebuilt itself endlessly. `PropertyField` reports a change when it is
  bound, not only when edited, so applying an edit rebuilt the pane, which reported
  another edit.
- Zooming the graph drew the curve outside the plot area. The value bounds covered the
  visible range while the line was drawn one sample wider, so the extra samples
  normalised past the top of the plot. Axis labels also kept announcing the track's full
  range while showing a slice of it.

## [0.2.0] — 2026-08-04

Simulation and validation. A curve change now becomes a statement about where players
get stuck and how long the game takes.

### Added

- **Track dependencies.** A track can read another through `TrackReferenceModel`, and
  `CompositeModel` combines several — expected gold really is monster count times drop
  value rather than a fourth curve kept in sync by hand. Evaluation is ordered by
  dependency, and a cycle is reported as the path that forms it.
- **Incremental evaluation.** An edit re-evaluates the track and its dependents, nothing
  else.
- **Combat simulation.** Deterministic Monte Carlo: the same profile, iteration count and
  seed always produce the same numbers. Reports win rate, time to kill, percentiles and
  how close the fight was.
- **Progression and economy.** Levels, upgrades and predicted playtime including retries;
  currency income, spending and the points where a surplus or a shortfall sets in.
- **Player presets** — Casual, Standard and Optimizer — so "which layer gets stuck" is a
  question the tool can answer.
- **Seven validation rules** with suggestions attached, not just findings.
- **Graph** gains overlaid series, a logarithmic axis, zoom and pan, and shading for the
  positions a rule flagged. Clicking a finding frames it.
- Simulation runs debounced on a worker thread, coarsened while a value is being dragged.

### Changed

- `BalanceProfile` carries a `TrackBinding` naming which track fills each simulated role,
  so a project keeps its own vocabulary.

### Known limitations

- Formulas are not authored as data yet; the damage rule is fixed with an exposed constant.
- No HTML report, profile diff or demo scene yet.

## [0.1.0] — 2026-08-04

First release. Authoring, evaluation and export work end to end.

### Added

- **Data model.** `BalanceProfile` holding axes, tracks, profile-wide constants and the
  notes explaining the tuning. Axes are user-defined rather than a fixed enumeration, so a
  project can balance over whatever it actually progresses along.
- **Growth models** — `Linear`, `Power`, `Exponential`, `Table`, `Curve` and `Piecewise`,
  held polymorphically through `[SerializeReference]`.
- **Post-processing chain** — `Round`, `Clamp`, `MinDelta`, `Multiplier` and `Smooth`,
  applied in the order the designer arranged them. Steps operate on the whole series, so
  smoothing and minimum-step rules can see their neighbours.
- **Evaluation** — `model → post-processing → accumulation → overrides`. On a cumulative
  track an override shifts everything after it, keeping the step size intact.
- **Manual overrides** — pin an axis position to a stated value, with a note recording why.
- **Authoring window** (`Window → Fulcrum → Balance Editor`) with a track list, a graph
  drawn without any external dependency, and an inspector generated from serialized data
  so user-defined models work with no extra editor code.
- **Export** — `BalanceTable` assets for runtime use, carrying a fingerprint of the profile
  they came from, and CSV for sharing with the design team.
- **Migration guard** — a committed fixture asset that fails the test suite if a serialized
  type is renamed, which otherwise empties every asset referencing it without any error.

### Known limitations

- Tracks cannot yet reference one another; the dependency graph arrives with simulation.
- No simulators or validation rules yet.
- Verified against Unity 6000.3; the manifest declares 6000.0 and no API newer than that
  is used, but earlier Unity 6 releases have not been exercised on hardware.

[0.3.0]: https://github.com/hakrimryu/fulcrum-unity/releases/tag/v0.3.0
[0.2.0]: https://github.com/hakrimryu/fulcrum-unity/releases/tag/v0.2.0
[0.1.0]: https://github.com/hakrimryu/fulcrum-unity/releases/tag/v0.1.0
