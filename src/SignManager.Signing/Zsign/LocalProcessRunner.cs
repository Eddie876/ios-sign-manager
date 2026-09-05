using System.Diagnostics;
using System.Text;

namespace SignManager.Signing.Zsign;

public sealed class LocalProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var arg in request.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        process.OutputDataReceived += (_, e) => AppendBounded(stdOut, e.Data, request.MaxOutputBytes);
        process.ErrorDataReceived += (_, e) => AppendBounded(stdErr, e.Data, request.MaxOutputBytes);

        var started = process.Start();
        if (!started)
        {
            throw new InvalidOperationException("Failed to start child process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var startedAt = DateTimeOffset.UtcNow;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(request.Timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return new ProcessRunResult(
                process.ExitCode,
                TimedOut: false,
                stdOut.ToString(),
                stdErr.ToString(),
                DateTimeOffset.UtcNow - startedAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill race if process already exited.
            }

            return new ProcessRunResult(
                ExitCode: -1,
                TimedOut: true,
                stdOut.ToString(),
                stdErr.ToString(),
                DateTimeOffset.UtcNow - startedAt);
        }
    }

    private static void AppendBounded(StringBuilder builder, string? line, int maxOutputBytes)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        if (builder.Length >= maxOutputBytes)
        {
            return;
        }

        builder.AppendLine(line);
    }
}
