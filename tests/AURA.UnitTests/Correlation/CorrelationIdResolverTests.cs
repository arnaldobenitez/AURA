using AURA.Api.Correlation;

namespace AURA.UnitTests.Correlation;

public class CorrelationIdResolverTests
{
    [Fact]
    public void Resolve_WithValidGuidHeader_ReturnsTheSameValue()
    {
        var incoming = Guid.NewGuid().ToString();

        var resolved = CorrelationIdResolver.Resolve(incoming);

        Assert.Equal(incoming, resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    public void Resolve_WithMissingOrInvalidHeader_GeneratesNewGuid(string? incoming)
    {
        var resolved = CorrelationIdResolver.Resolve(incoming);

        Assert.True(Guid.TryParse(resolved, out var parsed));
        Assert.NotEqual(Guid.Empty, parsed);
    }

    [Fact]
    public void Resolve_WithEmptyGuidHeader_GeneratesNewGuidInsteadOfEchoingEmpty()
    {
        var resolved = CorrelationIdResolver.Resolve(Guid.Empty.ToString());

        Assert.NotEqual(Guid.Empty.ToString(), resolved);
        Assert.True(Guid.TryParse(resolved, out var parsed));
        Assert.NotEqual(Guid.Empty, parsed);
    }
}
