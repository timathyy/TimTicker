namespace TimTicker.Application.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void ApplicationAssemblyCanBeLoaded()
    {
        Assert.Equal("TimTicker.Application", typeof(AssemblyMarker).Assembly.GetName().Name);
    }
}
