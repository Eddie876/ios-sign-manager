namespace SignManager.Signing.Zsign;

public sealed record ProcessRunRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout,
    int MaxOutputBytes = 256 * 1024);

public sealed record ProcessRunResult(
    int ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration);

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken);
}
