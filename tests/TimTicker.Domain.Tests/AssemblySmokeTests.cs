namespace TimTicker.Domain.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void DomainAssemblyCanBeLoaded()
    {
        Assert.Equal("TimTicker.Domain", typeof(AssemblyMarker).Assembly.GetName().Name);
    }
}
