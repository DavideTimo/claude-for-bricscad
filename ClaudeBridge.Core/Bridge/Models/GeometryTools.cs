namespace ClaudeBridge.Core.Bridge.Models;

public sealed record ListEntityClustersInput(
    BoundingBox BoundingBox,
    string? Layout = null,
    double? MaxClusterExtent = null);

public sealed record EntityCluster(
    string ClusterId,
    IReadOnlyList<string> Handles,
    BoundingBox BoundingBox,
    int EntityCount,
    bool IsBlockInstance);

public sealed record ListEntityClustersOutput(IReadOnlyList<EntityCluster> Clusters);

public sealed record PrimitiveCounts(int Line, int Arc, int Circle, int Polyline);

public sealed record GeometrySignature(
    PrimitiveCounts PrimitiveCounts,
    string RelativeGeometry,
    double BoundingBoxAspectRatio);

public sealed record GetGeometrySignatureInput(IReadOnlyList<string> Handles);

public sealed record GetGeometrySignatureOutput(
    GeometrySignature Signature,
    IReadOnlyList<string> SourceHandles);

public sealed record FindSimilarGeometryInput(
    GeometrySignature Signature,
    double Tolerance = 0.85,
    string Space = "any",
    BoundingBox? BoundingBox = null);

public sealed record GeometryMatch(
    IReadOnlyList<string> Handles,
    BoundingBox BoundingBox,
    double SimilarityScore);

public sealed record FindSimilarGeometryOutput(IReadOnlyList<GeometryMatch> Matches);

public sealed record SaveSignatureAsCategoryInput(
    GeometrySignature Signature,
    string Label,
    string Scope);

public sealed record SaveSignatureAsCategoryOutput(bool Saved, string Label, string Scope);

public sealed record ListLearnedCategoriesInput;

public sealed record LearnedCategory(string Label, string Scope, string CreatedAt);

public sealed record ListLearnedCategoriesOutput(IReadOnlyList<LearnedCategory> Categories);
