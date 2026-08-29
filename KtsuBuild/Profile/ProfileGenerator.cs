// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Globalization;
using KtsuBuild.Abstractions;
using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Generates an organization's profile README, from gathering the facts to writing the file.
/// </summary>
/// <param name="service">The service that gathers repository facts.</param>
/// <param name="gitHub">The GitHub API client, used to check the template's links.</param>
/// <param name="logger">The build logger.</param>
/// <remarks>
/// Lives in the library rather than in the command so it can be tested. The command is left with
/// argument parsing and mapping failures to an exit code.
/// </remarks>
public class ProfileGenerator(OrgProfileService service, IGitHubApiClient gitHub, IBuildLogger logger)
{
	/// <summary>
	/// Renders the profile README and writes it to disk.
	/// </summary>
	/// <param name="options">The gathering settings.</param>
	/// <param name="templatePath">The template the generated table is appended to.</param>
	/// <param name="outputPath">Where to write the rendered README.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The repositories that were listed.</returns>
	/// <exception cref="FileNotFoundException">The template does not exist. Writing a README without
	/// it would silently discard everything the profile page says about the organization.</exception>
	/// <exception cref="InvalidOperationException">The template links a repository that is archived or
	/// no longer public. Publishing a page that promotes retired work is worse than failing loudly.</exception>
	public async Task<IReadOnlyList<RepoFacts>> GenerateAsync(
		ProfileOptions options,
		string templatePath,
		string outputPath,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);
		Ensure.NotNull(templatePath);
		Ensure.NotNull(outputPath);

		if (!File.Exists(templatePath))
		{
			throw new FileNotFoundException($"Profile template not found: {templatePath}", templatePath);
		}

		string template = await File.ReadAllTextAsync(templatePath, cancellationToken).ConfigureAwait(false);

		await CheckTemplateLinksAsync(template, options, cancellationToken).ConfigureAwait(false);

		IReadOnlyList<RepoFacts> facts = await service.GatherAsync(options, cancellationToken).ConfigureAwait(false);
		string rendered = ProfileRenderer.Render(template, facts);

		string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		// LF regardless of platform, because Git normalizes the committed blob to LF anyway and a
		// stable byte sequence keeps the daily commit empty when nothing changed.
		await LineEndingHelper.WriteFileAsync(outputPath, rendered, "\n", cancellationToken).ConfigureAwait(false);

		logger.WriteSuccess($"Wrote {outputPath} with {facts.Count.ToString(CultureInfo.InvariantCulture)} {(facts.Count == 1 ? "repository" : "repositories")}");

		return facts;
	}

	/// <summary>
	/// Fails the run when the template promotes a repository that has been retired.
	/// </summary>
	/// <param name="template">The template content.</param>
	/// <param name="options">The gathering settings.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <exception cref="InvalidOperationException">A linked repository is archived or no longer public.</exception>
	/// <remarks>
	/// This costs one listing request that the gather then makes again. That duplication buys a clean
	/// split, where the generator owns the template and the service owns the facts, for about half a
	/// percent of the run's requests.
	/// </remarks>
	private async Task CheckTemplateLinksAsync(string template, ProfileOptions options, CancellationToken cancellationToken)
	{
		IReadOnlyList<GitHubRepository> repositories = await gitHub
			.ListOrganizationRepositoriesAsync(options.Organization, cancellationToken)
			.ConfigureAwait(false);

		if (repositories.Count == 0)
		{
			logger.WriteWarning("  Could not list the organization, so the template's links went unchecked");
			return;
		}

		IReadOnlyList<string> retired = TemplateLinks.FindRetired(template, options.Organization, repositories);
		if (retired.Count == 0)
		{
			return;
		}

		string subject = retired.Count == 1
			? "1 repository that is archived or no longer public"
			: $"{retired.Count.ToString(CultureInfo.InvariantCulture)} repositories that are archived or no longer public";

		throw new InvalidOperationException(
			$"The profile template links {subject}: {string.Join(", ", retired)}. " +
			"Remove each entry, or point it at whatever replaced it.");
	}
}
