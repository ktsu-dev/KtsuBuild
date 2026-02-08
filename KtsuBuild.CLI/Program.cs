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

internal sealed class Program
{
	public static async Task<int> Main(string[] args)
	{
		// Setup DI container
		var services = new ServiceCollection();
		ConfigureServices(services);
		using var serviceProvider = services.BuildServiceProvider();

		// Get services
		var processRunner = serviceProvider.GetRequiredService<IProcessRunner>();
		var logger = serviceProvider.GetRequiredService<IBuildLogger>();

		// Build command tree
		var rootCommand = new RootCommand("KtsuBuild CLI - Build automation for .NET projects")
		{
			Name = "ktsub",
		};

		// Add commands
		AddCiCommand(rootCommand, processRunner, logger);
		AddBuildCommand(rootCommand, processRunner, logger);
		AddReleaseCommand(rootCommand, processRunner, logger);
		AddVersionCommand(rootCommand, processRunner, logger);
		AddMetadataCommand(rootCommand, processRunner, logger);
		AddWingetCommand(rootCommand, processRunner, logger);

		// Parse and invoke
		return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
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
		var command = new CiCommand();
		var handler = CiCommand.CreateHandler(processRunner, logger);
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

	private static void AddBuildCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		var command = new BuildCommand();
		var handler = BuildCommand.CreateHandler(processRunner, logger);
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
		var command = new ReleaseCommand();
		var handler = ReleaseCommand.CreateHandler(processRunner, logger);
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
		var versionCommand = new VersionCommand();

		// Show subcommand
		var showCommand = versionCommand.Subcommands.First(c => c.Name == "show");
		showCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);

			logger.VerboseEnabled = verbose;

			var gitService = new KtsuBuild.Git.GitService(processRunner, logger);
			var versionCalculator = new KtsuBuild.Git.VersionCalculator(gitService, logger);

			try
			{
				string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, ct).ConfigureAwait(false);
				var versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: ct).ConfigureAwait(false);

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
		});

		// Bump subcommand
		var bumpCommand = versionCommand.Subcommands.First(c => c.Name == "bump");
		bumpCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);

			logger.VerboseEnabled = verbose;

			var gitService = new KtsuBuild.Git.GitService(processRunner, logger);
			var versionCalculator = new KtsuBuild.Git.VersionCalculator(gitService, logger);

			try
			{
				string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, ct).ConfigureAwait(false);
				var versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: ct).ConfigureAwait(false);

				Console.WriteLine(versionInfo.Version);
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"Failed to calculate version: {ex.Message}");
				return 1;
			}
		});

		// Create subcommand
		var createCommand = versionCommand.Subcommands.First(c => c.Name == "create");
		createCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);

			logger.VerboseEnabled = verbose;

			var gitService = new KtsuBuild.Git.GitService(processRunner, logger);
			var versionCalculator = new KtsuBuild.Git.VersionCalculator(gitService, logger);

			try
			{
				string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, ct).ConfigureAwait(false);
				var versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: ct).ConfigureAwait(false);
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
		});

		rootCommand.Subcommands.Add(versionCommand);
	}

	private static void AddMetadataCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		var metadataCommand = new MetadataCommand();

		// Update subcommand
		var updateCommand = metadataCommand.Subcommands.First(c => c.Name == "update");
		var noCommitOption = (Option<bool>)updateCommand.Options.First(o => o.Name == "no-commit");
		updateCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			bool noCommit = parseResult.GetValue(noCommitOption);

			logger.VerboseEnabled = verbose;
			logger.WriteStepHeader("Updating Metadata Files");

			var gitService = new KtsuBuild.Git.GitService(processRunner, logger);
			var gitHubService = new KtsuBuild.Publishing.GitHubService(processRunner, gitService, logger);
			var configProvider = new KtsuBuild.Configuration.BuildConfigurationProvider(gitService, gitHubService, logger);
			var metadataService = new KtsuBuild.Metadata.MetadataService(gitService, logger);

			try
			{
				var buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, ct).ConfigureAwait(false);

				var result = await metadataService.UpdateAllAsync(new KtsuBuild.Metadata.MetadataUpdateOptions
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
		});

		// License subcommand
		var licenseCommand = metadataCommand.Subcommands.First(c => c.Name == "license");
		licenseCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);

			logger.VerboseEnabled = verbose;

			var gitService = new KtsuBuild.Git.GitService(processRunner, logger);
			var gitHubService = new KtsuBuild.Publishing.GitHubService(processRunner, gitService, logger);
			var configProvider = new KtsuBuild.Configuration.BuildConfigurationProvider(gitService, gitHubService, logger);

			try
			{
				var buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, ct).ConfigureAwait(false);
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
		});

		// Changelog subcommand
		var changelogCommand = metadataCommand.Subcommands.First(c => c.Name == "changelog");
		changelogCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);

			logger.VerboseEnabled = verbose;

			var gitService = new KtsuBuild.Git.GitService(processRunner, logger);
			var changelogGenerator = new KtsuBuild.Metadata.ChangelogGenerator(gitService, logger);
			var versionCalculator = new KtsuBuild.Git.VersionCalculator(gitService, logger);

			try
			{
				string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, ct).ConfigureAwait(false);
				var versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: ct).ConfigureAwait(false);
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
		});

		rootCommand.Subcommands.Add(metadataCommand);
	}

	private static void AddWingetCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		var wingetCommand = new WingetCommand();

		// Generate subcommand
		var generateCommand = wingetCommand.Subcommands.First(c => c.Name == "generate");
		var versionOption = (Option<string>)generateCommand.Options.First(o => o.Aliases.Contains("-V"));
		var repoOption = (Option<string?>)generateCommand.Options.First(o => o.Aliases.Contains("-r"));
		var packageIdOption = (Option<string?>)generateCommand.Options.First(o => o.Aliases.Contains("-p"));
		var stagingOption = (Option<string?>)generateCommand.Options.First(o => o.Aliases.Contains("-s"));

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

			var wingetService = new KtsuBuild.Winget.WingetService(processRunner, logger);

			try
			{
				var options = new KtsuBuild.Winget.WingetOptions
				{
					Version = version,
					GitHubRepo = repo,
					PackageId = packageId,
					RootDirectory = workspace,
					OutputDirectory = Path.Combine(workspace, "winget"),
					StagingDirectory = staging ?? Path.Combine(workspace, "staging"),
				};

				var result = await wingetService.GenerateManifestsAsync(options, ct).ConfigureAwait(false);

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
		});

		// Upload subcommand
		var uploadCommand = wingetCommand.Subcommands.First(c => c.Name == "upload");
		var uploadVersionOption = (Option<string>)uploadCommand.Options.First(o => o.Aliases.Contains("-V"));

		uploadCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			string version = parseResult.GetValue(uploadVersionOption)!;

			logger.VerboseEnabled = verbose;

			var wingetService = new KtsuBuild.Winget.WingetService(processRunner, logger);

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
		});

		rootCommand.Subcommands.Add(wingetCommand);
	}
}
