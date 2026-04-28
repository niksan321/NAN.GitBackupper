using NAN.Git;

namespace NAN.GitBackupper.Api.Localization;

public sealed class EnglishGitLocalizer : IGitLocalizer
{
    public string Translate(string key, params object[] args)
    {
        return key switch
        {
            "GitExecutableNotFound" => "Git executable not found in PATH.",
            "GitLabApiError" when args.Length >= 3 =>
                $"GitLab API error for {args[0]}: HTTP {args[1]} {args[2]}",
            _ => args is { Length: > 0 } ? string.Join(" ", args.Select(a => a?.ToString() ?? "")) : key,
        };
    }
}
