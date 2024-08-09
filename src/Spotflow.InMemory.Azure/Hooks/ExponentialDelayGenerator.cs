namespace Spotflow.InMemory.Azure.Hooks;

public class ExponentialDelayGenerator : IDelayGenerator
{
    private readonly object? _syncObj;
    private readonly Random _random;
    private readonly double _offset;
    private readonly double _inversedNegativeScaledRate;

    /// <summary>
    /// Delays generated from an exponential distribution.
    /// 
    /// With default settings, minimal delay is 10ms and the <b>98.75%</b> of delays will be less than <b>4.01</b> seconds.
    /// </summary>
    /// <param name="rate">Sets the rate (lambda parameter) of the distribution. Must be > 0.</param>
    /// <param name="scale">Multiples the sample drawn from the distribution. Must be > 0.</param>
    /// <param name="offsetSeconds">Value that is added to the sample drawn from the distribution, after it is scaled with <paramref name="scale"/> parameter. Must be >= 0.</param>
    /// <param name="seed">Can be used to generate sample in a deterministic way.</param>
    public ExponentialDelayGenerator(double rate = 1.5, double scale = 1, double offsetSeconds = 0.01, int? seed = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        ArgumentOutOfRangeException.ThrowIfNegative(offsetSeconds);

        if (seed is null)
        {
            _random = Random.Shared;
        }
        else
        {
            _random = new Random(seed.Value);
            _syncObj = new();
        }

        _offset = offsetSeconds;
        _inversedNegativeScaledRate = -scale / rate;
    }

    public TimeSpan Next()
    {
        // offset + scale * (-1 / lambda) * log(1 - U)

        var uniformSample = SampleFromUniform();
        var totalSeconds = _offset + (_inversedNegativeScaledRate * Math.Log(1 - uniformSample));
        return TimeSpan.FromSeconds(totalSeconds);
    }

    private double SampleFromUniform()
    {
        if (_syncObj is null)
        {
            return _random.NextDouble();
        }

        lock (_syncObj)
        {
            return _random.NextDouble();
        }
    }
}
