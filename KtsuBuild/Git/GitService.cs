// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Git;

using KtsuBuild.Abstractions;
using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Implementation of Git operations.
/// </summary>
/// <param name="processRunner">The process runner.</param>
/// <param name="logger">The build logger.</param>
public class GitService(IProcessRunner processRunner, IBuildLogger logger) : IGitService
{
	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> GetTagsAsync(string workingDirectory, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);

		// Configure versionsort for prerelease handling
		string[] suffixes = ["-alpha", "-beta", "-rc", "-pre"];
		foreach (string suffix in suffixes)
		{
			await processRunner.RunAsync("git", $"config versionsort.suffix \"{suffix}\"", workingDirectory, cancellationToken).ConfigureAwait(false);
		}

		ProcessResult result = await processRunner.RunAsync("git", "tag --list --sort=-v:refname", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
		{
			return [];
		}

		return [.. result.StandardOutput
			.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Select(static s => s.Trim())
			.Where(static s => !string.IsNullOrEmpty(s))];
	}

	/// <inheritdoc/>
	public async Task<string> GetCurrentCommitHashAsync(string workingDirectory, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		ProcessResult result = await processRunner.RunAsync("git", "rev-parse HEAD", workingDirectory, cancellationToken).ConfigureAwait(false);
		return result.StandardOutput.Trim();
	}

	/// <inheritdoc/>
	public async Task<string?> GetTagCommitHashAsync(string workingDirectory, string tag, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(tag);
		ProcessResult result = await processRunner.RunAsync("git", $"rev-list -n 1 {tag}", workingDirectory, cancellationToken).ConfigureAwait(false);
		return result.Success ? result.StandardOutput.Trim() : null;
	}

	/// <inheritdoc/>
	public async Task<string?> GetRemoteUrlAsync(string workingDirectory, string remoteName = "origin", CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		ProcessResult result = await processRunner.RunAsync("git", $"remote get-url {remoteName}", workingDirectory, cancellationToken).ConfigureAwait(false);
		return result.Success ? result.StandardOutput.Trim() : null;
	}

