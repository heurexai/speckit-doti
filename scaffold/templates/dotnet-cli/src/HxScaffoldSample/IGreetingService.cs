namespace HxScaffoldSample;

/// <summary>Produces a greeting. Implementations are named <c>*Service</c> (architecture rule).</summary>
public interface IGreetingService
{
    string Greet(GreetingRequest request);
}
