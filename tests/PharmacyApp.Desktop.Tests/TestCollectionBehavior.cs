using Xunit;

// Avalonia property registry initialization is not thread-safe when static control
// constructors are invoked concurrently from multiple test threads without full platform bootstrap.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
