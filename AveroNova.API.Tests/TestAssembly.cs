using Xunit;

// Each web-application factory temporarily configures process-wide test settings.
// Serial execution prevents factories from racing over connection strings and JWT keys.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
