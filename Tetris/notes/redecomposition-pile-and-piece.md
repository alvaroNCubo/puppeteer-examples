# Re-decomposition: cutting `Well` into a pile role and a piece role

Paper 9 §8 reports re-decomposition as practice, not as measurement:

> "Sometimes the regret is not a missing verb but a boundary: a role was modelled too
> large and should have been two. The practice reported is to author the two, read the
> record of the original, and replay it into them, each with its own verbs and its own
> staging-facing surface; the original's code stays in version control and its record
> is kept."

This is that move, measured, on the cleanest available case: separating the Tetris
`Well` into a **pile role** and a **piece role**.

Everything below is reproducible; the commands are in the last section. All numbers
were measured against Puppeteer Pacifico **`e65f681`** — which matters, because an
earlier build's automatic Script→Action promotion broke this lab's choreography
outright and has since been reverted upstream (§8a). **The lab requires an engine at
or after `e65f681`.**

---

## 1. Why this case is sound

Collision in `Well` is atomic and membership-driven: one `Intersects` test decides
wall-, floor- and pile-collision alike, and `Land()` absorbs the piece and collapses
rows in a single private transition. Splitting that across two actors looks like it
must break an invariant, because `tell` is post-commit and reaction-only (Paper 4) —
there is no synchronous cross-actor question available.

It does not break, and the reason is a property of the game rather than of the
framework: **the pile is immutable for the entire lifetime of a falling piece.** The
pile changes only when a piece lands, and that landing is the act that ends the
piece's life. The two phases never overlap. So:

- the piece role is given the pile's occupied cells **once**, and decides every
  collision **locally** against that projection — no concurrent mutation is possible,
  so nothing is speculated;
- on landing, the piece role tells the pile role to absorb its cells; the pile role
  absorbs, collapses completed rows, and tells the piece role the pile that resulted.

There is no synchronous cross-role query anywhere in that flow.

## 2. What was built

**The two roles** (`Tetris/domain/`), each an aggregate root with its own actor, its
own journal, and its own verbs:

| | pile role — `PileWell` | piece role — `PieceWell` |
|---|---|---|
| holds | `Frame`, `Pile`, `ClearedLines` | `Frame`, the pile as a projection, the falling `Piece` |
| verbs | `Absorb(cells)` | `Spawn`, `MoveLeft`, `MoveRight`, `Rotate`, `Tick`, `Drop`, `Take(pile, over)` |
| owns | the collapse; **game over** | the collision decision; piece selection |
| queries | `Projection`, `IsGameOver`, `ClearedLines`, `OccupiedInterior` | `IsFalling`, `IsSettling`, `IsAwaitingPiece`, `Active`, `LandedCells`, `LandingToken` |

`Pile` and `Piece` were already separate classes; the work was moving the collision
decision to the piece side and the absorb/collapse to the pile side. The collapse
logic itself did not move — `Pile.Integrate` still owns it, and `PileWell` calls it.

**Game over is the pile role's, and only the pile role's.** The piece role could
derive it from the projection it already holds, and for exactly that reason must not:
two roles reckoning one fact can disagree, and then there is no fact of the matter.
So the piece role is *told*, and holds the pile role's word for it. This is Paper 9
§8's authority argument applied inside a domain rather than across clients.

**The `tell` wiring** (`Tetris/actor/SplitTetrisActor.cs`), per
`training-lab/guides/reactions.md` — a command records a fact, a reaction downstream
speaks about it, and the hearer takes it up with its own verb:

1. `piece.Tick()` / `piece.Drop()` lands a piece and `expose`s onto that same journal
   entry what the utterance will carry — the resting cells and the landing's identity.
   Guarded by `IsSettling`, so a tick that merely descended exposes nothing.
2. A reaction on the piece role matches the snapshot and
   `.Causation.Continue("tell Landed with @cells, @token to pile once @token;")`.
3. The pile role takes it up (`Told`) with its own verb — `pile.Absorb(@cells)` —
   acks the piece role, and exposes the pile that resulted plus its verdict on game
   over.
4. A reaction on the pile role matches that and `tell Absorbed … to piece`; the piece
   role takes it up with `piece.Take(cells, over)` and stops settling.

