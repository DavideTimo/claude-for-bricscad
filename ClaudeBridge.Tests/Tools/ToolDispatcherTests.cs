using System.Text.Json;
using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.Core.Tools;
using ClaudeBridge.Tests.Fakes;

namespace ClaudeBridge.Tests.Tools;

public class ToolDispatcherTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task DispatchAsync_FindBlocks_DeserializesInputAndReturnsBridgeOutput()
    {
        FindBlocksInput? received = null;
        var bridge = new FakeBricscadBridge
        {
            OnFindBlocks = input =>
            {
                received = input;
                return Task.FromResult(new FindBlocksOutput(
                    [new BlockMatch("2A4F", "TELECAMERA_ESTERNA", "IMPIANTI", new Point3D(1, 2, 0), "Model", true)],
                    1));
            },
        };

        var result = await ToolDispatcher.DispatchAsync(
            bridge, "find_blocks", Parse("""{ "name_pattern": "TELECAMERA*", "layer": "IMPIANTI" }"""));

        Assert.NotNull(received);
        Assert.Equal("TELECAMERA*", received!.NamePattern);
        Assert.Equal("IMPIANTI", received.Layer);
        Assert.Equal("any", received.Space); // valore di default del parametro, assente nel JSON
        var output = Assert.IsType<FindBlocksOutput>(result);
        Assert.Equal(1, output.Count);
        Assert.Equal("2A4F", output.Matches[0].Handle);
    }

    [Fact]
    public async Task DispatchAsync_UnknownTool_ThrowsToolNotFound()
    {
        var bridge = new FakeBricscadBridge();

        await Assert.ThrowsAsync<ToolNotFoundException>(() =>
            ToolDispatcher.DispatchAsync(bridge, "does_not_exist", Parse("{}")));
    }

    [Fact]
    public async Task DispatchAsync_ListEntityClusters_DeserializesNestedBoundingBox()
    {
        var bridge = new FakeBricscadBridge
        {
            OnListEntityClusters = _ => Task.FromResult(new ListEntityClustersOutput([])),
        };

        var input = Parse("""
            {
              "bounding_box": { "min": { "x": 0.0, "y": 0.0 }, "max": { "x": 10.0, "y": 10.0 } },
              "max_cluster_extent": 2.5
            }
            """);

        var result = await ToolDispatcher.DispatchAsync(bridge, "list_entity_clusters", input);

        Assert.IsType<ListEntityClustersOutput>(result);
    }
}
