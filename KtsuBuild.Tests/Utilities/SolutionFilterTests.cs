// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Utilities;

using System.Text.Json;

using KtsuBuild.Tests.Helpers;
using KtsuBuild.Utilities;

[TestClass]
public class SolutionFilterTests
{
	private const string ClassicSolution = @"Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""ImGui.App"", ""ImGui.App\ImGui.App.csproj"", ""{EEC5}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""tests"", ""tests"", ""{9999}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Widgets.UITests"", ""tests\Widgets.UITests\Widgets.UITests.csproj"", ""{AAAA}""
EndProject
";

	private const string XmlSolution = @"<Solution>
  <Project Path=""ImGui.App/ImGui.App.csproj"" />
  <Folder Name=""/tests/"">
    <Project Path=""tests/Widgets.UITests/Widgets.UITests.csproj"" />
  </Folder>
</Solution>
";

	private static readonly string[] ExpectedClassicProjects =
	[
		@"ImGui.App\ImGui.App.csproj", @"tests\Widgets.UITests\Widgets.UITests.csproj",
	];

	private static readonly string[] ExpectedXmlProjects =
	[
		"ImGui.App/ImGui.App.csproj", "tests/Widgets.UITests/Widgets.UITests.csproj",
	];

	private string _tempDir = null!;

	[TestInitialize]
	public void Setup() => _tempDir = TestHelpers.CreateTempDir("SolutionFilter");

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	[TestMethod]
	public void ParseSln_ReadsProjectPaths()
	{
		IReadOnlyList<string> projects = SolutionFilter.ParseSln(ClassicSolution);

		CollectionAssert.AreEquivalent(
			ExpectedClassicProjects,
			projects.ToList());
	}

	[TestMethod]
	public void ParseSln_SkipsSolutionFolders()
	{
		IReadOnlyList<string> projects = SolutionFilter.ParseSln(ClassicSolution);

		// The folder entry shares the project line's shape and differs only by having no project
		// file extension, so it is the case most likely to slip through.
		Assert.IsFalse(projects.Any(p => p.EndsWith("tests", StringComparison.Ordinal)));
	}

	[TestMethod]
	public void ParseSlnx_ReadsNestedProjectPaths()
	{
		IReadOnlyList<string> projects = SolutionFilter.ParseSlnx(XmlSolution);

		CollectionAssert.AreEquivalent(
			ExpectedXmlProjects,
			projects.ToList());
	}

	[TestMethod]
	public void Matches_TreatsBothSeparatorsAlike()
	{
		Assert.IsTrue(SolutionFilter.Matches(@"tests\Widgets.UITests\Widgets.UITests.csproj", "**/*.UITests/*"));
		Assert.IsTrue(SolutionFilter.Matches("tests/Widgets.UITests/Widgets.UITests.csproj", "**/*.UITests/*"));
	}

	[TestMethod]
	public void Matches_DoesNotMatchUnrelatedProjects()
	{
		Assert.IsFalse(SolutionFilter.Matches("ImGui.App/ImGui.App.csproj", "**/*.UITests/*"));
		Assert.IsFalse(SolutionFilter.Matches("tests/ImGui.App.Tests/ImGui.App.Tests.csproj", "**/*.UITests/*"));
	}

	[TestMethod]
	public void Matches_SingleStarDoesNotCrossDirectories()
	{
		Assert.IsFalse(SolutionFilter.Matches("tests/Widgets.UITests/Widgets.UITests.csproj", "*.csproj"));
		Assert.IsTrue(SolutionFilter.Matches("Widgets.UITests.csproj", "*.csproj"));
	}

	[TestMethod]
	public void Write_KeepsEverythingThePatternsDoNotMatch()
	{
		string solution = Path.Combine(_tempDir, "Sample.sln");
		File.WriteAllText(solution, ClassicSolution);
		string filter = Path.Combine(_tempDir, "Sample.slnf");

		IReadOnlyList<string> excluded = SolutionFilter.Write(solution, ["**/*.UITests/*"], filter);

		Assert.HasCount(1, excluded);
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filter));
		JsonElement projects = document.RootElement.GetProperty("solution").GetProperty("projects");
		Assert.HasCount(1, projects.EnumerateArray().ToList());
		Assert.AreEqual(@"ImGui.App\ImGui.App.csproj", projects[0].GetString());
	}

	[TestMethod]
	public void Write_NamesTheSolutionRelativeToTheFilter()
	{
		string solution = Path.Combine(_tempDir, "Sample.sln");
		File.WriteAllText(solution, ClassicSolution);
		string filter = Path.Combine(_tempDir, "Sample.slnf");

		SolutionFilter.Write(solution, [], filter);

		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filter));
		Assert.AreEqual("Sample.sln", document.RootElement.GetProperty("solution").GetProperty("path").GetString());
	}

	[TestMethod]
	public void Write_ExcludesNothingWhenNoPatternMatches()
	{
		string solution = Path.Combine(_tempDir, "Sample.sln");
		File.WriteAllText(solution, ClassicSolution);
		string filter = Path.Combine(_tempDir, "Sample.slnf");

		IReadOnlyList<string> excluded = SolutionFilter.Write(solution, ["**/NoSuchProject/*"], filter);

		Assert.IsEmpty(excluded);
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filter));
		Assert.HasCount(2, document.RootElement.GetProperty("solution").GetProperty("projects").EnumerateArray().ToList());
	}

	[TestMethod]
	public void FindSolution_PrefersTheXmlFormat()
	{
		File.WriteAllText(Path.Combine(_tempDir, "Sample.sln"), ClassicSolution);
		File.WriteAllText(Path.Combine(_tempDir, "Sample.slnx"), XmlSolution);

		string? found = SolutionFilter.FindSolution(_tempDir);

		Assert.IsNotNull(found);
		Assert.AreEqual(".slnx", Path.GetExtension(found));
	}

	[TestMethod]
	public void FindSolution_ReturnsNullWhenThereIsNoSolution()
		=> Assert.IsNull(SolutionFilter.FindSolution(_tempDir));
}
