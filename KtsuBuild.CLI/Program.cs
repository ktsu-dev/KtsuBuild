// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.CLI.Commands;
using KtsuBuild.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0028 // System.CommandLine.Command implements IEnumerable causing false positive collection init suggestions
internal sealed class Program
{
	private Program() { }

	public static async Task<int> Main(string[] args)
	{
		// Setup DI container
		ServiceCollection services = new();
		ConfigureServices(services);
		using ServiceProvider serviceProvider = services.BuildServiceProvider();

		// Get services
		IProcessRunner processRunner = serviceProvider.GetRequiredService<IProcessRunner>();
		IBuildLogger logger = serviceProvider.GetRequiredService<IBuildLogger>();

		// Build command tree
		RootCommand rootCommand = new("KtsuBuild CLI - Build automation for .NET projects");

		// Add commands
		AddCiCommand(rootCommand, processRunner, logger);
		AddBuildCommand(rootCommand, processRunner, logger);
		AddReleaseCommand(rootCommand, processRunner, logger);
		AddVersionCommand(rootCommand, processRunner, logger);
		AddMetadataCommand(rootCommand, processRunner, logger);
		AddWingetCommand(rootCommand, processRunner, logger);

		// Parse and invoke
		return await rootCommand.Parse(args).InvokeAsync(configuration: null, cancellationToken: CancellationToken.None).ConfigureAwait(false);
	}

