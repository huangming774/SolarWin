using System.Text.Json;
using SolarWin.Helpers;

namespace SolarWin.Tests;

public class JsonListParserTests
{
    private sealed class Item
    {
        public string? Name { get; set; }
    }

    private sealed class BoolHost
    {
        public bool Flag { get; set; }
    }

    [Fact]
    public void ParseList_BareArray()
    {
        var list = JsonListParser.ParseList<Item>("""[{"name":"a"},{"name":"b"}]""");
        Assert.Equal(2, list.Count);
        Assert.Equal("a", list[0].Name);
    }

    [Fact]
    public void ParseList_WrappedData()
    {
        var list = JsonListParser.ParseList<Item>("""{"data":[{"name":"x"}]}""");
        Assert.Single(list);
        Assert.Equal("x", list[0].Name);
    }

    /// <summary>
    /// Solian posts embed publishers with null bools (gatekept_follows etc.).
    /// Without FlexibleBoolConverter, the whole post list deserializes to empty.
    /// </summary>
    [Fact]
    public void FlexibleBool_NullMapsToFalse()
    {
        var obj = JsonSerializer.Deserialize<BoolHost>("""{"flag":null}""", JsonDefaults.Options);
        Assert.NotNull(obj);
        Assert.False(obj!.Flag);
    }
}
