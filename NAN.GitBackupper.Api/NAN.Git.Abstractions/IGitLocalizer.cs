namespace NAN.Git.Abstractions;

/// <summary>
/// Localization for git/API messages (implemented by the host app, e.g. Wpf.Translation).
/// </summary>
public interface IGitLocalizer
{
    string Translate(string key, params object[] args);
}