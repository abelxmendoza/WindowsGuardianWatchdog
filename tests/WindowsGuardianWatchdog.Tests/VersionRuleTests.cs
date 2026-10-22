using WindowsGuardianWatchdog.Core.Vulnerability;
using Xunit;

namespace WindowsGuardianWatchdog.Tests;

public class VersionRuleTests
{
    private static SoftwareVersion V(string text) => SoftwareVersion.TryParse(text)!;

    [Theory]
    [InlineData("3.0.11", "3.0.11", 0)]
    [InlineData("3.0", "3.0.0.0", 0)]
    [InlineData("3.0.11", "3.0.9", 1)]
    [InlineData("10.0", "9.99", 1)]
    [InlineData("v2.1", "2.2", -1)]
    public void ComparesNumerically(string left, string right, int expected)
    {
        Assert.Equal(expected, Math.Sign(V(left).CompareTo(V(right))));
    }

    [Fact]
    public void ShortVersionCoversItsPatchReleases()
    {
        Assert.True(V("3.0").Covers(V("3.0.11")));
        Assert.True(V("2.x").Covers(V("2.7.1")));
        Assert.False(V("3.0").Covers(V("3.1")));
    }

    [Fact]
    public void ReadsProductAndExactVersion()
    {
        var claim = Assert.Single(ExploitTitleParser.Parse("VLC media player 2.2.8 - Arbitrary Code Execution (PoC)"));
        Assert.Equal("VLC media player", claim.Product);
        Assert.Equal(VersionRuleKind.Exact, claim.Rule.Kind);
        Assert.Equal(VersionVerdict.Affected, claim.Rule.Evaluate(V("2.2.8")));
        Assert.Equal(VersionVerdict.NotAffected, claim.Rule.Evaluate(V("3.0.21")));
        Assert.Equal(VersionVerdict.OlderThanTested, claim.Rule.Evaluate(V("2.1.0")));
    }

    [Fact]
    public void ReadsUpperBound()
    {
        var claims = ExploitTitleParser.Parse("7-Zip < 18.00 / p7zip < 16.02 - Integer Underflow");
        Assert.Equal(2, claims.Count);
        Assert.Equal(new[] { "7-zip" }, claims[0].Tokens);
        Assert.Equal(VersionRuleKind.Below, claims[0].Rule.Kind);
        Assert.Equal(VersionVerdict.Affected, claims[0].Rule.Evaluate(V("9.20")));
        Assert.Equal(VersionVerdict.NotAffected, claims[0].Rule.Evaluate(V("18.00")));
    }

    [Fact]
    public void ReadsRange()
    {
        var rule = Assert.Single(ExploitTitleParser.Parse("Foo Server 1.2 < 1.4.3 - Overflow")).Rule;
        Assert.Equal(VersionRuleKind.Range, rule.Kind);
        Assert.Equal(VersionVerdict.Affected, rule.Evaluate(V("1.3.9")));
        Assert.Equal(VersionVerdict.NotAffected, rule.Evaluate(V("1.1")));
        Assert.Equal(VersionVerdict.NotAffected, rule.Evaluate(V("1.4.3")));
    }

    [Fact]
    public void ReadsSeveralVersionsAndProducts()
    {
        var claims = ExploitTitleParser.Parse("Mozilla Firefox 3.0.11 and Thunderbird 2.0.9 - RDF File Handling Remote Memory Corruption");
        Assert.Equal(new[] { "Mozilla Firefox", "Thunderbird" }, claims.Select(claim => claim.Product));

        var multi = Assert.Single(ExploitTitleParser.Parse("Mozilla Firefox 1.0.x/1.5 - HTML Parsing Denial of Service"));
        Assert.Equal(VersionVerdict.Affected, multi.Rule.Evaluate(V("1.0.7")));
        Assert.Equal(VersionVerdict.Affected, multi.Rule.Evaluate(V("1.5")));
        Assert.Equal(VersionVerdict.NotAffected, multi.Rule.Evaluate(V("131.0")));
    }

    [Fact]
    public void DropsVersionLabelFromProduct()
    {
        var claim = Assert.Single(ExploitTitleParser.Parse("WinRAR version 6.22 - Remote Code Execution via ZIP archive"));
        Assert.Equal("WinRAR", claim.Product);
    }

    [Fact]
    public void UnknownVersionsAreOnlyPossible()
    {
        var claim = Assert.Single(ExploitTitleParser.Parse("VideoLAN VLC Client (Windows x86) - 'smb://' URI Buffer Overflow"));
        Assert.Equal(VersionVerdict.Possible, claim.Rule.Evaluate(V("3.0.11")));
    }

    [Fact]
    public void YearVersionsAreNotComparedWithBuildNumbers()
    {
        var claim = Assert.Single(ExploitTitleParser.Parse("Contoso Office 2016 - Macro Execution"));
        Assert.Equal(VersionVerdict.Possible, claim.Rule.Evaluate(V("16.0.4266.1001")));
    }

    [Fact]
    public void MatcherNeedsEveryListedWordAndOneDistinctiveWord()
    {
        var installed = new HashSet<string>(ProductMatcher.Tokenize("Mozilla Firefox (x64 en-US)"));
        Assert.True(ProductMatcher.Matches(ProductMatcher.Tokenize("Mozilla Firefox"), installed));
        Assert.False(ProductMatcher.Matches(ProductMatcher.Tokenize("Mozilla Thunderbird"), installed));

        var runtime = new HashSet<string>(ProductMatcher.Tokenize("Microsoft Windows Desktop Runtime - 8.0.10 (x64)"));
        Assert.False(ProductMatcher.Matches(ProductMatcher.Tokenize("Microsoft Windows"), runtime));
    }
}
