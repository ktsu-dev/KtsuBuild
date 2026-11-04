// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Commands;

using System.CommandLine;
using KtsuBuild.Tool;
using KtsuBuild.Tool.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Covers the <c>ios</c> command tree's option surface: what <c>ios package</c> accepts and
/// that the handler wiring in <see cref="Program"/> finds every option it reads.
/// </summary>
[TestClass]
public class IosCommandTests
{
	[TestMethod]
	public void Package_ParsesBundleId()
	{
		ParseResult result = new IosCommand().Parse("package --bundle-id com.example.app.beta");

		Assert.IsEmpty(result.Errors);
		Assert.AreEqual("com.example.app.beta", result.GetValue<string?>("--bundle-id"));
	}

	[TestMethod]
	public void Package_WithoutBundleId_LeavesItUnset()
	{
		ParseResult result = new IosCommand().Parse("package");

		Assert.IsEmpty(result.Errors);
		Assert.IsNull(result.GetValue<string?>("--bundle-id"));
	}

	[TestMethod]
	public async Task Program_WiresIosPackageOptions()
	{
		// Building the command tree looks up every ios package option the handler reads, so a
		// missing one throws here rather than the first time someone runs the command.
		int exitCode = await Program.Main(["ios", "package", "--help"]).ConfigureAwait(false);

		Assert.AreEqual(0, exitCode);
	}
}
