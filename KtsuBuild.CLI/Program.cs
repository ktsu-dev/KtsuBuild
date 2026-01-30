// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI;

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
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

		// Add global options
		rootCommand.AddGlobalOption(GlobalOptions.Workspace);
		rootCommand.AddGlobalOption(GlobalOptions.Configuration);
		rootCommand.AddGlobalOption(GlobalOptions.Verbose);

		// Add commands
		AddCiCommand(rootCommand, processRunner, logger);
		AddBuildCommand(rootCommand, processRunner, logger);
		AddReleaseCommand(rootCommand, processRunner, logger);
		AddVersionCommand(rootCommand, processRunner, logger);
		AddMetadataCommand(rootCommand, processRunner, logger);
		AddWingetCommand(rootCommand, processRunner, logger);

		// Build and invoke
		var parser = new CommandLineBuilder(rootCommand)
			.UseDefaults()
			.Build();

		return await parser.InvokeAsync(args).ConfigureAwait(false);
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
		command.SetHandler(
			CiCommand.CreateHandler(processRunner, logger),
			GlobalOptions.Workspace,
			GlobalOptions.Configuration,
			GlobalOptions.Verbose,
			GlobalOptions.DryRun);
		rootCommand.AddCommand(command);
	}

	private static void AddBuildCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		var command = new BuildCommand();
		command.SetHandler(
			BuildCommand.CreateHandler(processRunner, logger),
			GlobalOptions.Workspace,
			GlobalOptions.Configuration,
			GlobalOptions.Verbose);
		rootCommand.AddCommand(command);
	}

	private static void AddReleaseCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		var command = new ReleaseCommand();
		command.SetHandler(
			ReleaseCommand.CreateHandler(processRunner, logger),
			GlobalOptions.Workspace,
			GlobalOptions.Configuration,
			GlobalOptions.Verbose,
			GlobalOptions.DryRun);
		rootCommand.AddCommand(command);
	}

	private static void AddVersionCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		var versionCommand = new VersionCommand();

		// Show subcommand
		var showCommand = (Command)versionCommand.Children.First(c => c.Name == "show");
		showCommand.SetHandler(
			Commands.VersionCommand.CreateHandler(processRunner, logger),
			GlobalOptions.Workspace,
			GlobalOptions.Verbose);

		// Bump subcommand
		var bumpCommand = (Command)versionCommand.Children.First(c => c.Name == "bump");
		bumpCommand.SetHandler(
			Commands.VersionCommand.CreateHandler(processRunner, logger),
			GlobalOptions.Workspace,
			GlobalOptions.Verbose);

		// Create subcommand
		var createCommand = (Command)versionCommand.Children.First(c => c.Name == "create");
		createCommand.SetHandler(
			Commands.VersionCommand.CreateHandler(processRunner, logger),
			GlobalOptions.Workspace,
			GlobalOptions.Verbose);

		rootCommand.AddCommand(versionCommand);
	}

	private static void AddMetadataCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		var metadataCommand = new MetadataCommand();

		// Update subcommand
		var updateCommand = (Command)metadataCommand.Children.First(c => c.Name == "update");
		var noCommitOption = updateCommand.Options.First(o => o.Name == "no-commit");
		updateCommand.SetHandler(
			async (string workspace, bool verbose, bool noCommit, CancellationToken ct) =>
			{
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
						Environment.ExitCode = 0;
					}
					else
					{
						logger.WriteError($"Metadata update failed: {result.Error}");
						Environment.ExitCode = 1;
					}
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to update metadata: {ex.Message}");
					Environment.ExitCode = 1;
				}
			},
			GlobalOptions.Workspace,
			GlobalOptions.Verbose,
			(Option<bool>)noCommitOption);

		// License subcommand
		var licenseCommand = (Command)metadataCommand.Children.First(c => c.Name == "license");
		licenseCommand.SetHandler(
			async (string workspace, bool verbose, CancellationToken ct) =>
			{
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
					Environment.ExitCode = 0;
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to generate license: {ex.Message}");
					Environment.ExitCode = 1;
				}
			},
			GlobalOptions.Workspace,
			GlobalOptions.Verbose);

		// Changelog subcommand
		var changelogCommand = (Command)metadataCommand.Children.First(c => c.Name == "changelog");
		changelogCommand.SetHandler(
			async (string workspace, bool verbose, CancellationToken ct) =>
			{
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
					Environment.ExitCode = 0;
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to generate changelog: {ex.Message}");
					Environment.ExitCode = 1;
				}
			},
			GlobalOptions.Workspace,
			GlobalOptions.Verbose);

		rootCommand.AddCommand(metadataCommand);
	}

	private static void AddWingetCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		var wingetCommand = new WingetCommand();

		// Generate subcommand
		var generateCommand = (Command)wingetCommand.Children.First(c => c.Name == "generate");
		var versionOption = generateCommand.Options.First(o => o.Aliases.Contains("-V"));
		var repoOption = generateCommand.Options.First(o => o.Aliases.Contains("-r"));
		var packageIdOption = generateCommand.Options.First(o => o.Aliases.Contains("-p"));
		var stagingOption = generateCommand.Options.First(o => o.Aliases.Contains("-s"));

		generateCommand.SetHandler(
			async (string workspace, bool verbose, string version, string? repo, string? packageId, string? staging, CancellationToken ct) =>
			{
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
						Environment.ExitCode = 0;
					}
					else if (result.Success)
					{
						logger.WriteSuccess($"Generated manifests for {result.PackageId}");
						Environment.ExitCode = 0;
					}
					else
					{
						logger.WriteError($"Failed to generate manifests: {result.Error}");
						Environment.ExitCode = 1;
					}
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to generate manifests: {ex.Message}");
					Environment.ExitCode = 1;
				}
			},
			GlobalOptions.Workspace,
			GlobalOptions.Verbose,
			(Option<string>)versionOption,
			(Option<string?>)repoOption,
			(Option<string?>)packageIdOption,
			(Option<string?>)stagingOption);

		// Upload subcommand
		var uploadCommand = (Command)wingetCommand.Children.First(c => c.Name == "upload");
		var uploadVersionOption = uploadCommand.Options.First(o => o.Aliases.Contains("-V"));

		uploadCommand.SetHandler(
			async (string workspace, bool verbose, string version, CancellationToken ct) =>
			{
				logger.VerboseEnabled = verbose;

				var wingetService = new KtsuBuild.Winget.WingetService(processRunner, logger);

				try
				{
					string manifestDir = Path.Combine(workspace, "winget");
					await wingetService.UploadManifestsAsync(version, manifestDir, ct).ConfigureAwait(false);

					logger.WriteSuccess("Manifests uploaded!");
					Environment.ExitCode = 0;
				}
				catch (Exception ex)
				{
					logger.WriteError($"Failed to upload manifests: {ex.Message}");
					Environment.ExitCode = 1;
				}
			},
			GlobalOptions.Workspace,
			GlobalOptions.Verbose,
			(Option<string>)uploadVersionOption);

		rootCommand.AddCommand(wingetCommand);
	}
}
