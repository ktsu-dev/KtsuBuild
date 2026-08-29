// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Text.Json;
using KtsuBuild.Abstractions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Reads the public nuget.org catalog over HTTP.
/// </summary>
/// <param name="httpClient">The HTTP client to use. The caller owns its lifetime.</param>
/// <param name="logger">The build logger.</param>
public class NuGetCatalogClient(HttpClient httpClient, IBuildLogger logger) : INuGetCatalogClient
{
	private const string FlatContainerUrl = "https://api.nuget.org/v3-flatcontainer";
	private const string SearchUrl = "https://azuresearch-usnc.nuget.org/query";

	/// <inheritdoc/>
	public async Task<NuGetPackageInfo?> GetPackageAsync(string packageId, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(packageId);

		JsonElement? index = await GetJsonAsync($"{FlatContainerUrl}/{packageId.ToLowerInvariant()}/index.json", cancellationToken).ConfigureAwait(false);
		if (index is not { ValueKind: JsonValueKind.Object } ||
			!index.Value.TryGetProperty("versions", out JsonElement versions) ||
			versions.ValueKind != JsonValueKind.Array ||
			versions.GetArrayLength() == 0)
		{
			return null;
		}

		// The flat container lists versions oldest first, so the last of each kind is the newest.
		string? stable = null;
		string? prerelease = null;
		foreach (JsonElement element in versions.EnumerateArray())
		{
			if (element.GetString() is not string version)
			{
				continue;
			}

			if (version.Contains('-', StringComparison.Ordinal))
			{
				prerelease = version;
			}
			else
			{
				stable = version;
			}
		}

		(long downloads, IReadOnlyList<string> packageTypes) = await GetSearchFactsAsync(packageId, cancellationToken).ConfigureAwait(false);

		return new NuGetPackageInfo(stable, prerelease, downloads, packageTypes);
	}

	/// <summary>
	/// Reads the download total and declared package types from the search index, which is the only
	/// public endpoint that carries them.
	/// </summary>
	/// <param name="packageId">The package identifier.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The download total and package types. Zero and empty when the search index has no entry.</returns>
	private async Task<(long Downloads, IReadOnlyList<string> PackageTypes)> GetSearchFactsAsync(string packageId, CancellationToken cancellationToken)
	{
		JsonElement? search = await GetJsonAsync($"{SearchUrl}?q=packageid:{packageId}&prerelease=true", cancellationToken).ConfigureAwait(false);
		if (search is not { ValueKind: JsonValueKind.Object } ||
			!search.Value.TryGetProperty("data", out JsonElement data) ||
			data.ValueKind != JsonValueKind.Array ||
			data.GetArrayLength() == 0)
		{
			return (0, []);
		}

		JsonElement entry = data[0];

		long downloads = entry.TryGetProperty("totalDownloads", out JsonElement total) && total.TryGetInt64(out long value)
			? value
			: 0;

		List<string> packageTypes = [];
		if (entry.TryGetProperty("packageTypes", out JsonElement types) && types.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement type in types.EnumerateArray())
			{
				if (type.TryGetProperty("name", out JsonElement name) && name.GetString() is string typeName)
				{
					packageTypes.Add(typeName);
				}
			}
		}

		return (downloads, packageTypes);
	}

	/// <summary>
	/// Fetches and parses a JSON document.
	/// </summary>
	/// <param name="url">The URL to fetch.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The parsed response, or <see langword="null"/> when the request failed. An unpublished
	/// package returns 404, which is an expected outcome rather than an error.</returns>
	private async Task<JsonElement?> GetJsonAsync(string url, CancellationToken cancellationToken)
	{
		try
		{
			using HttpResponseMessage response = await httpClient.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}

			string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			using JsonDocument document = JsonDocument.Parse(content);
			return document.RootElement.Clone();
		}
		catch (HttpRequestException exception)
		{
			logger.WriteVerbose($"  Request to {url} failed: {exception.Message}");
			return null;
		}
		catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			logger.WriteVerbose($"  Request to {url} timed out");
			return null;
		}
		catch (JsonException)
		{
			logger.WriteVerbose($"  Response from {url} was not valid JSON");
			return null;
		}
	}
}