`once @token` (`land-1`, `land-2`, …) makes each landing's utterance idempotent, and
the token counts landings rather than reading a clock, so a replay reproduces it
exactly.

## 3. Did the invariant survive?

Three checks after **every** op, all made from *outside* both roles by querying each
separately and combining the answers here — neither role is trusted to police a
property that spans them.

| policy | games | steps compared | landings | lines cleared | reached game over | divergences |
|---|---|---|---|---|---|---|
| `random` | 20 | 2,614 | 243 | 0 | 20/20 | **0** |
| `flat` | 20 | 5,169 | 685 | 20 | 19/20 | **0** |
| `clears` | 20 | 40,000 | 8,337 | 3,316 | 0/20 | **0** |
| **total** | **60** | **47,783** | **9,265** | **3,336** | **39/60** | **0** |

- **No piece ever overlapped the pile.** 47,783 cross-role overlap checks, zero hits.
- **Boards, line counts, game over, awaiting, and the falling piece's type matched
  the single-actor version step for step**, for identical input sequences, on all
  47,783 steps.
- **The two roles never disagreed about game over.** Checked on every step.
- **No half-finished landing was ever observable** once a verb returned.

The three policies exist because the first one was not good enough evidence. A random
player almost never completes a row: 20 random games cleared **0** lines, so they
tested nothing about the collapse — which is the one transition the re-cut moved from
one role to the other. `flat` plays properly (slides each piece onto the flattest
span, then hard-drops) and `clears` feeds nothing but squares, five of which fill two
whole rows across a 10-wide well. Together they exercise 3,336 collapses and 39
game-overs.

## 4. What did the split cost?

### The domain diff

| file | status | code lines |
|---|---|---|
| `domain/Well.cs` | **untouched** | 0 changed (166 remain) |
| `domain/Pile.cs` | changed | **2** (`Integrate(Piece)` → `Integrate(Shape)`) |
| `domain/PileWell.cs` | new | 83 |
| `domain/PieceWell.cs` | new | 156 |
| `domain/CellSet.cs` | new | 12 |
| `domain/CellCodec.cs` | new | 41 |

**One existing domain file changed, by two lines.** `Integrate` takes a `Shape`
instead of a `Piece` so a role holding only the landed *cells* can settle the pile
the same way. `Well.cs` was not touched at all — the original stays exactly as it is,
still running, still passing its own 44 tests.

The two roles come to 239 code lines against the well's 166 — **1.44×** — plus 53
lines of `CellSet` + `CellCodec` that exist *only* because a cell set now has to
cross a role boundary. Note that the duplication is deliberate: `PileWell` re-derives
the spawn region and both roles hold their own `Frame`, because extracting those into
something shared would have meant editing the original. Choosing duplication over an
edit is a judgment call, and the alternative cost is small (≈5 hunks in `Well.cs`);
it is recorded here because it is a real cost either way.

### What crosses the boundary

- **Per move (left, right, rotate, spawn, tick that does not land): 0 tells.** The
  piece role decides everything locally.
- **Per landing: 2 tells and 2 acks** — `Landed` → ack → `Absorbed` → ack.

### What the record costs

For the same 40 acts, measured on complete journals:

| | entries |
|---|---|
| single `Well` | 41 |
| piece role | 70 |
| pile role | 30 |
| **two roles together** | **100 (2.44×)** |

A landing costs **7 journal entries across two roles** where it cost **1** in one:
the landing act, the `tell Landed`, its ack and the `Take` on the piece side; the
`Absorb`, the `tell Absorbed` and its ack on the pile side.

### Was anything speculated, or any check relaxed?

**No, and two checks got stronger.** Nothing in either role is a guess about the
other: the projection the piece role decides against is not a prediction of the pile,
it *is* the pile, because the pile cannot change while a piece falls.

`PileWell.Absorb` now *refuses* what `Well` only asserted after the fact — it rejects
cells that are not exactly four, that fall outside the frame, or that the pile
already occupies. Under the well these were invariants checked after the transition;
here they are preconditions on being told something, which is the stronger position.

