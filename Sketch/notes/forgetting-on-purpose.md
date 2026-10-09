# Forgetting on Purpose — evidence note

**Claim.** An actor's journal can forget part of its history on purpose: every
stroke that a drawing session drew and then erased. Before anything is removed,
the forgetting is previewed on an isolated copy of the actor and proved to
change nothing the picture or the stroke count can show. After it is
committed, the journal is smaller and the canvas rehydrated from it prints the
same picture, character for character.

**The measurement is an equality, not an adjective.** The picture the canvas
prints after the commit and a restart from disk is the same text as before
(SHA-256 `f6587c64a5113d24fc36625f3afc8d16dd6e56d52181349e0b67c585675db8a4`
at seed 42, scale 1). The journal around it went from 3,504 records to 504.

What was given up is measured too. A shadow synced to the middle of the session
shows 7 hesitations on the canvas before Distill and none after it, and that
changed past is byte-equal to the one the elision-impact diff predicted before
anything was removed.

---

## 1. What was built

A new example, [`Sketch/`](../), next to Tetris and following its layout: a
pure domain with no engine reference, a typed actor facade that holds every
DSL string, a view, a lab host and two test suites.

The domain constraints this demonstration puts on the model are the ones
[`../README.md`](../README.md#the-model) explains. The picture is the
canvas's stack of strokes in drawing order, so a drawn-then-erased stroke
leaves no trace. Nothing in the domain counts past acts, because a counter
would rebuild differently once acts are forgotten. And the picture's order is
the stack's, never a container's.

## 2. Files added (purely additive)

```
Sketch/Sketch.sln
Sketch/Directory.Build.props                  # PuppeteerRoot, default ../../puppeteer
Sketch/.gitignore                             # out/
Sketch/README.md
Sketch/domain/                                # Canvas, Extent, Stroke, Point, Ink, SketchRuleException, anchor
Sketch/domain.tests/                          # 38 cases, no engine
Sketch/actor/                                 # SketchActor, ForgettingRule, JournalFolder, snapshots
Sketch/view/                                  # PictureSvg
Sketch/forgetting/                            # the lab
Sketch/forgetting.tests/                      # 29 end-to-end cases
Sketch/notes/forgetting-on-purpose.md         # this note
Sketch/notes/forgetting-on-purpose.log        # the four captured run logs
Sketch/notes/*.svg                            # the pictures of the public-engine scale-1 run
README.md                                     # + the Sketch entry in the list of examples
```

`git diff main -- Tetris HelloWorld` is empty.

## 3. Verified against the framework, not assumed

Every engine behaviour the example relies on was checked in the engine source.
Those the example's claims rest on are also pinned by a test or checked by the
lab at run time. Paths are relative to the engine root; both engines named in
§5 behave identically on each point.

- **The shadow is the actor's own primitive.** `Actor.Shadow(ShadowConfig)`
  (`Puppeteer/Actor.cs`) returns a `Shadow` (`Puppeteer/Shadow/Shadow.cs`)
  with `SyncUntil`, `EnableSkipPreview`, `Reactions`, `ElisionImpactDiff` and
  `PerformQry`. The `ShadowPerformance` that `Performance.Shadow` returns does
  not carry the preview or the diff. A shadow's storage is its own
  (`<primary>-shadow-<id>`), and the primary's reactions are not cloned into
  it. The facade registers the rule through `ShadowConfig.configureReactions`.
- **Skip-preview captures without committing.** In a shadow with preview on,
  an Elide reaction records its batch in `Reaction.WouldSkip` and returns
  before the commit (`Puppeteer/EventSourcing/Follower/MatchTree.cs`, the
  skip-preview branch of the complete-match path). Checked: the primary's head,
  records, bytes and printed picture are identical before and after a preview.
- **The diff compares printed answers.** `ElisionImpactDiff` seeds two twins
  from the shadow's journal, one with the candidate entries marked elided, and
  compares each observation query's output with an ordinal string comparison
  (`Puppeteer/Shadow/Shadow.cs`). The outputs are the default JSON formatter's
  text, the same text a query on the primary returns. The acceptance suite
  checks that the unelided side equals the primary's picture byte for byte.
  `ElisionImpactResult` (`Puppeteer/Shadow/ElisionImpactResult.cs`) states its
  own limit: safe with respect to the observers passed in.
- **Correlation by value.** A `$id` captured by the first seek constrains the
  second seek to the same value. In the suite's standard session, with a
  hesitation nested inside another, the preview lists exactly the hesitations'
  Draw and Erase entries and none of the survivors'.
- **Quantifiers.** Every seek of a multi-seek reaction declares its multiplicity
  (`Reaction.ValidateQuantifiersPresent`,
  `Puppeteer/EventSourcing/Follower/Reaction.cs`); the check is made in
  `Reaction.Execute`, not when the reaction is defined. The rule uses `.One()`
  on both seeks.
- **Reactions observe V2 Actions.** A domain reaction skips literal script
  commands (`Reaction.cs`, the pure-domain branch of event resolution), so every
  verb is a parametrised check-then-command. A refused check journals nothing,
  which the suite checks by the head not moving.
- **Elision is the whole matched chain; Define records survive.** A V2
  action's Define record is a record of its own, separate from its
  invocations (`Puppeteer/EventSourcing/DB/DiaryStorage.cs`), and it is never
  part of a match. After Distill the journal holds the seed's Define and
  Invocation, the Define records of `Draw` and `Erase`, and one invocation per
  surviving stroke: 2 + 2 + 500 = 504 at scale 1.
- **Distill keeps the journal's last record** (`DiaryStorage.Distill`, the
  invariant in its comment), and **rehydration skips elided entries.** One
  test pins both. When the last act is an elided `Erase`, one more record than
  the pairs' arithmetic stays on disk, and it is an `Erase` whose `Draw` is
  gone. The reopened canvas still prints the right picture, which it could not
  do if rehydration replayed that record. The session is generated to end on a
  surviving stroke.