	/// <inheritdoc/>
	public async Task<string> GetLineEndingAsync(string workingDirectory, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		LineEndingHelper helper = new(processRunner);
		return await helper.GetLineEndingAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> GetCommitMessagesAsync(string workingDirectory, string range, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(range);
		ProcessResult result = await processRunner.RunAsync("git", $"log --format=format:%s \"{range}\"", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
		{
			return [];
		}

		return [.. result.StandardOutput
			.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Select(static s => s.Trim())
			.Where(static s => !string.IsNullOrEmpty(s))];
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<CommitInfo>> GetCommitsAsync(string workingDirectory, string range, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(range);
		const string format = "%h|%s|%aN";
		ProcessResult result = await processRunner.RunAsync("git", $"log --pretty=format:\"{format}\" \"{range}\"", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
		{
			return [];
		}

		List<CommitInfo> commits = [];
		foreach (string line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static s => s.Trim()).Where(static s => !string.IsNullOrEmpty(s)))
		{
			string[] parts = line.Split('|');
			if (parts.Length >= 3)
			{
				commits.Add(new CommitInfo
				{
					Hash = parts[0],
					Subject = parts[1],
					Author = parts[2],
				});
			}
		}

		return commits;
	}

	/// <inheritdoc/>
	public async Task<string> GetDiffAsync(string workingDirectory, string range, string? pathSpec = null, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(range);
		string args = $"diff \"{range}\"";
		ProcessResult result;
		if (!string.IsNullOrEmpty(pathSpec))
		{
			args += $" -- \"{pathSpec}\"";
		}

		result = await processRunner.RunAsync("git", args, workingDirectory, cancellationToken).ConfigureAwait(false);
		return result.StandardOutput;
	}

	/// <inheritdoc/>
	public async Task<bool> IsCommitTaggedAsync(string workingDirectory, string commitHash, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(commitHash);
		ProcessResult result = await processRunner.RunAsync("git", "show-ref --tags -d", workingDirectory, cancellationToken).ConfigureAwait(false);
		return result.StandardOutput.Contains(commitHash);
	}

	/// <inheritdoc/>
	public async Task<string> GetFirstCommitAsync(string workingDirectory, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		ProcessResult result = await processRunner.RunAsync("git", "rev-list HEAD", workingDirectory, cancellationToken).ConfigureAwait(false);
		string[] commits = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
		return commits.Length > 0 ? commits[^1].Trim() : string.Empty;
	}

	/// <inheritdoc/>
	public async Task CreateAndPushTagAsync(string workingDirectory, string tagName, string commitHash, string message, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(tagName);
		Ensure.NotNull(commitHash);
		Ensure.NotNull(message);
		ProcessResult createResult = await processRunner.RunAsync("git", $"tag -a \"{tagName}\" \"{commitHash}\" -m \"{message}\"", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!createResult.Success)
		{
			throw new InvalidOperationException($"Failed to create tag: {createResult.StandardError}");
		}

		ProcessResult pushResult = await processRunner.RunAsync("git", $"push origin \"{tagName}\"", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!pushResult.Success)
		{
			throw new InvalidOperationException($"Failed to push tag: {pushResult.StandardError}");
		}
	}

	/// <inheritdoc/>
	public async Task StageFilesAsync(string workingDirectory, IEnumerable<string> files, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(files);
		string fileList = string.Join(" ", files.Select(static f => $"\"{f}\""));
		ProcessResult result = await processRunner.RunAsync("git", $"add {fileList}", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!result.Success)
		{
			throw new InvalidOperationException($"Failed to stage files: {result.StandardError}");
		}
	}

	/// <inheritdoc/>
	public async Task<string> CommitAsync(string workingDirectory, string message, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(message);
		ProcessResult result = await processRunner.RunAsync("git", $"commit -m \"{message}\"", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!result.Success)
		{
			throw new InvalidOperationException($"Failed to commit: {result.StandardError}");
		}

		return await GetCurrentCommitHashAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task PushAsync(string workingDirectory, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		ProcessResult result = await processRunner.RunAsync("git", "push", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!result.Success)
		{
			throw new InvalidOperationException($"Failed to push: {result.StandardError}");
		}
	}

	/// <inheritdoc/>
	public async Task<bool> HasUncommittedChangesAsync(string workingDirectory, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		ProcessResult result = await processRunner.RunAsync("git", "status --porcelain", workingDirectory, cancellationToken).ConfigureAwait(false);
		return !string.IsNullOrWhiteSpace(result.StandardOutput);
	}

	/// <inheritdoc/>
	public async Task<bool> HasStagedChangesAsync(string workingDirectory, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		// --cached limits the diff to staged changes, which is exactly what `git commit` would commit.
		// --name-only keeps the output empty when nothing is staged so we can detect a no-op commit.
		ProcessResult result = await processRunner.RunAsync("git", "diff --cached --name-only", workingDirectory, cancellationToken).ConfigureAwait(false);
		return !string.IsNullOrWhiteSpace(result.StandardOutput);
	}

	/// <inheritdoc/>
	public async Task SetIdentityAsync(string workingDirectory, string name, string email, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(name);
		Ensure.NotNull(email);
		logger.WriteInfo($"Configuring git user for this repository: {name} <{email}>");

		// --local, never --global. A CI runner's config is throwaway, so the scope made no
		// difference there, but `release` and `ci` are documented as runnable by hand, and --global
		// rewrote the developer's machine-wide identity so that every later commit in every
		// repository on that machine was authored by the build. --local is correct in CI too,
		// because the runner's checkout belongs to one job.
		ProcessResult nameResult = await processRunner.RunAsync("git", $"config --local user.name \"{name}\"", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!nameResult.Success)
		{
			throw new InvalidOperationException($"Failed to set git user name: {nameResult.StandardError}");
		}

		ProcessResult emailResult = await processRunner.RunAsync("git", $"config --local user.email \"{email}\"", workingDirectory, cancellationToken).ConfigureAwait(false);
		if (!emailResult.Success)
		{
			throw new InvalidOperationException($"Failed to set git user email: {emailResult.StandardError}");
		}
	}

	/// <inheritdoc/>
	public async Task<bool> EnsureIdentityAsync(string workingDirectory, string fallbackName, string fallbackEmail, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(fallbackName);
		Ensure.NotNull(fallbackEmail);

		// Scopeless reads, so an identity from any level counts: the repository's own config, the
		// developer's global config, or a system one. Whatever git would already attribute the
		// commit to is left in place, and only a checkout with nothing to attribute it to is written.
		string? existingName = await ReadConfigAsync(workingDirectory, "user.name", cancellationToken).ConfigureAwait(false);
		string? existingEmail = await ReadConfigAsync(workingDirectory, "user.email", cancellationToken).ConfigureAwait(false);

		if (existingName is not null && existingEmail is not null)
		{
			logger.WriteInfo($"Using the configured git user: {existingName} <{existingEmail}>");
			return false;
		}

		await SetIdentityAsync(workingDirectory, fallbackName, fallbackEmail, cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <summary>
	/// Reads a git config value, returning null when it is unset or empty.
	/// </summary>
	/// <param name="workingDirectory">The repository directory.</param>
	/// <param name="key">The config key to read.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The trimmed value, or null when the key resolves to nothing.</returns>
	/// <remarks>
	/// An unset key makes git exit with code 1 and no output, which is an answer rather than a
	/// failure, so the exit code is read as "no value" instead of being thrown on.
	/// </remarks>
	private async Task<string?> ReadConfigAsync(string workingDirectory, string key, CancellationToken cancellationToken)
	{
		ProcessResult result = await processRunner.RunAsync("git", $"config --get {key}", workingDirectory, cancellationToken).ConfigureAwait(false);
		string value = result.StandardOutput?.Trim() ?? string.Empty;
		return result.Success && value.Length > 0 ? value : null;
	}
}
