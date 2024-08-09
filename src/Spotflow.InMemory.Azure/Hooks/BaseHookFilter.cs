namespace Spotflow.InMemory.Azure.Hooks;

public abstract record BaseHookFilter<TContext>
{
    public abstract bool Covers(TContext context);
}
