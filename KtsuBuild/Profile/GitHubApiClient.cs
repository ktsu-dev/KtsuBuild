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

	/// <summary>The one HTTP failure that is an answer rather than an outage.</summary>
	private const int NotFoundStatus = 404;

	/// <inheritdoc/>
	public async Task<IReadOnlyList<GitHubRepository>> ListOrganizationRepositoriesAsync(string organization, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(organization);

		List<GitHubRepository> repositories = [];

		bool hasMorePages = true;
		int page = 1;
		while (hasMorePages)
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
					item.TryGetProperty("archived", out JsonElement archived) && archived.ValueKind == JsonValueKind.True,
					GetInt(item, "stargazers_count"))));

			// A page shorter than the page size is the last one.
			hasMorePages = items.Length == PageSize;
			page++;
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
	public async Task<int> CountCommitsSinceAsync(string organization, string repository, DateTimeOffset since, CancellationToken cancellationToken = default)
	{
		string timestamp = since.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

		// The commits API truncates a response at the page size, so reading one page reports the cap
		// rather than the count for any repository busy enough to fill it, and the activity number then
		// stops moving for exactly the repositories the column exists to tell apart.
		int count = 0;
		bool hasMorePages = true;
		int page = 1;
		while (hasMorePages)
		{
			string endpoint = $"/repos/{organization}/{repository}/commits?since={timestamp}&page={page.ToString(CultureInfo.InvariantCulture)}&per_page={PageSize.ToString(CultureInfo.InvariantCulture)}";
			JsonElement? response = await GetJsonAsync(endpoint, cancellationToken).ConfigureAwait(false);
			if (response is not { ValueKind: JsonValueKind.Array })
			{
				break;
			}

			int pageLength = response.Value.GetArrayLength();
			count += pageLength;

			// A page shorter than the page size is the last one.
			hasMorePages = pageLength == PageSize;
			page++;
		}

		return count;
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
	/// <remarks>
	/// The caller cannot tell a failed call from a confirmed empty one, because both arrive as
	/// <see langword="null"/>. Until it can, the log is the only place the difference exists, so a
	/// failure is written as a warning rather than as verbose output: the daily profile job runs
	/// without <c>--verbose</c>, and a rate limit or a transient 5xx on one repository's call would
	/// otherwise drop that repository from the generated README with nothing said about why.
	/// </remarks>
	private async Task<JsonElement?> GetJsonAsync(string endpoint, CancellationToken cancellationToken)
	{
		ProcessResult result = await processRunner.RunAsync("gh", $"api \"{endpoint}\"", null, cancellationToken).ConfigureAwait(false);
		if (!result.Success)
		{
			// A missing resource is an expected answer here: several of these calls ask for files a
			// repository is free not to have. Anything else is the call not happening at all.
			if (ReadHttpStatus(result.StandardError) == NotFoundStatus)
			{
				logger.WriteVerbose($"  gh api {endpoint} returned no data (HTTP 404)");
			}
			else
			{
				logger.WriteWarning($"  gh api {endpoint} failed{DescribeFailure(result)}, so this run is reading it as no data");
			}

			return null;
		}

		if (string.IsNullOrWhiteSpace(result.StandardOutput))
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
			// The call reported success, so an unreadable body is a truncated or corrupted response
			// rather than a missing resource, and it costs the same data as an outright failure.
			logger.WriteWarning($"  gh api {endpoint} returned unparseable JSON, so this run is reading it as no data");
			return null;
		}
	}

	/// <summary>
	/// Describes why a <c>gh api</c> call failed, for the warning that reports it.
	/// </summary>
	/// <param name="result">The failed process result.</param>
	/// <returns>A phrase naming the reason, or the exit code when <c>gh</c> said nothing.</returns>
	private static string DescribeFailure(ProcessResult result)
	{
		string reason = FirstLine(result.StandardError);
		return reason.Length == 0
			? $" with exit code {result.ExitCode.ToString(CultureInfo.InvariantCulture)}"
			: $": {reason}";
	}

	/// <summary>
	/// Reads the HTTP status <c>gh</c> reports on a failed call.
	/// </summary>
	/// <param name="standardError">The standard error written by <c>gh</c>.</param>
	/// <returns>The status code, or <see langword="null"/> when the output carries none, which is
	/// what a failure short of a response looks like.</returns>
	/// <remarks>
	/// <c>gh</c> reports the status in its message rather than in its exit code, which is 1 for
	/// every HTTP error alike, for example <c>gh: Not Found (HTTP 404)</c>.
	/// </remarks>
	private static int? ReadHttpStatus(string standardError)
	{
		const string marker = "(HTTP ";

		int start = standardError.LastIndexOf(marker, StringComparison.Ordinal);
		if (start < 0)
		{
			return null;
		}

		start += marker.Length;
		int end = standardError.IndexOf(')', start);
		return end > start && int.TryParse(standardError[start..end].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int status)
			? status
			: null;
	}

	/// <summary>
	/// Returns the first non-empty line of some output, trimmed.
	/// </summary>
	/// <param name="text">The output to read.</param>
	/// <returns>The first non-empty line, or an empty string when there is none.</returns>
	private static string FirstLine(string text) =>
		text.Split('\n').Select(static line => line.Trim()).FirstOrDefault(static line => line.Length > 0) ?? string.Empty;

	private static int GetInt(JsonElement element, string propertyName) =>
		element.ValueKind == JsonValueKind.Object &&
		element.TryGetProperty(propertyName, out JsonElement value) &&
		value.ValueKind == JsonValueKind.Number &&
		value.TryGetInt32(out int number)
			? number
			: 0;

	private static string? GetString(JsonElement element, string propertyName) =>
		element.ValueKind == JsonValueKind.Object &&
		element.TryGetProperty(propertyName, out JsonElement value) &&
		value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;
}
