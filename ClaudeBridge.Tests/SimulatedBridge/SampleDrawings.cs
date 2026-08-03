using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.SimulatedBridge;
using ClaudeBridge.Tests.GeometryEngine;

namespace ClaudeBridge.Tests.SimulatedBridgeTests;

/// <summary>
/// Disegno di terzi con blocchi nominati casualmente e telecamere disegnate come semplice
/// geometria (non blocchi) — lo scenario di riferimento del documento "Esempio end-to-end
/// tracciato" e dello Scenario H del Piano di Test.
/// </summary>
internal static class SampleDrawings
{
    public static SimulatedDrawing BuildHeterogeneousDrawing()
    {
        var drawing = new SimulatedDrawing();

        drawing.Layers.Add(new LayerInfo("0", false, false, "white"));
        drawing.Layers.Add(new LayerInfo("IMPIANTI", false, false, "red"));

        for (var i = 0; i < 12; i++)
        {
            drawing.Blocks.Add(new SimulatedBlockInstance(
                $"mnc-{i}", "mncfasoijds", "0", new Point3D(i * 5, 0, 0), "Model", []));
        }

        for (var i = 0; i < 3; i++)
        {
            drawing.Blocks.Add(new SimulatedBlockInstance(
                $"acx-{i}", "A$C4E2F1B0", "IMPIANTI", new Point3D(i * 5, 20, 0), "Model", []));
        }

        var cameraOrigins = new[]
        {
            new Point2D(0, 0),
            new Point2D(50, 0),
            new Point2D(100, 0),
            new Point2D(150, 0),
            new Point2D(500, 500), // la quinta, isolata, disegnata leggermente più grande
        };

        for (var i = 0; i < cameraOrigins.Length; i++)
        {
            var scale = i == cameraOrigins.Length - 1 ? 1.3 : 1.0;
            drawing.LooseGeometry.AddRange(SyntheticSymbols.CameraLike($"cam{i}", cameraOrigins[i], rotation: 0, scale: scale));
        }

        // Un elemento chiaramente diverso, non una telecamera (per verificare i falsi positivi).
        drawing.LooseGeometry.AddRange(SyntheticSymbols.SquareLike("noise", new Point2D(900, 900)));

        drawing.TitleblockHandle = "titleblock-1";
        drawing.Blocks.Add(new SimulatedBlockInstance(
            "titleblock-1",
            "CARTIGLIO",
            "0",
            new Point3D(0, -50, 0),
            "Layout1",
            [new SimulatedAttribute("PROGETTO", "Impianto videosorveglianza"), new SimulatedAttribute("DATA", "")]));
        drawing.TitleblockRequiredFields.Add(new TitleblockFieldDefinition("PROGETTO", true));
        drawing.TitleblockRequiredFields.Add(new TitleblockFieldDefinition("DATA", true));

        return drawing;
    }
}
