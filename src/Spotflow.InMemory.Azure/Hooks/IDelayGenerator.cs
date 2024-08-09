namespace Spotflow.InMemory.Azure.Hooks;

public interface IDelayGenerator
{
    TimeSpan Next();
}
