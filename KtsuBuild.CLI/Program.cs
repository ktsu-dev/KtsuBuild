// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.CLI;

using System.CommandLine;
using System.Runtime.InteropServices;
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
		AddIosCommand(rootCommand, processRunner, logger);

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

	private static void AddIosCommand(RootCommand rootCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		IosCommand iosCommand = new();

		AddIosBuildSubcommand(iosCommand, processRunner, logger);
		AddIosPackageSubcommand(iosCommand, processRunner, logger);
		AddIosUploadSubcommand(iosCommand, processRunner, logger);

		rootCommand.Subcommands.Add(iosCommand);
	}

	private static void AddIosBuildSubcommand(IosCommand iosCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		Command buildCommand = iosCommand.Subcommands.First(c => c.Name == "build");
		Option<string?> projectOption = (Option<string?>)buildCommand.Options.First(o => o.Aliases.Contains("-p"));
		Option<string?> runtimeOption = (Option<string?>)buildCommand.Options.First(o => o.Aliases.Contains("-r"));
		Option<string[]> requireFrameworkOption = (Option<string[]>)buildCommand.Options.First(o => o.Name == "--require-framework");

		buildCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			string configuration = parseResult.GetValue(GlobalOptions.Configuration)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			string? project = parseResult.GetValue(projectOption);
			string? runtime = parseResult.GetValue(runtimeOption);
			string[] requireFrameworks = parseResult.GetValue(requireFrameworkOption) ?? [];

			logger.VerboseEnabled = verbose;
			logger.WriteStepHeader("Building iOS Head(s)");

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				// iOS heads build only on macOS (the workload and the Xcode toolchain
				// are macOS-only). Skip cleanly elsewhere so the command is safe to call
				// unconditionally from a consumer workflow.
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					logger.WriteInfo("iOS builds require a macOS host. Skipping iOS build on this platform.");
					return 0;
				}

				KtsuBuild.DotNet.DotNetService dotNetService = new(processRunner, logger);
				KtsuBuild.Ios.IosBuildService iosBuildService = new(dotNetService, logger);

				bool success = await iosBuildService.BuildAsync(new KtsuBuild.Ios.IosBuildOptions
				{
					WorkingDirectory = workspace,
					Configuration = configuration,
					Project = project,
					Runtime = runtime,
					RequiredFrameworks = requireFrameworks,
				}, ct).ConfigureAwait(false);

				if (!success)
				{
					return 1;
				}

				logger.WriteSuccess("iOS build(s) completed successfully!");
				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"iOS build failed: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});
	}

	private static void AddIosPackageSubcommand(IosCommand iosCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		Command packageCommand = iosCommand.Subcommands.First(c => c.Name == "package");
		Option<string?> packageProjectOption = (Option<string?>)packageCommand.Options.First(o => o.Aliases.Contains("-p"));
		Option<string?> packageRuntimeOption = (Option<string?>)packageCommand.Options.First(o => o.Aliases.Contains("-r"));
		Option<string?> packageFrameworkOption = (Option<string?>)packageCommand.Options.First(o => o.Aliases.Contains("-f"));
		Option<string?> packageVersionOption = (Option<string?>)packageCommand.Options.First(o => o.Aliases.Contains("-V"));
		Option<string?> packageBuildNumberOption = (Option<string?>)packageCommand.Options.First(o => o.Aliases.Contains("-b"));

		packageCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			string configuration = parseResult.GetValue(GlobalOptions.Configuration)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			string? project = parseResult.GetValue(packageProjectOption);
			string? runtime = parseResult.GetValue(packageRuntimeOption);
			string? framework = parseResult.GetValue(packageFrameworkOption);
			string? version = parseResult.GetValue(packageVersionOption);
			string? buildNumber = parseResult.GetValue(packageBuildNumberOption);

			logger.VerboseEnabled = verbose;
			logger.WriteStepHeader("Packaging iOS Head(s)");

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				// iOS packaging builds only on macOS. Skip cleanly elsewhere so the command
				// is safe to call unconditionally from a consumer workflow, before touching
				// git or the signing material.
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					logger.WriteInfo("iOS packaging requires a macOS host. Skipping iOS packaging on this platform.");
					return 0;
				}

				KtsuBuild.Git.GitService gitService = new(processRunner, logger);
				KtsuBuild.Publishing.GitHubService gitHubService = new(processRunner, gitService, logger);
				KtsuBuild.Configuration.BuildConfigurationProvider configProvider = new(gitService, gitHubService);
				KtsuBuild.Configuration.BuildConfiguration buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, ct).ConfigureAwait(false);

				// Resolve the marketing version (KtsuBuild's computed version unless overridden)
				// and the monotonic build number (CI run number unless overridden).
				if (string.IsNullOrEmpty(version))
				{
					KtsuBuild.Git.VersionCalculator versionCalculator = new(gitService, logger);
					string commitHash = await gitService.GetCurrentCommitHashAsync(workspace, ct).ConfigureAwait(false);
					KtsuBuild.Git.VersionInfo versionInfo = await versionCalculator.GetVersionInfoAsync(workspace, commitHash, cancellationToken: ct).ConfigureAwait(false);
					version = versionInfo.Version;
				}

				if (string.IsNullOrEmpty(buildNumber))
				{
					buildNumber = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
					if (string.IsNullOrEmpty(buildNumber))
					{
						buildNumber = "1";
					}
				}

				logger.WriteInfo($"iOS signing available: {buildConfig.IosSigningAvailable}");

				KtsuBuild.DotNet.DotNetService dotNetService = new(processRunner, logger);
				KtsuBuild.Ios.IosService iosService = new(dotNetService, processRunner, logger);

				KtsuBuild.Ios.IosPackageResult result = await iosService.PackageAsync(new KtsuBuild.Ios.IosPackageOptions
				{
					WorkingDirectory = workspace,
					Configuration = configuration,
					Project = project,
					Runtime = string.IsNullOrEmpty(runtime) ? "ios-arm64" : runtime,
					Framework = framework,
					ShortVersion = version,
					BuildNumber = buildNumber,
					SigningAvailable = buildConfig.IosSigningAvailable,
					CodesignKey = buildConfig.IosCodesignKey,
					ProvisionName = buildConfig.IosProvisionName,
					CertificateP12Base64 = buildConfig.IosCertP12Base64,
					CertificatePassword = buildConfig.IosCertP12Password,
					KeychainPassword = buildConfig.IosKeychainPassword,
					ProvisioningProfileBase64 = buildConfig.IosProvisioningProfileBase64,
					XcodeVersion = buildConfig.XcodeVersion,
					WorkloadVersion = buildConfig.IosWorkloadVersion,
				}, ct).ConfigureAwait(false);

				if (result.Skipped)
				{
					logger.WriteInfo(result.SkipReason ?? "iOS packaging was skipped.");
					return 0;
				}

				if (!result.Success)
				{
					logger.WriteError($"iOS packaging failed: {result.Error}");
					return 1;
				}

				if (result.IpaPaths.Count == 0)
				{
					logger.WriteInfo("iOS packaging completed with no archives (no iOS heads found).");
					return 0;
				}

				logger.WriteSuccess($"Packaged {result.IpaPaths.Count} iOS archive(s):");
				foreach (string ipa in result.IpaPaths)
				{
					logger.WriteInfo($"  - {ipa}");
				}

				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"iOS packaging failed: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});
	}

	private static void AddIosUploadSubcommand(IosCommand iosCommand, IProcessRunner processRunner, IBuildLogger logger)
	{
		Command uploadCommand = iosCommand.Subcommands.First(c => c.Name == "upload");
		Option<string?> uploadProjectOption = (Option<string?>)uploadCommand.Options.First(o => o.Aliases.Contains("-p"));
		Option<string?> uploadIpaOption = (Option<string?>)uploadCommand.Options.First(o => o.Aliases.Contains("-i"));

		uploadCommand.SetAction(async (parseResult, ct) =>
		{
			string workspace = parseResult.GetValue(GlobalOptions.Workspace)!;
			string configuration = parseResult.GetValue(GlobalOptions.Configuration)!;
			bool verbose = parseResult.GetValue(GlobalOptions.Verbose);
			string? project = parseResult.GetValue(uploadProjectOption);
			string? ipa = parseResult.GetValue(uploadIpaOption);

			logger.VerboseEnabled = verbose;
			logger.WriteStepHeader("Uploading iOS Head(s) to TestFlight");

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				// The upload runs altool, which is macOS-only. Skip cleanly elsewhere so the
				// command is safe to call unconditionally, before touching the signing material.
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					logger.WriteInfo("iOS upload requires a macOS host. Skipping iOS upload on this platform.");
					return 0;
				}

				KtsuBuild.Git.GitService gitService = new(processRunner, logger);
				KtsuBuild.Publishing.GitHubService gitHubService = new(processRunner, gitService, logger);
				KtsuBuild.Configuration.BuildConfigurationProvider configProvider = new(gitService, gitHubService);
				KtsuBuild.Configuration.BuildConfiguration buildConfig = await configProvider.CreateFromEnvironmentAsync(workspace, ct).ConfigureAwait(false);

				logger.WriteInfo($"iOS signing available: {buildConfig.IosSigningAvailable}");

				KtsuBuild.DotNet.DotNetService dotNetService = new(processRunner, logger);
				KtsuBuild.Ios.IosService iosService = new(dotNetService, processRunner, logger);

				KtsuBuild.Ios.IosUploadResult result = await iosService.UploadAsync(new KtsuBuild.Ios.IosUploadOptions
				{
					WorkingDirectory = workspace,
					Configuration = configuration,
					Project = project,
					IpaPath = ipa,
					SigningAvailable = buildConfig.IosSigningAvailable,
					AppStoreConnectKeyBase64 = buildConfig.AppStoreConnectKeyBase64,
					AppStoreConnectKeyId = buildConfig.AppStoreConnectKeyId,
					AppStoreConnectIssuerId = buildConfig.AppStoreConnectIssuerId,
				}, ct).ConfigureAwait(false);

				if (result.Skipped)
				{
					logger.WriteInfo(result.SkipReason ?? "iOS upload was skipped.");
					return 0;
				}

				if (!result.Success)
				{
					logger.WriteError($"iOS upload failed: {result.Error}");
					return 1;
				}

				if (result.UploadedIpaPaths.Count == 0)
				{
					logger.WriteInfo("iOS upload completed with no archives (no .ipa found to upload).");
					return 0;
				}

				logger.WriteSuccess($"Uploaded {result.UploadedIpaPaths.Count} iOS archive(s) to TestFlight:");
				foreach (string uploaded in result.UploadedIpaPaths)
				{
					logger.WriteInfo($"  - {uploaded}");
				}

				return 0;
			}
			catch (Exception ex)
			{
				logger.WriteError($"iOS upload failed: {ex.Message}");
				return 1;
			}
#pragma warning restore CA1031
		});
	}
}
