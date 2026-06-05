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
public class IosBuildServiceTests
{
	private IDotNetService _dotNetService = null!;
	private IosBuildService _service = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_dotNetService = Substitute.For<IDotNetService>();
		_service = new IosBuildService(_dotNetService, new MockBuildLogger());
		_tempDir = TestHelpers.CreateTempDir("IosBuildSvc");
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	// IsDeviceRuntime

	[TestMethod]
	public void IsDeviceRuntime_DeviceRid_ReturnsTrue() =>
		Assert.IsTrue(IosBuildService.IsDeviceRuntime("ios-arm64"));

	[TestMethod]
	public void IsDeviceRuntime_SimulatorRid_ReturnsFalse() =>
		Assert.IsFalse(IosBuildService.IsDeviceRuntime("iossimulator-arm64"));

	// ClassifyForCi

	[TestMethod]
	public void ClassifyForCi_NoHeads_ReturnsNoHeads() =>
		Assert.AreEqual(IosCiDisposition.NoHeads, IosBuildService.ClassifyForCi(0, hostIsMacOs: true));

	[TestMethod]
	public void ClassifyForCi_NoHeads_ReturnsNoHeadsRegardlessOfHost() =>
		Assert.AreEqual(IosCiDisposition.NoHeads, IosBuildService.ClassifyForCi(0, hostIsMacOs: false));

	[TestMethod]
	public void ClassifyForCi_HeadsOnMacOs_ReturnsBuild() =>
		Assert.AreEqual(IosCiDisposition.Build, IosBuildService.ClassifyForCi(1, hostIsMacOs: true));

	[TestMethod]
	public void ClassifyForCi_HeadsOnNonMacOs_ReturnsSkipNotMacOs() =>
		Assert.AreEqual(IosCiDisposition.SkipNotMacOs, IosBuildService.ClassifyForCi(2, hostIsMacOs: false));

	// BuildAsync — head resolution

