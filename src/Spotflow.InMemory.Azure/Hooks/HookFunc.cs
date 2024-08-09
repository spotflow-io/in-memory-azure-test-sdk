namespace Spotflow.InMemory.Azure.Hooks;

public delegate Task HookFunc<in TContext>(TContext context);
