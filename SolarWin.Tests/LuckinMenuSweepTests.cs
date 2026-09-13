using SolarWin.Services;

namespace SolarWin.Tests;

public sealed class LuckinMenuSweepTests
{
    private static HashSet<string> Issued(params string[] queries)
        => new(queries, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void SeedQueries_HaveNoDuplicates_IgnoringCase()
    {
        var distinct = new HashSet<string>(LuckinMenuSweep.SeedQueries, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(distinct.Count, LuckinMenuSweep.SeedQueries.Length);
    }

    [Fact]
    public void BuildWave_Round1_ReturnsSeedsNotYetIssued()
    {
        var issued = Issued(LuckinMenuSweep.SeedQueries[0]);

        var wave = LuckinMenuSweep.BuildWave(1, [], issued);

        Assert.Equal(LuckinMenuSweep.SeedQueries.Length - 1, wave.Count);
        Assert.Equal(LuckinMenuSweep.SeedQueries[1], wave[0]);
        Assert.Equal(LuckinMenuSweep.SeedQueries[^1], wave[^1]);
        Assert.Equal(LuckinMenuSweep.SeedQueries.Length, issued.Count);
    }

    [Fact]
    public void BuildWave_Round1_SkipsAlreadyIssuedSeeds()
    {
        var issued = Issued(LuckinMenuSweep.SeedQueries[..3]);

        var wave = LuckinMenuSweep.BuildWave(1, [], issued);

        Assert.DoesNotContain(LuckinMenuSweep.SeedQueries[1], wave);
        Assert.DoesNotContain(LuckinMenuSweep.SeedQueries[2], wave);
        Assert.Equal(LuckinMenuSweep.SeedQueries.Length - 3, wave.Count);
    }

    [Fact]
    public void BuildWave_Round2_DerivesTwoCharWindowsFromNames_SkippingIssuedAndParentheticals()
    {
        var issued = Issued(LuckinMenuSweep.SeedQueries);

        var wave = LuckinMenuSweep.BuildWave(2, ["冰吸生椰拿铁（首创）"], issued);

        // 去掉（首创）后按 2 字滑窗：冰吸/吸生/生椰/椰拿/拿铁；其中“冰吸”“生椰”“拿铁”是种子词（已发）被排除
        Assert.Equal(["吸生", "椰拿"], wave);
    }

    [Fact]
    public void BuildWave_Round2_SkipsMixedScriptAndNonLetterWindows()
    {
        var issued = Issued("占位");

        var wave = LuckinMenuSweep.BuildWave(2, ["橙C美式 Dirty"], issued);

        // “橙C”“C美”混排、“式 ”带空格的窗口无匹配价值；“美式”与纯 ASCII 字母窗口保留
        Assert.Equal(["美式", "Di", "ir", "rt", "ty"], wave);
    }

    [Fact]
    public void BuildWave_Round2_DedupesWindowsAcrossNames()
    {
        var issued = Issued(LuckinMenuSweep.SeedQueries);

        var wave = LuckinMenuSweep.BuildWave(2, ["生椰拿铁", "厚乳拿铁"], issued);

        // “拿铁”窗口在两个名字里都出现且是种子词；“厚乳”“生椰”同理；只有新词根入选
        Assert.Equal(["椰拿", "乳拿"], wave);
    }

    [Fact]
    public void BuildWave_Round2_IgnoresCaseForIssuedQueries()
    {
        var issued = Issued("DI", "拿铁");

        var wave = LuckinMenuSweep.BuildWave(2, ["Dirty 拿铁"], issued);

        // “Di”与已发的“DI”大小写不敏感等价，不再重复查询
        Assert.Equal(["ir", "rt", "ty"], wave);
    }

    [Fact]
    public void BuildWave_ReturnsEmptyBeyondMaxRounds()
    {
        var issued = Issued("x");

        Assert.NotEmpty(LuckinMenuSweep.BuildWave(LuckinMenuSweep.MaxRounds, ["生椰拿铁"], issued));
        Assert.Empty(LuckinMenuSweep.BuildWave(LuckinMenuSweep.MaxRounds + 1, ["生椰拿铁"], issued));
    }

    [Fact]
    public void BuildWave_ReturnsEmptyForInvalidRound()
    {
        var issued = Issued("x");

        Assert.Empty(LuckinMenuSweep.BuildWave(0, ["生椰拿铁"], issued));
    }

    [Fact]
    public void BuildWave_StopsAtQueryBudget()
    {
        var issued = Issued(LuckinMenuSweep.SeedQueries[0]);
        // 占满预算只留 1 个空位
        for (var i = 0; i < LuckinMenuSweep.MaxQueries - 2; i++) issued.Add($"已发{i}");

        var wave = LuckinMenuSweep.BuildWave(1, [], issued);
        Assert.Single(wave);
        Assert.Equal(LuckinMenuSweep.MaxQueries, issued.Count);

        // 预算耗尽后即使还有候选也返回空
        var next = LuckinMenuSweep.BuildWave(2, ["生椰拿铁"], issued);
        Assert.Empty(next);
    }
}
