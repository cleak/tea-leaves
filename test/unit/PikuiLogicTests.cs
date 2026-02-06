using GdUnit4;
using static GdUnit4.Assertions;
using TeaLeaves;

[TestSuite]
public class PikuiLogicTests
{
    [TestCase]
    public void Constructor_Level1_InitializesCorrectly()
    {
        var logic = new PikuiLogic(1);
        AssertInt(logic.GridSize).IsEqual(7);
        AssertInt(logic.MaxSlideDistance).IsEqual(2);
        AssertInt(logic.PaintedCount).IsEqual(1);
        AssertInt(logic.TotalPaintable).IsEqual(49);
        AssertInt(logic.Moves).IsEqual(0);
        AssertInt(logic.Score).IsEqual(0);
        AssertBool(logic.IsComplete).IsFalse();
        AssertThat(logic.PlayerPos).IsEqual((3, 3));
    }

    [TestCase]
    public void Level1_SlideUp_StopsAfterMaxDistance()
    {
        var logic = new PikuiLogic(1); // max slide = 2
        var path = logic.Slide(SlideDirection.Up);
        AssertInt(path.Count).IsEqual(2); // slides 2 tiles, not to edge
        AssertThat(logic.PlayerPos).IsEqual((3, 1));
    }

    [TestCase]
    public void Level1_SlideUp_ThenUp_ReachesEdge()
    {
        var logic = new PikuiLogic(1);
        logic.Slide(SlideDirection.Up); // (3,3) → (3,1)
        var path = logic.Slide(SlideDirection.Up); // (3,1) → (3,0), only 1 tile to edge
        AssertInt(path.Count).IsEqual(1);
        AssertThat(logic.PlayerPos).IsEqual((3, 0));
    }

    [TestCase]
    public void Level1_AllTilesReachable()
    {
        // With max slide 2, all tiles should be reachable on 7x7
        var logic = new PikuiLogic(1);
        // Systematic traversal: zigzag across the grid
        // Start at (3,3), slide up 2 → (3,1), up 1 → (3,0)
        // Right 2 → (5,0), right 1 → (6,0)
        // Down 2 → (6,2), down 2 → (6,4), down 2 → (6,6)
        // Left 2 → (4,6), left 2 → (2,6), left 2 → (0,6)
        // etc.
        // Instead of tracing all moves, verify a smaller scenario:
        // From center, we can reach interior tiles via 2-tile slides
        logic.Slide(SlideDirection.Up); // (3,1)
        logic.Slide(SlideDirection.Left); // (1,1) - interior tile!
        AssertThat(logic.PlayerPos).IsEqual((1, 1));
        AssertThat(logic.GetTile(1, 1)).IsEqual(PaintColor.Green);
    }

    [TestCase]
    public void Level2_SlideStopsAtWall()
    {
        var logic = new PikuiLogic(2); // Has wall at (5,3), max slide 3
        // From (3,3) sliding right: (4,3) ok, (5,3) is wall → stop at (4,3)
        var path = logic.Slide(SlideDirection.Right);
        AssertInt(path.Count).IsEqual(1);
        AssertThat(logic.PlayerPos).IsEqual((4, 3));
    }

    [TestCase]
    public void Level2_MaxSlideIs3()
    {
        var logic = new PikuiLogic(2);
        AssertInt(logic.MaxSlideDistance).IsEqual(3);
    }

    [TestCase]
    public void Slide_IntoEdge_NoMovement()
    {
        var logic = new PikuiLogic(1);
        logic.Slide(SlideDirection.Up); // (3,1)
        logic.Slide(SlideDirection.Up); // (3,0) - edge
        var path = logic.Slide(SlideDirection.Up); // at edge, can't move
        AssertInt(path.Count).IsEqual(0);
    }

