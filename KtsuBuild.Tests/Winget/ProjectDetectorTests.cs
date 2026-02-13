// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Winget;

using KtsuBuild.Winget;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ProjectDetectorTests
{
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), $"ProjDetect_{Guid.NewGuid():N}");
		Directory.CreateDirectory(_tempDir);
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	[TestMethod]
	public void Detect_CSharpProject_IdentifiesType()
	{
		// Arrange
		string csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <AssemblyName>MyApp</AssemblyName>
    <Description>A test application</Description>
  </PropertyGroup>
</Project>";
		File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), csproj);

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("csharp", result.Type);
		Assert.AreEqual("MyApp", result.Name);
		Assert.AreEqual("MyApp.exe", result.ExecutableName);
		Assert.IsTrue(result.Tags.Contains("dotnet"));
		Assert.IsTrue(result.Tags.Contains("csharp"));
	}

	[TestMethod]
	public void Detect_LibraryOnlyProject_DetectedCorrectly()
	{
		// Arrange
		string projectName = Path.GetFileName(_tempDir);
		string csproj = @$"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>net6.0;net7.0;net8.0</TargetFrameworks>
    <IsPackable>true</IsPackable>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  </PropertyGroup>
</Project>";
		File.WriteAllText(Path.Combine(_tempDir, $"{projectName}.csproj"), csproj);

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);
		bool isLibraryOnly = ProjectDetector.IsLibraryOnlyProject(_tempDir, result);

		// Assert
		Assert.IsTrue(isLibraryOnly, "Should detect as library-only project");
	}

	[TestMethod]
	public void Detect_TestProject_Excluded()
	{
		// Arrange
		string csproj = @"<Project Sdk=""Microsoft.NET.Sdk.Test"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>";
		File.WriteAllText(Path.Combine(_tempDir, "MyApp.Tests.csproj"), csproj);

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);
		bool isLibraryOnly = ProjectDetector.IsLibraryOnlyProject(_tempDir, result);

		// Assert
		Assert.IsTrue(isLibraryOnly, "Test projects should be treated as library-only (excluded from Winget)");
	}

	[TestMethod]
	public void Detect_ReadsVersionFromVersionMd()
	{
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "VERSION.md"), "2.5.1");
		File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("2.5.1", result.Version);
	}

	[TestMethod]
	public void Detect_ReadsPublisherFromAuthorsMd()
	{
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "AUTHORS.md"), "ktsu.dev\nOther Author");
		File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("ktsu.dev", result.Publisher);
	}

	[TestMethod]
	public void Detect_ReadsNameFromReadmeMd()
	{
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "README.md"), "# MyAwesomeProject\n\nThis is a cool project.");
		File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("MyAwesomeProject", result.Name);
	}

	[TestMethod]
	public void Detect_ReadsShortDescriptionFromReadme()
	{
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "README.md"), "# Project\n\nA short description of the project.");
		File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("A short description of the project.", result.ShortDescription);
	}

	[TestMethod]
	public void Detect_ReadsQuotedDescriptionFromReadme()
	{
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "README.md"), "# Project\n\n> A quoted description");
		File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("A quoted description", result.ShortDescription);
	}

	[TestMethod]
	public void Detect_ReadsDescriptionFromDescriptionMd()
	{
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "DESCRIPTION.md"), "A full detailed description of the project.");
		File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("A full detailed description of the project.", result.Description);
	}

	[TestMethod]
	public void Detect_ReadsTagsFromTagsMd()
	{
		// Arrange
		File.WriteAllText(Path.Combine(_tempDir, "TAGS.md"), "utility;cli;tool");
		File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.IsTrue(result.Tags.Contains("utility"));
		Assert.IsTrue(result.Tags.Contains("cli"));
		Assert.IsTrue(result.Tags.Contains("tool"));
	}

	[TestMethod]
	public void Detect_NodeProject_IdentifiesType()
	{
		// Arrange
		string packageJson = @"{
  ""name"": ""my-node-app"",
  ""version"": ""1.0.0"",
  ""description"": ""A Node.js application"",
  ""bin"": {
    ""myapp"": ""./bin/index.js""
  }
}";
		File.WriteAllText(Path.Combine(_tempDir, "package.json"), packageJson);

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("node", result.Type);
		Assert.AreEqual("my-node-app", result.Name);
		Assert.IsTrue(result.Tags.Contains("nodejs"));
		Assert.IsTrue(result.Tags.Contains("javascript"));
	}

	[TestMethod]
	public void Detect_NodeLibrary_DetectedAsLibraryOnly()
	{
		// Arrange
		string packageJson = @"{
  ""name"": ""my-lib"",
  ""version"": ""1.0.0"",
  ""main"": ""index.js""
}";
		File.WriteAllText(Path.Combine(_tempDir, "package.json"), packageJson);

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);
		bool isLibraryOnly = ProjectDetector.IsLibraryOnlyProject(_tempDir, result);

		// Assert
		Assert.IsTrue(isLibraryOnly, "Node library without bin should be library-only");
	}

	[TestMethod]
	public void Detect_RustProject_IdentifiesType()
	{
		// Arrange
		string cargoToml = @"[package]
name = ""my-rust-app""
version = ""0.1.0""
edition = ""2021""
";
		File.WriteAllText(Path.Combine(_tempDir, "Cargo.toml"), cargoToml);

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("rust", result.Type);
		Assert.AreEqual("my-rust-app", result.Name);
		Assert.IsTrue(result.Tags.Contains("rust"));
	}

	[TestMethod]
	public void Detect_UnknownProject_FallsBackToDirectoryName()
	{
		// Arrange - empty directory

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("unknown", result.Type);
		Assert.AreEqual(Path.GetFileName(_tempDir), result.Name);
	}

	[TestMethod]
	public void Detect_CSharpWithWingetProperties_ReadsCommandAlias()
	{
		// Arrange
		string csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <WinGetPackageExecutable>myapp.exe</WinGetPackageExecutable>
    <WinGetCommandAlias>myapp</WinGetCommandAlias>
  </PropertyGroup>
</Project>";
		File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), csproj);

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.AreEqual("myapp.exe", result.ExecutableName);
		Assert.AreEqual("myapp", result.CommandAlias);
	}

	[TestMethod]
	public void IsLibraryOnlyProject_ExecutableProject_ReturnsFalse()
	{
		// Arrange
		string projectName = Path.GetFileName(_tempDir);
		string csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>";
		File.WriteAllText(Path.Combine(_tempDir, $"{projectName}.csproj"), csproj);

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);
		bool isLibraryOnly = ProjectDetector.IsLibraryOnlyProject(_tempDir, result);

		// Assert
		Assert.IsFalse(isLibraryOnly, "Executable project should not be library-only");
	}

	[TestMethod]
	public void Detect_CSharpProject_ReadsPackageTags()
	{
		// Arrange
		string csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <PackageTags>serialization;json;xml</PackageTags>
  </PropertyGroup>
</Project>";
		File.WriteAllText(Path.Combine(_tempDir, "MyLib.csproj"), csproj);

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);

		// Assert
		Assert.IsTrue(result.Tags.Contains("serialization"));
		Assert.IsTrue(result.Tags.Contains("json"));
		Assert.IsTrue(result.Tags.Contains("xml"));
	}

	[TestMethod]
	public void Detect_DemoProjectExcluded()
	{
		// Arrange
		string projectName = Path.GetFileName(_tempDir);
		string mainCsproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>net6.0;net8.0</TargetFrameworks>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
</Project>";
		string demoCsproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>";
		File.WriteAllText(Path.Combine(_tempDir, $"{projectName}.csproj"), mainCsproj);
		File.WriteAllText(Path.Combine(_tempDir, $"{projectName}.Demo.csproj"), demoCsproj);

		// Act
		ProjectInfo result = ProjectDetector.Detect(_tempDir);
		bool isLibraryOnly = ProjectDetector.IsLibraryOnlyProject(_tempDir, result);

		// Assert
		Assert.IsTrue(isLibraryOnly, "Demo projects should be excluded, leaving library-only");
	}
}
