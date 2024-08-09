namespace Spotflow.InMemory.Azure.Hooks;

public class ConstantDelayGenerator : IDelayGenerator
{
    private readonly TimeSpan _delay;

    public ConstantDelayGenerator(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay must be greater than or equal to zero.");
        }
        _delay = delay;
    }

    public TimeSpan Next() => _delay;
}
