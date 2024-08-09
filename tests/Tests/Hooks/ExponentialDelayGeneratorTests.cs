using Spotflow.InMemory.Azure.Hooks;

namespace Tests.Hooks;

[TestClass]
public class ExponentialDelayGeneratorTests
{
    [TestMethod]
    [DataRow(42)]
    [DataRow(43)]
    [DataRow(44)]
    [DataRow(45)]
    [DataRow(46)]
    [DataRow(47)]
    public void Generated_Delays_For_Default_Setting_Should_Be_In_Range(int seed)
    {
        var generator = new ExponentialDelayGenerator(seed: seed);

        const int samplesCount = 1000;

        var samples = new double[samplesCount];

        for (var i = 0; i < samplesCount; i++)
        {
            samples[i] = generator.Next().TotalSeconds;
        }

        samples.Should().AllSatisfy(s => s.Should().BeGreaterThan(0.01));
        samples.Order().Take((int) (0.95 * 1000)).Should().AllSatisfy(s => s.Should().BeLessThan(4.01));
    }
}
