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
/// <param name="logger">The build logger.</param>
/// <remarks>
/// Lives in the library rather than in the command so it can be tested. The command is left with
/// argument parsing and mapping failures to an exit code.
/// </remarks>
public class ProfileGenerator(OrgProfileService service, IBuildLogger logger)
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
}