- **The pairing window is 1001 entries.** A `Draw` still waiting for its
  `Erase` is a partial match. `MatchTree` prunes a childless node that has not
  advanced for more than `staleNodeThreshold` (1000) entries
  (`Puppeteer/EventSourcing/Follower/MatchTree.cs`, `PruneStaleNodes`).
  The suite pins it: an `Erase` 1001 entries after its `Draw` is paired, and
  one 1002 entries after is not, whether the entries in between are surviving
  strokes or other drawn-then-erased pairs. The session erases every
  hesitation within 13 entries.
- **Without a registered destination, `Distill().Now()` runs unconditionally**
  (`Puppeteer/DistillCommand.cs`). The census below never registers a
  destination on the primary, so the primary's Distill is never gated.
- **Releasing and reopening a journal in one process.** `Actor.GracefulExit()`
  followed by `Performance.Dispose()` releases the actor, and a new
  `PerformanceV2` on the same folder rehydrates it. The lab and the suite run
  Distill on the instance that wrote the journal, then release and restart.
- **Counting records in one pass.** `Introspection.ShowEntry` reads every
  record after the one it shows (`Puppeteer/EventSourcing/ActorHandler.cs`), so
  walking a journal with it is quadratic. The census instead copies the journal folder,
  opens the copy with `ConfigureStorageForIntrospection` (no rehydration, head
  0), registers a destination on the copy, and reads every record with
  `Materialization.ReadRecordsAfter(destination, 0)` and every elision mark
  with `ReadElidedRange` (`Puppeteer/Materialization.cs`). The primary's
  folder is only read. On the small journals of the acceptance suite, the
  census matches the journal layout the acts imply, record by record.
- **Seeding once.** The facade seeds only a journal whose head is 0, and the
  suite checks that a reopen journals nothing and keeps the head.
- **The shadow for the past keeps the primary's entry ids.** `SyncUntil` copies
  each record with its EntryId (`ActorHandler.CopyPrimaryRecordsToShadow`).
  The FileSystem storage writes the id it is given
  (`Puppeteer/EventSourcing/DB/DiaryStorageFileSystem.cs`), while the in-memory
  storage numbers what it stores afresh (`DiaryStorageInMemory.cs`). After
  Distill the journal has gaps, so `PictureAt` opens its shadow on FileSystem
  storage in a scratch folder. Previews and proofs run before Distill, on
  in-memory shadows. The suite and the lab check that the shadow after Distill
  replays the past the diff predicted.