Four things were genuinely given up or added, and they are the finding:

1. **A new state exists: `IsSettling`.** The piece role has four states where the
   well had three. Under the well, landing was one atomic transition, because one
   object owned both halves. Across two roles the absorb is somebody else's act, so
   there is a moment in between and a caller can see it. It is not speculation — no
   piece exists while settling, so nothing can be wrong — but it is a state that has
   to be named and handled.
2. **A guard had to be restated.** The well guarded its move verbs with
   `IsGameOver == false && IsAwaitingPiece == false`, which *was* "a piece is
   falling" only because the well had three states. With four it is not, so the piece
   role states `IsFalling` outright.
3. **A cell set had to acquire a canonical rendering.** A `tell` carries ordered
   scalars; a pile is a variable-length set. So `CellCodec` renders it as one
   canonical string (sorted, so the same set always renders the same way — which
   matters because an utterance's identity may be a content hash). This is a
   serialization *inside the domain* that the single-actor version had no need for.
4. **The frame push channel cannot be reproduced, and this is the one real capability
   lost.** `TetrisActor` pushes a whole-well frame from one reaction. A whole-well
   frame is a join over *both* roles, and `.Program.Emit` is read-only within a
   single actor — so **neither role can emit it**. Each could push its own half, and
   something outside would have to join them. The pull path (`Snapshot()`) does that
   join, in the staging; the push path has no equivalent, and is simply absent from
   `SplitTetrisActor`. Reported rather than engineered around.

## 5. The re-decomposition itself

### How the acts were read

Not by touching the journal. The original journal is **opened read-only, read as the
account of what happened, and kept** — it is never cut, transformed, rewritten, or
copied into the new roles. Transplanting entries would put acts a role never
performed into that role's record, which is exactly what a first-person journal must
not contain (Paper 4).

The read is the framework's own, and it is the same read a rehydration does:

- `Actor.ConfigureStorageForIntrospection(...)` — configures storage **without
  rehydrating**, so the actor cannot perform, tell, or react; only the read verbs are
  open;
- `Actor.Introspection.ShowEntry(entryId)` — walked in order, taking each act with
  its parameters;
- an `invocation` records only an `actionId` and its arguments, so it is **joined to
  the `define` entry that holds its sentence** — the same join a rehydration performs.

That last step is implemented but, on this engine build, **not exercised**: all 41
entries of the record are `script`, none `define` or `invocation`. It is worth saying
why it is there anyway, because the history is instructive. An earlier engine build
auto-promoted any recurring command shape to an Action after ten occurrences, so the
same game's record came back as 46 `script` + 4 `define` + 54 `invocation` entries, and
a reader that understood only `script` would have mis-read 54 of 104 acts. That
promotion has since been reverted upstream (§8a). The join stays in the reader because
it is the correct reading of a journal — genuinely parametrized commands still journal
as Action pairs, and journals written by pre-revert builds still exist.

**This is also the direction that scales**, and it is worth saying because the
instruction could be read the other way. Operating on journals as artifacts — cut this
one in two, transform its entries — gets rapidly worse as journals grow, and here it
is not even available: the FileSystem backend is binary. Reading acts and
re-performing them is bounded by the acts themselves, and it is what a follower
already does.

### What each role performed

The translation, on a 40-act game whose record is complete:

| acts in the original record | issued | recorded | translates into |
|---|---|---|---|
| `upgrade('seed') { well = Well(10,20); }` | — | 1 | opens both roles (not performed) |
| `well.Spawn(letter)` | 10 | 10 | piece role: `piece.Spawn` |
| `well.MoveLeft()` | 12 | 12 | piece role: `piece.MoveLeft` |
| `well.MoveRight()` | 9 | 9 | piece role: `piece.MoveRight` |
| `well.Drop()` | 9 | 9 | piece role: `piece.Drop` |
| **total** | **40** | **40** | **40 re-performed** |

**Every act of the original translates, and none was lost.** But look at which
vocabulary they translate into: **all of them are the piece role's, and not one is the
pile role's.** That is the sharpest thing this experiment found.

The pile role's own act — `Absorb` — appears **nowhere in the original record**,
because under the well the absorb was never an act: it was a private consequence of a
tick. There is no `absorb` sentence to translate. So the pile role's record is not
transcribed from the original at all; it is **generated** during the re-decomposition,
by the piece role performing its own verbs and telling. The record that carries across
is the piece role's; the pile role's record is a consequence of replaying it.

Resulting journals, read back in a fresh process (9 landings; the tenth piece is still
falling):

| piece role — 70 entries | | pile role — 30 entries | |
|---|---|---|---|
| `piece.MoveLeft` | 12 | `pile.Absorb` | 9 |
| `piece.Spawn` | 10 | `tell Absorbed` | 9 |
| `piece.MoveRight` | 9 | `tell ack` (from piece) | 9 |
| `piece.Drop` | 9 | `upgrade` | 1 |
| `piece.Take` | 9 | *(+2 declarations)* | |
| `tell Landed` | 9 | | |
| `tell ack` (from pile) | 9 | | |
| `upgrade` | 1 | | |
| *(+2 declarations)* | | | |

The first four rows are exactly the 40 acts the record held, re-performed. Everything
below them exists only because the domain is now two roles.

### Is the resulting state equivalent?

Yes, exactly. Re-performing the recorded acts on the two roles reached the state the
well was in after performing exactly those acts:

```
  MATCH  board: 0,3 0,4 1,4 1,5 14,0 14,1 14,2 14,3 14,6 15,1 15,4 15,5 ...
  MATCH  lines cleared: 0
  MATCH  game over: False
  MATCH  awaiting piece: False
  MATCH  falling piece: Z
```

### Was the original journal modified?

**The record: no.** 41 acts before, 41 after, identical entry for entry — same ids,
same kinds, same sentences, same arguments. Nothing appended, nothing removed (file
lengths unchanged).

**The container: not byte-identical, once.** On the *first* open after the writing
process exits, `meta.bin` and the journal file's header change content (same length).
A second open changes nothing further. So the act of opening a journal normalises its
header once; it does not alter the recorded acts. Reported precisely rather than
rounded to "unchanged", because the fingerprints do differ and a reader of this note
would find that out.

**Append-only, confirmed.** Both new journals have entry ids forming the contiguous
run 1..N — nothing written out of order, nothing edited in place.

## 6. Staging ripple

Two counts, kept separate.

**Hosts and clients not touched at all: 11 of 12.** Not one line. Every one of them
still drives the single `Well` exactly as before, because nothing they depend on
changed. Verified by a full-solution build (0 warnings, 0 errors) and by playing
`console --auto` through to game over on the single-`Well` path.

**Hosts edited to drive two roles instead of one: 1, by one line.** The interactive
console, which now drives either cut and still works on both:

```csharp
using IGameActor game = args.Contains("--split")
    ? SplitTetrisActor.InMemory("console", width, height)
    : new TetrisActor("console", width, height);
```

Everything else in the interactive console — the keyboard handling, gravity,
rendering, and all 20-odd call sites — is unchanged and unaware of which cut it is
driving. `console --auto --split` plays a full game to game over on the two roles.

What made it one line: a new `IGameActor` interface (14 code lines) that both cuts
satisfy, at a cost of **one line** in `TetrisActor` (`: IDisposable` → `: IGameActor`)
plus one additive method (`Spawn(letter)`, needed to feed both cuts identical input).
This is the cheap side of the line, as Paper 9 would predict: the surface absorbed the
re-cut, and neither cut had to negotiate with the other.

**Of the remaining 11 hosts, 8 would need more than an edit, for two reasons I should
name rather than leave implied:**

- **5 wire the frame push channel** (`ai`, `server`, `input`, `web`, `web-rest`).
  These cannot be pointed at the split as built, because no reaction can emit a
  whole-well frame from one role (§4, item 4). This is a missing capability, not a
  missing line.
- **3 are `StageV2` hosts** (`sm-duo`, `sm-duo-tls`, `sm-server`). `SplitTetrisActor`
  is `PerformanceV2`-only; a distributed two-role staging was not built. That is
  scope, not an obstacle — the reaction and `Told` wiring sits at the actor handler,
  below the host topology.

The other 3 (`observer`, `watch`, `send`) either read the frame file or send keys
through a pipe, and are indifferent.

## 7. Verdict

**The claim holds, with one qualification worth stating plainly.**

*What carries across is the record* — supported, and more sharply than expected. The
original journal was read, never altered, and never transformed; the two roles were
put to perform their own verbs from it; and the state they reached was exactly the
state the well was in. Each ended with an append-only journal in its own voice
containing only its own acts. The old code and the old record both remain.

*The re-cut is the author's move, not a staging's demand* — supported. Nothing in any
staging asked for this. The staging side of the line absorbed the whole re-cut for one
line per host plus one interface; the domain side is where all the work was. And the
direction of dependence held in the sharpest available sense: **11 of 12 hosts were
never touched at all while the domain was cut in two beneath them**, and the twelfth
needed one line.

The qualification: **the record that carries across is not neutral between the two
roles.** Every act in the well's journal turned out to be the *piece* role's. The pile
role's defining act was invisible in the original because it had never been an act at
all — only a private consequence — so the pile role's record had to be generated
rather than translated. Paper 9 says the two roles are reached "by replaying [the
acts] rather than by discarding them", and that is true; but on this evidence a
re-decomposition does not distribute an existing record between two new roles. It
replays the record into the role that inherits the original's *voice*, and the other
role's record comes into existence as a consequence. Whether that generalises — whether
one of the two roles always inherits the record — this one case cannot say. It is what
happened here, and it happened because the boundary being drawn was around a
transition that had been private.

