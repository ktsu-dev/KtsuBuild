// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

using System.CommandLine;
using System.Globalization;
using KtsuBuild.Abstractions;
using KtsuBuild.Profile;
using KtsuBuild.Utilities;

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
	/// <param name="WingetPublisher">The winget publisher whose manifests are searched.</param>
	/// <param name="Exclude">Repositories to leave out of the tables.</param>
	/// <param name="Only">The only repositories to consider, or empty for all of them.</param>
	/// <param name="FallbackWorkflows">Workflow file names to try when a repository has no <c>dotnet.yml</c>.</param>
	/// <param name="Verbose">Whether to enable verbose logging.</param>
	public sealed record ProfileOptionsInput(
		string Organization,
		string TemplatePath,
		string OutputPath,
		string PackagePrefix,
		string WingetPublisher,
		IReadOnlyList<string> Exclude,
		IReadOnlyList<string> Only,
		IReadOnlyList<string> FallbackWorkflows,
		bool Verbose);

	/// <summary>Gets the organization option.</summary>
	public static Option<string> Organization { get; } = new("--org", "-o")
	{
		Description = "The GitHub organization to profile",
		Required = true,
	};

	/// <summary>Gets the template path option.</summary>
	public static Option<string> Template { get; } = new("--template", "-t")
	{
		Description = "The README template the generated tables are appended to",
		DefaultValueFactory = _ => "./profile/README.template",
	};

	/// <summary>Gets the output path option.</summary>
	public static Option<string> Output { get; } = new("--output")
	{
		Description = "Where to write the rendered README",
		DefaultValueFactory = _ => "./profile/README.md",
	};

	/// <summary>Gets the NuGet package prefix option.</summary>
	public static Option<string> PackagePrefix { get; } = new("--package-prefix")
	{
		Description = "The NuGet package prefix, so repo Extensions resolves to prefix.Extensions",
		DefaultValueFactory = _ => "ktsu",
	};

	/// <summary>Gets the winget publisher option.</summary>
	public static Option<string> WingetPublisher { get; } = new("--winget-publisher")
	{
		Description = "The winget publisher whose manifests are searched for shipped applications",
		DefaultValueFactory = _ => "ktsu",
	};

	/// <summary>Gets the repository exclusion option.</summary>
	public static Option<string[]> Exclude { get; } = new("--exclude")
	{
		Description = "A repository to leave out of the tables, repeatable",
		AllowMultipleArgumentsPerToken = true,
		DefaultValueFactory = _ => [],
	};

	/// <summary>Gets the repository filter option.</summary>
	public static Option<string[]> Only { get; } = new("--only")
	{
		Description = "Consider only this repository, repeatable",
		AllowMultipleArgumentsPerToken = true,
		DefaultValueFactory = _ => [],
	};

	/// <summary>Gets the fallback workflow option.</summary>
	public static Option<string[]> FallbackWorkflows { get; } = new("--fallback-workflow")
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
		if (!File.Exists(input.TemplatePath))
		{
			logger.WriteError($"Template not found: {input.TemplatePath}");
			return 1;
		}

		string template = await File.ReadAllTextAsync(input.TemplatePath, cancellationToken).ConfigureAwait(false);

		using HttpClient httpClient = new()
		{
			Timeout = TimeSpan.FromSeconds(30),
		};

		GitHubApiClient gitHub = new(processRunner, logger);
		NuGetCatalogClient nuGet = new(httpClient, logger);
		OrgProfileService service = new(gitHub, nuGet, logger);

		ProfileOptions options = new()
		{
			Organization = input.Organization,
			PackagePrefix = input.PackagePrefix,
			WingetPublisher = input.WingetPublisher,
			ExcludedRepositories = input.Exclude,
			OnlyRepositories = input.Only,
			FallbackWorkflowFileNames = input.FallbackWorkflows,
		};

		IReadOnlyList<RepoFacts> facts = await service.GatherAsync(options, cancellationToken).ConfigureAwait(false);
		string rendered = ProfileRenderer.Render(template, facts);

		string? directory = Path.GetDirectoryName(Path.GetFullPath(input.OutputPath));
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		// LF regardless of platform, because Git normalizes the committed blob to LF anyway and a
		// stable byte sequence keeps the daily commit empty when nothing changed.
		await LineEndingHelper.WriteFileAsync(input.OutputPath, rendered, "\n", cancellationToken).ConfigureAwait(false);

		logger.WriteSuccess($"Wrote {input.OutputPath} with {facts.Count.ToString(CultureInfo.InvariantCulture)} repositories");
		return 0;
	}

#pragma warning disable CA1010
	private sealed class ReadmeCommand : Command
#pragma warning restore CA1010
	{
		public ReadmeCommand() : base("readme", "Generate the organization profile README")
		{
			Options.Add(Organization);
			Options.Add(Template);
			Options.Add(Output);
			Options.Add(PackagePrefix);
			Options.Add(WingetPublisher);
			Options.Add(Exclude);
			Options.Add(Only);
			Options.Add(FallbackWorkflows);
			Options.Add(GlobalOptions.Verbose);
		}
	}
}
