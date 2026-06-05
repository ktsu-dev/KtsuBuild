// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.DotNet;

using System.Runtime.InteropServices;
using KtsuBuild.Abstractions;
using KtsuBuild.DotNet;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;
using NSubstitute;

[TestClass]
public class DotNetServiceTests
{
	private IProcessRunner _processRunner = null!;
	private DotNetService _service = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_processRunner = Substitute.For<IProcessRunner>();
		_service = new DotNetService(_processRunner, new MockBuildLogger());
		_tempDir = TestHelpers.CreateTempDir("DotNetSvc");
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	// RestoreAsync

	[TestMethod]
	public async Task RestoreAsync_Success_Completes()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.RestoreAsync(_tempDir).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("restore")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task RestoreAsync_LockedMode_IncludesLockedModeFlag()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.RestoreAsync(_tempDir, lockedMode: true).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("--locked-mode")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task RestoreAsync_NotLockedMode_OmitsLockedModeFlag()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.RestoreAsync(_tempDir, lockedMode: false).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => !a.Contains("--locked-mode")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task RestoreAsync_Failure_ThrowsInvalidOperationException()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.RestoreAsync(_tempDir)).ConfigureAwait(false);
	}

	// BuildAsync

	[TestMethod]
	public async Task BuildAsync_Success_Completes()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.BuildAsync(_tempDir).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("build") && a.Contains("--configuration Release")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task BuildAsync_AdditionalArgs_AppendsToCommand()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.BuildAsync(_tempDir, additionalArgs: "-maxCpuCount:1").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("-maxCpuCount:1")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task BuildAsync_FirstFailRetrySucceeds_Completes()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1, 0);

		await _service.BuildAsync(_tempDir).ConfigureAwait(false);

		// Should have been called twice (first attempt + retry)
		await _processRunner.Received(2).RunWithCallbackAsync("dotnet",
			Arg.Any<string>(),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task BuildAsync_BothAttemptsFail_ThrowsInvalidOperationException()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.BuildAsync(_tempDir)).ConfigureAwait(false);
	}

	// TestAsync

	[TestMethod]
	public async Task TestAsync_NoTestProjects_SkipsExecution()
	{
		// No .csproj files in tempDir, so no test projects to find
		await _service.TestAsync(_tempDir).ConfigureAwait(false);

		// RunWithCallbackAsync should not be called for dotnet test
		await _processRunner.DidNotReceive().RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("test")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task TestAsync_WithTestProjects_RunsDotnetTest()
	{
		// Create a test project file
		string projDir = Path.Combine(_tempDir, "MyProject.Tests");
		Directory.CreateDirectory(projDir);
		await File.WriteAllTextAsync(Path.Combine(projDir, "MyProject.Tests.csproj"),
			"<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>").ConfigureAwait(false);

		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.TestAsync(_tempDir).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("test")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task TestAsync_Failure_ThrowsInvalidOperationException()
	{
		string projDir = Path.Combine(_tempDir, "MyProject.Tests");
		Directory.CreateDirectory(projDir);
		await File.WriteAllTextAsync(Path.Combine(projDir, "MyProject.Tests.csproj"),
			"<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>").ConfigureAwait(false);

		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.TestAsync(_tempDir)).ConfigureAwait(false);
	}

	// PackAsync

	[TestMethod]
	public async Task PackAsync_NoProjects_SkipsExecution()
	{
		string outputPath = Path.Combine(_tempDir, "output");

		await _service.PackAsync(_tempDir, outputPath).ConfigureAwait(false);

		await _processRunner.DidNotReceive().RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("pack")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PackAsync_ExcludesTestProjects_FromPacking()
	{
		// Create a library project and a test project
		string libDir = Path.Combine(_tempDir, "MyLib");
		Directory.CreateDirectory(libDir);
		await File.WriteAllTextAsync(Path.Combine(libDir, "MyLib.csproj"),
			"<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>").ConfigureAwait(false);

		string testDir = Path.Combine(_tempDir, "MyLib.Tests");
		Directory.CreateDirectory(testDir);
		await File.WriteAllTextAsync(Path.Combine(testDir, "MyLib.Tests.csproj"),
			"<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>").ConfigureAwait(false);

		string outputPath = Path.Combine(_tempDir, "output");
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		await _service.PackAsync(_tempDir, outputPath).ConfigureAwait(false);

		// Should only pack the library, not the test project
		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("pack") && a.Contains("MyLib.csproj") && !a.Contains("Tests")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PackAsync_PackFailure_LogsWarningButContinues()
	{
		string libDir = Path.Combine(_tempDir, "MyLib");
		Directory.CreateDirectory(libDir);
		await File.WriteAllTextAsync(Path.Combine(libDir, "MyLib.csproj"),
			"<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>").ConfigureAwait(false);

		string outputPath = Path.Combine(_tempDir, "output");
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		// Should NOT throw - pack failures are logged as warnings
		await _service.PackAsync(_tempDir, outputPath).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PackAsync_CreatesOutputDirectory()
	{
		string outputPath = Path.Combine(_tempDir, "staging", "packages");

		await _service.PackAsync(_tempDir, outputPath).ConfigureAwait(false);

		Assert.IsTrue(Directory.Exists(outputPath), "Output directory should be created");
	}

	// PublishAsync

	[TestMethod]
	public async Task PublishAsync_Success_Completes()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		string outputPath = Path.Combine(_tempDir, "publish");
		await _service.PublishAsync(_tempDir, "project.csproj", outputPath, "win-x64").ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("publish") && a.Contains("--runtime win-x64")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishAsync_SelfContained_IncludesSelfContainedFlag()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		string outputPath = Path.Combine(_tempDir, "publish");
		await _service.PublishAsync(_tempDir, "project.csproj", outputPath, "win-x64", selfContained: true).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("--self-contained true")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishAsync_NotSelfContained_IncludesNotSelfContainedFlag()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		string outputPath = Path.Combine(_tempDir, "publish");
		await _service.PublishAsync(_tempDir, "project.csproj", outputPath, "win-x64", selfContained: false).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("--self-contained false")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishAsync_SingleFile_IncludesSingleFileFlag()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		string outputPath = Path.Combine(_tempDir, "publish");
		await _service.PublishAsync(_tempDir, "project.csproj", outputPath, "win-x64", singleFile: true).ConfigureAwait(false);

		await _processRunner.Received(1).RunWithCallbackAsync("dotnet",
			Arg.Is<string>(a => a.Contains("PublishSingleFile=true")),
			Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishAsync_Failure_ThrowsInvalidOperationException()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(1);

		string outputPath = Path.Combine(_tempDir, "publish");
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => _service.PublishAsync(_tempDir, "project.csproj", outputPath, "win-x64")).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task PublishAsync_CreatesOutputDirectory()
	{
		_processRunner.RunWithCallbackAsync("dotnet", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
			.Returns(0);

		string outputPath = Path.Combine(_tempDir, "deep", "publish", "dir");
		await _service.PublishAsync(_tempDir, "project.csproj", outputPath, "win-x64").ConfigureAwait(false);

		Assert.IsTrue(Directory.Exists(outputPath));
	}

	// GetProjectFiles

	[TestMethod]
	public void GetProjectFiles_FindsCsprojFiles()
	{
		string dir = Path.Combine(_tempDir, "MyProject");
		Directory.CreateDirectory(dir);
		File.WriteAllText(Path.Combine(dir, "MyProject.csproj"), "<Project />");

		IReadOnlyList<string> files = _service.GetProjectFiles(_tempDir);

		Assert.AreEqual(1, files.Count);
		Assert.IsTrue(files[0].EndsWith("MyProject.csproj", StringComparison.Ordinal));
	}

	[TestMethod]
	public void GetProjectFiles_ReturnsEmptyForNoProjects()
	{
		IReadOnlyList<string> files = _service.GetProjectFiles(_tempDir);

		Assert.AreEqual(0, files.Count);
	}

	// IsExecutableProject

	[TestMethod]
	public void IsExecutableProject_OutputTypeExe_ReturnsTrue()
	{
		string projPath = Path.Combine(_tempDir, "App.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>");

		Assert.IsTrue(_service.IsExecutableProject(projPath));
	}

	[TestMethod]
	public void IsExecutableProject_OutputTypeWinExe_ReturnsTrue()
	{
		string projPath = Path.Combine(_tempDir, "App.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><OutputType>WinExe</OutputType></PropertyGroup></Project>");

		Assert.IsTrue(_service.IsExecutableProject(projPath));
	}

	[TestMethod]
	public void IsExecutableProject_SdkApp_ReturnsTrue()
	{
		string projPath = Path.Combine(_tempDir, "App.csproj");
		File.WriteAllText(projPath, "<Project Sdk=\"ktsu.Sdk.App/1.0.0\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		Assert.IsTrue(_service.IsExecutableProject(projPath));
	}

	[TestMethod]
	public void IsExecutableProject_LibraryProject_ReturnsFalse()
	{
		string projPath = Path.Combine(_tempDir, "Lib.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		Assert.IsFalse(_service.IsExecutableProject(projPath));
	}

	[TestMethod]
	public void IsExecutableProject_FileNotFound_ReturnsFalse() =>
		Assert.IsFalse(_service.IsExecutableProject(Path.Combine(_tempDir, "nonexistent.csproj")));

	[TestMethod]
	public void IsExecutableProject_CaseInsensitive_ReturnsTrue()
	{
		string projPath = Path.Combine(_tempDir, "App.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><OutputType>exe</OutputType></PropertyGroup></Project>");

		Assert.IsTrue(_service.IsExecutableProject(projPath));
	}

	// IsTestProject

	[TestMethod]
	public void IsTestProject_EndingWithTest_ReturnsTrue()
	{
		string dir = Path.Combine(_tempDir, "MyProject.Test");
		Directory.CreateDirectory(dir);
		string projPath = Path.Combine(dir, "MyProject.Test.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		Assert.IsTrue(_service.IsTestProject(projPath));
	}

	[TestMethod]
	public void IsTestProject_EndingWithTests_ReturnsTrue()
	{
		string dir = Path.Combine(_tempDir, "MyProject.Tests");
		Directory.CreateDirectory(dir);
		string projPath = Path.Combine(dir, "MyProject.Tests.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		Assert.IsTrue(_service.IsTestProject(projPath));
	}

	[TestMethod]
	public void IsTestProject_TestSdk_ReturnsTrue()
	{
		string dir = Path.Combine(_tempDir, "MyProj");
		Directory.CreateDirectory(dir);
		string projPath = Path.Combine(dir, "MyProj.csproj");
		File.WriteAllText(projPath, "<Project Sdk=\"Microsoft.NET.Sdk.Test\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		Assert.IsTrue(_service.IsTestProject(projPath));
	}

	[TestMethod]
	public void IsTestProject_IsTestProjectElement_ReturnsTrue()
	{
		string dir = Path.Combine(_tempDir, "MyProj");
		Directory.CreateDirectory(dir);
		string projPath = Path.Combine(dir, "MyProj.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>");

		Assert.IsTrue(_service.IsTestProject(projPath));
	}

	[TestMethod]
	public void IsTestProject_RegularProject_ReturnsFalse()
	{
		string dir = Path.Combine(_tempDir, "MyLib");
		Directory.CreateDirectory(dir);
		string projPath = Path.Combine(dir, "MyLib.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		Assert.IsFalse(_service.IsTestProject(projPath));
	}

	[TestMethod]
	public void IsTestProject_FileNotFound_ReturnsFalse() =>
		Assert.IsFalse(_service.IsTestProject(Path.Combine(_tempDir, "nonexistent.csproj")));

	// IsExecutableProject - iOS

	[TestMethod]
	public void IsExecutableProject_SdkIos_ReturnsTrue()
	{
		string projPath = Path.Combine(_tempDir, "App.csproj");
		File.WriteAllText(projPath, "<Project Sdk=\"ktsu.Sdk.Ios/1.0.0\"><PropertyGroup><TargetFramework>net10.0-ios</TargetFramework></PropertyGroup></Project>");

		Assert.IsTrue(_service.IsExecutableProject(projPath));
	}

	[TestMethod]
	public void IsExecutableProject_IosHeadWithOutputTypeExe_ReturnsTrue()
	{
		string projPath = Path.Combine(_tempDir, "App.iOS.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-ios</TargetFramework></PropertyGroup></Project>");

		Assert.IsTrue(_service.IsExecutableProject(projPath));
	}

	// ClassifyTargetFrameworks

	[TestMethod]
	public void ClassifyTargetFrameworks_Neutral_ReturnsNeutral() =>
		Assert.AreEqual(ProjectPlatform.Neutral, DotNetService.ClassifyTargetFrameworks(["net10.0"]));

	[TestMethod]
	public void ClassifyTargetFrameworks_NetStandard_ReturnsNeutral() =>
		Assert.AreEqual(ProjectPlatform.Neutral, DotNetService.ClassifyTargetFrameworks(["netstandard2.0"]));

	[TestMethod]
	public void ClassifyTargetFrameworks_IosOnly_ReturnsIos() =>
		Assert.AreEqual(ProjectPlatform.Ios, DotNetService.ClassifyTargetFrameworks(["net10.0-ios"]));

	[TestMethod]
	public void ClassifyTargetFrameworks_IosWithVersion_ReturnsIos() =>
		Assert.AreEqual(ProjectPlatform.Ios, DotNetService.ClassifyTargetFrameworks(["net10.0-ios17.0"]));

	[TestMethod]
	public void ClassifyTargetFrameworks_WindowsOnly_ReturnsWindows() =>
		Assert.AreEqual(ProjectPlatform.Windows, DotNetService.ClassifyTargetFrameworks(["net10.0-windows10.0.19041.0"]));

	[TestMethod]
	public void ClassifyTargetFrameworks_NeutralAndIos_ReturnsNeutral() =>
		Assert.AreEqual(ProjectPlatform.Neutral, DotNetService.ClassifyTargetFrameworks(["net10.0", "net10.0-ios"]));

	[TestMethod]
	public void ClassifyTargetFrameworks_IosAndWindows_ReturnsNeutral() =>
		Assert.AreEqual(ProjectPlatform.Neutral, DotNetService.ClassifyTargetFrameworks(["net10.0-ios", "net10.0-windows10.0.19041.0"]));

	[TestMethod]
	public void ClassifyTargetFrameworks_Empty_ReturnsNeutral() =>
		Assert.AreEqual(ProjectPlatform.Neutral, DotNetService.ClassifyTargetFrameworks([]));

	// GetProjectPlatform

	[TestMethod]
	public void GetProjectPlatform_NeutralProject_ReturnsNeutral()
	{
		string projPath = Path.Combine(_tempDir, "Lib.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		Assert.AreEqual(ProjectPlatform.Neutral, _service.GetProjectPlatform(projPath));
	}

	[TestMethod]
	public void GetProjectPlatform_IosProject_ReturnsIos()
	{
		string projPath = Path.Combine(_tempDir, "App.iOS.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><TargetFramework>net10.0-ios</TargetFramework></PropertyGroup></Project>");

		Assert.AreEqual(ProjectPlatform.Ios, _service.GetProjectPlatform(projPath));
	}

	[TestMethod]
	public void GetProjectPlatform_WindowsProject_ReturnsWindows()
	{
		string projPath = Path.Combine(_tempDir, "App.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><TargetFramework>net10.0-windows10.0.19041.0</TargetFramework></PropertyGroup></Project>");

		Assert.AreEqual(ProjectPlatform.Windows, _service.GetProjectPlatform(projPath));
	}

	[TestMethod]
	public void GetProjectPlatform_MultiTargetWithNeutral_ReturnsNeutral()
	{
		string projPath = Path.Combine(_tempDir, "Lib.csproj");
		File.WriteAllText(projPath, "<Project><PropertyGroup><TargetFrameworks>net10.0;net10.0-ios</TargetFrameworks></PropertyGroup></Project>");

		Assert.AreEqual(ProjectPlatform.Neutral, _service.GetProjectPlatform(projPath));
	}

	[TestMethod]
	public void GetProjectPlatform_FileNotFound_ReturnsNeutral() =>
		Assert.AreEqual(ProjectPlatform.Neutral, _service.GetProjectPlatform(Path.Combine(_tempDir, "nonexistent.csproj")));

	// CanPlatformBuildOnHost

	[TestMethod]
	public void CanPlatformBuildOnHost_Neutral_AlwaysTrue()
	{
		Assert.IsTrue(DotNetService.CanPlatformBuildOnHost(ProjectPlatform.Neutral, hostIsWindows: true, hostIsMacOs: false));
		Assert.IsTrue(DotNetService.CanPlatformBuildOnHost(ProjectPlatform.Neutral, hostIsWindows: false, hostIsMacOs: true));
		Assert.IsTrue(DotNetService.CanPlatformBuildOnHost(ProjectPlatform.Neutral, hostIsWindows: false, hostIsMacOs: false));
	}

	[TestMethod]
	public void CanPlatformBuildOnHost_Windows_OnlyOnWindows()
	{
		Assert.IsTrue(DotNetService.CanPlatformBuildOnHost(ProjectPlatform.Windows, hostIsWindows: true, hostIsMacOs: false));
		Assert.IsFalse(DotNetService.CanPlatformBuildOnHost(ProjectPlatform.Windows, hostIsWindows: false, hostIsMacOs: true));
		Assert.IsFalse(DotNetService.CanPlatformBuildOnHost(ProjectPlatform.Windows, hostIsWindows: false, hostIsMacOs: false));
	}

	[TestMethod]
	public void CanPlatformBuildOnHost_Ios_OnlyOnMacOs()
	{
		Assert.IsTrue(DotNetService.CanPlatformBuildOnHost(ProjectPlatform.Ios, hostIsWindows: false, hostIsMacOs: true));
		Assert.IsFalse(DotNetService.CanPlatformBuildOnHost(ProjectPlatform.Ios, hostIsWindows: true, hostIsMacOs: false));
		Assert.IsFalse(DotNetService.CanPlatformBuildOnHost(ProjectPlatform.Ios, hostIsWindows: false, hostIsMacOs: false));
	}

	// GetBuildableProjects

	[TestMethod]
	public void GetBuildableProjects_AlwaysIncludesNeutralProjects()
	{
		string libDir = Path.Combine(_tempDir, "MyLib");
		Directory.CreateDirectory(libDir);
		File.WriteAllText(Path.Combine(libDir, "MyLib.csproj"),
			"<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		IReadOnlyList<string> buildable = _service.GetBuildableProjects(_tempDir);

		Assert.AreEqual(1, buildable.Count);
		Assert.IsTrue(buildable[0].EndsWith("MyLib.csproj", StringComparison.Ordinal));
	}

	[TestMethod]
	public void GetBuildableProjects_IosProjectIncludedOnlyOnMacOs()
	{
		string iosDir = Path.Combine(_tempDir, "MyApp.iOS");
		Directory.CreateDirectory(iosDir);
		string iosProj = Path.Combine(iosDir, "MyApp.iOS.csproj");
		File.WriteAllText(iosProj,
			"<Project><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-ios</TargetFramework></PropertyGroup></Project>");

		IReadOnlyList<string> buildable = _service.GetBuildableProjects(_tempDir);

		bool expected = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
		Assert.AreEqual(expected, buildable.Any(p => p.EndsWith("MyApp.iOS.csproj", StringComparison.Ordinal)));
	}

	[TestMethod]
	public void GetBuildableProjects_IsSubsetOfAllProjects()
	{
		string libDir = Path.Combine(_tempDir, "MyLib");
		Directory.CreateDirectory(libDir);
		File.WriteAllText(Path.Combine(libDir, "MyLib.csproj"),
			"<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

		string iosDir = Path.Combine(_tempDir, "MyApp.iOS");
		Directory.CreateDirectory(iosDir);
		File.WriteAllText(Path.Combine(iosDir, "MyApp.iOS.csproj"),
			"<Project><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-ios</TargetFramework></PropertyGroup></Project>");

		IReadOnlyList<string> all = _service.GetProjectFiles(_tempDir);
		IReadOnlyList<string> buildable = _service.GetBuildableProjects(_tempDir);

		Assert.IsTrue(buildable.Count <= all.Count);
		Assert.IsTrue(buildable.All(all.Contains));
	}
}