## 4. Acceptance tests (`forgetting.tests/`, 29 cases, real FileSystem journals)

| Class | What it pins |
|---|---|
| `CanvasActorTests` | seeding once; draw, erase and the picture round-trip; refused checks journal nothing; a reopen rehydrates the same picture |
| `PreviewAndProofTests` | the census counts every record by kind; the preview lists exactly the erased pairs and leaves the primary untouched; the pairs are safe; an Erase without its Draw is unsafe and names the picture; the unelided side is the primary's picture byte for byte |
| `CommitTests` | eliding marks without removing; commit and reopen keep the picture byte-equal and remove exactly the pairs (Define rows kept); the last record is kept; entry ids continue with no reuse; the rule twice elides nothing more; Distill twice is harmless; every journal state rehydrates cold to the same picture; Distill shrinks the record segments |
| `PairingWindowTests` | an Erase within 1001 entries of its Draw is paired and one 1002 or more entries away is not, with surviving strokes or other pairs in between |
| `PastTests` | a shadow at a mid-session entry shows the strokes on the canvas then; after Distill the same entry no longer shows the hesitation, and that past is byte-equal to the one the diff predicted; the forgetting is safe for the present picture and unsafe for the picture of that moment |

```
dotnet test Sketch/Sketch.sln -p:PuppeteerRoot=<private checkout>   →  38/38 + 29/29 passed
dotnet test Sketch/Sketch.sln -p:PuppeteerRoot=<public clone>       →  38/38 + 29/29 passed
```

## 5. Measured results

Four runs of the lab on 2026-09-30, from commit `540a915` of this branch: seed
42, scale 1 (500 survivors, 1,500 hesitations) and scale 10 (5,000 and 15,000),
5 timed cold starts per journal state after one uncounted warm-up. Each scale
ran against the public engine clone `2673d57` and the private engine checkout
`b083a5a`, both built in Release, with these commands:

```
dotnet run --project Sketch/forgetting -c Release -p:PuppeteerRoot=<engine> -- --out <dir>
dotnet run --project Sketch/forgetting -c Release -p:PuppeteerRoot=<engine> -- --out <dir> --scale 10
```

