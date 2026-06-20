using HxScaffoldSample;
using Xunit;

namespace HxScaffoldSample.Tests;

public sealed class GreetingServiceTests
{
    [Fact]
    public void GreetsByName()
    {
        IGreetingService service = new GreetingService();
        Assert.Equal("Hello, Ada!", service.Greet(new GreetingRequest("Ada")));
    }

    [Fact]
    public void GreetsTheWorldWhenNameIsBlank()
    {
        IGreetingService service = new GreetingService();
        Assert.Equal("Hello, world!", service.Greet(new GreetingRequest("  ")));
    }
}
