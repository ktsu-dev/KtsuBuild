// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Globalization;
using System.Text;
using System.Text.Json;
using KtsuBuild.Abstractions;
using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Reads the GitHub REST API through the <c>gh</c> CLI, which supplies authentication from the
/// caller's existing login or from <c>GH_TOKEN</c> in CI.
/// </summary>
/// <param name="processRunner">The process runner used to invoke <c>gh</c>.</param>
/// <param name="logger">The build logger.</param>
public class GitHubApiClient(IProcessRunner processRunner, IBuildLogger logger) : IGitHubApiClient
{
	private const int PageSize = 100;

	/// <inheritdoc/>
	public async Task<IReadOnlyList<GitHubRepository>> ListOrganizationRepositoriesAsync(string organization, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(organization);

		List<GitHubRepository> repositories = [];

		bool hasMorePages = true;
		for (int page = 1; hasMorePages; page++)
		{
			string endpoint = $"/orgs/{organization}/repos?type=public&sort=full_name&direction=asc&page={page.ToString(CultureInfo.InvariantCulture)}&per_page={PageSize.ToString(CultureInfo.InvariantCulture)}";
			JsonElement? response = await GetJsonAsync(endpoint, cancellationToken).ConfigureAwait(false);
			if (response is not { ValueKind: JsonValueKind.Array })
			{
				break;
			}

			JsonElement[] items = [.. response.Value.EnumerateArray()];
			repositories.AddRange(items
				.Where(static item => GetString(item, "name") is not null)
				.Select(static item => new GitHubRepository(
					GetString(item, "name")!,
					GetString(item, "default_branch") ?? "main",
					item.TryGetProperty("archived", out JsonElement archived) && archived.ValueKind == JsonValueKind.True)));

			// A page shorter than the page size is the last one.
			hasMorePages = items.Length == PageSize;
		}

		return repositories;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<GitHubRelease>> ListReleasesAsync(string organization, string repository, CancellationToken cancellationToken = default)
	{
		JsonElement? response = await GetJsonAsync($"/repos/{organization}/{repository}/releases", cancellationToken).ConfigureAwait(false);
		if (response is not { ValueKind: JsonValueKind.Array })
		{
			return [];
		}

		return
		[
			.. response.Value.EnumerateArray()
				.Select(static item => GetString(item, "tag_name"))
				.OfType<string>()
				.Select(static tag => new GitHubRelease(tag)),
		];
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> ListTreePathsAsync(string organization, string repository, string branch, CancellationToken cancellationToken = default)
	{
		// One recursive tree request replaces a directory-by-directory walk of the contents API, which
		// cost one call per directory per repository.
		JsonElement? response = await GetJsonAsync($"/repos/{organization}/{repository}/git/trees/{branch}?recursive=1", cancellationToken).ConfigureAwait(false);
		if (response is not { ValueKind: JsonValueKind.Object } || !response.Value.TryGetProperty("tree", out JsonElement tree))
		{
			return [];
		}

		if (response.Value.TryGetProperty("truncated", out JsonElement truncated) && truncated.ValueKind == JsonValueKind.True)
		{
			logger.WriteWarning($"  Tree for {repository} was truncated by the API, so some files are not listed");
		}

		return
		[
			.. tree.EnumerateArray()
				.Where(static item => GetString(item, "type") == "blob")
				.Select(static item => GetString(item, "path"))
				.OfType<string>(),
		];
	}

	/// <inheritdoc/>
	public async Task<string?> GetFileTextAsync(string organization, string repository, string path, CancellationToken cancellationToken = default)
	{
		JsonElement? response = await GetJsonAsync($"/repos/{organization}/{repository}/contents/{path}", cancellationToken).ConfigureAwait(false);
		if (response is not { ValueKind: JsonValueKind.Object } || GetString(response.Value, "content") is not string encoded)
		{
			return null;
		}

		try
		{
			return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
		}
		catch (FormatException)
		{
			logger.WriteVerbose($"  Could not decode {path} in {repository}");
			return null;
		}
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> ListDirectoryNamesAsync(string organization, string repository, string path, CancellationToken cancellationToken = default)
	{
		JsonElement? response = await GetJsonAsync($"/repos/{organization}/{repository}/contents/{path}", cancellationToken).ConfigureAwait(false);
		if (response is not { ValueKind: JsonValueKind.Array })
		{
			return [];
		}

		return
		[
			.. response.Value.EnumerateArray()
				.Where(static item => GetString(item, "type") == "dir")
				.Select(static item => GetString(item, "name"))
				.OfType<string>(),
		];
	}

	/// <inheritdoc/>
	public async Task<int> CountCommitsSinceAsync(string organization, string repository, DateTimeOffset since, CancellationToken cancellationToken = default)
	{
		string timestamp = since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
		JsonElement? response = await GetJsonAsync($"/repos/{organization}/{repository}/commits?since={timestamp}&per_page={PageSize.ToString(CultureInfo.InvariantCulture)}", cancellationToken).ConfigureAwait(false);

		return response is { ValueKind: JsonValueKind.Array } ? response.Value.GetArrayLength() : 0;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> ListActiveWorkflowFileNamesAsync(string organization, string repository, CancellationToken cancellationToken = default)
	{
		JsonElement? response = await GetJsonAsync($"/repos/{organization}/{repository}/actions/workflows", cancellationToken).ConfigureAwait(false);
		if (response is not { ValueKind: JsonValueKind.Object } || !response.Value.TryGetProperty("workflows", out JsonElement workflows))
		{
			return [];
		}

		return
		[
			.. workflows.EnumerateArray()
				.Where(static item => GetString(item, "state") == "active")
				.Select(static item => GetString(item, "path"))
				.OfType<string>()
				.Select(static path => path[(path.LastIndexOf('/') + 1)..]),
		];
	}

	/// <inheritdoc/>
	public async Task<GitHubWorkflowRun?> GetLatestWorkflowRunAsync(string organization, string repository, string workflowFileName, string branch, CancellationToken cancellationToken = default)
	{
		string endpoint = $"/repos/{organization}/{repository}/actions/workflows/{workflowFileName}/runs?per_page=1";
		if (!string.IsNullOrEmpty(branch))
		{
			endpoint += $"&branch={branch}";
		}

		JsonElement? response = await GetJsonAsync(endpoint, cancellationToken).ConfigureAwait(false);
		if (response is not { ValueKind: JsonValueKind.Object } ||
			!response.Value.TryGetProperty("workflow_runs", out JsonElement runs) ||
			runs.ValueKind != JsonValueKind.Array ||
			runs.GetArrayLength() == 0)
		{
			return null;
		}

		JsonElement run = runs[0];
		return new GitHubWorkflowRun(GetString(run, "status"), GetString(run, "conclusion"));
	}

	/// <summary>
	/// Runs <c>gh api</c> against an endpoint and parses the response.
	/// </summary>
	/// <param name="endpoint">The API endpoint, including any query string.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The parsed response, or <see langword="null"/> when the call failed or returned nothing
	/// parseable. A missing resource is an expected outcome here, not an error.</returns>
	private async Task<JsonElement?> GetJsonAsync(string endpoint, CancellationToken cancellationToken)
	{
		ProcessResult result = await processRunner.RunAsync("gh", $"api \"{endpoint}\"", null, cancellationToken).ConfigureAwait(false);
		if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
		{
			logger.WriteVerbose($"  gh api {endpoint} returned no data");
			return null;
		}

		try
		{
			using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
			return document.RootElement.Clone();
		}
		catch (JsonException)
		{
			logger.WriteVerbose($"  gh api {endpoint} returned unparseable JSON");
			return null;
		}
	}

	private static string? GetString(JsonElement element, string propertyName) =>
		element.ValueKind == JsonValueKind.Object &&
		element.TryGetProperty(propertyName, out JsonElement value) &&
		value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;
}