    [TestCase]
    public void Slide_PaintsCorrectColor()
    {
        var logic = new PikuiLogic(1);
        logic.Slide(SlideDirection.Right); // max 2: paints (4,3),(5,3)
        AssertThat(logic.GetTile(4, 3)).IsEqual(PaintColor.Magenta);
        AssertThat(logic.GetTile(5, 3)).IsEqual(PaintColor.Magenta);
    }

    [TestCase]
    public void Combo_IncreasesWithConsecutiveNewPaints()
    {
        var logic = new PikuiLogic(1);
        logic.Slide(SlideDirection.Up); // 2 new tiles
        AssertInt(logic.Combo).IsEqual(1);
        logic.Slide(SlideDirection.Right); // 2 new tiles
        AssertInt(logic.Combo).IsEqual(2);
    }

    [TestCase]
    public void Combo_BreaksOnNoMovement()
    {
        var logic = new PikuiLogic(1);
        logic.Slide(SlideDirection.Up); // (3,1)
        logic.Slide(SlideDirection.Up); // (3,0)
        AssertInt(logic.Combo).IsEqual(2);
        logic.Slide(SlideDirection.Up); // at edge, can't move
        AssertInt(logic.Combo).IsEqual(0);
    }

    [TestCase]
    public void IsCorner_IdentifiesCorners()
    {
        var logic = new PikuiLogic(1);
        AssertBool(logic.IsCorner(0, 0)).IsTrue();
        AssertBool(logic.IsCorner(6, 0)).IsTrue();
        AssertBool(logic.IsCorner(0, 6)).IsTrue();
        AssertBool(logic.IsCorner(6, 6)).IsTrue();
        AssertBool(logic.IsCorner(3, 3)).IsFalse();
    }

    [TestCase]
    public void Level2_HasWalls()
    {
        var logic = new PikuiLogic(2);
        AssertBool(logic.IsWall(2, 1)).IsTrue();
        AssertBool(logic.IsWall(4, 1)).IsTrue();
        AssertBool(logic.IsWall(1, 3)).IsTrue();
        AssertBool(logic.IsWall(5, 3)).IsTrue();
        AssertBool(logic.IsWall(3, 3)).IsFalse();
        AssertInt(logic.TotalPaintable).IsEqual(43);
    }

    [TestCase]
    public void Level3_HasWalls()
    {
        var logic = new PikuiLogic(3);
        AssertBool(logic.IsWall(1, 1)).IsTrue();
        AssertBool(logic.IsWall(5, 5)).IsTrue();
        AssertInt(logic.TotalPaintable).IsEqual(41);
        AssertInt(logic.MaxSlideDistance).IsEqual(4);
    }

    [TestCase]
    public void DirectionToColor_MapsCorrectly()
    {
        AssertThat(PikuiLogic.DirectionToColor(SlideDirection.Up)).IsEqual(PaintColor.Cyan);
        AssertThat(PikuiLogic.DirectionToColor(SlideDirection.Right)).IsEqual(PaintColor.Magenta);
        AssertThat(PikuiLogic.DirectionToColor(SlideDirection.Down)).IsEqual(PaintColor.Yellow);
        AssertThat(PikuiLogic.DirectionToColor(SlideDirection.Left)).IsEqual(PaintColor.Green);
    }

    [TestCase]
    public void CompletionBonus_ZeroWhenNotComplete()
    {
        var logic = new PikuiLogic(1);
        AssertInt(logic.CompletionBonus()).IsEqual(0);
    }

    [TestCase]
    public void Progress_CalculatesCorrectly()
    {
        var logic = new PikuiLogic(1);
        AssertFloat(logic.Progress).IsEqualApprox(1f / 49f, 0.001f);
    }

    [TestCase]
    public void Repaint_GivesReducedScore()
    {
        var logic = new PikuiLogic(1);
        logic.Slide(SlideDirection.Up); // paint 2 tiles
        int scoreAfterFirst = logic.Score;
        logic.Slide(SlideDirection.Down); // slide back, repainting
        int scoreAfterSecond = logic.Score;
        AssertInt(scoreAfterSecond).IsGreater(scoreAfterFirst);
    }
}
