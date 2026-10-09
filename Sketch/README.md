# Sketch

A drawing canvas: a clean, infrastructure-free domain of straight strokes on a
surface, and on top of it the demonstration **Forgetting on Purpose**. That
demonstration removes part of an actor's journal, and proves with measurements
that nothing anyone can see has changed.

The domain has **no Puppeteer reference**. It is plain C# that builds and tests
on its own, and its source reads as if no framework existed. The framework
enters in [`actor/`](actor/), a typed facade that hosts the canvas on a
journaled actor. The solution is [`Sketch.sln`](Sketch.sln).

To learn elision and Distill from this example, begin at
[Start here](#start-here-elision-and-distill).

- [Start here: elision and Distill](#start-here-elision-and-distill)
- [The model](#the-model)
- [Forgetting on Purpose](#forgetting-on-purpose)
- [Build and run](#build-and-run)

## Layout

```
Sketch/
├── Sketch.sln
├── Directory.Build.props  PuppeteerRoot: where the engine checkout lives
├── domain/            SketchDomain library        (no Puppeteer reference)
├── domain.tests/      MSTest suite over the aggregate, no engine
├── actor/             SketchActor: the PerformanceV2 facade — every DSL string, the rule, the queries
├── view/              SketchView: draws the printed picture as SVG (references nothing)
├── forgetting/        the lab: performs the session, runs the scenes, writes the evidence
├── forgetting.tests/  MSTest acceptance suite over real FileSystem journals
└── notes/             the evidence note, its run logs and pictures
```

| Project | What it is |
|---|---|
| [`domain/`](domain/) | The model. Only the anchor `SketchDomain` is public; `Canvas`, `Extent`, `Stroke`, `Point`, `Ink` and `SketchRuleException` are `internal`. |
| [`domain.tests/`](domain.tests/) | 38 MSTest cases that drive the canvas directly: refusals, edges, stacking order, the same picture from different histories, the ink palette. |
| [`actor/`](actor/) | `SketchActor`, which hosts the canvas on a FileSystem journal, and `ForgettingRule`, the one reaction that decides what the journal may forget. |
| [`view/`](view/) | `PictureSvg`, which draws the picture projection's printed text. It can draw either side of an elision diff as it is. |
| [`forgetting/`](forgetting/) | The lab: a seeded session, the six scenes in order, every claim checked at run time, evidence written to `--out`. |
| [`forgetting.tests/`](forgetting.tests/) | 30 end-to-end cases over real journals in temporary folders. |

## Start here: elision and Distill

Forgetting part of a journal takes two steps, and this example shows both.
**Elision** marks entries: every record stays on disk, and rehydration steps
over the marked ones. **Distill** then removes the marked records from the
journal. Before either step, a **shadow**, an isolated copy of the actor,
previews what a rule would mark, and the **elision-impact diff** checks
whether marking those entries changes the answers to the questions you ask.

### The mechanism, in the engine's own calls

Given `performance`, the `PerformanceV2` that hosts the canvas, with a session
in its journal:

```csharp
// The rule: a stroke drawn, then the same stroke erased. Elide both acts.
static void DefineForgetting(Reactions reactions) =>
    reactions.DefineReaction("ForgetErasedStrokes")
        .Job().Company().WithSharedHydration()
        .Seek("Drawn")
            .OnMatch("[_:Canvas].Draw($id, _, _, _, _, _)").One()
        .ThenSeek("Erased")
            .OnMatch("[_:Canvas].Erase($id)").One()
        .Metadata.Elide();

// 1. Preview: a shadow replays the journal into storage of its own and lists
//    the entries the rule would elide. Nothing is elided anywhere.
IReadOnlyList<long> wouldElide;
using (Shadow preview = performance.Actor.Shadow(new ShadowConfig(
    "preview", DatabaseType.IN_MEMORY, "memory",
    configureReactions: actor => DefineForgetting(actor.Reactions))))
{
    preview.EnableSkipPreview();
    preview.SyncUntil(performance.CurrentEntryId);
    preview.Reactions.Execute();
    wouldElide = preview.Reactions["ForgetErasedStrokes"].WouldSkip;
}

// 2. Proof: another shadow rehydrates twice, as the journal is and with those
//    entries skipped, and compares the answers to the questions it is given.
bool safe;
using (Shadow proof = performance.Actor.Shadow(new ShadowConfig(
    "proof", DatabaseType.IN_MEMORY, "memory")))
{
    proof.SyncUntil(performance.CurrentEntryId);
    safe = proof.ElisionImpactDiff(wouldElide.ToArray(),
        SketchActor.PictureQuery, SketchActor.StrokeCountQuery).IsSafe;
}

if (safe)
{
    // 3. Elide: the rule runs on the primary and marks the pairs. Every record
    //    stays on disk; rehydration steps over the marked ones.
    DefineForgetting(performance.Actor.Reactions);
    performance.Actor.Reactions.Execute();

    // 4. Distill: the marked records leave the journal.
    performance.Distill();
}
```

Release the journal and open it again, and the canvas rehydrates the same
picture from the records that remain.
[`StartHereTests`](forgetting.tests/StartHereTests.cs) runs this code as
written, on a session of three strokes in which the second is erased. The
preview lists entries 5 and 7, that stroke's Draw and Erase. The proof is
safe. After Distill the journal holds entries 1, 2, 3, 4, 6 and 8: the seed's
two records, the Define records of `Draw` and `Erase`, and the two strokes
that stay.

The facade wraps the same calls. [`ForgettingRule`](actor/ForgettingRule.cs)
holds the rule, and `Preview`, `Prove`, `ElideErasedStrokes` and `Distill` in
[`SketchActor`](actor/SketchActor.cs) are steps 1 to 4.

### Reading order

1. [`actor/ForgettingRule.cs`](actor/ForgettingRule.cs): the rule, in the one
   place it is defined.
2. [`actor/SketchActor.cs`](actor/SketchActor.cs): `Preview`, `Prove`,
   `ElideErasedStrokes` and `Distill`; then `PictureAt` and `ProveAt`, which
   ask the same questions of an earlier entry
   ([What was given up](#what-was-given-up)).
3. [`forgetting.tests/PreviewAndProofTests.cs`](forgetting.tests/PreviewAndProofTests.cs)
   and [`forgetting.tests/CommitTests.cs`](forgetting.tests/CommitTests.cs):
   each test's name states one behaviour of the preview, the proof, elision or
   Distill, such as `Eliding_MarksThePairs_ButKeepsEveryRecordOnDisk` and
   `Distill_KeepsTheJournalsLastRecord_EvenWhenItIsAnElidedErase`.
4. [The model](#the-model), for why forgetting is safe on this canvas at all:
   the picture depends only on the strokes it holds, and nothing in the domain
   counts past acts.

[Forgetting on Purpose](#forgetting-on-purpose) then runs the same steps on a
full session and measures each one.

### What you can skip

Much of the code measures the demonstration rather than forgetting anything.
`SketchActor.Census` and [`JournalFolder`](actor/JournalFolder.cs) count
records and bytes. The census copies the journal and reads the copy through the
Materialization API, so the primary's journal is never written to; it is a way
of counting for the evidence, not a step of forgetting. The lab in
[`forgetting/`](forgetting/) runs the scenes, times them and writes the
report, and [`view/`](view/) draws the pictures. If you do read
[`Lab.cs`](forgetting/Lab.cs), note that it takes the first picture of scene 6
before scene 5 commits: after Distill, that past can no longer be replayed as
it was.

### A first run

The full lab takes about a minute. A smaller session runs every scene in a few
seconds and writes the same evidence:

```
dotnet run --project Sketch/forgetting -c Release -- --out Sketch/out/first --survivors 50 --hesitations 150
```

### Break it on purpose

**Elide half of each pair.** In [`ForgettingRule.cs`](actor/ForgettingRule.cs),
change `.Metadata.Elide()` to `.Metadata.Elide("Erased")`. Naming a seek elides
only that seek's entries, so the rule now forgets each Erase and keeps its
Draw. Run `dotnet test Sketch/Sketch.sln` and read what fails. The preview
lists half as many entries. The proof of them comes back unsafe: with the
Erases skipped, every hesitation is back in the picture. Committed anyway, the
forgetting does not show at once. The canvas that ran the commit still prints
the right picture, because elision changes what rehydration replays, not the
state already in memory; the same journal opened again shows every
hesitation. The lab stops at its preview check.

**Give the canvas a memory.** In [`Canvas`](domain/Canvas.cs), add a count of
every stroke ever drawn: an `internal int Draws { get; private set; }` that
`Draw` increments after it lays the stroke. Then ask the diff about it: in
`ProveAt` in [`SketchActor`](actor/SketchActor.cs), pass a third question,
`"{ print canvas.Draws draws; }"`, to `ElisionImpactDiff`. Run the lab, and it
stops at the proof: eliding the rule's pairs changed an observation. The
picture and the stroke count are still unchanged. The new question's answer is
not: it drops from the number of Draws performed to the number of strokes that
stay, because a forgotten Draw no longer counts. That is why the domain keeps
no counter ([invariants](#invariants-and-where-they-live)). "Safe" is always
relative to the questions asked, and a canvas that remembers how many acts
came before has a question that forgetting changes.

## The model

### A picture is its strokes, in the order they were laid

A canvas is a **stack of strokes**. Drawing lays a stroke on top of every
stroke already there. Erasing lifts one stroke out and leaves the others
exactly as they lay. The picture is the stack read from the bottom up, each
later stroke lying over the earlier ones.

That one decision gives the model the property everything below relies on. The
picture depends only on *which strokes are on the canvas* and *the order they
were drawn in*. A stroke that was drawn and then erased leaves no trace: not
in the picture, not in the count, and not in the order of the others. Two
canvases that reach the same strokes by different histories, one hesitating
and correcting itself and one not, hold the same picture.

[`Canvas`](domain/Canvas.cs) keeps the stack as its single ordered sequence.
The order of the picture is the stack's own order, defined by the domain,
never the enumeration order of a container whose layout depends on its insert
and remove history. The difference matters for this example. The elision diff
below compares printed pictures character for character, so a container that
reordered itself after removals would turn a harmless forgetting into a false
"unsafe".

### What the canvas is made of

- [`Extent`](domain/Extent.cs) is the canvas's size, `Width` by `Height` whole
  units, and the single authority on what lies on the canvas. The surface runs
  from 0 to its width across and from 0 to its height down, **edges
  included**. `Contains(point)` is the membership predicate. `Encloses(stroke)`
  states the one geometric law the model needs: a straight mark lies inside a
  rectangle exactly when both of its end points do.
- [`Stroke`](domain/Stroke.cs) is one straight mark from a `From` point to a
  `To` point in one `Ink`. It is an entity known by its `Id`, and **the id is
  received from the host**: the canvas never invents one. A stroke whose two
  points coincide is a dot, and it is a mark like any other.
- [`Point`](domain/Point.cs) is a plain carrier of two coordinates.
- [`Ink`](domain/Ink.cs) is a closed palette of five: graphite, indigo,
  vermilion, ochre, viridian. Each is one shared instance, so an ink is
  recognised by reference and an ink outside the palette cannot be
  represented. On the wire an ink travels as its name. `Ink.Named(name)`
  resolves the name to the shared instance and refuses any other name.
- [`SketchRuleException`](domain/SketchRuleException.cs) is the one exception
  the domain throws.

No type overrides equality, and no collection is keyed by a value. Two points,
strokes or inks are the same only if they are the same instance.

### The verbs

- `Canvas(width, height)` opens a new, empty canvas. The constructor *is* the
  new canvas; there is no separate "new" operation.
- `Draw(id, fromX, fromY, toX, toY, ink)` lays stroke `id` on top. It is
  refused, leaving the canvas as it was, when a stroke `id` is already on the
  canvas, when either end point is off the canvas, or when the ink is not in
  the palette.
- `Erase(id)` lifts stroke `id` off. Erasing a stroke that is not on the
  canvas is refused. The query `HasStroke(id)` answers any id, so a caller
  asks first.
- `HasStroke(id)`, `StrokeCount` and `Picture()` are the queries. `Picture()`
  returns the strokes bottom to top, as values with their points and ink rather
  than as a format. How they are printed is the host's choice.

The operations are primitive-first. Ids, coordinates and the ink's name arrive
as plain values, and the canvas builds its points, its stroke and its ink from
them, validating before it touches the stack.

### Invariants (and where they live)

| Invariant | Enforced where |
|---|---|
| A canvas has a positive width and height | `Extent` constructor → `SketchRuleException` |
| Every stroke lies on the canvas, both end points, edges included | `Canvas.Draw`, through `Extent.Encloses` |
| No two strokes on the canvas share an id | `Canvas.Draw` refuses; `HasStroke` answers first |
| An ink is one of the palette's five | `Ink.Named` refuses any other name; `Ink`'s constructor is private |
| Only a stroke on the canvas can be erased | `Canvas.Erase` refuses; `HasStroke` answers first |
| Erasing a stroke leaves every other stroke where it lay | `Canvas.Erase` removes one element of the stack and nothing else |
| The picture's order is the stacking order | `Canvas.Picture()` reads the stack; no container order is involved |
| No state depends on how many acts came before | there is no counter and no generated id anywhere in the domain; the tests draw and erase a thousand strokes and compare with a new canvas |
| Determinism | no clock, no randomness, no I/O in the domain; the host supplies every id |
| A refused operation changes nothing | every verb validates first and mutates the stack last |

### Trade-offs

- **No stroke constrains another.** Strokes may cross, overlap or repeat each
  other. A rule such as "strokes may not overlap" would make the order of acts
  matter across strokes. Forgetting a drawn-then-erased pair could then change
  what a later stroke was allowed to be, which is exactly what the elision diff
  would catch. The core leaves such rules out; an extension could add one to
  show the diff catching it.
- **Ink resolved from a name, not an enum.** The palette is small and fixed,
  which usually argues for an enum. But the ink arrives on the wire by name,
  and an unknown name has to be refused by the domain in its own words. An
  enum parameter would be refused earlier, by the host's coercion.
- **Lookups scan the stack.** `HasStroke` and `Erase` walk the stack, which
  stays one ordered structure with no second index to keep in step. At the
  thousands of strokes this example draws, that is cheap. A much larger canvas
  would add an id index.
- **An erased id may be drawn again.** The canvas only refuses an id that is
  *on* it, so a stroke drawn under a freed id lands on top as a new stroke.
  The host never reuses ids; the domain does not need to know that.
- **Straight strokes only.** A curve is many short strokes, which is why the
  demonstration's picture is made of hundreds of them.

## Forgetting on Purpose

### The question

**The journal keeps everything. Can part of it be removed, with proof that
nothing anyone can see has changed?**

An actor's journal records every act it ever performed, and replaying it
rebuilds the actor. Most of a long history, though, no longer decides
anything. A drawing session is a clean case. The artist hesitates, draws a
short stroke, thinks better of it and erases it. Every such pair stays in the
journal forever and contributes nothing to the picture.

### What stays fixed, and what changes

- **Fixed:** the canvas domain, the scripted session (seed 42), and the two
  questions asked of the canvas, *what is the picture?* and *how many strokes
  are on it?*
- **Changes:** the journal, through three states. **Full** is the session as
  performed. **Logical** is the drawn-then-erased pairs marked as elided, with
  every record still on disk. **Physical** is the elided records removed by
  Distill.

The session draws a flower in five inks: 500 strokes that stay, and 1,500
short hesitation strokes, each erased a few acts after it was drawn. That is
3,500 acts in all, most of them drawn-then-erased pairs.

![The picture the session draws](notes/picture.svg)

### The rule

One reaction, defined in one place, [`ForgettingRule`](actor/ForgettingRule.cs):

```csharp
reactions.DefineReaction("ForgetErasedStrokes")
    .Job().Company().WithSharedHydration()
    .Seek("Drawn")
        .OnMatch("[_:Canvas].Draw($id, _, _, _, _, _)").One()
    .ThenSeek("Erased")
        .OnMatch("[_:Canvas].Erase($id)").One()
    .Metadata.Elide();
```

Read aloud: *when a stroke was drawn and the same stroke was later erased,
forget both acts.* The `$id` captured in the first seek fixes the id the second
must match, by value. Every seek of a multi-seek reaction declares how many
events of its kind make one match; `.One()` says one Draw and one Erase per
pair. The reaction observes V2 Actions, which is why the facade issues every
verb as a parametrised command rather than as script text.

The line after `DefineReaction` says how and where the rule runs. `.Job()`
makes each call to `Reactions.Execute()` one sweep of the journal, from where
the rule last stopped, after which the call returns. So the journal forgets
only when the host asks it to, and the same definition can preview on a shadow
and commit on the primary at moments the host chooses. A `.Cue()` reaction
does not return from `Execute()`: after its sweep it keeps running, on a
thread the host gives it, and marks each new pair moments after its Erase is
journaled, with no preview or proof in between.

`.Company()` is the activation scope, which matters only when the actor is
replicated: the rule runs on the director and on every follower, where
`.DirectorOnly()` or `.CastOnly()` would narrow it to the director or to the
followers. This actor is not replicated, so here the scope changes nothing.
`.WithSharedHydration()` sets how the matcher searches the journal,
breadth-first; `.WithIndependentHydration()` searches depth-first. For this
rule the choice changes how the search runs, not what it finds, but one of the
two must be named before the first `.Seek`. Paper 3 develops the two modes in
§6.1 and the activation scopes in §6.6.

The same definition runs in two places: on a shadow, to preview and prove the
forgetting, and on the primary, to commit it.

### Preview

A **shadow** is an isolated copy of the actor. It replays the primary's journal
into storage of its own and produces no effect outside itself. Opened in
*skip-preview* mode, its elision rule records the entries it *would* elide
(`Reaction.WouldSkip`) and commits nothing anywhere.

On the full journal the rule would elide **3,000 entries**: 1,500 pairs,
exactly the Draw and Erase of every hesitation. The primary's head, record
count, bytes on disk and printed picture were compared before and after the
preview, and all were unchanged.

### Proof

The elision-impact diff (`Shadow.ElisionImpactDiff`) rehydrates the journal
twice, once as it is and once with a candidate set of entries skipped. It asks
both twins the same questions and compares the answers character for
character.

- **The rule's 3,000 entries: safe.** Neither the picture nor the stroke count
  changes.
- **A near miss: unsafe.** The candidate set is every one of the rule's
  entries except one Draw, so stroke 1114's Erase is elided but its Draw is
  kept. The diff comes back non-empty and names both questions. With the Erase
  skipped, the erased stroke is back on the canvas:

| Without elision | With that candidate set elided |
|---|---|
| ![without](notes/unsafe-without.svg) | ![with](notes/unsafe-with.svg) |

Both pictures are drawn from the diff's own `WithoutElision` and `WithElision`
texts. The stroke that came back is highlighted.

### Commit

The rule then runs on the primary, which marks the pairs as elided in its
journal. Distill then removes the elided records physically. The journal is
released and the actor restarts from disk:

- **The picture is byte-equal.** The printed projection before the commit and
  after the restart is the same text (SHA-256 `f6587c64a5113d24…`), and the
  stroke count is the same.
- **Fewer records and fewer bytes**, as the table below shows: 3,504 records
  become 504. What remains is the seed's two records, the Define records of
  `Draw` and `Erase`, and one invocation per surviving stroke. An action's
  Define is a record of its own, so it survives the elision of its
  invocations. The elision marks go with the records they marked.
- **Entry ids continue.** After the restart the head is still 3,504, and the
  next command journals entry 3,505. No id is reused.

Distill never removes a journal's last record, even an elided one; the engine
defers it to a later Distill. The session ends on a surviving stroke, so here
nothing is held back. A test pins the other case: when the last record is an
elided Erase, it stays on disk, and rehydration still skips it.

### What was given up

The present is intact. The past is not.

Right after entry 1,695, in the middle of the session, the canvas held 241
strokes, 7 of them hesitations not yet erased. **Before Distill**, a shadow
synced to that entry shows them. **After Distill**, a shadow synced to the same
entry shows 234 strokes and none of those hesitations: their Draw records are
no longer in the journal. Up to that entry the journal held 1,695 records
before Distill and 238 after it.

| Before Distill: a shadow at entry 1,695 | After Distill: a shadow at the same entry |
|---|---|
| ![before](notes/past-then.svg) | ![after](notes/past-after-distill.svg) |

The diff saw it coming. Asked of that moment before anything was removed, the
same forgetting is **unsafe**: it changes the picture and the stroke count. The
picture it predicted is byte-equal to the one the shadow replays after Distill.

This is the honest cost of forgetting. Every question about the present
answers as before, and a question about what the canvas looked like halfway
through now has a different answer.

### Measured

Four lab runs on 2026-09-30, seed 42, at scale 1 (500 survivors, 1,500
hesitations) and scale 10. Each scale ran against the public engine clone
(`2673d57`) and the private engine checkout (`b083a5a`), both built in Release.
The evidence note has every number and the captured logs:
[`notes/forgetting-on-purpose.md`](notes/forgetting-on-purpose.md).

| | public `2673d57`, scale 1 | private `b083a5a`, scale 1 | public `2673d57`, scale 10 | private `b083a5a`, scale 10 |
|---|---|---|---|---|
| Acts (draws + erases) | 3,500 (2,000 + 1,500) | 3,500 (2,000 + 1,500) | 35,000 (20,000 + 15,000) | 35,000 (20,000 + 15,000) |
| Entries the rule would elide (`WouldSkip`) | 3,000 | 3,000 | 30,000 | 30,000 |
| Diff, the rule's entries | safe | safe | safe | safe |
| Diff, one Erase without its Draw | unsafe: picture, stroke count | unsafe: picture, stroke count | unsafe: picture, stroke count | unsafe: picture, stroke count |
| Records: full / logical / physical | 3,504 / 3,504 / 504 | 3,504 / 3,504 / 504 | 35,004 / 35,004 / 5,004 | 35,004 / 35,004 / 5,004 |
| Record-segment bytes: full / logical / physical | 196,158 / 196,158 / 34,250 | 196,158 / 196,158 / 34,250 | 1,992,086 / 1,992,086 / 343,214 | 1,992,086 / 1,992,086 / 343,214 |
| All bytes: full / logical / physical | 196,236 / 256,574 / 34,666 | 196,236 / 256,573 / 34,665 | 1,992,164 / 2,592,501 / 343,629 | 1,992,164 / 2,592,502 / 343,630 |
| Cold start, median of 5: full / logical / physical | 41.4 / 37.0 / 14.2 ms | 41.1 / 37.6 / 14.1 ms | 380.8 / 222.8 / 57.7 ms | 444.2 / 239.3 / 63.6 ms |
| Picture after the restart | byte-equal | byte-equal | byte-equal | byte-equal |
| Head after the restart, then the next entry id | 3,504, then 3,505 | 3,504, then 3,505 | 35,004, then 35,005 | 35,004, then 35,005 |
| Shadow at mid-session: strokes before / after Distill | 241 / 234 | 241 / 234 | 2,315 / 2,306 | 2,315 / 2,306 |
| After Distill, that past equals the diff's prediction | byte-equal | byte-equal | byte-equal | byte-equal |
| Whole run | 60.6 s | 60.2 s | 695.8 s | 676.0 s |

The logical state is larger on disk than the full one, because the elision marks
are written beside the records. Distill removes both. The logical state already
rehydrates faster than the full one: a cold start skips the elided entries,
though they are still on disk. Most of each run is the commit's elision on the primary:
49–50 s for 1,500 pairs and 581–595 s for 15,000. The same rule previewed on an
in-memory shadow took 1.3–1.4 s and 10.5–11.0 s.

### What it does not establish

- **"Safe" is relative to the questions passed to the diff**, here the picture
  and the stroke count. A question outside that list can change, and the
  canvas halfway through the session is one.
- **The developer writes the rule.** The engine does not work out what is safe
  to forget; it previews, proves and executes the forgetting it is told.
- **It is not a personal-data erasure mechanism.** Nothing here addresses
  copies, backups or anything outside this journal.
- **The timings come from one machine and one session size.** This is not a
  performance evaluation.
- **No backup destination was registered.** With a Materialization destination
  registered, `Distill().Now()` refuses until the destination has confirmed the
  entries (or the call is `.Forced()`). This run exercises Distill without
  that gate.

### Run it yourself

From the repository root, with the engine checked out beside this repository
(see the root [README](../README.md)):

```
dotnet test Sketch/Sketch.sln
dotnet run --project Sketch/forgetting -c Release -- --out Sketch/out/scale1
dotnet run --project Sketch/forgetting -c Release -- --out Sketch/out/scale10 --scale 10
```

The lab writes `report.html` (self-contained, with the pictures inline),
`numbers.json`, `run.log` and the pictures to `--out`. Options: `--seed 42`,
`--survivors 500`, `--hesitations 1500`, `--scale 1` (multiplies both counts)
and `--runs 5` (timed cold starts per journal state). Build the lab in
`Release`: the engine's Debug build writes diagnostics on every act and would
dominate the timings.

The lab checks every claim above while it runs, and a claim that does not hold
stops the run before any evidence is written. The acceptance tests pin the
same claims on small journals. They also pin the one limit of the rule: a Draw
and its Erase are paired only when the Erase arrives within **1001 entries** of
the Draw. The engine prunes a partial match that has not advanced for more
than 1000 entries, so an Erase that comes later is not paired, and both acts
stay in the journal. The session erases every hesitation well inside that
window, at most 13 entries after it was drawn.

### Grounded in

In [`alvaroNCubo/puppeteer-papers`](https://github.com/alvaroNCubo/puppeteer-papers):

- **Paper 3, *Reactions and the partition*, §6.5, "Metadata — declaring a
  trajectory closed":** the Metadata plane marks the events of a matched
  pattern as elided, and later rehydrations walk past them. The paper presents
  this as the natural outcome of a declarative correlation rather than a
  cleaning of the journal after the fact. §6.3 gives the correlation this rule
  relies on: a captured primitive is matched by literal equality in the stages
  that follow.
- **Paper 1, *Anti-porous architecture*, §A ("Domains where the actor cannot
  achieve state locality"):** skips evict journal events, and they presuppose
  locality: the framework can implement skips, but it cannot manufacture
  locality. Here the rule is where that locality is stated. The design says
  which acts no longer decide anything, and the engine carries the decision
  out.
- **Paper 6, *Most infrastructure layers are symptoms of the persistence
  model*, §4.1 (journal density):** Distill compacts the journal in place, and
  together with elision markers and Materialize watermarks (Paper 5, *The
  Journal as Substrate*, §7.3) it fills the role rolling snapshots play in
  conventional event-sourced systems, without snapshot semantics.

## Build and run

From the repository root:

```
dotnet build Sketch/Sketch.sln
dotnet test Sketch/Sketch.sln
dotnet run --project Sketch/forgetting -c Release -- --out Sketch/out/run
```

The engine is referenced from the checkout named by `PuppeteerRoot`, which
defaults to a `puppeteer` folder beside this repository (see
[`Directory.Build.props`](Directory.Build.props)). To build against another
checkout, pass `-p:PuppeteerRoot=<path>` or set a `PuppeteerRoot` environment
variable. The domain and its tests reference no engine at all, and the test
projects use MSTest from nuget.org. `Sketch/out/` is ignored by git.
