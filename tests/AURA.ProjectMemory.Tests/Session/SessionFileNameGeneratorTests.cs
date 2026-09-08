using AURA.ProjectMemory.Session;

namespace AURA.ProjectMemory.Tests.Session;

public class SessionFileNameGeneratorTests
{
    private static readonly DateTimeOffset FixedInstant =
        new(2026, 9, 4, 18, 52, 14, TimeSpan.FromHours(-3));

    [Fact]
    public void Generate_WithSlug_MatchesNormativeConvention()
    {
        var name = SessionFileNameGenerator.Generate(
            FixedInstant, "550e8400-e29b-41d4-a716-446655440000", "Implement Validator!");

        Assert.Equal(
            "2026-09-04T21-52-14Z__550e8400-e29b-41d4-a716-446655440000__implement-validator.md",
            name);
    }

    [Fact]
    public void Generate_WithoutSlug_OmitsSlugSegment()
    {
        var name = SessionFileNameGenerator.Generate(
            FixedInstant, "550e8400-e29b-41d4-a716-446655440000", null);

        Assert.Equal("2026-09-04T21-52-14Z__550e8400-e29b-41d4-a716-446655440000.md", name);
    }

    [Fact]
    public void Generate_UsesUtcInFilenameRegardlessOfLocalOffset()
    {
        var name = SessionFileNameGenerator.Generate(FixedInstant, "id", null);

        Assert.StartsWith("2026-09-04T21-52-14Z__", name);
    }

    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("../../etc/passwd", "etc-passwd")]
    [InlineData("Ñoño Test", "o-o-test")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    public void Sanitize_ProducesSafeLowercaseSlug(string input, string? expected)
    {
        Assert.Equal(expected, SessionFileNameGenerator.Sanitize(input));
    }

    [Fact]
    public void Sanitize_TruncatesToMaximumLength()
    {
        var longSlug = new string('a', 200);

        var sanitized = SessionFileNameGenerator.Sanitize(longSlug);

        Assert.NotNull(sanitized);
        Assert.True(sanitized!.Length <= 60);
    }

    [Fact]
    public void Generate_TwoDifferentSessionIds_ProduceDifferentFileNames()
    {
        var first = SessionFileNameGenerator.Generate(FixedInstant, Guid.NewGuid().ToString(), "same-slug");
        var second = SessionFileNameGenerator.Generate(FixedInstant, Guid.NewGuid().ToString(), "same-slug");

        Assert.NotEqual(first, second);
    }
}