Two further honest limits. The cost is not nothing: 2.4× the journal, 7 entries per
landing instead of 1, a new state, a serialization inside the domain, and one
capability (the pushed frame) that the split cannot currently provide at all. And the
same threat to validity that Paper 9 §8 raises about its own zeros applies here: the
re-cut, the roles, and the hosts were all authored by the same party.

## 8. Three engine defects found

Two of these blocked the work outright and had to be diagnosed before it could
proceed. All three are about the engine, not about the re-cut, and all three were
filed separately against `Puppeteer Pacifico`. As of 2026-07-26 (a) is fixed and merged,
(b) is fixed on an unmerged branch, and (c) is unreachable after (a)'s revert. What
follows is what each one was and how it was pinned down, since that is the part a
future reader needs.

**(a) Literal lifting renamed an `expose` alias — FIXED upstream.** The engine
promoted any command shape repeated ten times into a `define action` by rewriting its
literals into generated parameters. The rewrite worked on the *rendered text*, where a
LABEL is indistinguishable from a VALUE, so it consumed labels: an `expose`/`print`
alias — which is both the `exposeData` key and the identifier a reaction pattern
matches — became `p0`. The exposed field was silently renamed and every reaction
naming that alias stopped matching. It broke this lab's landing choreography mid-game:
the first ten landings were spoken, and from the eleventh the piece role's utterance
never fired and the game hung with a landing that could not settle.

