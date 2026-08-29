// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using KtsuBuild.Profile;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ProjectClassifierTests
{
	[TestMethod]
	public void SelectPrimaryProject_WithNoProjects_ReturnsNull() =>
		Assert.IsNull(ProjectClassifier.SelectPrimaryProject("Extensions", []));

	[TestMethod]
	public void SelectPrimaryProject_WithConventionalLibrary_ReturnsTheLibrary() =>
		Assert.AreEqual(
			"Extensions/Extensions.csproj",
			ProjectClassifier.SelectPrimaryProject("Extensions", [
				"Extensions.Test/Extensions.Test.csproj",
				"Extensions/Extensions.csproj",
			]));

	[TestMethod]
	public void SelectPrimaryProject_PrefersApplicationOverLibrary() =>
		// A repository that ships an app is about the app, even when a supporting library sits beside it.
		Assert.AreEqual(
			"Coder.App/Coder.App.csproj",
			ProjectClassifier.SelectPrimaryProject("Coder", [
				"Coder.App/Coder.App.csproj",
				"Coder.Core/Coder.Core.csproj",
				"Coder.Test/Coder.Test.csproj",
			]));

	[TestMethod]
	public void SelectPrimaryProject_PrefersConsoleAppOverLibrary() =>
		Assert.AreEqual(
			"BlastMerge.ConsoleApp/BlastMerge.ConsoleApp.csproj",
			ProjectClassifier.SelectPrimaryProject("BlastMerge", [
				"BlastMerge.ConsoleApp/BlastMerge.ConsoleApp.csproj",
				"BlastMerge.Test/BlastMerge.Test.csproj",
				"BlastMerge/BlastMerge.csproj",
			]));

	[TestMethod]
	public void SelectPrimaryProject_FallsBackToCoreNamedLibrary() =>
		Assert.AreEqual(
			"Widget.Core/Widget.Core.csproj",
			ProjectClassifier.SelectPrimaryProject("Widget", [
				"Widget.Support/Widget.Support.csproj",
				"Widget.Core/Widget.Core.csproj",
			]));

	[TestMethod]
	public void SelectPrimaryProject_WithNoConventionalName_ReturnsShortestPath() =>
		// ImGuiApp names none of its projects after the repository, so the shallowest path wins.
		Assert.AreEqual(
			"ImGui.App/ImGui.App.csproj",
			ProjectClassifier.SelectPrimaryProject("ImGuiApp", [
				"ForceDirectedLayout/ForceDirectedLayout.csproj",
				"ImGui.App/ImGui.App.csproj",
				"ImGui.Widgets/ImGui.Widgets.csproj",
				"NodeGraph/NodeGraph.csproj",
			]));

	[TestMethod]
	public void SelectPrimaryProject_IsDeterministicWhenPathLengthsTie()
	{
		string[] paths = ["NodeGraph/NodeGraph.csproj", "ImGui.App/ImGui.App.csproj"];
		string[] reversed = [.. paths.Reverse()];

		Assert.AreEqual(
			ProjectClassifier.SelectPrimaryProject("ImGuiApp", paths),
			ProjectClassifier.SelectPrimaryProject("ImGuiApp", reversed));
	}

	[TestMethod]
	[DataRow("Extensions.Test/Extensions.Test.csproj")]
	[DataRow("tests/Widget.Tests/Widget.Tests.csproj")]
	[DataRow("Widget.Benchmark/Widget.Benchmark.csproj")]
	[DataRow("Widget.Benchmarks/Widget.Benchmarks.csproj")]
	[DataRow("samples/Widget.Sample/Widget.Sample.csproj")]
	[DataRow("Widget.Examples/Widget.Examples.csproj")]
	public void SelectPrimaryProject_SkipsSupportingProjects(string supportingPath) =>
		Assert.AreEqual(
			"Widget/Widget.csproj",
			ProjectClassifier.SelectPrimaryProject("Widget", [supportingPath, "Widget/Widget.csproj"]));

	[TestMethod]
	public void SelectPrimaryProject_WithOnlySupportingProjects_UsesThemAnyway() =>
		// Reporting nothing would drop the repository from the profile, which is worse than a guess.
		Assert.AreEqual(
			"Widget.Test/Widget.Test.csproj",
			ProjectClassifier.SelectPrimaryProject("Widget", ["Widget.Test/Widget.Test.csproj"]));

	[TestMethod]
	public void SelectPrimaryProject_DoesNotTreatTestingAsATestProject() =>
		// ImGui.App.Testing is a shipped library, not a test project.
		Assert.AreEqual(
			"ImGui.App.Testing/ImGui.App.Testing.csproj",
			ProjectClassifier.SelectPrimaryProject("Widget", ["ImGui.App.Testing/ImGui.App.Testing.csproj"]));

	[TestMethod]
	public void SelectPrimaryProject_MatchesProjectAtRepositoryRoot() =>
		Assert.AreEqual(
			"Widget.csproj",
			ProjectClassifier.SelectPrimaryProject("Widget", ["src/Other.csproj", "Widget.csproj"]));

	[TestMethod]
	[DataRow("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <Sdk Name=\"ktsu.Sdk\" />\n  <Sdk Name=\"ktsu.Sdk.ConsoleApp\" />\n</Project>")]
	[DataRow("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <Sdk Name=\"ktsu.Sdk.App\" />\n</Project>")]
	[DataRow("<Project Sdk=\"ktsu.Sdk.ConsoleApp\">\n</Project>")]
	[DataRow("<Project Sdk=\"ktsu.Sdk.App/1.75.0\">\n</Project>")]
	[DataRow("<Sdk Name=\"ktsu.Sdk.App/2.28.0\" />")]
	public void IsApplication_WithApplicationSdk_ReturnsTrue(string content) =>
		Assert.IsTrue(ProjectClassifier.IsApplication(content));

	[TestMethod]
	[DataRow("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <Sdk Name=\"ktsu.Sdk\" />\n</Project>")]
	[DataRow("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <Sdk Name=\"ktsu.Sdk.Tool\" />\n</Project>")]
	[DataRow("<Project Sdk=\"MSTest.Sdk\">\n</Project>")]
	public void IsApplication_WithoutApplicationSdk_ReturnsFalse(string content) =>
		Assert.IsFalse(ProjectClassifier.IsApplication(content));

	[TestMethod]
	public void IsApplication_WithMissingContent_ReturnsFalse() =>
		Assert.IsFalse(ProjectClassifier.IsApplication(null));
}
