namespace ClaudeBridge.Core.Bridge.Models;

public sealed record GetTextInRegionInput(BoundingBox BoundingBox, string? Layout = null);

public sealed record TextEntity(string Handle, string Content, Point2D Position);

public sealed record GetTextInRegionOutput(IReadOnlyList<TextEntity> TextEntities);

public sealed record GetRegionScreenshotInput(
    BoundingBox BoundingBox,
    string? Layout = null,
    double ZoomMarginFactor = 1.3,
    int MaxResolution = 1568);

public sealed record GetRegionScreenshotOutput(
    string ImageBase64,
    string MediaType,
    BoundingBox ActualBoundingBox);

public sealed record ExportCurrentViewImageInput(string? Layout = null, int MaxResolution = 1568);

public sealed record ExportCurrentViewImageOutput(string ImageBase64, string MediaType);
