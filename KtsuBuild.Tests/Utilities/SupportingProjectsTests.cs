// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Utilities;

using KtsuBuild.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class SupportingProjectsTests
{
	[TestMethod]
	[DataRow("Widget.Tests/Widget.Tests.csproj")]
	[DataRow("Keybinding.Demo/Keybinding.Demo.csproj")]
	[DataRow("examples/ImGuiAppDemo/ImGuiAppDemo.csproj")]
	[DataRow("benchmarks/Perf/Perf.csproj")]
	public void IsSupporting_WithASupportingProject_ReturnsTrue(string path) =>
		Assert.IsTrue(SupportingProjects.IsSupporting(path));

	[TestMethod]
	[DataRow("BlastMerge/BlastMerge.csproj")]
	[DataRow("Widget/Widget.csproj")]
	public void IsSupporting_WithADeliverable_ReturnsFalse(string path) =>
		Assert.IsFalse(SupportingProjects.IsSupporting(path));

	[TestMethod]
	public void IsSupporting_WithAWorkspace_JudgesOnlyThePathBelowIt() =>
		// Project discovery returns absolute paths. The directories above the checkout say nothing
		// about the repository, so a clone under samples/ still ships what it declares.
		Assert.IsFalse(SupportingProjects.IsSupporting(
			Path.Combine("home", "dev", "samples", "ImGuiApp"),
			Path.Combine("home", "dev", "samples", "ImGuiApp", "ImGuiApp", "ImGuiApp.csproj")));

	[TestMethod]
	public void IsSupporting_WithAWorkspace_StillFindsASupportingDirectoryBelowIt() =>
		Assert.IsTrue(SupportingProjects.IsSupporting(
			Path.Combine("home", "dev", "ImGuiApp"),
			Path.Combine("home", "dev", "ImGuiApp", "examples", "ImGuiAppDemo", "ImGuiAppDemo.csproj")));
}