Isolated with a deterministic probe (repetitions 1–11 matched, 12–14 did not, and the
journal showed `define action 1 (p0:string) as Expose well.NextPieceLetter() p0;
end;`). Resolved upstream in `e65f681` not by patching the alias case but by
**reverting the whole automatic Script→Action promotion mechanism**: at the text level
the label/value distinction is not mis-implemented, it is absent, so every construct
that renders a name was another instance of the same bug. The lab's workaround
(forcing the command to be an Action from its first use with `WithParameters`) has been
removed, and the probe retired — its invariant is now covered upstream by
`ScriptIsNotRewrittenTests`. The 9,265 landings of §3 re-run clean without it.

**(b) A stale sparse index hides the journal's tail — FIXED on an unmerged branch.**
`ForEachRawRecord` (`DiaryStorageFileSystem.cs:715`) skips a journal file whose indexed
`LastEntryId <= afterEntryId`. The index is not persisted per append, so after a clean
shutdown every entry past its high-water mark is unreadable — to introspection *and* to
rehydration — while its bytes sit on disk. Measured on `e65f681`: the journal file keeps
growing (5,031 → 5,285 → 6,467 bytes) while the readable count freezes at exactly **100
entries**; a 129-act game loses 30 acts. With no index file present, everything reads
back correctly, which is what identifies the persisted index as the culprit rather than
the writer. This is why §5 uses a 40-act game: its record is complete.

