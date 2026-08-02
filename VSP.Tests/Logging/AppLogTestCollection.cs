using Xunit;

namespace VSP.Tests.Logging;

/// <summary>
/// Every test class that calls <c>AppLog.Initialize</c> shares this collection so xunit never
/// runs two of them in parallel against AppLog's single process-wide static target -- otherwise
/// one test's RecordingLogger could observe another test's log calls, or vice versa.
/// </summary>
[CollectionDefinition("AppLog", DisableParallelization = true)]
public class AppLogTestCollection
{
}
