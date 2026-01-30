// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Publishing;

using KtsuBuild.Abstractions;
using static Polyfill;

/// <summary>
/// Implementation of NuGet package publishing operations.
/// </summary>
/// <param name="processRunner">The process runner.</param>
/// <param name="logger">The build logger.</param>
public class NuGetPublisher(IProcessRunner processRunner, IBuildLogger logger) : INuGetPublisher
{
	/// <inheritdoc/>
	public async Task PublishToGitHubAsync(string packagePattern, string owner, string token, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(packagePattern);
		Ensure.NotNull(owner);
		Ensure.NotNull(token);
		logger.WriteStepHeader("Publishing to GitHub Packages");

		string source = $"https://nuget.pkg.github.com/{owner}/index.json";
		await PublishAsync(packagePattern, source, token, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task PublishToNuGetOrgAsync(string packagePattern, string apiKey, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(packagePattern);
		Ensure.NotNull(apiKey);
		logger.WriteStepHeader("Publishing to NuGet.org");

		string source = "https://api.nuget.org/v3/index.json";
		await PublishAsync(packagePattern, source, apiKey, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task PublishToSourceAsync(string packagePattern, string source, string apiKey, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(packagePattern);
		Ensure.NotNull(source);
		Ensure.NotNull(apiKey);
		logger.WriteStepHeader($"Publishing to {source}");
		await PublishAsync(packagePattern, source, apiKey, cancellationToken).ConfigureAwait(false);
	}

	private async Task PublishAsync(string packagePattern, string source, string apiKey, CancellationToken cancellationToken)
	{
		string args = $"nuget push \"{packagePattern}\" --api-key \"{apiKey}\" --source \"{source}\" --skip-duplicate";

		int exitCode = await processRunner.RunWithCallbackAsync(
			"dotnet",
			args,
			null,
			line => logger.WriteInfo(line),
			line => logger.WriteError(line),
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			throw new InvalidOperationException($"Package publish to {source} failed with exit code {exitCode}");
		}
	}
}
