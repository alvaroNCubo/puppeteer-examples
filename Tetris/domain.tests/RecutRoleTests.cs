using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tetris.Tests;

/// <summary>
/// The two roles <see cref="Well"/> was re-cut into, each tested on its own — which
/// is the point of the re-cut, that each can be. These tests exercise the seam the
/// split created and nothing else: what each role refuses to be told, the state that
/// exists only because landing is now two acts by two parties, and the fact that the
/// legality rule survived being given one of its operands by somebody else.
/// <para>
/// The whole-game equivalence of the two cuts is measured separately, by the harness
/// in <c>Tetris/redecomp</c>; see <c>Tetris/notes/redecomposition-pile-and-piece.md</c>.
/// </para>
/// </summary>
[TestClass]
public sealed class RecutRoleTests
{
    private static string Cells(params (int Row, int Column)[] cells) =>
        CellCodec.Encode(cells.Select(c => new Position(c.Row, c.Column)));

    // ── The pile role ───────────────────────────────────────────────────────

    [TestMethod]
    public void PileRole_AbsorbsALandingAndCollapsesTheRowItCompleted()
    {
        var pile = new PileWell(4, 6);

        // Rows 4 and 5, columns 0..1 — an O's worth of cells in the bottom-left.
        pile.Absorb(Cells((4, 0), (4, 1), (5, 0), (5, 1)));
        Assert.AreEqual(0, pile.ClearedLines, "nothing is complete yet");

        // The mirror image completes both rows across the width of 4.
        pile.Absorb(Cells((4, 2), (4, 3), (5, 2), (5, 3)));
        Assert.AreEqual(2, pile.ClearedLines, "both rows completed and collapsed");
        Assert.AreEqual(2, pile.LastCollapsed);
        Assert.AreEqual(string.Empty, pile.Projection, "the pile is empty again");
    }

    [TestMethod]
    public void PileRole_RefusesToAbsorbCellsThePileAlreadyOccupies()
    {
        var pile = new PileWell(6, 6);
        pile.Absorb(Cells((4, 0), (4, 1), (5, 0), (5, 1)));

        // Under the well this was an invariant asserted after the fact. Here it is a
        // precondition on being TOLD something, which is the stronger position: the
        // role that owns the pile is the one that refuses.
        Assert.ThrowsException<TetrisRuleException>(
            () => pile.Absorb(Cells((4, 1), (4, 2), (5, 1), (5, 2))));
    }

    [TestMethod]
    public void PileRole_RefusesToAbsorbSomethingThatIsNotATetromino()
    {
        var pile = new PileWell(6, 6);

        Assert.ThrowsException<TetrisRuleException>(
            () => pile.Absorb(Cells((5, 0), (5, 1))), "two cells are not a tetromino");
        Assert.ThrowsException<TetrisRuleException>(
            () => pile.Absorb(Cells((5, 0), (5, 1), (5, 2), (5, 3), (5, 4))), "nor are five");
    }

    [TestMethod]
    public void PileRole_RefusesToAbsorbCellsOutsideTheFrame()
    {
        var pile = new PileWell(6, 6);

        Assert.ThrowsException<TetrisRuleException>(
            () => pile.Absorb(Cells((5, 6), (5, 7), (4, 6), (4, 7))), "beyond the right wall");
        Assert.ThrowsException<TetrisRuleException>(
            () => pile.Absorb(Cells((6, 0), (6, 1), (5, 0), (5, 1))), "below the floor");
    }

    [TestMethod]
    public void PileRole_DerivesGameOverFromTheSpawnRegion()
    {
        var pile = new PileWell(4, 6);
        Assert.IsFalse(pile.IsGameOver, "an empty pile is not game over");

        // The spawn region is the top two rows across the four spawn columns. A
        // landing that reaches row 1 of those columns means the next piece has
        // nowhere clear to appear.
        pile.Absorb(Cells((0, 0), (0, 1), (1, 0), (1, 1)));
        Assert.IsTrue(pile.IsGameOver, "the pile has risen into the spawn region");
    }

    // ── The piece role ──────────────────────────────────────────────────────

    [TestMethod]
    public void PieceRole_DecidesCollisionAgainstTheProjectionItWasGiven()
    {
        var piece = new PieceWell(10, 20);

        // A floor's worth of pile, told to the piece role as a projection. Nothing is
        // asked of the pile role from here on: the piece decides locally.
        piece.Take(CellCodec.Encode(
            Enumerable.Range(0, 10).Select(column => new Position(19, column))), over: false);

        piece.Spawn(PieceType.O);
        for (var i = 0; i < 30; i++)
        {
            if (!piece.IsFalling)
            {
                break;
            }

            piece.Tick();
        }

        Assert.IsTrue(piece.IsSettling, "the piece landed on the projected pile");

        // The O spawns at column (10-4)/2 = 3, so it occupies columns 3..4, and it
        // comes to rest on rows 17..18 — on TOP of the projected row 19, not through
        // it. The projection was decided against locally; nothing was asked of the
        // pile role.
        Assert.AreEqual(Cells((17, 3), (17, 4), (18, 3), (18, 4)), piece.LandedCells,
            "it came to rest on top of row 19, not through it");
    }

