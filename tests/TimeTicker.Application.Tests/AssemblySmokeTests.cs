namespace TimeTicker.Application.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void ApplicationAssemblyCanBeLoaded()
    {
        Assert.Equal("TimeTicker.Application", typeof(AssemblyMarker).Assembly.GetName().Name);
    }
}