| | public `2673d57`, scale 1 | private `b083a5a`, scale 1 | public `2673d57`, scale 10 | private `b083a5a`, scale 10 |
|---|---|---|---|---|
| Acts (draws + erases) | 3,500 (2,000 + 1,500) | 3,500 (2,000 + 1,500) | 35,000 (20,000 + 15,000) | 35,000 (20,000 + 15,000) |
| Strokes on the canvas at the end | 500 | 500 | 5,000 | 5,000 |
| Longest draw → erase distance | 13 entries | 13 entries | 13 entries | 13 entries |
| Session | 4.88 s | 4.99 s | 56.14 s | 48.68 s |
| Entries the rule would elide (preview) | 3,000 (1,500 pairs) | 3,000 (1,500 pairs) | 30,000 (15,000 pairs) | 30,000 (15,000 pairs) |
| Preview | 1.42 s | 1.33 s | 10.5 s | 10.97 s |
| Primary after the preview | unchanged | unchanged | unchanged | unchanged |
| Diff, the rule's entries | safe | safe | safe | safe |
| Diff, one Erase without its Draw (stroke brought back) | unsafe: picture, stroke count (1114) | unsafe: picture, stroke count (1114) | unsafe: picture, stroke count (14984) | unsafe: picture, stroke count (14984) |
| Records: full / logical / physical | 3,504 / 3,504 / 504 | 3,504 / 3,504 / 504 | 35,004 / 35,004 / 5,004 | 35,004 / 35,004 / 5,004 |
| Elision marks: full / logical / physical | 0 / 3,000 / 0 | 0 / 3,000 / 0 | 0 / 30,000 / 0 | 0 / 30,000 / 0 |
| Record-segment bytes: full / logical / physical | 196,158 / 196,158 / 34,250 | 196,158 / 196,158 / 34,250 | 1,992,086 / 1,992,086 / 343,214 | 1,992,086 / 1,992,086 / 343,214 |
| All bytes: full / logical / physical | 196,236 / 256,574 / 34,666 | 196,236 / 256,573 / 34,665 | 1,992,164 / 2,592,501 / 343,629 | 1,992,164 / 2,592,502 / 343,630 |
| Cold start, median: full | 41.4 ms (40.0–47.2) | 41.1 ms (39.1–47.0) | 380.8 ms (372.8–384.4) | 444.2 ms (434.2–448.0) |
| Cold start, median: logical | 37.0 ms (35.8–38.3) | 37.6 ms (36.4–38.6) | 222.8 ms (221.4–223.6) | 239.3 ms (237.6–242.8) |
| Cold start, median: physical | 14.2 ms (14.1–14.5) | 14.1 ms (13.9–14.3) | 57.7 ms (56.4–58.4) | 63.6 ms (61.9–66.6) |
| Elide on the primary / Distill | 49.86 s / 0.05 s | 49.22 s / 0.05 s | 595.03 s / 0.25 s | 581.33 s / 0.26 s |
| Picture after the restart | byte-equal | byte-equal | byte-equal | byte-equal |
| Head after the restart, then the next entry id | 3,504, then 3,505 | 3,504, then 3,505 | 35,004, then 35,005 | 35,004, then 35,005 |
| Mid-session entry (hesitations on the canvas then) | 1,695 (7) | 1,695 (7) | 16,249 (9) | 16,249 (9) |
| Forgetting, asked of that entry | unsafe: picture, stroke count | unsafe: picture, stroke count | unsafe: picture, stroke count | unsafe: picture, stroke count |
| Shadow at that entry: strokes before / after Distill | 241 / 234 | 241 / 234 | 2,315 / 2,306 | 2,315 / 2,306 |
| After Distill, that past equals the diff's prediction | byte-equal | byte-equal | byte-equal | byte-equal |
| Records up to that entry: before / after Distill | 1,695 / 238 | 1,695 / 238 | 16,249 / 2,310 | 16,249 / 2,310 |
| Whole run | 60.55 s | 60.16 s | 695.8 s | 676.02 s |

The printed picture's SHA-256 is
`f6587c64a5113d24fc36625f3afc8d16dd6e56d52181349e0b67c585675db8a4` at scale 1
and `7f701067f459aab6e4e1eeec649ffac7d3ccb45d24d40c33465036e3c7804a7e` at
scale 10, identical on both engines and after every restart. Ranges in
parentheses are the minimum and maximum of the five timed starts.

The pictures in this folder come from the public-engine scale-1 run:
[`picture.svg`](picture.svg), [`unsafe-without.svg`](unsafe-without.svg),
[`unsafe-with.svg`](unsafe-with.svg), [`past-then.svg`](past-then.svg) and
[`past-after-distill.svg`](past-after-distill.svg). The four `run.log` files are
in [`forgetting-on-purpose.log`](forgetting-on-purpose.log).

## 6. Honest caveats

1. **"Safe" is relative to two questions**, the picture and the stroke count.
   The same forgetting changes the answer to a third question: what the canvas
   looked like halfway through (scene 6).
2. **"Cold start" means a cold actor, not a cold machine.** Each start opens a
   fresh copy of the journal state in a new actor, in the same process and
   with the operating system's file cache warm. The first start of a process
   pays for JIT compilation, so one uncounted warm-up start precedes the timed
   rounds, and the three states are timed in interleaved rounds.
3. **The session is synthetic.** Hesitations are spread at random across the
   survivors and erased within 12 acts. A real session would have longer and
   less regular gaps, and a gap of more than 1001 entries would leave a pair
   in the journal (§3).
4. **One machine, one day, two session sizes.** Nothing here is a performance
   evaluation. The slowest step measured is the elision on the primary's
   FileSystem journal: about 33 ms per pair at scale 1 and 39–40 ms at
   scale 10. The preview of the same rule on an in-memory shadow ran 35 to 57
   times faster. That is recorded as measured, not analysed.
5. **The census reads a copy.** The copy is taken with the primary's writer
   open, so the active segment is copied unsealed; opening it is the recovery
   path the engine takes after a crash.
