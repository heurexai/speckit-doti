namespace HxScaffoldSample;

/// <summary>Default greeting service.</summary>
public sealed class GreetingService : IGreetingService
{
    public string Greet(GreetingRequest request) =>
        string.IsNullOrWhiteSpace(request.Name) ? "Hello, world!" : $"Hello, {request.Name}!";
}
