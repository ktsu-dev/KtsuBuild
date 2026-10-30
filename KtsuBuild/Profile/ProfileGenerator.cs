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
	/// Renders the profile README and writes it to disk, or reports its rows without writing anything.
	/// </summary>
	/// <param name="options">The gathering settings.</param>
	/// <param name="templatePath">The template the generated table is appended to.</param>
	/// <param name="outputPath">Where to write the rendered README, or <see langword="null"/> to report
	/// the rows through the logger and leave every file alone. A preview of a few repositories passes
	/// null, because writing it would replace the whole table with those few rows.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The repositories that were listed.</returns>
	/// <exception cref="FileNotFoundException">The template does not exist. Writing a README without
	/// it would silently discard everything the profile page says about the organization.</exception>
	/// <exception cref="InvalidOperationException">The organization could not be listed, or listed no
	/// public repositories, so the README would be written without its table. Or the template links a
	/// repository that is archived or no longer public. Publishing a page that promotes retired work is
	/// worse than failing loudly.</exception>
	public async Task<IReadOnlyList<RepoFacts>> GenerateAsync(
		ProfileOptions options,
		string templatePath,
		string? outputPath,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);
		Ensure.NotNull(templatePath);

		if (!File.Exists(templatePath))
		{
			throw new FileNotFoundException($"Profile template not found: {templatePath}", templatePath);
		}

		string template = await File.ReadAllTextAsync(templatePath, cancellationToken).ConfigureAwait(false);

		await CheckTemplateLinksAsync(template, options, cancellationToken).ConfigureAwait(false);

		IReadOnlyList<RepoFacts> facts = await service.GatherAsync(options, cancellationToken).ConfigureAwait(false);

		if (outputPath is null)
		{
			foreach (RepoFacts repository in facts)
			{
				logger.WriteInfo(ProfileRenderer.RenderRow(repository).TrimEnd('\n'));
			}

			logger.WriteSuccess($"Previewed {DescribeCount(facts.Count)} without writing a README");
			return facts;
		}

		string rendered = ProfileRenderer.Render(template, facts);

		string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		// LF regardless of platform, because Git normalizes the committed blob to LF anyway and a
		// stable byte sequence keeps the daily commit empty when nothing changed.
		await LineEndingHelper.WriteFileAsync(outputPath, rendered, "\n", cancellationToken).ConfigureAwait(false);

		logger.WriteSuccess($"Wrote {outputPath} with {DescribeCount(facts.Count)}");

		return facts;
	}

	private static string DescribeCount(int count) =>
		$"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "repository" : "repositories")}";

	/// <summary>
	/// Fails the run when the template promotes a repository that has been retired.
	/// </summary>
	/// <param name="template">The template content.</param>
	/// <param name="options">The gathering settings.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <exception cref="InvalidOperationException">The organization listed no public repositories, or a
	/// linked repository is archived or no longer public.</exception>
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

		// An organization with no public repositories has no profile table to show, so an empty listing
		// is far more likely an outage than the truth. Writing it would wipe the table from the page.
		if (repositories.Count == 0)
		{
			throw new InvalidOperationException(
				$"{options.Organization} listed no public repositories, so the README was not written.");
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
