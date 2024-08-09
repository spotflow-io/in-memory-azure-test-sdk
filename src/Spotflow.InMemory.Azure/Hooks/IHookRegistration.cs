namespace Spotflow.InMemory.Azure.Hooks;

public interface IHookRegistration
{
    void Disable();
    void DisableAfter(int invocationCount);
    void Enable();
}
