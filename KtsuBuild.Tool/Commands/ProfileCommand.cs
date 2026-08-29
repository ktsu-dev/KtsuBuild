// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

using System.CommandLine;
using KtsuBuild.Abstractions;
using KtsuBuild.Profile;

/// <summary>
/// Profile command for generating an organization's public profile README.
/// </summary>
#pragma warning disable CA1010 // System.CommandLine.Command implements IEnumerable for collection initializer support
public class ProfileCommand : Command
#pragma warning restore CA1010
{
	/// <summary>
	/// The inputs to a profile README generation run.
	/// </summary>
	/// <param name="Organization">The GitHub organization to profile.</param>
	/// <param name="TemplatePath">The path to the README template the tables are appended to.</param>
	/// <param name="OutputPath">The path the rendered README is written to.</param>
	/// <param name="PackagePrefix">The NuGet package prefix for the organization.</param>
	/// <param name="SdkPackage">The MSBuild SDK whose pinned version is reported and compared.</param>
	/// <param name="Exclude">Repositories to leave out of the tables.</param>
	/// <param name="Only">The only repositories to consider, or empty for all of them.</param>
	/// <param name="FallbackWorkflows">Workflow file names to try when a repository has no <c>dotnet.yml</c>.</param>
	/// <param name="Verbose">Whether to enable verbose logging.</param>
	public sealed record ProfileOptionsInput(
		string Organization,
		string TemplatePath,
		string OutputPath,
		string PackagePrefix,
		string SdkPackage,
		IReadOnlyList<string> Exclude,
		IReadOnlyList<string> Only,
		IReadOnlyList<string> FallbackWorkflows,
		bool Verbose);

	/// <summary>Gets the organization option.</summary>
	public static Option<string> OrganizationOption { get; } = new("--org", "-o")
	{
		Description = "The GitHub organization to profile",
		Required = true,
	};

	/// <summary>Gets the template path option.</summary>
	public static Option<string> TemplateOption { get; } = new("--template", "-t")
	{
		Description = "The README template the generated tables are appended to",
		DefaultValueFactory = _ => "./profile/README.template",
	};

	/// <summary>Gets the output path option.</summary>
	public static Option<string> OutputOption { get; } = new("--output")
	{
		Description = "Where to write the rendered README",
		DefaultValueFactory = _ => "./profile/README.md",
	};

	/// <summary>Gets the NuGet package prefix option.</summary>
	public static Option<string> PackagePrefixOption { get; } = new("--package-prefix")
	{
		Description = "The NuGet package prefix, so repo Extensions resolves to prefix.Extensions",
		DefaultValueFactory = _ => "ktsu",
	};

	/// <summary>Gets the SDK package option.</summary>
	public static Option<string> SdkPackageOption { get; } = new("--sdk-package")
	{
		Description = "The MSBuild SDK whose pinned version is reported and compared",
		DefaultValueFactory = _ => "ktsu.Sdk",
	};

	/// <summary>Gets the repository exclusion option.</summary>
	public static Option<string[]> ExcludeOption { get; } = new("--exclude")
	{
		Description = "A repository to leave out of the tables, repeatable",
		AllowMultipleArgumentsPerToken = true,
		DefaultValueFactory = _ => [],
	};

	/// <summary>Gets the repository filter option.</summary>
	public static Option<string[]> OnlyOption { get; } = new("--only")
	{
		Description = "Consider only this repository, repeatable",
		AllowMultipleArgumentsPerToken = true,
		DefaultValueFactory = _ => [],
	};

	/// <summary>Gets the fallback workflow option.</summary>
	public static Option<string[]> FallbackWorkflowsOption { get; } = new("--fallback-workflow")
	{
		Description = "A workflow file name to try when a repository has no dotnet.yml, repeatable",
		AllowMultipleArgumentsPerToken = true,
		DefaultValueFactory = _ => [],
	};

	/// <summary>
	/// Initializes a new instance of the <see cref="ProfileCommand"/> class.
	/// </summary>
	public ProfileCommand() : base("profile", "Organization profile generation") => Subcommands.Add(new ReadmeCommand());

	/// <summary>
	/// Creates the handler for the readme subcommand.
	/// </summary>
	/// <param name="processRunner">The process runner.</param>
	/// <param name="logger">The build logger.</param>
	/// <returns>The command handler action.</returns>
	public static Func<ProfileOptionsInput, CancellationToken, Task<int>> CreateReadmeHandler(
		IProcessRunner processRunner,
		IBuildLogger logger)
	{
		return async (input, cancellationToken) =>
		{
			logger.VerboseEnabled = input.Verbose;
			logger.WriteStepHeader($"Generating profile README for {input.Organization}");

#pragma warning disable CA1031 // Top-level command handler must catch all exceptions
			try
			{
				return await ExecuteAsync(processRunner, logger, input, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				logger.WriteWarning("Cancelled");
				return 1;
			}
			catch (FileNotFoundException exception)
			{
				logger.WriteError(exception.Message);
				return 1;
			}
			catch (Exception exception)
			{
				logger.WriteError($"Failed to generate profile README: {exception.Message}");
				return 1;
			}
#pragma warning restore CA1031
		};
	}

	private static async Task<int> ExecuteAsync(
		IProcessRunner processRunner,
		IBuildLogger logger,
		ProfileOptionsInput input,
		CancellationToken cancellationToken)
	{
		using HttpClient httpClient = new()
		{
			Timeout = TimeSpan.FromSeconds(30),
		};

		GitHubApiClient gitHub = new(processRunner, logger);
		NuGetCatalogClient nuGet = new(httpClient, logger);
		ProfileGenerator generator = new(new OrgProfileService(gitHub, nuGet, logger), gitHub, logger);

		ProfileOptions options = new()
		{
			Organization = input.Organization,
			PackagePrefix = input.PackagePrefix,
			SdkPackageId = input.SdkPackage,
			ExcludedRepositories = input.Exclude,
			OnlyRepositories = input.Only,
			FallbackWorkflowFileNames = input.FallbackWorkflows,
		};

		await generator.GenerateAsync(options, input.TemplatePath, input.OutputPath, cancellationToken).ConfigureAwait(false);

		return 0;
	}

#pragma warning disable CA1010
	private sealed class ReadmeCommand : Command
#pragma warning restore CA1010
	{
		public ReadmeCommand() : base("readme", "Generate the organization profile README")
		{
			Options.Add(OrganizationOption);
			Options.Add(TemplateOption);
			Options.Add(OutputOption);
			Options.Add(PackagePrefixOption);
			Options.Add(SdkPackageOption);
			Options.Add(ExcludeOption);
			Options.Add(OnlyOption);
			Options.Add(FallbackWorkflowsOption);
			Options.Add(GlobalOptions.Verbose);
		}
	}
}
