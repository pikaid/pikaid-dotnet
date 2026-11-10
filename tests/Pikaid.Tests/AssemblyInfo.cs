using Xunit;

// Generation tests swap the internal entropy source and clock seams. Running the
// suite sequentially keeps those seams deterministic and avoids cross-test races.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