	private static void ConfigureServices(IServiceCollection services)
	{
		// Add logging
		services.AddLogging(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Information);
		});

		// Add services
		services.AddSingleton<IProcessRunner, ProcessRunner>();
		services.AddSingleton<IBuildLogger, BuildLogger>();
	}

	private static void AddCiCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		CiCommand command = new();
		Func<string, string, bool, bool, string, CancellationToken, Task<int>> handler = CiCommand.CreateHandler(processRunner, logger);
		command.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			string configuration = parseResult.GetValue(GlobalOptions.Configuration)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			bool dryRun = parseResult.GetValue(GlobalOptions.DryRun);
			string versionBump = parseResult.GetValue(GlobalOptions.VersionBump)!;
			return await handler(workspace, configuration, verbose, dryRun, versionBump, ct).ConfigureAwait(false);
		});
		rootCommand.Subcommands.Add(command);
	}

	private static void AddBuildCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		BuildCommand command = new();
		Func<string, string, bool, CancellationToken, Task<int>> handler = BuildCommand.CreateHandler(processRunner, logger);
		command.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			string configuration = parseResult.GetValue(GlobalOptions.Configuration)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			return await handler(workspace, configuration, verbose, ct).ConfigureAwait(false);
		});
		rootCommand.Subcommands.Add(command);
	}

	private static void AddReleaseCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		ReleaseCommand command = new();
		Func<string, string, bool, bool, CancellationToken, Task<int>> handler = ReleaseCommand.CreateHandler(processRunner, logger);
		command.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			string configuration = parseResult.GetValue(GlobalOptions.Configuration)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			bool dryRun = parseResult.GetValue(GlobalOptions.DryRun);
			return await handler(workspace, configuration, verbose, dryRun, ct).ConfigureAwait(false);
		});
		rootCommand.Subcommands.Add(command);
	}

	private static void AddVersionCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		VersionCommand versionCommand = new();

		// Show subcommand
		Command showCommand = versionCommand.Subcommands.First(c => c.Name == "show");
		showCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);

			logger.VerboseEnabled = verbose;

			KtsuBuild.Git.GitService gitService = new(processRunner, logger);
			KtsuBuild.Git.VersionCalculator versionCalculator = new(gitService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, ct).ConfigureAwait(false);
				KtsuBuild.Git.VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: ct).ConfigureAwait(false);

				Console.WriteLine($"Current Version: {versionInfo.Version}");
				Console.WriteLine($"Last Tag: {versionInfo.LastTag}");
				Console.WriteLine($"Last Version: {versionInfo.LastVersion}");
				Console.WriteLine($"Version Increment: {versionInfo.VersionIncrement}");
				Console.WriteLine($"Reason: {versionInfo.IncrementReason}");
				Console.WriteLine($"Is Prerelease: {versionInfo.IsPrerelease}");

				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Failed to get version info: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});

		// Bump subcommand
		Command bumpCommand = versionCommand.Subcommands.First(c => c.Name == "bump");
		bumpCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);

			logger.VerboseEnabled = verbose;

			KtsuBuild.Git.GitService gitService = new(processRunner, logger);
			KtsuBuild.Git.VersionCalculator versionCalculator = new(gitService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, ct).ConfigureAwait(false);
				KtsuBuild.Git.VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: ct).ConfigureAwait(false);

				Console.WriteLine(versionInfo.Version);
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Failed to calculate version: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});

		// Create subcommand
		Command createCommand = versionCommand.Subcommands.First(c => c.Name == "create");
		createCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);

			logger.VerboseEnabled = verbose;

			KtsuBuild.Git.GitService gitService = new(processRunner, logger);
			KtsuBuild.Git.VersionCalculator versionCalculator = new(gitService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, ct).ConfigureAwait(false);
				KtsuBuild.Git.VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: ct).ConfigureAwait(false);
				string lineEnding = await gitService.GetLineEndingAsync(workspace, ct).ConfigureAwait(false);

				await KtsuBuild.Metadata.VersionFileWriter.WriteAsync(versionInfo.Version, workspace, lineEnding, ct).ConfigureAwait(false);

				logger.WriteSuccess($"Created VERSION.md with version {versionInfo.Version}");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Failed to create VERSION.md: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});

		rootCommand.Subcommands.Add(versionCommand);
	}

	private static void AddMetadataCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		MetadataCommand metadataCommand = new();

		// Update subcommand
		Command updateCommand = metadataCommand.Subcommands.First(c => c.Name == "update");
		Option<bool> noCommitOption = (Option<bool>)updateCommand.Options.First(o => o.Name == "--no-commit");
		updateCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			bool noCommit = parseResult.GetValue(noCommitOption);

			logger.VerboseEnabled = verbose;
			logger.WriteStepHeader("Updating Metadata Files");

			KtsuBuild.Git.GitService gitService = new(processRunner, logger);
			KtsuBuild.Publishing.GitHubService gitHubService = new(processRunner, gitService, logger);
			KtsuBuild.Configuration.BuildConfigurationProvider configProvider = new(gitService, gitHubService);
			KtsuBuild.Metadata.MetadataService metadataService = new(gitService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				KtsuBuild.Configuration.BuildConfiguration buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, ct).ConfigureAwait(false);

				KtsuBuild.Metadata.MetadataUpdateResult result = await metadataService.UpdateAllAsync(new KtsuBuild.Metadata.MetadataUpdateOptions
				{
					BuildConfiguration = buildConfig,
					CommitChanges = !noCommit,
				}, ct).ConfigureAwait(false);

				if (result.Success)
				{
					logger.WriteSuccess($"Metadata updated successfully! Version: {result.Version}");
					return 0;
				}
				else
				{
					logger.WriteError($"Metadata update failed: {result.Error}");
					return 1;
				}
			}
			catch (Exception ex)
			{
				logger.WriteError($"Failed to update metadata: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});

		// License subcommand
		Command licenseCommand = metadataCommand.Subcommands.First(c => c.Name == "license");
		licenseCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);

			logger.VerboseEnabled = verbose;

			KtsuBuild.Git.GitService gitService = new(processRunner, logger);
			KtsuBuild.Publishing.GitHubService gitHubService = new(processRunner, gitService, logger);
			KtsuBuild.Configuration.BuildConfigurationProvider configProvider = new(gitService, gitHubService);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				KtsuBuild.Configuration.BuildConfiguration buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, ct).ConfigureAwait(false);
				string lineEnding = await gitService.GetLineEndingAsync(workspace, ct).ConfigureAwait(false);

				await KtsuBuild.Metadata.LicenseGenerator.GenerateAsync(
					buildConfig.ServerUrl,
					buildConfig.GitHubOwner,
					buildConfig.GitHubRepo,
					workspace,
					lineEnding,
					ct).ConfigureAwait(false);

				logger.WriteSuccess("License files generated!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Failed to generate license: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});

		// Changelog subcommand
		Command changelogCommand = metadataCommand.Subcommands.First(c => c.Name == "changelog");
		changelogCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);

			logger.VerboseEnabled = verbose;

			KtsuBuild.Git.GitService gitService = new(processRunner, logger);
			KtsuBuild.Metadata.ChangelogGenerator changelogGenerator = new(gitService, logger);
			KtsuBuild.Git.VersionCalculator versionCalculator = new(gitService, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, ct).ConfigureAwait(false);
				KtsuBuild.Git.VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: ct).ConfigureAwait(false);
				string lineEnding = await gitService.GetLineEndingAsync(workspace, ct).ConfigureAwait(false);

				await changelogGenerator.GenerateAsync(
					versionInfo.Version,
					commitHash,
					workspace,
					workspace,
					lineEnding,
					cancellationToken: ct).ConfigureAwait(false);

				logger.WriteSuccess("Changelog generated!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Failed to generate changelog: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});

		rootCommand.Subcommands.Add(metadataCommand);
	}

	private static void AddWingetCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		WingetCommand wingetCommand = new();

		// Generate subcommand
		Command generateCommand = wingetCommand.Subcommands.First(c => c.Name == "generate");
		Option<string> versionOption = (Option<string>)generateCommand.Options.First(o => o.Aliases.Contains("-V"));
		Option<string?> repoOption = (Option<string?>)generateCommand.Options.First(o => o.Aliases.Contains("-r"));
		Option<string?> packageIdOption = (Option<string?>)generateCommand.Options.First(o => o.Aliases.Contains("-p"));
		Option<string?> stagingOption = (Option<string?>)generateCommand.Options.First(o => o.Aliases.Contains("-s"));

		generateCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			string version = parseResult.GetValue(versionOption)!;
			string? repo = parseResult.GetValue(repoOption);
			string? packageId = parseResult.GetValue(packageIdOption);
			string? staging = parseResult.GetValue(stagingOption);

			logger.VerboseEnabled = verbose;
			logger.WriteStepHeader("Generating Winget Manifests");

			KtsuBuild.Winget.WingetService wingetService = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				KtsuBuild.Winget.WingetOptions options = new()
				{
					Version = version,
					GitHubRepo = repo,
					PackageId = packageId,
					RootDirectory = workspace,
					OutputDirectory = Path.Combine(workspace, "winget"),
					StagingDirectory = staging ?? Path.Combine(workspace, "staging"),
				};

				KtsuBuild.Winget.WingetManifestResult result = await wingetService.GenerateManifestsAsync(options, ct).ConfigureAwait(false);

				if (result.IsLibraryOnly)
				{
					logger.WriteInfo("Library-only project - no manifests generated");
					return 0;
				}
				else if (result.Success)
				{
					logger.WriteSuccess($"Generated manifests for {result.PackageId}");
					return 0;
				}
				else
				{
					logger.WriteError($"Failed to generate manifests: {result.Error}");
					return 1;
				}
			}
			catch (Exception ex)
			{
				logger.WriteError($"Failed to generate manifests: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});

		// Upload subcommand
		Command uploadCommand = wingetCommand.Subcommands.First(c => c.Name == "upload");
		Option<string> uploadVersionOption = (Option<string>)uploadCommand.Options.First(o => o.Aliases.Contains("-V"));

		uploadCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			string version = parseResult.GetValue(uploadVersionOption)!;

			logger.VerboseEnabled = verbose;

			KtsuBuild.Winget.WingetService wingetService = new(processRunner, logger);

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				string manifestDir = Path.Combine(workspace, "winget");
				await wingetService.UploadManifestsAsync(version, manifestDir, ct).ConfigureAwait(false);

				logger.WriteSuccess("Manifests uploaded!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Failed to upload manifests: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});

		rootCommand.Subcommands.Add(wingetCommand);
	}
}