	[TestMethod]
	public async Task BuildAsync_NoHeads_ReturnsTrueWithoutBuilding()
	{
		_dotNetService.GetIosHeads(_tempDir).Returns([]);

		bool result = await _service.BuildAsync(new IosBuildOptions { WorkingDirectory = _tempDir }).ConfigureAwait(false);

		Assert.IsTrue(result);
		await _dotNetService.DidNotReceive().BuildIosAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task BuildAsync_AutoDetectsHeads_WhenProjectNotSpecified()
	{
		string head = CreateHeadWithDeviceBundle("MyApp.iOS", embedFramework: null);
		_dotNetService.GetIosHeads(_tempDir).Returns([head]);

		await _service.BuildAsync(new IosBuildOptions { WorkingDirectory = _tempDir }).ConfigureAwait(false);

		_dotNetService.Received(1).GetIosHeads(_tempDir);
	}

	[TestMethod]
	public async Task BuildAsync_UsesExplicitProject_WithoutAutoDetecting()
	{
		string head = CreateHeadWithDeviceBundle("MyApp.iOS", embedFramework: null);

		await _service.BuildAsync(new IosBuildOptions { WorkingDirectory = _tempDir, Project = head }).ConfigureAwait(false);

		_dotNetService.DidNotReceive().GetIosHeads(Arg.Any<string>());
	}

	// BuildAsync — runtime selection

	[TestMethod]
	public async Task BuildAsync_DefaultRuntimes_BuildsSimulatorAndDevice()
	{
		string head = CreateHeadWithDeviceBundle("MyApp.iOS", embedFramework: null);
		_dotNetService.GetIosHeads(_tempDir).Returns([head]);

		await _service.BuildAsync(new IosBuildOptions { WorkingDirectory = _tempDir }).ConfigureAwait(false);

		await _dotNetService.Received(1).BuildIosAsync(_tempDir, head, IosBuildService.SimulatorRuntime, "Release", false, Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _dotNetService.Received(1).BuildIosAsync(_tempDir, head, IosBuildService.DeviceRuntime, "Release", false, Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task BuildAsync_ExplicitSimulatorRuntime_SkipsDeviceAndVerification()
	{
		string head = Path.Combine(_tempDir, "MyApp.iOS", "MyApp.iOS.csproj");
		Directory.CreateDirectory(Path.GetDirectoryName(head)!);
		await File.WriteAllTextAsync(head, "<Project />").ConfigureAwait(false);

		bool result = await _service.BuildAsync(new IosBuildOptions
		{
			WorkingDirectory = _tempDir,
			Project = head,
			Runtime = IosBuildService.SimulatorRuntime,
		}).ConfigureAwait(false);

		// Simulator-only: no device build, and no missing-bundle failure.
		Assert.IsTrue(result);
		await _dotNetService.Received(1).BuildIosAsync(_tempDir, head, IosBuildService.SimulatorRuntime, "Release", false, Arg.Any<CancellationToken>()).ConfigureAwait(false);
		await _dotNetService.DidNotReceive().BuildIosAsync(_tempDir, head, IosBuildService.DeviceRuntime, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task BuildAsync_PassesConfiguration()
	{
		string head = CreateHeadWithDeviceBundle("MyApp.iOS", embedFramework: null, configuration: "Debug");
		_dotNetService.GetIosHeads(_tempDir).Returns([head]);

		await _service.BuildAsync(new IosBuildOptions { WorkingDirectory = _tempDir, Configuration = "Debug" }).ConfigureAwait(false);

		await _dotNetService.Received(1).BuildIosAsync(_tempDir, head, IosBuildService.DeviceRuntime, "Debug", false, Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	// BuildAsync — device bundle verification

	[TestMethod]
	public async Task BuildAsync_DeviceBundleMissing_ReturnsFalse()
	{
		// Head exists but no .app bundle is produced under bin/Release.
		string head = Path.Combine(_tempDir, "MyApp.iOS", "MyApp.iOS.csproj");
		Directory.CreateDirectory(Path.GetDirectoryName(head)!);
		await File.WriteAllTextAsync(head, "<Project />").ConfigureAwait(false);
		_dotNetService.GetIosHeads(_tempDir).Returns([head]);

		bool result = await _service.BuildAsync(new IosBuildOptions { WorkingDirectory = _tempDir }).ConfigureAwait(false);

		Assert.IsFalse(result);
	}

	[TestMethod]
	public async Task BuildAsync_RequiredFrameworkEmbedded_ReturnsTrue()
	{
		string head = CreateHeadWithDeviceBundle("MyApp.iOS", embedFramework: "libSkiaSharp");
		_dotNetService.GetIosHeads(_tempDir).Returns([head]);

		bool result = await _service.BuildAsync(new IosBuildOptions
		{
			WorkingDirectory = _tempDir,
			RequiredFrameworks = ["libSkiaSharp"],
		}).ConfigureAwait(false);

		Assert.IsTrue(result);
	}

	[TestMethod]
	public async Task BuildAsync_RequiredFrameworkMissing_ReturnsFalse()
	{
		string head = CreateHeadWithDeviceBundle("MyApp.iOS", embedFramework: null);
		_dotNetService.GetIosHeads(_tempDir).Returns([head]);

		bool result = await _service.BuildAsync(new IosBuildOptions
		{
			WorkingDirectory = _tempDir,
			RequiredFrameworks = ["libSkiaSharp"],
		}).ConfigureAwait(false);

		Assert.IsFalse(result);
	}

	// VerifyDeviceBundle (direct)

	[TestMethod]
	public void VerifyDeviceBundle_BundlePresentNoRequirements_ReturnsTrue()
	{
		string head = CreateHeadWithDeviceBundle("MyApp.iOS", embedFramework: null);

		Assert.IsTrue(_service.VerifyDeviceBundle(head, "Release", IosBuildService.DeviceRuntime, []));
	}

	[TestMethod]
	public void VerifyDeviceBundle_NoBundle_ReturnsFalse()
	{
		string head = Path.Combine(_tempDir, "MyApp.iOS", "MyApp.iOS.csproj");
		Directory.CreateDirectory(Path.GetDirectoryName(head)!);
		File.WriteAllText(head, "<Project />");

		Assert.IsFalse(_service.VerifyDeviceBundle(head, "Release", IosBuildService.DeviceRuntime, []));
	}

	// Creates a head project plus a device .app bundle under bin/{configuration}, optionally
	// embedding a named native framework binary. Returns the head .csproj path.
	private string CreateHeadWithDeviceBundle(string name, string? embedFramework, string configuration = "Release")
	{
		string headDir = Path.Combine(_tempDir, name);
		Directory.CreateDirectory(headDir);
		string head = Path.Combine(headDir, $"{name}.csproj");
		File.WriteAllText(head, "<Project><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-ios</TargetFramework></PropertyGroup></Project>");

		string bundle = Path.Combine(headDir, "bin", configuration, "net10.0-ios", IosBuildService.DeviceRuntime, $"{name}.app");
		Directory.CreateDirectory(bundle);

		if (!string.IsNullOrEmpty(embedFramework))
		{
			string frameworkDir = Path.Combine(bundle, "Frameworks", $"{embedFramework}.framework");
			Directory.CreateDirectory(frameworkDir);
			File.WriteAllText(Path.Combine(frameworkDir, embedFramework), "native");
		}

		return head;
	}
}
