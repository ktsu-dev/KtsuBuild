// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using KtsuBuild.Profile;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ShippedVariantsTests
{
	private static string Labels(IEnumerable<ShippedVariant> variants) =>
		string.Join(" ", variants.Select(ShippedVariants.ToLabel));

	[TestMethod]
	[DataRow("BlastMerge/BlastMerge.csproj")]
	[DataRow("ImGui.App/ImGui.App.csproj")]
	[DataRow("ImGui.App.Testing/ImGui.App.Testing.csproj")]
	[DataRow("Widget.csproj")]
	public void IsShippingProject_WithADeliverable_ReturnsTrue(string path) =>
		Assert.IsTrue(ShippedVariants.IsShippingProject(path));

	[TestMethod]
	[DataRow("Extensions.Test/Extensions.Test.csproj")]
	[DataRow("Widget.Tests/Widget.Tests.csproj")]
	[DataRow("Widget.Benchmark/Widget.Benchmark.csproj")]
	[DataRow("Widget.Sample/Widget.Sample.csproj")]
	[DataRow("Widget.Examples/Widget.Examples.csproj")]
	[DataRow("Keybinding.Demo/Keybinding.Demo.csproj")]
	[DataRow("ThemeProviderDemo/ThemeProviderDemo.csproj")]
	public void IsShippingProject_WithASupportingName_ReturnsFalse(string path) =>
		Assert.IsFalse(ShippedVariants.IsShippingProject(path));

	[TestMethod]
	[DataRow("examples/ImGuiAppDemo/ImGuiAppDemo.csproj")]
	[DataRow("tests/NodeGraph.Tests/NodeGraph.Tests.csproj")]
	[DataRow("samples/Anything/Anything.csproj")]
	[DataRow("benchmarks/Perf/Perf.csproj")]
	[DataRow("demos/Showcase/Showcase.csproj")]
	[DataRow("Examples/Nested/Deep/Thing.csproj")]
	public void IsShippingProject_WithASupportingDirectory_ReturnsFalse(string path) =>
		// ImGuiApp's demo applications live under examples/ with names that say nothing about being
		// demos, so filtering on the file name alone would report applications the repository does not ship.
		Assert.IsFalse(ShippedVariants.IsShippingProject(path));

	[TestMethod]
	public void IsShippingProject_DoesNotMatchADirectoryNamedLikeAProject() =>
		// The final segment is the file, so a repository literally named Tests is not filtered out.
		Assert.IsTrue(ShippedVariants.IsShippingProject("Widget/Widget.csproj"));

	[TestMethod]
	public void FromProject_WithPlainSdk_ReportsALibrary() =>
		Assert.AreEqual("lib", Labels(ShippedVariants.FromProject(
			"""<Project Sdk="Microsoft.NET.Sdk"><Sdk Name="ktsu.Sdk" /></Project>""")));

	[TestMethod]
	public void FromProject_WithConsoleAppSdk_ReportsACli() =>
		Assert.AreEqual("cli", Labels(ShippedVariants.FromProject(
			"""<Project Sdk="Microsoft.NET.Sdk"><Sdk Name="ktsu.Sdk" /><Sdk Name="ktsu.Sdk.ConsoleApp" /></Project>""")));

	[TestMethod]
	public void FromProject_WithAppSdk_ReportsAnApp() =>
		Assert.AreEqual("app", Labels(ShippedVariants.FromProject("""<Sdk Name="ktsu.Sdk.App" />""")));

	[TestMethod]
	public void FromProject_WithToolSdk_ReportsATool() =>
		Assert.AreEqual("tool", Labels(ShippedVariants.FromProject(
			"""<Project Sdk="Microsoft.NET.Sdk"><Sdk Name="ktsu.Sdk" /><Sdk Name="ktsu.Sdk.Tool" /></Project>""")));

	[TestMethod]
	public void FromProject_WithAVariant_DoesNotAlsoReportALibrary() =>
		// The plain SDK is always present alongside a variant, so counting it would label every
		// application a library too.
		Assert.AreEqual("cli", Labels(ShippedVariants.FromProject(
			"""<Sdk Name="ktsu.Sdk" /><Sdk Name="ktsu.Sdk.ConsoleApp" />""")));

	[TestMethod]
	public void FromProject_WithAPinnedSdkVersion_StillMatches() =>
		Assert.AreEqual("app", Labels(ShippedVariants.FromProject("""<Sdk Name="ktsu.Sdk.App/2.28.0" />""")));

	[TestMethod]
	public void FromProject_WithSdkOnTheProjectAttribute_StillMatches() =>
		Assert.AreEqual("cli", Labels(ShippedVariants.FromProject("""<Project Sdk="ktsu.Sdk.ConsoleApp">""")));

	// Every platform ktsu.Sdk suffix, on its own and alongside a kind suffix. A platform SDK sets
	// OutputType itself, so DotNetService.IsExecutableProject already treats a bare one as an
	// executable and attaches RID zips to the release; reporting the same project as a library here
	// made the profile page and the release it links to disagree about what the repository ships.

	[TestMethod]
	[DataRow("ktsu.Sdk.Windows")]
	[DataRow("ktsu.Sdk.Linux")]
	[DataRow("ktsu.Sdk.macOS")]
	[DataRow("ktsu.Sdk.iOS")]
	public void FromProject_WithOnlyAPlatformSdk_ReportsAnApp(string sdk) =>
		Assert.AreEqual("app", Labels(ShippedVariants.FromProject(
			$"""<Sdk Name="ktsu.Sdk" /><Sdk Name="{sdk}" />""")), $"{sdk} produces an executable");

	[TestMethod]
	[DataRow("ktsu.Sdk.Windows")]
	[DataRow("ktsu.Sdk.Linux")]
	[DataRow("ktsu.Sdk.macOS")]
	[DataRow("ktsu.Sdk.iOS")]
	public void FromProject_WithAPlatformSdkBesideAKindSdk_ReportsOnlyTheKind(string sdk) =>
		// A platform SDK says which platform a project targets, not what kind of thing it is, so a
		// console app built for Windows is a cli and not also an app.
		Assert.AreEqual("cli", Labels(ShippedVariants.FromProject(
			$"""<Sdk Name="ktsu.Sdk" /><Sdk Name="ktsu.Sdk.ConsoleApp" /><Sdk Name="{sdk}" />""")),
			$"{sdk} does not add a second variant");

	[TestMethod]
	public void FromProject_WithAPlatformSdkOnTheProjectAttribute_StillReportsAnApp() =>
		Assert.AreEqual("app", Labels(ShippedVariants.FromProject(
			"""<Project Sdk="ktsu.Sdk.Windows/2.28.1">""")));

	[TestMethod]
	public void FromProject_WithNoKtsuSdk_ReportsNothing() =>
		Assert.IsEmpty(ShippedVariants.FromProject("""<Project Sdk="Microsoft.NET.Sdk"></Project>"""));

	[TestMethod]
	public void FromProject_WithNoContent_ReportsNothing() =>
		Assert.IsEmpty(ShippedVariants.FromProject(null));

	[TestMethod]
	public void Combine_MergesAcrossProjects() =>
		// Coder ships a library, a windowed app, and a console app across three projects.
		Assert.AreEqual("lib cli app", Labels(ShippedVariants.Combine([
			ShippedVariants.FromProject("""<Sdk Name="ktsu.Sdk" />"""),
			ShippedVariants.FromProject("""<Sdk Name="ktsu.Sdk" /><Sdk Name="ktsu.Sdk.App" />"""),
			ShippedVariants.FromProject("""<Sdk Name="ktsu.Sdk" /><Sdk Name="ktsu.Sdk.ConsoleApp" />"""),
		])));

	[TestMethod]
	public void Combine_DeduplicatesRepeatedVariants() =>
		// TUI has two console app projects and should say cli once.
		Assert.AreEqual("cli", Labels(ShippedVariants.Combine([
			ShippedVariants.FromProject("""<Sdk Name="ktsu.Sdk.ConsoleApp" />"""),
			ShippedVariants.FromProject("""<Sdk Name="ktsu.Sdk.ConsoleApp" />"""),
		])));

	[TestMethod]
	public void Combine_IsOrderIndependent()
	{
		IReadOnlyList<ShippedVariant> library = ShippedVariants.FromProject("""<Sdk Name="ktsu.Sdk" />""");
		IReadOnlyList<ShippedVariant> tool = ShippedVariants.FromProject("""<Sdk Name="ktsu.Sdk.Tool" />""");

		Assert.AreEqual(
			Labels(ShippedVariants.Combine([library, tool])),
			Labels(ShippedVariants.Combine([tool, library])));
	}

	[TestMethod]
	public void Combine_WithNothing_ReportsNothing() =>
		Assert.IsEmpty(ShippedVariants.Combine([]));
}
