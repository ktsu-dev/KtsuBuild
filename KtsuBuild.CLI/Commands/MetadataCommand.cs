// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;

/// <summary>
/// Metadata command for metadata file management.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class MetadataCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MetadataCommand"/> class.
	/// </summary>
	public MetadataCommand() : base("metadata", "Metadata file management")
	{
		Subcommands.Add(new UpdateCommand());
		Subcommands.Add(new LicenseCommand());
		Subcommands.Add(new ChangelogCommand());
	}

#pragma warning disable CA1010
	private sealed class UpdateCommand : Command
#pragma warning restore CA1010
	{
		public UpdateCommand() : base("update", "Update all metadata files")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
			Options.Add(new Option<bool>("--no-commit") { Description = "Don't commit changes" });
		}
	}

#pragma warning disable CA1010
	private sealed class LicenseCommand : Command
#pragma warning restore CA1010
	{
		public LicenseCommand() : base("license", "Generate LICENSE.md and COPYRIGHT.md")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
		}
	}

#pragma warning disable CA1010
	private sealed class ChangelogCommand : Command
#pragma warning restore CA1010
	{
		public ChangelogCommand() : base("changelog", "Generate CHANGELOG.md")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
		}
	}
}
