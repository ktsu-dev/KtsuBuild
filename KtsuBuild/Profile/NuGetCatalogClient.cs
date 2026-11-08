// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.IO.Compression;
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
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "nuget.org's published registration endpoint; the profile reads only nuget.org.")]
	private const string RegistrationUrl = "https://api.nuget.org/v3/registration5-gz-semver2";
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

		// The flat container lists every version, unlisted ones included, so the registration index
		// decides which are current. It is the fallback only when the registration index can't be read.
		(string? stable, string? prerelease) = await GetListedVersionsAsync(packageId, cancellationToken).ConfigureAwait(false)
			?? NewestOfEachKind(versions.EnumerateArray().Select(static element => (element.GetString(), true)));

		(long downloads, IReadOnlyList<string> packageTypes) = await GetSearchFactsAsync(packageId, cancellationToken).ConfigureAwait(false);

		return new NuGetPackageInfo(stable, prerelease, downloads, packageTypes);
	}

	/// <summary>
	/// Picks the newest listed stable and prerelease version from versions given oldest first.
	/// </summary>
	/// <param name="versions">Each version with whether it is listed, oldest first.</param>
	/// <returns>The newest listed stable and prerelease versions, either of which may be absent.</returns>
	private static (string? Stable, string? Prerelease) NewestOfEachKind(IEnumerable<(string? Version, bool Listed)> versions)
	{
		string? stable = null;
		string? prerelease = null;
		foreach ((string? version, bool listed) in versions)
		{
			if (version is null || !listed)
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

		return (stable, prerelease);
	}

	/// <summary>
	/// Reads the newest listed versions from the registration index, which is the endpoint that
	/// records whether a version is unlisted. Unlisting is how a bad release is withdrawn, so an
	/// unlisted version must not be reported as current.
	/// </summary>
	/// <param name="packageId">The package identifier.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The newest listed stable and prerelease versions, or <see langword="null"/> when the
	/// registration index or one of its pages can't be read.</returns>
	private async Task<(string? Stable, string? Prerelease)?> GetListedVersionsAsync(string packageId, CancellationToken cancellationToken)
	{
		JsonElement? index = await GetJsonAsync($"{RegistrationUrl}/{packageId.ToLowerInvariant()}/index.json", cancellationToken).ConfigureAwait(false);
		if (index is not { ValueKind: JsonValueKind.Object } ||
			!index.Value.TryGetProperty("items", out JsonElement pages) ||
			pages.ValueKind != JsonValueKind.Array)
		{
			return null;
		}

		// Pages and the entries in them are ordered oldest first.
		List<(string? Version, bool Listed)> entries = [];
		foreach (JsonElement page in pages.EnumerateArray())
		{
			if (await GetRegistrationPageItemsAsync(page, cancellationToken).ConfigureAwait(false) is not JsonElement items)
			{
				return null;
			}

			entries.AddRange(items.EnumerateArray()
				.Where(static item => item.TryGetProperty("catalogEntry", out JsonElement entry) && entry.ValueKind == JsonValueKind.Object)
				.Select(static item => ReadCatalogEntry(item.GetProperty("catalogEntry"))));
		}

		return NewestOfEachKind(entries);
	}

	/// <summary>
	/// Reads a registration catalog entry's version and whether it is listed.
	/// </summary>
	/// <param name="entry">The <c>catalogEntry</c> object.</param>
	/// <returns>The version, and whether it is listed. An entry that doesn't say is listed, as nuget.org treats it.</returns>
	private static (string? Version, bool Listed) ReadCatalogEntry(JsonElement entry) =>
		(entry.TryGetProperty("version", out JsonElement value) ? value.GetString() : null,
		!entry.TryGetProperty("listed", out JsonElement flag) || flag.ValueKind != JsonValueKind.False);

	/// <summary>
	/// Returns the entries of one registration page. A package with many versions leaves its pages
	/// out of the index, and each one then has to be fetched by its <c>@id</c>.
	/// </summary>
	/// <param name="page">The page as the registration index gives it.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The page's <c>items</c> array, or <see langword="null"/> when it can't be read.</returns>
	private async Task<JsonElement?> GetRegistrationPageItemsAsync(JsonElement page, CancellationToken cancellationToken)
	{
		if (!page.TryGetProperty("items", out JsonElement items) &&
			(!page.TryGetProperty("@id", out JsonElement pageId) ||
			pageId.GetString() is not string pageUrl ||
			await GetJsonAsync(pageUrl, cancellationToken).ConfigureAwait(false) is not { ValueKind: JsonValueKind.Object } fetched ||
			!fetched.TryGetProperty("items", out items)))
		{
			return null;
		}

		return items.ValueKind == JsonValueKind.Array ? items : null;
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

		IReadOnlyList<string> packageTypes =
			entry.TryGetProperty("packageTypes", out JsonElement types) && types.ValueKind == JsonValueKind.Array
				? [.. types.EnumerateArray()
					.Select(static type => type.TryGetProperty("name", out JsonElement name) ? name.GetString() : null)
					.OfType<string>()]
				: [];

		return (downloads, packageTypes);
	}

	/// <summary>
	/// Parses a JSON document from a stream.
	/// </summary>
	/// <param name="stream">The stream to read.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The root element, detached from the document.</returns>
	private static async Task<JsonElement> ParseAsync(Stream stream, CancellationToken cancellationToken)
	{
		using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
		return document.RootElement.Clone();
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

			// The registration index is served gzip-compressed whether or not it was asked for, and an
			// HttpClient decompresses only when its handler is configured to.
			using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
			if (response.Content.Headers.ContentEncoding.Contains("gzip", StringComparer.OrdinalIgnoreCase))
			{
				using GZipStream decompressed = new(body, CompressionMode.Decompress);
				return await ParseAsync(decompressed, cancellationToken).ConfigureAwait(false);
			}

			return await ParseAsync(body, cancellationToken).ConfigureAwait(false);
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
		catch (InvalidDataException)
		{
			logger.WriteVerbose($"  Response from {url} claimed gzip encoding but was not gzip data");
			return null;
		}
	}
}
