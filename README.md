# Puppeteer examples

Self-contained, runnable examples that exercise the public Puppeteer V2 API.

## Companion repositories

This repo references the Puppeteer framework as a sibling project on disk.
Clone both side by side:

```
git clone https://github.com/alvaroNCubo/puppeteer.git
git clone https://github.com/alvaroNCubo/puppeteer-examples.git
```

The expected layout is:

```
<parent-dir>/
├── puppeteer/             the framework source (referenced by every example)
└── puppeteer-examples/    this repo
```

The papers that develop the design conditions illustrated here live at
[`alvaroNCubo/puppeteer-papers`](https://github.com/alvaroNCubo/puppeteer-papers).
Paper 1 (*Anti-porosity*) is the conceptual entry point.

## Examples

- [`HelloWorld/`](HelloWorld/) — one actor, one verb, two storage backends
  (`IN_MEMORY` and `FileSystem`). The minimal first contact with the
  framework. *Por uno se empieza.*
- [`Tetris/`](Tetris/) — a clean, infrastructure-free DDD model of Tetris
  (pieces, well, pile, boundary, collision, line clears) built around one
  abstraction: everything occupied is a *figure of cells*. No Puppeteer
  reference yet; a later phase wraps the aggregate root as an actor for the
  distributed-observation labs.
- [`Sketch/`](Sketch/) — a drawing canvas whose picture depends only on the
  strokes it holds, and the demonstration *Forgetting on Purpose*: the
  drawn-then-erased strokes of a session are forgotten from the journal,
  previewed on a Shadow, proved harmless by the elision-impact diff, and
  removed by Distill. The picture stays byte-equal and the journal shrinks.

## How to run

From the root of this repo:

```
dotnet build HelloWorld/HelloWorld.sln
dotnet run --project HelloWorld/inmemory/HostInMemory.csproj
dotnet run --project HelloWorld/filesystem/HostFileSystem.csproj
```

See each example's `README.md` for the expected output.
