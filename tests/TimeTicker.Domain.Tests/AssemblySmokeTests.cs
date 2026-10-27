namespace TimeTicker.Domain.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void DomainAssemblyCanBeLoaded()
    {
        Assert.Equal("TimeTicker.Domain", typeof(AssemblyMarker).Assembly.GetName().Name);
    }
}
