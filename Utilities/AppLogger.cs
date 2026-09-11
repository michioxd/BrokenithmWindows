using System.Diagnostics;

namespace BrokenithmWindows.Utilities;

public interface IAppLogger
{
    void Error(string operation, Exception exception);
}

public sealed class AppLogger : IAppLogger
{
    public void Error(string operation, Exception exception) =>
        Trace.TraceError("{0}: {1}", operation, exception);
}
