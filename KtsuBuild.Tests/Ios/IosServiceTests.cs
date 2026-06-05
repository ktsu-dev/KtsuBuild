// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Ios;

using KtsuBuild.Abstractions;
using KtsuBuild.Ios;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;
using NSubstitute;

[TestClass]
public class IosServiceTests
{
	private IDotNetService _dotNetService = null!;
	private IProcessRunner _processRunner = null!;
	private IosService _service = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_dotNetService = Substitute.For<IDotNetService>();
		_processRunner = Substitute.For<IProcessRunner>();
		_service = new IosService(_dotNetService, _processRunner, new MockBuildLogger());
		_tempDir = TestHelpers.CreateTempDir("IosPkgSvc");
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	// PackageAsync — signing gate

	[TestMethod]
	public async Task PackageAsync_SigningUnavailable_SkipsWithoutAnyProcessCalls()
	{
		IosPackageResult result = await _service.PackageAsync(new IosPackageOptions
		{
			WorkingDirectory = _tempDir,
			ShortVersion = "1.0.0",
			SigningAvailable = false,
		}).ConfigureAwait(false);

		Assert.IsTrue(result.Success);
		Assert.IsTrue(result.Skipped);
		Assert.IsNotNull(result.SkipReason);
		Assert.AreEqual(0, result.IpaPaths.Count);

		// The signing material is never touched, and no heads are even enumerated.
		_dotNetService.DidNotReceive().GetIosHeads(Arg.Any<string>());
		await _processRunner.DidNotReceive().RunWithCallbackAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _processRunner.DidNotReceive().RunAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	// UploadAsync — signing gate

	[TestMethod]
	public async Task UploadAsync_SigningUnavailable_SkipsWithoutAnyProcessCalls()
	{
		IosUploadResult result = await _service.UploadAsync(new IosUploadOptions
		{
			WorkingDirectory = _tempDir,
			SigningAvailable = false,
		}).ConfigureAwait(false);

		Assert.IsTrue(result.Success);
		Assert.IsTrue(result.Skipped);
		Assert.IsNotNull(result.SkipReason);
		Assert.AreEqual(0, result.UploadedIpaPaths.Count);

		// The signing material is never touched, and no heads are even enumerated.
		_dotNetService.DidNotReceive().GetIosHeads(Arg.Any<string>());
		await _processRunner.DidNotReceive().RunAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	// UploadIpaAsync — command-string construction and failure detection

	[TestMethod]
	public async Task UploadIpaAsync_BuildsAltoolArgs_OnSuccess()
	{
		_processRunner.RunAsync("xcrun", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("No errors uploading."));

		await _service.UploadIpaAsync("/path/to/MyApp.ipa", "KEYID123", "issuer-uuid").ConfigureAwait(false);

		await _processRunner.Received(1).RunAsync(
			"xcrun",
			Arg.Is<string>(a =>
				a.Contains("altool --upload-app") &&
				a.Contains("--type ios") &&
				a.Contains("--file \"/path/to/MyApp.ipa\"") &&
				a.Contains("--apiKey \"KEYID123\"") &&
				a.Contains("--apiIssuer \"issuer-uuid\"")),
			Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task UploadIpaAsync_NonZeroExit_Throws()
	{
		_processRunner.RunAsync("xcrun", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.UploadIpaAsync("/path/to/MyApp.ipa", "KEYID123", "issuer-uuid")).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task UploadIpaAsync_FailureStringWithZeroExit_Throws()
	{
		// altool's exit code is unreliable: a zero exit with a reported failure must still fail.
		_processRunner.RunAsync("xcrun", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("*** Error: UPLOAD FAILED with errors."));

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.UploadIpaAsync("/path/to/MyApp.ipa", "KEYID123", "issuer-uuid")).ConfigureAwait(false);
	}

	[TestMethod]
	public void AltoolReportedFailure_DetectsFailureStrings()
	{
		Assert.IsTrue(IosService.AltoolReportedFailure("*** Error: UPLOAD FAILED with errors."));
		Assert.IsTrue(IosService.AltoolReportedFailure("Failed to upload package to App Store Connect."));
		Assert.IsFalse(IosService.AltoolReportedFailure("No errors uploading 'MyApp.ipa'."));
		Assert.IsFalse(IosService.AltoolReportedFailure(string.Empty));
	}

	// LocateIpas

	[TestMethod]
	public void LocateIpas_ExplicitPath_ReturnedWhenPresent()
	{
		string ipa = Path.Combine(_tempDir, "Explicit.ipa");
		File.WriteAllText(ipa, "archive");

		IReadOnlyList<string> located = _service.LocateIpas(new IosUploadOptions
		{
			WorkingDirectory = _tempDir,
			IpaPath = ipa,
		});

		Assert.AreEqual(1, located.Count);
		Assert.AreEqual(ipa, located[0]);
		_dotNetService.DidNotReceive().GetIosHeads(Arg.Any<string>());
	}

	[TestMethod]
	public void LocateIpas_ExplicitPathMissing_Throws()
	{
		Assert.ThrowsExactly<InvalidOperationException>(() => _service.LocateIpas(new IosUploadOptions
		{
			WorkingDirectory = _tempDir,
			IpaPath = Path.Combine(_tempDir, "does-not-exist.ipa"),
		}));
	}

	[TestMethod]
	public void LocateIpas_FindsArchiveUnderHeadBin()
	{
		string head = CreateHead("MyApp.iOS");
		string ipa = Path.Combine(_tempDir, "MyApp.iOS", "bin", "Release", "net10.0-ios", "ios-arm64", "publish", "MyApp.ipa");
		Directory.CreateDirectory(Path.GetDirectoryName(ipa)!);
		File.WriteAllText(ipa, "archive");

		_dotNetService.GetIosHeads(_tempDir).Returns([head]);

		IReadOnlyList<string> located = _service.LocateIpas(new IosUploadOptions
		{
			WorkingDirectory = _tempDir,
		});

		Assert.AreEqual(1, located.Count);
		Assert.AreEqual(ipa, located[0]);
	}

	// ArchiveAsync — command-string construction

	[TestMethod]
	public async Task ArchiveAsync_BuildsSignedPublishArgs_AndReturnsIpa()
	{
		string head = CreateHead("MyApp.iOS");
		string ipa = Path.Combine(_tempDir, "MyApp.iOS", "bin", "Release", "net10.0-ios", "ios-arm64", "publish", "MyApp.ipa");
		Directory.CreateDirectory(Path.GetDirectoryName(ipa)!);
		await File.WriteAllTextAsync(ipa, "archive").ConfigureAwait(false);

		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		string produced = await _service.ArchiveAsync(_tempDir, head, "ios-arm64", "Release", "net10.0-ios", "Apple Distribution: ktsu", "ktsu profile").ConfigureAwait(false);

		Assert.AreEqual(ipa, produced);
		await _processRunner.Received(1).RunWithCallbackAsync(
			"dotnet",
			Arg.Is<string>(a =>
				a.Contains("publish") &&
				a.Contains("--configuration Release") &&
				a.Contains("-f net10.0-ios") &&
				a.Contains("-p:RuntimeIdentifier=ios-arm64") &&
				a.Contains("-p:ArchiveOnBuild=true") &&
				a.Contains("-p:BuildIpa=true") &&
				a.Contains("-p:CodesignKey=\"Apple Distribution: ktsu\"") &&
				a.Contains("-p:CodesignProvision=\"ktsu profile\"")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ArchiveAsync_OmitsFrameworkFlag_WhenFrameworkNull()
	{
		string head = CreateHead("MyApp.iOS");
		string ipa = Path.Combine(_tempDir, "MyApp.iOS", "bin", "MyApp.ipa");
		Directory.CreateDirectory(Path.GetDirectoryName(ipa)!);
		await File.WriteAllTextAsync(ipa, "archive").ConfigureAwait(false);

		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.ArchiveAsync(_tempDir, head, "ios-arm64", "Release", framework: null, "key", "profile").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync(
			"dotnet",
			Arg.Is<string>(a => !a.Contains(" -f ")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ArchiveAsync_NonZeroExit_Throws()
	{
		string head = CreateHead("MyApp.iOS");
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.ArchiveAsync(_tempDir, head, "ios-arm64", "Release", null, "key", "profile")).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ArchiveAsync_NoIpaProduced_Throws()
	{
		string head = CreateHead("MyApp.iOS");
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.ArchiveAsync(_tempDir, head, "ios-arm64", "Release", null, "key", "profile")).ConfigureAwait(false);
	}

	// StampVersionAsync

	[TestMethod]
	public async Task StampVersionAsync_SetsBothKeys_WhenPresent()
	{
		string plist = Path.Combine(_tempDir, "Info.plist");
		await File.WriteAllTextAsync(plist, "<plist/>").ConfigureAwait(false);

		// Set succeeds, so the Add fallback is never reached.
		_processRunner.RunAsync("/usr/libexec/PlistBuddy", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult());

		await _service.StampVersionAsync(plist, "1.2.3", "42").ConfigureAwait(false);

		await _processRunner.Received(1).RunAsync(
			"/usr/libexec/PlistBuddy",
			Arg.Is<string>(a => a.Contains("Set :CFBundleShortVersionString 1.2.3")),
			Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _processRunner.Received(1).RunAsync(
			"/usr/libexec/PlistBuddy",
			Arg.Is<string>(a => a.Contains("Set :CFBundleVersion 42")),
			Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);

		// Both keys exist, so no Add is issued.
		await _processRunner.DidNotReceive().RunWithCallbackAsync(
			"/usr/libexec/PlistBuddy", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task StampVersionAsync_AddsKey_WhenSetFails()
	{
		string plist = Path.Combine(_tempDir, "Info.plist");
		await File.WriteAllTextAsync(plist, "<plist/>").ConfigureAwait(false);

		// Set fails ("Does Not Exist"), so the service falls back to Add.
		_processRunner.RunAsync("/usr/libexec/PlistBuddy", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());
		_processRunner.RunWithCallbackAsync("/usr/libexec/PlistBuddy", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.StampVersionAsync(plist, "1.2.3", "42").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync(
			"/usr/libexec/PlistBuddy",
			Arg.Is<string>(a => a.Contains("Add :CFBundleShortVersionString string 1.2.3")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _processRunner.Received(1).RunWithCallbackAsync(
			"/usr/libexec/PlistBuddy",
			Arg.Is<string>(a => a.Contains("Add :CFBundleVersion string 42")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	// ProvisionToolchainAsync

	[TestMethod]
	public async Task ProvisionToolchainAsync_EmptyPins_MakesNoProcessCalls()
	{
		await _service.ProvisionToolchainAsync(string.Empty, string.Empty).ConfigureAwait(false);

		await _processRunner.DidNotReceive().RunWithCallbackAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _processRunner.DidNotReceive().RunAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task ProvisionToolchainAsync_WorkloadPin_InstallsViaRollbackFile()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		// No Xcode pin, so only the workload install runs (Xcode selection touches /Applications).
		await _service.ProvisionToolchainAsync(string.Empty, "26.2.10233/10.0.100").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync(
			"dotnet",
			Arg.Is<string>(a => a.Contains("workload install ios --from-rollback-file")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	// SetupSigningAsync — empty certificate

	[TestMethod]
	public async Task SetupSigningAsync_EmptyCertificate_Throws()
	{
		// The keychain steps succeed (default mock), then the empty cert fails the decode.
		_processRunner.RunWithCallbackAsync("security", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.SetupSigningAsync(new IosPackageOptions
			{
				WorkingDirectory = _tempDir,
				ShortVersion = "1.0.0",
				CertificateP12Base64 = string.Empty,
			})).ConfigureAwait(false);
	}

	// Static helpers

	[TestMethod]
	public void ResolveIosFramework_ReturnsIosTfm()
	{
		string head = Path.Combine(_tempDir, "MyApp.iOS.csproj");
		File.WriteAllText(head, "<Project><PropertyGroup><TargetFramework>net10.0-ios</TargetFramework></PropertyGroup></Project>");

		Assert.AreEqual("net10.0-ios", IosService.ResolveIosFramework(head));
	}

	[TestMethod]
	public void ResolveIosFramework_MultiTarget_PicksIosTfm()
	{
		string head = Path.Combine(_tempDir, "MyApp.csproj");
		File.WriteAllText(head, "<Project><PropertyGroup><TargetFrameworks>net10.0;net10.0-ios</TargetFrameworks></PropertyGroup></Project>");

		Assert.AreEqual("net10.0-ios", IosService.ResolveIosFramework(head));
	}

	[TestMethod]
	public void ResolveIosFramework_NoIosTfm_ReturnsNull()
	{
		string head = Path.Combine(_tempDir, "MyLib.csproj");
		File.WriteAllText(head, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		Assert.IsNull(IosService.ResolveIosFramework(head));
	}

	[TestMethod]
	public void ResolveInfoPlist_FindsPlistNextToProject()
	{
		string headDir = Path.Combine(_tempDir, "MyApp.iOS");
		Directory.CreateDirectory(headDir);
		string head = Path.Combine(headDir, "MyApp.iOS.csproj");
		File.WriteAllText(head, "<Project />");
		string plist = Path.Combine(headDir, "Info.plist");
		File.WriteAllText(plist, "<plist/>");

		Assert.AreEqual(plist, IosService.ResolveInfoPlist(head));
	}

	[TestMethod]
	public void ResolveInfoPlist_NoPlist_ReturnsNull()
	{
		string headDir = Path.Combine(_tempDir, "MyApp.iOS");
		Directory.CreateDirectory(headDir);
		string head = Path.Combine(headDir, "MyApp.iOS.csproj");
		File.WriteAllText(head, "<Project />");

		Assert.IsNull(IosService.ResolveInfoPlist(head));
	}

	[TestMethod]
	public void FindIpa_LocatesArchive()
	{
		string binDir = Path.Combine(_tempDir, "bin", "Release");
		Directory.CreateDirectory(binDir);
		string ipa = Path.Combine(binDir, "MyApp.ipa");
		File.WriteAllText(ipa, "archive");

		Assert.AreEqual(ipa, IosService.FindIpa(Path.Combine(_tempDir, "bin")));
	}

	[TestMethod]
	public void FindIpa_MissingRoot_ReturnsNull() =>
		Assert.IsNull(IosService.FindIpa(Path.Combine(_tempDir, "does-not-exist")));

	// Creates an iOS head project file and returns its path.
	private string CreateHead(string name)
	{
		string headDir = Path.Combine(_tempDir, name);
		Directory.CreateDirectory(headDir);
		string head = Path.Combine(headDir, $"{name}.csproj");
		File.WriteAllText(head, "<Project><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-ios</TargetFramework></PropertyGroup></Project>");
		return head;
	}
}
