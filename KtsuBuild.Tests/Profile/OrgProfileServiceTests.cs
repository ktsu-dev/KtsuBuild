// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using KtsuBuild.Abstractions;
using KtsuBuild.Profile;
using KtsuBuild.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

[TestClass]
public class OrgProfileServiceTests
{
	private IGitHubApiClient _gitHub = null!;
	private INuGetCatalogClient _nuGet = null!;
	private OrgProfileService _service = null!;

	[TestInitialize]
	public void Setup()
	{
		_gitHub = Substitute.For<IGitHubApiClient>();
		_nuGet = Substitute.For<INuGetCatalogClient>();
		_service = new OrgProfileService(_gitHub, _nuGet, new MockBuildLogger());

		_gitHub.ListReleasesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<GitHubRelease>>([new GitHubRelease("v1.0.0")]));
		_gitHub.ListTreePathsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>([]));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>(new string('x', 512)));
		_gitHub.ListDirectoryNamesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>([]));
		_nuGet.GetPackageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<NuGetPackageInfo?>(null));
	}

	private void HaveRepositories(params GitHubRepository[] repositories) =>
		_gitHub.ListOrganizationRepositoriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<GitHubRepository>>(repositories));

	private static ProfileOptions Options => new() { Organization = "ktsu-dev" };

	[TestMethod]
	public async Task GatherAsync_SkipsArchivedRepositories()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false), new GitHubRepository("Bravo", "main", true));

		IReadOnlyList<RepoFacts> facts = await _service.GatherAsync(Options).ConfigureAwait(false);

		Assert.AreEqual("Alpha", string.Join(",", facts.Select(static f => f.Name)));
	}

	[TestMethod]
	public async Task GatherAsync_SkipsExcludedRepositories()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false), new GitHubRepository("Sdk", "main", false));

		IReadOnlyList<RepoFacts> facts = await _service.GatherAsync(Options with { ExcludedRepositories = ["Sdk"] }).ConfigureAwait(false);

		Assert.AreEqual("Alpha", string.Join(",", facts.Select(static f => f.Name)));
	}

	[TestMethod]
	public async Task GatherAsync_WithOnlyFilter_KeepsJustThoseRepositories()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false), new GitHubRepository("Bravo", "main", false), new GitHubRepository("Charlie", "main", false));

		IReadOnlyList<RepoFacts> facts = await _service.GatherAsync(Options with { OnlyRepositories = ["Bravo"] }).ConfigureAwait(false);

		Assert.AreEqual("Bravo", string.Join(",", facts.Select(static f => f.Name)));
	}

	[TestMethod]
	public async Task GatherAsync_SkipsRepositoriesWithNoStableRelease()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false));
		_gitHub.ListReleasesAsync(Arg.Any<string>(), "Alpha", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<GitHubRelease>>([new GitHubRelease("v1.0.0-pre.1")]));

		Assert.AreEqual(0, (await _service.GatherAsync(Options).ConfigureAwait(false)).Count);
	}

	[TestMethod]
	public async Task GatherAsync_SkipsRepositoriesWithNoReleasesAtAll()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false));
		_gitHub.ListReleasesAsync(Arg.Any<string>(), "Alpha", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<GitHubRelease>>([]));

		Assert.AreEqual(0, (await _service.GatherAsync(Options).ConfigureAwait(false)).Count);
	}

	[TestMethod]
	public async Task GatherAsync_StripsASingleVersionPrefixFromTags()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false));
		_gitHub.ListReleasesAsync(Arg.Any<string>(), "Alpha", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<GitHubRelease>>([new GitHubRelease("v2.3.1"), new GitHubRelease("v2.4.0-pre.1")]));

		RepoFacts facts = (await _service.GatherAsync(Options).ConfigureAwait(false))[0];

		Assert.AreEqual("2.3.1", facts.ReleaseStableVersion);
		Assert.AreEqual("2.4.0-pre.1", facts.ReleasePrereleaseVersion);
	}

	[TestMethod]
	public async Task GatherAsync_ChecksWingetOnlyForApplications()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false));

		await _service.GatherAsync(Options).ConfigureAwait(false);

		await _gitHub.DidNotReceive()
			.ListDirectoryNamesAsync("microsoft", "winget-pkgs", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.ConfigureAwait(false);
	}

	[TestMethod]
	public async Task GatherAsync_PicksTheNewestWingetVersionByPrecedence()
	{
		HaveRepositories(new GitHubRepository("BlastMerge", "main", false));
		_gitHub.ListTreePathsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["BlastMerge.ConsoleApp/BlastMerge.ConsoleApp.csproj"]));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "BlastMerge.ConsoleApp/BlastMerge.ConsoleApp.csproj", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("""<Sdk Name="ktsu.Sdk.ConsoleApp" />"""));
		_gitHub.ListDirectoryNamesAsync("microsoft", "winget-pkgs", "manifests/k/ktsu/BlastMerge", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["1.0.9", "1.0.21", "1.0.19"]));

		RepoFacts facts = (await _service.GatherAsync(Options).ConfigureAwait(false))[0];

		Assert.AreEqual("cli", string.Join(" ", facts.Variants.Select(ShippedVariants.ToLabel)));
		Assert.AreEqual("1.0.21", facts.WingetVersion, "Sorted as text, 1.0.9 would win");
	}

	[TestMethod]
	public async Task GatherAsync_WithShortReadme_MarksItFailing()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "README.md", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("# Alpha"));

		Assert.IsFalse((await _service.GatherAsync(Options).ConfigureAwait(false))[0].ReadmePasses);
	}

	[TestMethod]
	public async Task GatherAsync_WithMissingReadme_MarksItFailing()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "README.md", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>(null));

		Assert.IsFalse((await _service.GatherAsync(Options).ConfigureAwait(false))[0].ReadmePasses);
	}

	[TestMethod]
	public async Task GatherAsync_UsesTheConventionalWorkflowWithoutListingWorkflows()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false));
		_gitHub.GetLatestWorkflowRunAsync(Arg.Any<string>(), "Alpha", "dotnet.yml", "main", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<GitHubWorkflowRun?>(new GitHubWorkflowRun("completed", "success")));

		RepoFacts facts = (await _service.GatherAsync(Options).ConfigureAwait(false))[0];

		Assert.AreEqual("success", facts.WorkflowConclusion);
		Assert.IsTrue(facts.HasWorkflowRun);
		await _gitHub.DidNotReceive().ListActiveWorkflowFileNamesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task GatherAsync_WithNoConventionalWorkflowAndNoFallbacks_ReportsNoStatus()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false));

		RepoFacts facts = (await _service.GatherAsync(Options).ConfigureAwait(false))[0];

		Assert.IsFalse(facts.HasWorkflowRun);
	}

	[TestMethod]
	public async Task GatherAsync_FallsBackToANamedWorkflowWhenAllowed()
	{
		HaveRepositories(new GitHubRepository("VST", "main", false));
		_gitHub.ListActiveWorkflowFileNamesAsync(Arg.Any<string>(), "VST", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["ci.yml"]));
		_gitHub.GetLatestWorkflowRunAsync(Arg.Any<string>(), "VST", "ci.yml", "main", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<GitHubWorkflowRun?>(new GitHubWorkflowRun("completed", "success")));

		RepoFacts facts = (await _service.GatherAsync(Options with { FallbackWorkflowFileNames = ["ci.yml"] }).ConfigureAwait(false))[0];

		Assert.AreEqual("success", facts.WorkflowConclusion);
	}

	[TestMethod]
	public async Task GatherAsync_LooksUpThePackageUnderTheConfiguredPrefix()
	{
		HaveRepositories(new GitHubRepository("Extensions", "main", false));
		_nuGet.GetPackageAsync("ktsu.Extensions", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<NuGetPackageInfo?>(new NuGetPackageInfo("1.6.8", null, 1000, [])));

		RepoFacts facts = (await _service.GatherAsync(Options).ConfigureAwait(false))[0];

		Assert.AreEqual("1.6.8", facts.NuGetStableVersion);
	}

	[TestMethod]
	public async Task GatherAsync_KeepsOrganizationListingOrder()
	{
		HaveRepositories(new GitHubRepository("Charlie", "main", false), new GitHubRepository("Alpha", "main", false), new GitHubRepository("Bravo", "main", false));

		IReadOnlyList<RepoFacts> facts = await _service.GatherAsync(Options).ConfigureAwait(false);

		Assert.AreEqual("Charlie,Alpha,Bravo", string.Join(",", facts.Select(static f => f.Name)));
	}

	[TestMethod]
	public async Task GatherAsync_CombinesVariantsAcrossShippingProjects()
	{
		HaveRepositories(new GitHubRepository("Coder", "main", false));
		_gitHub.ListTreePathsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>([
				"Coder.App/Coder.App.csproj",
				"Coder.ConsoleApp/Coder.ConsoleApp.csproj",
				"Coder.Core/Coder.Core.csproj",
				"Coder.Test/Coder.Test.csproj",
			]));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "Coder.App/Coder.App.csproj", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("""<Sdk Name="ktsu.Sdk" /><Sdk Name="ktsu.Sdk.App" />"""));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "Coder.ConsoleApp/Coder.ConsoleApp.csproj", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("""<Sdk Name="ktsu.Sdk" /><Sdk Name="ktsu.Sdk.ConsoleApp" />"""));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "Coder.Core/Coder.Core.csproj", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("""<Sdk Name="ktsu.Sdk" />"""));

		RepoFacts facts = (await _service.GatherAsync(Options).ConfigureAwait(false))[0];

		Assert.AreEqual("lib cli app", string.Join(" ", facts.Variants.Select(ShippedVariants.ToLabel)));
	}

	[TestMethod]
	public async Task GatherAsync_IgnoresProjectsUnderSupportingDirectories()
	{
		// ImGuiApp keeps demo applications under examples/, and they are not what it ships.
		HaveRepositories(new GitHubRepository("ImGuiApp", "main", false));
		_gitHub.ListTreePathsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>([
				"ImGui.App/ImGui.App.csproj",
				"examples/ImGuiAppDemo/ImGuiAppDemo.csproj",
			]));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "ImGui.App/ImGui.App.csproj", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("""<Sdk Name="ktsu.Sdk" />"""));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "examples/ImGuiAppDemo/ImGuiAppDemo.csproj", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("""<Sdk Name="ktsu.Sdk" /><Sdk Name="ktsu.Sdk.App" />"""));

		RepoFacts facts = (await _service.GatherAsync(Options).ConfigureAwait(false))[0];

		Assert.AreEqual("lib", string.Join(" ", facts.Variants.Select(ShippedVariants.ToLabel)));
	}

	[TestMethod]
	public async Task GatherAsync_ReadsThePinnedSdkVersion()
	{
		HaveRepositories(new GitHubRepository("Extensions", "main", false));
		_gitHub.ListTreePathsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["global.json"]));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "global.json", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("""{"msbuild-sdks":{"ktsu.Sdk":"2.28.0"}}"""));
		_nuGet.GetPackageAsync("ktsu.Sdk", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<NuGetPackageInfo?>(new NuGetPackageInfo("2.28.0", null, 0, [])));

		RepoFacts facts = (await _service.GatherAsync(Options).ConfigureAwait(false))[0];

		Assert.AreEqual("2.28.0", facts.SdkVersion);
		Assert.IsTrue(facts.SdkIsCurrent);
	}

	[TestMethod]
	public async Task GatherAsync_FlagsARepositoryLeftBehindOnAnOlderSdk()
	{
		HaveRepositories(new GitHubRepository("VST", "main", false));
		_gitHub.ListTreePathsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>(["global.json"]));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "global.json", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>("""{"msbuild-sdks":{"ktsu.Sdk":"2.8.0"}}"""));
		_nuGet.GetPackageAsync("ktsu.Sdk", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<NuGetPackageInfo?>(new NuGetPackageInfo("2.28.0", null, 0, [])));

		RepoFacts facts = (await _service.GatherAsync(Options).ConfigureAwait(false))[0];

		Assert.AreEqual("2.8.0", facts.SdkVersion);
		Assert.IsFalse(facts.SdkIsCurrent);
	}

	[TestMethod]
	public async Task GatherAsync_WithNoGlobalJson_SkipsTheRequestEntirely()
	{
		HaveRepositories(new GitHubRepository("Alpha", "main", false));

		RepoFacts facts = (await _service.GatherAsync(Options).ConfigureAwait(false))[0];

		Assert.IsNull(facts.SdkVersion);
		await _gitHub.DidNotReceive()
			.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), "global.json", Arg.Any<CancellationToken>())
			.ConfigureAwait(false);
	}
}
