// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;

/// <summary>
/// iOS command for building, packaging, and uploading iOS application heads.
/// Phase 2 ships the unsigned <c>build</c> subcommand; phase 3 adds the signed
/// <c>package</c> subcommand; <c>upload</c> arrives with the TestFlight work.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class IosCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// Initializes a new instance of the <see cref="IosCommand"/> class.
	/// </summary>
	public IosCommand() : base("ios", "iOS build commands")
	{
		Subcommands.Add(new BuildSubcommand());
		Subcommands.Add(new PackageSubcommand());
	}

#pragma warning disable CA1010
	private sealed class BuildSubcommand : Command
#pragma warning restore CA1010
	{
		private static readonly Option<string?> ProjectOption = new(
			"--project", "-p")
		{
			Description = "Path to a specific iOS head to build. Defaults to auto-detecting all iOS heads in the workspace.",
		};

		private static readonly Option<string?> RuntimeOption = new(
			"--runtime", "-r")
		{
			Description = "A specific iOS runtime identifier to build (for example ios-arm64). Defaults to building both iossimulator-arm64 and ios-arm64.",
		};

		private static readonly Option<string[]> RequireFrameworkOption = new(
			"--require-framework")
		{
			Description = "Fail the device build if the named native framework is not embedded in the produced .app bundle. May be repeated.",
			AllowMultipleArgumentsPerToken = true,
		};

		public BuildSubcommand() : base("build", "Build iOS head(s) for simulator and device, unsigned (no secrets)")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Configuration);
			Options.Add(GlobalOptions.Verbose);
			Options.Add(ProjectOption);
			Options.Add(RuntimeOption);
			Options.Add(RequireFrameworkOption);
		}
	}

#pragma warning disable CA1010
	private sealed class PackageSubcommand : Command
#pragma warning restore CA1010
	{
		private static readonly Option<string?> ProjectOption = new(
			"--project", "-p")
		{
			Description = "Path to a specific iOS head to package. Defaults to auto-detecting all iOS heads in the workspace.",
		};

		private static readonly Option<string?> RuntimeOption = new(
			"--runtime", "-r")
		{
			Description = "The device iOS runtime identifier to archive for. Defaults to ios-arm64.",
		};

		private static readonly Option<string?> FrameworkOption = new(
			"--framework", "-f")
		{
			Description = "The iOS target framework to archive (for example net10.0-ios). Defaults to detecting it from the head's project file.",
		};

		private static readonly Option<string?> VersionOption = new(
			"--version", "-V")
		{
			Description = "The marketing version stamped into CFBundleShortVersionString. Defaults to KtsuBuild's computed version.",
		};

		private static readonly Option<string?> BuildNumberOption = new(
			"--build-number", "-b")
		{
			Description = "The monotonic build number stamped into CFBundleVersion. Defaults to GITHUB_RUN_NUMBER, then 1.",
		};

		public PackageSubcommand() : base("package", "Provision the toolchain, stamp the version, sign, and archive an .ipa (no-ops without signing secrets)")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Configuration);
			Options.Add(GlobalOptions.Verbose);
			Options.Add(ProjectOption);
			Options.Add(RuntimeOption);
			Options.Add(FrameworkOption);
			Options.Add(VersionOption);
			Options.Add(BuildNumberOption);
		}
	}
}
