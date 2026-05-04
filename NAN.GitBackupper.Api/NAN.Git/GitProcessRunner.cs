using System.Diagnostics;
using NAN.Git.Abstractions;

namespace NAN.Git;

public sealed class GitProcessRunner(IGitLocalizer localizer)
{
    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string workingDirectory, string[] args,
        CancellationToken ct = default)
    {
        var gitExe = ResolveGitExecutablePath();
        if (string.IsNullOrEmpty(gitExe))
            return (-1, "", localizer.Translate("GitExecutableNotFound"));

        var psi = new ProcessStartInfo
        {
            FileName = gitExe,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var stdOut = await proc.StandardOutput.ReadToEndAsync(ct);
        var stdErr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return (proc.ExitCode, stdOut, stdErr);
    }

    private static string ResolveGitExecutablePath()
    {
        var name = OperatingSystem.IsWindows() ? "git.exe" : "git";
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
            foreach (var segment in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(segment, name);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                    // ignore invalid path segments
                }
            }

        return File.Exists(name) ? name : null;
    }
}
