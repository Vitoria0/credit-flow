using Credit.Application.Interfaces;

namespace Credit.Application.Services;

public class RandomScoreGenerator : IScoreGenerator
{
    public int Generate() => Random.Shared.Next(0, 1001);
}
