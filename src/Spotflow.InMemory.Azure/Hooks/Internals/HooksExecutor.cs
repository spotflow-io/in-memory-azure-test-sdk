using System.Collections.Concurrent;

namespace Spotflow.InMemory.Azure.Hooks.Internals;

internal class HooksExecutor<TFilter, TBaseContext>
    where TFilter : BaseHookFilter<TBaseContext>
    where TBaseContext : class
{
    private readonly ConcurrentDictionary<Type, IReadOnlyList<Type>> _typeChainCache = [];
    private readonly ConcurrentDictionary<Type, HookCollection> _hooks = [];

    public IHookRegistration AddHook<TContext>(HookFunc<TContext> hook, TFilter filter) where TContext : TBaseContext
    {
        var hooks = _hooks.GetOrAdd(typeof(TContext), _ => new());

        var reg = HookRegistration.Create(hook, filter);

        hooks.Add(reg);

        return reg;
    }

    public async Task ExecuteHooksAsync<TContext>(TContext context) where TContext : TBaseContext
    {
        var typeChain = _typeChainCache.GetOrAdd(typeof(TContext), GetTypeChain);

        for (var i = typeChain.Count - 1; i >= 0; i--)
        {
            if (!_hooks.TryGetValue(typeChain[i], out var scopedHooks))
            {
                continue;
            }

            await scopedHooks.InvokeAsync(context).ConfigureAwait(ConfigureAwaitOptions.None);
        }
    }

    private static List<Type> GetTypeChain(Type contextType)
    {
        var typeChain = new List<Type>();
        var currentContextType = contextType;

        while (true)
        {
            typeChain.Add(currentContextType);

            if (currentContextType == typeof(TBaseContext))
            {
                break;
            }

            currentContextType = currentContextType.BaseType ?? throw new InvalidOperationException($"Unexpected end of type chain on type '{currentContextType.Name}'.");
        }

        return typeChain;
    }

    private class HookCollection
    {
        private readonly object _syncObj = new();

        private HookRegistration[] _hooks = [];

        public async Task InvokeAsync<TContext>(TContext context) where TContext : TBaseContext
        {
            HookRegistration[] hooksLocal;
            lock (_syncObj)
            {
                hooksLocal = _hooks;
            }

            foreach (var hook in hooksLocal)
            {
                await hook.InvokeAsync(context).ConfigureAwait(ConfigureAwaitOptions.None);
            }

        }

        public void Add(HooksExecutor<TFilter, TBaseContext>.HookRegistration reg)
        {
            lock (_syncObj)
            {
                var old = _hooks;
                _hooks = new HookRegistration[_hooks.Length + 1];
                old.CopyTo(_hooks, 0);
                _hooks[^1] = reg;
            }
        }
    }


    private class HookRegistration(HookFunc<TBaseContext> hook, BaseHookFilter<TBaseContext> filter) : IHookRegistration
    {
        private readonly object _syncObj = new();

        private bool _isDisabled = false;
        private int? _disableAfter = null;
        private long _invocationCount = 0;

        public void Disable()
        {
            lock (_syncObj)
            {
                _isDisabled = true;
                _disableAfter = null;
                _invocationCount = 0;
            }
        }

        public void DisableAfter(int invocationCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(invocationCount);

            lock (_syncObj)
            {
                if (_isDisabled)
                {
                    throw new InvalidOperationException("Hook is already disabled.");
                }

                _isDisabled = false;
                _disableAfter = invocationCount;
                _invocationCount = 0;
            }
        }

        public void Enable()
        {
            lock (_syncObj)
            {
                _isDisabled = false;
                _disableAfter = null;
                _invocationCount = 0;
            }
        }

        public async Task InvokeAsync<TContext>(TContext context) where TContext : TBaseContext
        {
            if (!filter.Covers(context))
            {
                return;
            }

            lock (_syncObj)
            {
                _invocationCount++;

                if (_invocationCount > _disableAfter)
                {
                    _isDisabled = true;
                }

                if (_isDisabled)
                {
                    return;
                }

            }

            await hook(context).ConfigureAwait(ConfigureAwaitOptions.None);

        }

        public static HookRegistration Create<TContext>(HookFunc<TContext> hook, BaseHookFilter<TBaseContext> filter) where TContext : TBaseContext
        {
            Task hookCasted(TBaseContext ctx) => hook((TContext) ctx);

            return new(hookCasted, filter);
        }

    }
}
