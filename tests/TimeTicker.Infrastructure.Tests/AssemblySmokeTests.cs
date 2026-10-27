namespace TimeTicker.Infrastructure.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void InfrastructureAssemblyCanBeLoaded()
    {
        Assert.Equal("TimeTicker.Infrastructure", typeof(AssemblyMarker).Assembly.GetName().Name);
    }
}
