namespace TimTicker.Infrastructure.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void InfrastructureAssemblyCanBeLoaded()
    {
        Assert.Equal("TimTicker.Infrastructure", typeof(AssemblyMarker).Assembly.GetName().Name);
    }
}
