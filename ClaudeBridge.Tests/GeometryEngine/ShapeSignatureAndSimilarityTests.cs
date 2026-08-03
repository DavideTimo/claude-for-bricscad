using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.GeometryEngine;

namespace ClaudeBridge.Tests.GeometryEngine;

public class ShapeSignatureAndSimilarityTests
{
    // Piano di Test §1: "la stessa forma, traslata/ruotata/scalata, produce una firma con
    // similarità alta rispetto all'originale (invarianza)."
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(50, -30, 0, 1)]
    [InlineData(0, 0, Math.PI / 3, 1)]
    [InlineData(0, 0, 0, 2.5)]
    [InlineData(120, 75, 2.1, 0.4)]
    [InlineData(0, 0, Math.PI, 1)]
    [InlineData(-10, -10, -1.5, 1)]
    [InlineData(0, 0, 0, 0.1)]
    [InlineData(-200, 300, 5.7, 3.0)]
    public void Compute_TranslatedRotatedScaledCopy_HasHighSimilarityToOriginal(
        double tx, double ty, double rotation, double scale)
    {
        var original = SyntheticSymbols.CameraLike("orig", new Point2D(0, 0));
        var transformed = SyntheticSymbols.CameraLike("t", new Point2D(tx, ty), rotation, scale);

        var signatureA = ShapeSignature.Compute(original);
        var signatureB = ShapeSignature.Compute(transformed);
        var score = SimilarityMatcher.Compare(signatureA, signatureB);

        Assert.True(score >= 0.9, $"Expected high similarity for a transformed copy, got {score}.");
    }

    // Piano di Test §1: "due forme chiaramente diverse (es. un cerchio vs un rettangolo)
    // producono una similarità bassa."
    [Fact]
    public void Compute_ClearlyDifferentShapes_HasLowSimilarity()
    {
        var camera = SyntheticSymbols.CameraLike("cam", new Point2D(0, 0));
        var square = SyntheticSymbols.SquareLike("sq", new Point2D(100, 100));

        var signatureA = ShapeSignature.Compute(camera);
        var signatureB = ShapeSignature.Compute(square);
        var score = SimilarityMatcher.Compare(signatureA, signatureB);

        Assert.True(score < 0.5, $"Expected low similarity for clearly different shapes, got {score}.");
    }

    [Fact]
    public void Compute_IdenticalShape_HasPerfectSimilarity()
    {
        var shape = SyntheticSymbols.CameraLike("s", new Point2D(0, 0));

        var signature = ShapeSignature.Compute(shape);
        var score = SimilarityMatcher.Compare(signature, signature);

        Assert.Equal(1.0, score, precision: 6);
    }

    // Piano di Test §1: "find_similar_geometry: soglie di tolleranza applicate correttamente
    // (match sopra soglia inclusi, sotto soglia esclusi)."
    [Fact]
    public void FindSimilar_AppliesToleranceThreshold()
    {
        var target = ShapeSignature.Compute(SyntheticSymbols.CameraLike("target", new Point2D(0, 0)));

        var closeMatch = ShapeSignature.Compute(SyntheticSymbols.CameraLike("close", new Point2D(10, 5), scale: 1.1));
        var differentShape = ShapeSignature.Compute(SyntheticSymbols.SquareLike("far", new Point2D(200, 200)));

        var candidates = new (IReadOnlyList<string> Handles, BoundingBox BoundingBox, GeometrySignature Signature)[]
        {
            (["close"], new BoundingBox(new Point2D(8, 3), new Point2D(12, 7)), closeMatch),
            (["far"], new BoundingBox(new Point2D(199, 199), new Point2D(201, 201)), differentShape),
        };

        var matches = SimilarityMatcher.FindSimilar(target, candidates, tolerance: 0.85);

        var handles = Assert.Single(matches).Handles;
        Assert.Equal(["close"], handles);
    }

    [Fact]
    public void FindSimilar_OrdersMatchesBySimilarityDescending()
    {
        var target = ShapeSignature.Compute(SyntheticSymbols.CameraLike("target", new Point2D(0, 0)));
        var veryClose = ShapeSignature.Compute(SyntheticSymbols.CameraLike("very-close", new Point2D(1, 1)));
        var lessClose = ShapeSignature.Compute(SyntheticSymbols.CameraLike("less-close", new Point2D(30, 20), rotation: 0.4, scale: 1.3));

        var candidates = new (IReadOnlyList<string> Handles, BoundingBox BoundingBox, GeometrySignature Signature)[]
        {
            (["less-close"], new BoundingBox(new Point2D(0, 0), new Point2D(1, 1)), lessClose),
            (["very-close"], new BoundingBox(new Point2D(0, 0), new Point2D(1, 1)), veryClose),
        };

        var matches = SimilarityMatcher.FindSimilar(target, candidates, tolerance: 0.0);

        Assert.Equal(2, matches.Count);
        Assert.Equal("very-close", matches[0].Handles[0]);
        Assert.True(matches[0].SimilarityScore >= matches[1].SimilarityScore);
    }
}
