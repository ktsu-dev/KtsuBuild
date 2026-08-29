// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using KtsuBuild.Profile;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class GlobalJsonTests
{
	private const string RealGlobalJson = """
		{
		  "sdk": {
		    "version": "10.0.100",
		    "rollForward": "latestFeature"
		  },
		  "msbuild-sdks": {
		    "MSTest.Sdk": "4.3.3",
		    "ktsu.Sdk": "2.28.0",
		    "ktsu.Sdk.ConsoleApp": "2.28.0",
		    "ktsu.Sdk.Tool": "2.28.0",
		    "ktsu.Sdk.App": "2.28.0"
		  },
		  "test": {
		    "runner": "Microsoft.Testing.Platform"
		  }
		}
		""";

	[TestMethod]
	public void TryGetMsBuildSdkVersion_ReadsThePinnedVersion() =>
		Assert.AreEqual("2.28.0", GlobalJson.TryGetMsBuildSdkVersion(RealGlobalJson, "ktsu.Sdk"));

	[TestMethod]
	public void TryGetMsBuildSdkVersion_DoesNotConfuseAVariantForTheBaseSdk() =>
		Assert.AreEqual("4.3.3", GlobalJson.TryGetMsBuildSdkVersion(RealGlobalJson, "MSTest.Sdk"));

	[TestMethod]
	public void TryGetMsBuildSdkVersion_IgnoresTheDotnetSdkVersion() =>
		// The top-level sdk.version pins the .NET SDK, which is a different thing from an MSBuild SDK.
		Assert.AreNotEqual("10.0.100", GlobalJson.TryGetMsBuildSdkVersion(RealGlobalJson, "ktsu.Sdk"));

	[TestMethod]
	public void TryGetMsBuildSdkVersion_WithAnUnpinnedSdk_ReturnsNull() =>
		Assert.IsNull(GlobalJson.TryGetMsBuildSdkVersion(RealGlobalJson, "Other.Sdk"));

	[TestMethod]
	public void TryGetMsBuildSdkVersion_WithNoMsBuildSdksSection_ReturnsNull() =>
		Assert.IsNull(GlobalJson.TryGetMsBuildSdkVersion("""{"sdk":{"version":"10.0.100"}}""", "ktsu.Sdk"));

	[TestMethod]
	public void TryGetMsBuildSdkVersion_AllowsCommentsAndTrailingCommas() =>
		Assert.AreEqual("2.28.0", GlobalJson.TryGetMsBuildSdkVersion(
			"""
			{
			  // pinned by the build
			  "msbuild-sdks": { "ktsu.Sdk": "2.28.0", }
			}
			""", "ktsu.Sdk"));

	[TestMethod]
	[DataRow(null)]
	[DataRow("")]
	[DataRow("   ")]
	[DataRow("not json")]
	[DataRow("[]")]
	public void TryGetMsBuildSdkVersion_WithUnusableContent_ReturnsNull(string? content) =>
		Assert.IsNull(GlobalJson.TryGetMsBuildSdkVersion(content, "ktsu.Sdk"));

	[TestMethod]
	public void TryGetMsBuildSdkVersion_WithANonStringVersion_ReturnsNull() =>
		Assert.IsNull(GlobalJson.TryGetMsBuildSdkVersion("""{"msbuild-sdks":{"ktsu.Sdk":228}}""", "ktsu.Sdk"));
}
