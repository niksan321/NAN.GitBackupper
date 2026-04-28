using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using NAN.Git.Models;

namespace NAN.Git;

public sealed class GitHubProviderClient : IGitProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<RequestReplay> ValidateAsync(IGitBackupTarget target, CancellationToken ct = default)
    {
        try
        {
            using var client = CreateClient(target);
            using var response =
                await client.GetAsync("https://api.github.com/user/repos?per_page=1&page=1", ct);
            if (response.IsSuccessStatusCode)
                return new RequestReplay
                {
                    IsSended = true,
                    IsSuccess = true,
                    Message = response.ReasonPhrase,
                };

            var err = await response.Content.ReadAsStringAsync(ct);
            return new RequestReplay
            {
                IsSended = true,
                IsSuccess = false,
                Message = $"{(int)response.StatusCode}: {err}",
            };
        }
        catch (Exception ex)
        {
            return RequestReplay.Error(ex);
        }
    }

    public async Task<IReadOnlyList<GitRepositoryDescriptor>> ListRepositoriesAsync(IGitBackupTarget target,
        CancellationToken ct = default)
    {
        using var client = CreateClient(target);
        string userLogin;
        using (var userResponse = await client.GetAsync("https://api.github.com/user", ct))
        {
            userResponse.EnsureSuccessStatusCode();
            await using var userStream = await userResponse.Content.ReadAsStreamAsync(ct);
            var user = await JsonSerializer.DeserializeAsync<GitHubUserJson>(userStream, JsonOptions, ct);
            userLogin = user?.Login ?? "";
        }

        List<GitRepositoryDescriptor> list = [];
        var page = 1;
        while (true)
        {
            using var response =
                await client.GetAsync($"https://api.github.com/user/repos?per_page=100&page={page}", ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var batch = await JsonSerializer.DeserializeAsync<GitHubRepoJson[]>(stream, JsonOptions, ct);
            if (batch == null || batch.Length == 0) break;

            foreach (var item in batch)
            {
                if (!string.IsNullOrEmpty(item.FullName) && !string.IsNullOrEmpty(item.CloneUrl))
                {
                    var owned = !string.IsNullOrEmpty(userLogin) && item.Owner != null &&
                                string.Equals(item.Owner.Login, userLogin, StringComparison.OrdinalIgnoreCase);
                    list.Add(new GitRepositoryDescriptor(item.FullName, item.CloneUrl, item.DefaultBranch, owned));
                }
            }

            if (batch.Length < 100) break;

            page++;
        }

        return list;
    }

    public async Task<IReadOnlyList<string>> ListBranchesAsync(IGitBackupTarget target, GitRepositoryDescriptor repository,
        CancellationToken ct = default)
    {
        var slash = repository.DisplayKey.IndexOf('/');
        if (slash <= 0 || slash >= repository.DisplayKey.Length - 1)
            return [];

        var owner = repository.DisplayKey[..slash];
        var repo = repository.DisplayKey[(slash + 1)..];
        using var client = CreateClient(target);
        List<string> names = [];
        var page = 1;
        while (true)
        {
            using var response = await client.GetAsync(
                $"https://api.github.com/repos/{owner}/{repo}/branches?per_page=100&page={page}",
                ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var batch = await JsonSerializer.DeserializeAsync<GitHubBranchJson[]>(stream, JsonOptions, ct);
            if (batch == null || batch.Length == 0) break;

            foreach (var item in batch)
            {
                if (!string.IsNullOrEmpty(item.Name))
                    names.Add(item.Name);
            }

            if (batch.Length < 100) break;

            page++;
        }

        return names;
    }

    public string BuildAuthenticatedCloneUrl(IGitBackupTarget target, string httpsCloneUrl)
    {
        return new UriBuilder(httpsCloneUrl)
        {
            UserName = "x-access-token",
            Password = target.ApiKey,
        }.Uri.ToString();
    }

    private static HttpClient CreateClient(IGitBackupTarget target)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TrayGitBackupper/1.0");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", target.ApiKey);
        client.Timeout = target.HttpTimeout;
        return client;
    }

    private sealed record GitHubUserJson([property: JsonPropertyName("login")] string Login);

    private sealed record GitHubOwnerJson([property: JsonPropertyName("login")] string Login);

    private sealed record GitHubRepoJson([property: JsonPropertyName("full_name")] string FullName,
        [property: JsonPropertyName("clone_url")] string CloneUrl,
        [property: JsonPropertyName("default_branch")] string DefaultBranch,
        [property: JsonPropertyName("owner")] GitHubOwnerJson Owner);

    private sealed record GitHubBranchJson([property: JsonPropertyName("name")] string Name);
}
