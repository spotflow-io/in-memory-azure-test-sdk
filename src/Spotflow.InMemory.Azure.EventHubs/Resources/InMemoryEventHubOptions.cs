namespace Spotflow.InMemory.Azure.EventHubs.Resources;

public class InMemoryEventHubOptions
{
    /// <summary>
    /// <para>
    /// If true, the initial sequence numbers of each partition are selected at random.
    /// Otherwise, all initial seqence numbers are set to 0.
    /// </para>
    /// <para>The randomization can be further controlled by <see cref="RandomizationSeed"/> and <see cref="MaxRandomInitialSequenceNumber"/> properties.</para>
    /// </summary>
    public bool RandomizeInitialSequenceNumbers { get; set; } = false;

    /// <summary>
    /// Seed that is used to initialize the random number generator used to randomize the initial sequence numbers.
    /// If set to null, the <see cref="Random.Shared"/> is used.
    /// </summary>
    public int? RandomizationSeed { get; set; } = 21092023;

    /// <summary>
    /// Maximum value of the randomized initial sequence number.
    /// </summary>
    public int MaxRandomInitialSequenceNumber { get; set; } = 1000;

    /// <summary>
    /// Minimum value of the randomized initial sequence number.
    /// </summary>
    public int MinRandomInitialSequenceNumber { get; set; } = 10;
}
