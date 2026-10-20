// The SDK's accessors hold process-wide state (facades, fallbacks, service bindings) that tests replace.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