    [TestMethod]
    public void PieceRole_LandingLeavesItSettlingUntilItIsToldThePileThatResulted()
    {
        var piece = new PieceWell(10, 20);
        piece.Spawn(PieceType.O);
        piece.Drop();

        // The state the split created. Under the well, landing was one atomic
        // transition because one object owned both halves; across two roles the
        // absorb is somebody else's act, so there is a moment in between.
        Assert.IsTrue(piece.IsSettling);
        Assert.IsFalse(piece.IsFalling);
        Assert.IsFalse(piece.IsAwaitingPiece, "it is not ready for the next piece yet");
        Assert.AreEqual("land-1", piece.LandingToken);

        Assert.ThrowsException<TetrisRuleException>(
            () => piece.Spawn(PieceType.I), "cannot spawn before the landing settles");

        piece.Take(piece.LandedCells, over: false);
        Assert.IsFalse(piece.IsSettling);
        Assert.IsTrue(piece.IsAwaitingPiece, "now it is between pieces");
    }

    [TestMethod]
    public void PieceRole_TakesGameOverAsToldRatherThanReckoningIt()
    {
        var piece = new PieceWell(10, 20);
        piece.Spawn(PieceType.O);
        piece.Drop();

        // The pile role owns the verdict. The piece role holds its word for it — even
        // though the projection it is given would let it compute the same thing, which
        // is exactly why it must not: two roles reckoning one fact can disagree.
        piece.Take(piece.LandedCells, over: true);

        Assert.IsTrue(piece.IsGameOver);
        Assert.IsFalse(piece.IsAwaitingPiece);
        Assert.ThrowsException<TetrisRuleException>(() => piece.Spawn(PieceType.I));
    }

    [TestMethod]
    public void PieceRole_RefusesToBeToldAPileWhileAPieceIsStillFalling()
    {
        var piece = new PieceWell(10, 20);
        piece.Spawn(PieceType.O);

        // The pile cannot change under a live piece — that is the whole reason the
        // split is sound — so being told one is refused rather than absorbed.
        Assert.ThrowsException<TetrisRuleException>(() => piece.Take(string.Empty, over: false));
    }

    [TestMethod]
    public void PieceRole_BlockedMovesAreNoOpsAndTheMoveVerbsNeedAFallingPiece()
    {
        var piece = new PieceWell(10, 20);

        Assert.ThrowsException<TetrisRuleException>(() => piece.MoveLeft(), "nothing is falling");
        Assert.ThrowsException<TetrisRuleException>(() => piece.Tick());

        piece.Spawn(PieceType.O);
        for (var i = 0; i < 10; i++)
        {
            piece.MoveLeft();
        }

        var atTheWall = piece.Active!.Cells;
        piece.MoveLeft();
        CollectionAssert.AreEquivalent(
            atTheWall.ToList(), piece.Active!.Cells.ToList(),
            "a blocked slide is a valid no-op, not a failure");
    }

    // ── The rendering the two must be joined for ────────────────────────────

    [TestMethod]
    public void TheTwoRolesTogetherRenderWhatTheWellRenderedAlone()
    {
        var pile = new PileWell(10, 20);
        var piece = new PieceWell(10, 20);

        pile.Absorb(Cells((19, 0), (19, 1), (18, 0), (18, 1)));
        piece.Take(pile.Projection, pile.IsGameOver);
        piece.Spawn(PieceType.T);

        // Neither role knows the whole board. The union is the frame, and the join
        // happens outside both of them — which is a cost of the split, not a feature.
        var joined = pile.OccupiedInterior().Union(piece.OccupiedInterior());
        Assert.AreEqual(4 + Piece.CellCount, joined.Count);
        foreach (var cell in piece.Active!.Cells)
        {
            Assert.IsTrue(joined.Contains(cell));
        }
    }

    // ── The rendering the roles' one shared encoding has to survive ─────────

    [TestMethod]
    public void CellCodec_RoundTripsAndIsCanonical()
    {
        var cells = new[] { new Position(5, 3), new Position(1, 9), new Position(1, 2) };

        Assert.AreEqual("1,2 1,9 5,3", CellCodec.Encode(cells), "sorted by row then column");
        Assert.AreEqual(CellCodec.Encode(cells), CellCodec.Encode(cells.Reverse()),
            "the same set always renders the same way, whatever order it arrives in");
        CollectionAssert.AreEquivalent(cells, CellCodec.Decode(CellCodec.Encode(cells)).ToList());
        Assert.AreEqual(0, CellCodec.Decode(string.Empty).Count, "the empty set is the empty string");
        Assert.ThrowsException<TetrisRuleException>(() => CellCodec.Decode("5"));
    }
}
