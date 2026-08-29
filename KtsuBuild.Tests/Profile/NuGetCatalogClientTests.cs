// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using System.Net;
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

	private sealed class StubHandler : HttpMessageHandler
	{
		public Dictionary<string, (HttpStatusCode Status, string Body)> Responses { get; } = [];

		public List<string> Requested { get; } = [];

		public Exception? ThrowOnSend { get; set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (ThrowOnSend is not null)
			{
				throw ThrowOnSend;
			}

			string url = request.RequestUri!.ToString();
			Requested.Add(url);

			foreach ((string fragment, (HttpStatusCode status, string body)) in Responses)
			{
				if (url.Contains(fragment, StringComparison.OrdinalIgnoreCase))
				{
					return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
				}
			}

			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) });
		}
	}
}
