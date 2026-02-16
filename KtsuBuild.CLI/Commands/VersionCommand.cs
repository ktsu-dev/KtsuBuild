// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI.Commands;

using System.CommandLine;

/// <summary>
/// Version command for version management operations.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class VersionCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// Initializes a new instance of the <see cref="VersionCommand"/> class.
	/// </summary>
	public VersionCommand() : base("version", "Version management")
	{
		Subcommands.Add(new ShowCommand());
		Subcommands.Add(new BumpCommand());
		Subcommands.Add(new CreateCommand());
	}

#pragma warning disable CA1010
	private sealed class ShowCommand : Command
#pragma warning restore CA1010
	{
		public ShowCommand() : base("show", "Show current version info")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
		}
	}

#pragma warning disable CA1010
	private sealed class BumpCommand : Command
#pragma warning restore CA1010
	{
		public BumpCommand() : base("bump", "Calculate next version")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
		}
	}

#pragma warning disable CA1010
	private sealed class CreateCommand : Command
#pragma warning restore CA1010
	{
		public CreateCommand() : base("create", "Create VERSION.md")
		{
			Options.Add(GlobalOptions.Workspace);
			Options.Add(GlobalOptions.Verbose);
		}
	}
}
