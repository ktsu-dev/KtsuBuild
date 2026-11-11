// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using System.IO.Compression;
using System.Net;
using System.Text;
using KtsuBuild.Abstractions;
using KtsuBuild.Profile;
using KtsuBuild.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class NuGetCatalogClientTests : IDisposable
{
	private StubHandler _handler = null!;
	private HttpClient _httpClient = null!;
	private NuGetCatalogClient _client = null!;

	private const string IndexPath = "v3-flatcontainer";
	private const string SearchPath = "azuresearch";
	private const string RegistrationPath = "registration5-gz-semver2/";

	[TestInitialize]
	public void Setup()
	{
		_handler = new StubHandler();
		_httpClient = new HttpClient(_handler);
		_client = new NuGetCatalogClient(_httpClient, new MockBuildLogger());
	}

	[TestCleanup]
	public void Cleanup() => Dispose();

	/// <summary>
	/// Disposes the client, which also disposes the handler it was given.
	/// </summary>
	public void Dispose()
	{
		_httpClient?.Dispose();
		_handler?.Dispose();
		GC.SuppressFinalize(this);
	}

	private void Respond(string urlFragment, string body, HttpStatusCode status = HttpStatusCode.OK) =>
		_handler.Responses[urlFragment] = (status, body);

	private static string CatalogEntries(params (string Version, bool Listed)[] entries) =>
		string.Join(",", entries.Select(static e => $$$"""{"catalogEntry":{"version":"{{{e.Version}}}","listed":{{{(e.Listed ? "true" : "false")}}}}}"""));

	private void RespondWithRegistration(params (string Version, bool Listed)[] entries) =>
		Respond(RegistrationPath, $$"""{"items":[{"items":[{{CatalogEntries(entries)}}]}]}""");

	private void RespondWithVersions(params string[] versions) =>
		Respond(IndexPath, $$"""{"versions":[{{string.Join(",", versions.Select(static v => $"\"{v}\""))}}]}""");

	[TestMethod]
	public async Task GetPackageAsync_ReturnsTheNewestStableAndPrerelease()
	{
		// The flat container lists versions oldest first, so the last of each kind is the newest.
		RespondWithVersions("1.0.0", "1.1.0-pre.1", "1.1.0", "1.2.0-pre.1");

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Extensions").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual("1.1.0", package.StableVersion);
		Assert.AreEqual("1.2.0-pre.1", package.PrereleaseVersion);
	}

	[TestMethod]
	public async Task GetPackageAsync_WithOnlyPrereleases_ReportsNoStableVersion()
	{
		RespondWithVersions("1.0.0-pre.1", "1.0.0-pre.2");

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.IsNull(package.StableVersion);
		Assert.AreEqual("1.0.0-pre.2", package.PrereleaseVersion);
	}

	[TestMethod]
	public async Task GetPackageAsync_LowercasesThePackageIdInTheUrl()
	{
		// The flat container addresses packages by their lowercased id.
		RespondWithVersions("1.0.0");

		await _client.GetPackageAsync("ktsu.AppDataStorage").ConfigureAwait(false);

		Assert.IsTrue(
			_handler.Requested.Exists(static url => url.Contains("/ktsu.appdatastorage/", StringComparison.Ordinal)),
			$"Expected a lowercased package id, got: {string.Join(", ", _handler.Requested)}");
	}

	[TestMethod]
	public async Task GetPackageAsync_WithAnUnpublishedPackage_ReturnsNull()
	{
		// An unpublished package answers 404, which is an expected outcome rather than an error.
		Respond(IndexPath, string.Empty, HttpStatusCode.NotFound);

		Assert.IsNull(await _client.GetPackageAsync("ktsu.DoesNotExist").ConfigureAwait(false));
	}

	[TestMethod]
	public async Task GetPackageAsync_WithNoVersions_ReturnsNull()
	{
		Respond(IndexPath, """{"versions":[]}""");

		Assert.IsNull(await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false));
	}

	[TestMethod]
	public async Task GetPackageAsync_WithUnparseableResponse_ReturnsNull()
	{
		Respond(IndexPath, "not json");

		Assert.IsNull(await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false));
	}

	[TestMethod]
	public async Task GetPackageAsync_ReadsDownloadsAndPackageTypesFromTheSearchIndex()
	{
		RespondWithVersions("2.9.0");
		Respond(SearchPath, """{"data":[{"totalDownloads":6301,"packageTypes":[{"name":"DotnetTool"}]}]}""");

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.KtsuBuild.Tool").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual(6301, package.TotalDownloads);
		Assert.AreEqual("DotnetTool", string.Join(",", package.PackageTypes));
	}

	[TestMethod]
	public async Task GetPackageAsync_ReadsTheDependencyPackageType()
	{
		RespondWithVersions("1.6.8");
		Respond(SearchPath, """{"data":[{"totalDownloads":100,"packageTypes":[{"name":"Dependency"}]}]}""");

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Extensions").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual("Dependency", string.Join(",", package.PackageTypes));
	}

	[TestMethod]
	public async Task GetPackageAsync_WithNoSearchEntry_StillReturnsVersions()
	{
		// The search index lags publication, so a package can be in the flat container but not yet
		// searchable. Losing the download count should not lose the versions.
		RespondWithVersions("1.0.0");
		Respond(SearchPath, """{"data":[]}""");

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual("1.0.0", package.StableVersion);
		Assert.AreEqual(0, package.TotalDownloads);
		Assert.IsEmpty(package.PackageTypes);
	}

	[TestMethod]
	public async Task GetPackageAsync_WithASearchEntryMissingItsFields_DegradesQuietly()
	{
		RespondWithVersions("1.0.0");
		Respond(SearchPath, """{"data":[{}]}""");

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual(0, package.TotalDownloads);
		Assert.IsEmpty(package.PackageTypes);
	}

	[TestMethod]
	public async Task GetPackageAsync_WithAFailedRequest_ReturnsNull()
	{
		_handler.ThrowOnSend = new HttpRequestException("no network");

		Assert.IsNull(await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false));
	}

	[TestMethod]
	public async Task GetPackageAsync_WithATimedOutRequest_ReturnsNull()
	{
		_handler.ThrowOnSend = new TaskCanceledException("timed out");

		Assert.IsNull(await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false));
	}

	[TestMethod]
	public async Task GetPackageAsync_SkipsAnUnlistedNewestStableVersion()
	{
		// Unlisting is how a bad release is withdrawn. The flat container still lists it, so only the
		// registration index can say it isn't current.
		RespondWithVersions("2.31.3", "2.32.0");
		RespondWithRegistration(("2.31.3", true), ("2.32.0", false));

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Sdk").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual("2.31.3", package.StableVersion);
	}

	[TestMethod]
	public async Task GetPackageAsync_SkipsAnUnlistedNewestPrerelease()
	{
		RespondWithVersions("1.0.0", "1.1.0-pre.1", "1.1.0-pre.2");
		RespondWithRegistration(("1.0.0", true), ("1.1.0-pre.1", true), ("1.1.0-pre.2", false));

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual("1.0.0", package.StableVersion);
		Assert.AreEqual("1.1.0-pre.1", package.PrereleaseVersion);
	}

	[TestMethod]
	public async Task GetPackageAsync_WithEveryStableVersionUnlisted_ReportsNoStableVersion()
	{
		RespondWithVersions("1.0.0", "1.1.0-pre.1");
		RespondWithRegistration(("1.0.0", false), ("1.1.0-pre.1", true));

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.IsNull(package.StableVersion);
		Assert.AreEqual("1.1.0-pre.1", package.PrereleaseVersion);
	}

	[TestMethod]
	public async Task GetPackageAsync_FetchesRegistrationPagesTheIndexLeavesOut()
	{
		// A package with many versions has an index whose pages carry only an @id.
		RespondWithVersions("1.0.0", "1.1.0", "1.2.0");
		Respond(
			"registration5-gz-semver2/ktsu.widget/index.json",
			"""{"items":[{"@id":"https://api.nuget.org/v3/registration5-gz-semver2/ktsu.widget/page/1.0.0/1.0.0.json"},{"@id":"https://api.nuget.org/v3/registration5-gz-semver2/ktsu.widget/page/1.1.0/1.2.0.json"}]}""");
		Respond("page/1.0.0/1.0.0.json", $$"""{"items":[{{CatalogEntries(("1.0.0", true))}}]}""");
		Respond("page/1.1.0/1.2.0.json", $$"""{"items":[{{CatalogEntries(("1.1.0", true), ("1.2.0", false))}}]}""");

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual("1.1.0", package.StableVersion);
	}

	[TestMethod]
	public async Task GetPackageAsync_ReadsAGzipEncodedRegistrationIndex()
	{
		// nuget.org serves the registration index gzip-compressed, and the client the profile command
		// builds doesn't decompress on its own.
		RespondWithVersions("1.0.0", "1.1.0");
		_handler.GzipFragments.Add(RegistrationPath);
		RespondWithRegistration(("1.0.0", true), ("1.1.0", false));

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual("1.0.0", package.StableVersion);
	}

	[TestMethod]
	public async Task GetPackageAsync_WithAnUnreadableRegistrationIndex_FallsBackToTheFlatContainer()
	{
		RespondWithVersions("1.0.0", "1.1.0");
		Respond(RegistrationPath, string.Empty, HttpStatusCode.InternalServerError);

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual("1.1.0", package.StableVersion);
	}

	[TestMethod]
	public async Task GetPackageAsync_WithAnUnreadableRegistrationPage_FallsBackToTheFlatContainer()
	{
		// Half a registration index could miss the newest versions, so a page that can't be read
		// discards it rather than reporting an older version as current.
		RespondWithVersions("1.0.0", "1.1.0");
		Respond(RegistrationPath, """{"items":[{"@id":"https://api.nuget.org/v3/registration5-gz-semver2/ktsu.widget/page/missing.json"}]}""");
		Respond("registration5-gz-semver2/ktsu.widget/page/missing.json", string.Empty, HttpStatusCode.NotFound);

		NuGetPackageInfo? package = await _client.GetPackageAsync("ktsu.Widget").ConfigureAwait(false);

		Assert.IsNotNull(package);
		Assert.AreEqual("1.1.0", package.StableVersion);
	}

	private sealed class StubHandler : HttpMessageHandler
	{
		public Dictionary<string, (HttpStatusCode Status, string Body)> Responses { get; } = [];

		public List<string> Requested { get; } = [];

		public Exception? ThrowOnSend { get; set; }

		/// <summary>
		/// URL fragments whose responses are gzip-compressed and marked as such, as nuget.org serves
		/// its registration index.
		/// </summary>
		public List<string> GzipFragments { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (ThrowOnSend is not null)
			{
				throw ThrowOnSend;
			}

			string url = request.RequestUri!.ToString();
			Requested.Add(url);

			// The longest matching fragment wins, so a page URL isn't answered with its index's body.
			foreach ((string fragment, (HttpStatusCode status, string body)) in Responses.OrderByDescending(static r => r.Key.Length))
			{
				if (url.Contains(fragment, StringComparison.OrdinalIgnoreCase))
				{
					HttpContent content = CreateContent(url, body);
					return Task.FromResult(new HttpResponseMessage(status) { Content = content });
				}
			}

			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) });
		}

		private HttpContent CreateContent(string url, string body)
		{
			if (!GzipFragments.Exists(fragment => url.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
			{
				return new StringContent(body);
			}

			using MemoryStream compressed = new();
			using (GZipStream gzip = new(compressed, CompressionLevel.Fastest, leaveOpen: true))
			{
				byte[] bytes = Encoding.UTF8.GetBytes(body);
				gzip.Write(bytes, 0, bytes.Length);
			}

			ByteArrayContent content = new(compressed.ToArray());
			content.Headers.ContentEncoding.Add("gzip");
			return content;
		}
	}
}