The root cause, from the session that fixed it: `index.bin` is persisted every
`PERSIST_METADATA_INTERVAL` = **100 writes** while every record is durably flushed, and
the loaded index was never validated against the journal files at open. Since readers
treat `LastEntryId` as an upper bound on what a file *contains*, a lagging index
discards the whole file. That is exactly the 100 measured here — the ceiling was the
last periodic save, not a property of the data. Fixed by reconciling the index against
the journal files in `Initialize` (sealed per-file header, or a record scan when the
header was never sealed), widening ranges only, and persisting the healed index. Branch
`claude/cool-euler-980e55`, commit `7dec2a1`, **not merged to master**; 4 new regression
tests in `UnitTestPuppeteer/PersistedIndexStalenessTests.cs`, all 4 failing without it.

Two consequences worse than the one this lab hit, which the same fix covers and which
are worth recording because a reader of these numbers should know the defect was not
merely cosmetic:

- with a small `maxFileSize`, a rollover *after* the last index save leaves the whole
  new file with no index entry — invisible even to a **full rehydration**, not just to
  `ShowEntry`;
- `SkipStore.FindFullySkippedFiles` derives a file's range from its index entry, so a
  short range could make `Trim` **delete a file that still held live records**.

Until that branch merges, the numbers above stand as measured. After it merges the
sweep in §9 should show the readable count tracking the written count for every N —
which is the confirmation, and re-running it is the check to make.

**(c) Rehydration failed on an auto-promoted nullary action — moot after (a)'s
revert.** Promotion also fired for shapes with no literals to lift, writing invocations
with an empty argument list; replay then threw `LanguageException: Arguments cannot be
null or empty` per entry, logged it, and continued — so the actor recovered a silently
wrong state. With automatic promotion gone there is no longer a path that produces such
an entry, so this is unreachable on `e65f681`. It is recorded because journals written
by earlier builds still contain those rows, and because the failure mode it exposed —
a rehydration parse error that is logged and swallowed, handing back a started actor
with knowingly incomplete state — is worth fixing on its own terms.

Note what (b) and (c) meant together on the pre-revert build: **the well's own journal
could not be correctly replayed by the framework that wrote it.** The
re-decomposition's read coped (joining defines to invocations, and reporting gaps
rather than stopping at the first one); the framework's own rehydration of the same
record did not. That comparison is what turned two silent misreadings into two filed
defects, and it is the reason the re-decomposition is measured on a short game.

## 9. Reproducing

From `Tetris/`:

```bash
dotnet test domain.tests/TetrisDomain.Tests.csproj
```

56 tests: the original 44 over `Well`, unchanged and still passing, plus 12 over the
two roles.

```bash
dotnet run --project redecomp/TetrisRedecomp.csproj -- equivalence clears 20 2000
```

Experiment 1 — the invariant and the agreement of the two cuts. Also `flat` and
`random`.

```bash
dotnet run --project redecomp/TetrisRedecomp.csproj -- play /tmp/rd/original 3 40
```

Experiment 3a — play a game on the single `Well` and leave its journal behind. A
separate process on purpose: the record has to be closed to be the record.

```bash
dotnet run --project redecomp/TetrisRedecomp.csproj -- redecompose /tmp/rd/original /tmp/rd/recut
```

Experiment 3b — the re-decomposition: read the acts, re-perform them onto the two
roles, compare the state, fingerprint the original before and after, and report each
new journal's verbs.

```bash
dotnet run --project redecomp/TetrisRedecomp.csproj -- dump recut-piece /tmp/rd/recut/piece
```

Read any journal in a fresh process.

```bash
dotnet run --project console/TetrisConsole.csproj -- --auto --split
```

The staging ripple — the same interactive console, one line changed, driving two roles.
Drop `--split` for the single `Well`.

Requires an engine at or after `e65f681` (§8a). On an earlier build the landing
choreography silently stops working after the tenth landing.
