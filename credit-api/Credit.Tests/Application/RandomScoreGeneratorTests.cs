using Credit.Application.Services;

namespace Credit.Tests.Application;

public class RandomScoreGeneratorTests
{
    [Fact]
    public void Generate_ReturnsScoreWithinAllowedRange()
    {
        var score = new RandomScoreGenerator().Generate();

        Assert.InRange(score, 0, 1000);
    }
}
