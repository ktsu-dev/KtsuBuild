// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Ios;

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using KtsuBuild.Abstractions;
using KtsuBuild.Utilities;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Orchestrates the signed iOS packaging path: it provisions the toolchain (Xcode +
/// pinned iOS workload), stamps the version into the head's <c>Info.plist</c>, imports
/// the distribution certificate and provisioning profile into a temporary keychain,
/// and archives a signed <c>.ipa</c>. This is the release path; it no-ops cleanly when
/// the signing material is unavailable or the host is not macOS.
/// </summary>
/// <param name="dotNetService">The .NET SDK service, used to resolve iOS heads.</param>
/// <param name="processRunner">The process runner.</param>
/// <param name="logger">The build logger.</param>
public class IosService(IDotNetService dotNetService, IProcessRunner processRunner, IBuildLogger logger) : IIosService
{
	/// <summary>
	/// The temporary keychain the distribution certificate is imported into. The CI
	/// runner is ephemeral, so the keychain lives only for the run.
	/// </summary>
	public const string SigningKeychain = "build.keychain";

#pragma warning disable SYSLIB1045 // GeneratedRegex not available in netstandard2.0/2.1
	private static readonly Regex TargetFrameworkRegex = new(@"<TargetFrameworks?>\s*([^<]+?)\s*</TargetFrameworks?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
#pragma warning restore SYSLIB1045

	/// <inheritdoc/>
	public async Task<IosPackageResult> PackageAsync(IosPackageOptions options, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);

		// Gate on the signing material. Forks and contributors without secrets get a
		// clean no-op rather than a failure, exactly like MeltdownMonitor's check-secrets
		// job. Only the boolean is ever logged; the secret values never are.
		if (!options.SigningAvailable)
		{
			string reason = "iOS signing material is not available. Skipping the package step.";
			logger.WriteInfo(reason);
			return new IosPackageResult { Success = true, Skipped = true, SkipReason = reason };
		}

		// iOS archives only on macOS (the Xcode toolchain and the iOS workload are
		// macOS-only). Skip cleanly elsewhere so the command is safe to call
		// unconditionally from a consumer workflow.
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			string reason = "iOS packaging requires a macOS host. Skipping the package step on this platform.";
			logger.WriteInfo(reason);
			return new IosPackageResult { Success = true, Skipped = true, SkipReason = reason };
		}

		IReadOnlyList<string> heads = string.IsNullOrEmpty(options.Project)
			? dotNetService.GetIosHeads(options.WorkingDirectory)
			: [options.Project];

		if (heads.Count == 0)
		{
			logger.WriteInfo("No iOS heads found in workspace. Nothing to package.");
			return new IosPackageResult { Success = true };
		}

		// Provision the toolchain and import the signing material once, then archive each
		// head against it.
		await ProvisionToolchainAsync(options.XcodeVersion, options.WorkloadVersion, cancellationToken).ConfigureAwait(false);
		await SetupSigningAsync(options, cancellationToken).ConfigureAwait(false);

		List<string> ipas = [];
		foreach (string head in heads)
		{
			logger.WriteInfo($"Packaging iOS head: {head}");

			string? plist = string.IsNullOrEmpty(options.InfoPlistPath) ? ResolveInfoPlist(head) : options.InfoPlistPath;
			if (!string.IsNullOrEmpty(plist) && File.Exists(plist))
			{
				await StampVersionAsync(plist!, options.ShortVersion, options.BuildNumber, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				logger.WriteWarning($"No Info.plist found for {head}; skipping version stamping. The archive will use the version baked into the project.");
			}

			string? framework = string.IsNullOrEmpty(options.Framework) ? ResolveIosFramework(head) : options.Framework;
			string ipa = await ArchiveAsync(options.WorkingDirectory, head, options.Runtime, options.Configuration, framework, options.CodesignKey, options.ProvisionName, cancellationToken).ConfigureAwait(false);
			ipas.Add(ipa);
		}

		return new IosPackageResult { Success = true, IpaPaths = ipas };
	}

	/// <summary>
	/// Provisions the iOS toolchain: selects the pinned Xcode (when a version is given)
	/// and installs the pinned iOS workload via a rollback file (when a version is given).
	/// Either step is skipped when its pin is empty, leaving the host default in place.
	/// </summary>
	/// <param name="xcodeVersion">The pinned Xcode version, or empty to keep the host default.</param>
	/// <param name="workloadVersion">The pinned iOS workload version (for example <c>26.2.10233/10.0.100</c>), or empty to skip installation.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public async Task ProvisionToolchainAsync(string xcodeVersion, string workloadVersion, CancellationToken cancellationToken = default)
	{
		if (!string.IsNullOrWhiteSpace(xcodeVersion))
		{
			await SelectXcodeAsync(xcodeVersion, cancellationToken).ConfigureAwait(false);
		}

		if (!string.IsNullOrWhiteSpace(workloadVersion))
		{
			await InstallIosWorkloadAsync(workloadVersion, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Stamps the marketing version and build number into a head's <c>Info.plist</c> with
	/// <c>PlistBuddy</c>. Editing the plist directly is authoritative for a plain
	/// <c>Microsoft.NET.Sdk</c> iOS head: an explicit <c>Info.plist</c> value wins over the
	/// <c>ApplicationVersion</c>/<c>ApplicationDisplayVersion</c> MSBuild properties, which
	/// only take effect for MAUI single-project apps.
	/// </summary>
	/// <param name="infoPlistPath">The path to the head's <c>Info.plist</c>.</param>
	/// <param name="shortVersion">The marketing version for <c>CFBundleShortVersionString</c>.</param>
	/// <param name="buildNumber">The monotonic build number for <c>CFBundleVersion</c>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public async Task StampVersionAsync(string infoPlistPath, string shortVersion, string buildNumber, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(infoPlistPath);
		Ensure.NotNull(shortVersion);
		Ensure.NotNull(buildNumber);
		logger.WriteInfo($"Stamping {infoPlistPath}: marketing version {shortVersion}, build {buildNumber}.");

		await SetOrAddPlistEntryAsync(infoPlistPath, "CFBundleShortVersionString", shortVersion, cancellationToken).ConfigureAwait(false);
		await SetOrAddPlistEntryAsync(infoPlistPath, "CFBundleVersion", buildNumber, cancellationToken).ConfigureAwait(false);
	}

	private async Task SetOrAddPlistEntryAsync(string infoPlistPath, string key, string value, CancellationToken cancellationToken)
	{
		// PlistBuddy's Set fails ("Does Not Exist") when the key is absent, so fall back to
		// Add for a plist that lacks the entry (for example a head that relies on the
		// MSBuild version properties rather than an authored value).
		ProcessResult set = await processRunner.RunAsync(
			"/usr/libexec/PlistBuddy",
			$"-c \"Set :{key} {value}\" \"{infoPlistPath}\"",
			null,
			cancellationToken).ConfigureAwait(false);
		if (set.Success)
		{
			return;
		}

		await RunOrThrowAsync(
			"/usr/libexec/PlistBuddy",
			$"-c \"Add :{key} string {value}\" \"{infoPlistPath}\"",
			null,
			$"Failed to stamp {key} into Info.plist.",
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Archives a signed <c>.ipa</c> for a single head and returns its path. This is a
	/// distinct build shape from the desktop publish: it drives the iOS archive toolchain
	/// via <c>ArchiveOnBuild</c>/<c>BuildIpa</c> and the signing properties.
	/// </summary>
	/// <param name="workingDirectory">The working directory.</param>
	/// <param name="projectPath">The iOS head project file.</param>
	/// <param name="runtimeIdentifier">The device runtime identifier (for example <c>ios-arm64</c>).</param>
	/// <param name="configuration">The build configuration.</param>
	/// <param name="framework">The iOS target framework for <c>-f</c>, or null to omit it.</param>
	/// <param name="codesignKey">The distribution certificate common name.</param>
	/// <param name="provisionName">The provisioning profile name.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The path to the produced <c>.ipa</c>.</returns>
	public async Task<string> ArchiveAsync(
		string workingDirectory,
		string projectPath,
		string runtimeIdentifier,
		string configuration,
		string? framework,
		string codesignKey,
		string provisionName,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(workingDirectory);
		Ensure.NotNull(projectPath);
		Ensure.NotNull(runtimeIdentifier);
		logger.WriteStepHeader($"Archiving iOS Head ({runtimeIdentifier})");

		string frameworkArg = string.IsNullOrEmpty(framework) ? string.Empty : $" -f {framework}";

		// The signing properties carry the certificate common name and profile name. They
		// are passed straight to MSBuild, matching the reference pipeline; the process
		// runner does not echo arguments, so they are not logged here.
		string args = $"publish \"{projectPath}\" --configuration {configuration}{frameworkArg} " +
			$"-p:RuntimeIdentifier={runtimeIdentifier} -p:ArchiveOnBuild=true -p:BuildIpa=true " +
			$"-p:CodesignKey=\"{codesignKey}\" -p:CodesignProvision=\"{provisionName}\"";

		int exitCode = await processRunner.RunWithCallbackAsync(
			"dotnet",
			args,
			workingDirectory,
			logger.WriteInfo,
			logger.WriteError,
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			throw new InvalidOperationException($"iOS archive failed for {projectPath} ({runtimeIdentifier}) with exit code {exitCode}");
		}

		string headDir = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? Directory.GetCurrentDirectory();
		string? ipa = FindIpa(Path.Combine(headDir, "bin"));
		if (ipa is null)
		{
			throw new InvalidOperationException($"iOS archive completed but no .ipa was produced under {Path.Combine(headDir, "bin")}.");
		}

		logger.WriteInfo($"Archive: {ipa}");
		return ipa;
	}

	/// <summary>
	/// Imports the distribution certificate into a temporary keychain and installs the
	/// provisioning profile, from the base64 secrets in the options. Includes the OpenSSL
	/// 3DES transcode fallback for <c>.p12</c> files macOS cannot import directly. Decoded
	/// material is written to a temporary directory and wiped after import.
	/// </summary>
	/// <param name="options">The packaging options carrying the signing material.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public async Task SetupSigningAsync(IosPackageOptions options, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);
		logger.WriteStepHeader("Setting Up iOS Signing");

		// Decode and validate both secrets before touching the keychain, so malformed input
		// fails fast without displacing the host's default keychain.
		byte[] certBytes = DecodeBase64(options.CertificateP12Base64);
		if (certBytes.Length == 0)
		{
			throw new InvalidOperationException("The base64 signing certificate decoded to nothing. Set IOS_CERT_P12_BASE64 to a base64-encoded .p12.");
		}

		byte[] profileBytes = DecodeBase64(options.ProvisioningProfileBase64);
		if (profileBytes.Length == 0)
		{
			throw new InvalidOperationException("The base64 provisioning profile decoded to nothing. Set IOS_PROVISIONING_PROFILE_BASE64 to a base64-encoded .mobileprovision.");
		}

		await CreateKeychainAsync(options.KeychainPassword, cancellationToken).ConfigureAwait(false);

		string workDir = Path.Combine(Path.GetTempPath(), $"ktsubuild-ios-{Guid.NewGuid():N}");
		Directory.CreateDirectory(workDir);
		try
		{
			await ImportCertificateAsync(certBytes, options, workDir, cancellationToken).ConfigureAwait(false);
			await InstallProvisioningProfileAsync(profileBytes, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			// Wipe the decoded certificate material.
			TryDeleteDirectory(workDir);
		}
	}

	/// <summary>
	/// Resolves the head's <c>Info.plist</c>: the conventional location is next to the
	/// project file. Returns null when none is found.
	/// </summary>
	/// <param name="projectPath">The iOS head project file.</param>
	/// <returns>The <c>Info.plist</c> path, or null.</returns>
	public static string? ResolveInfoPlist(string projectPath)
	{
		Ensure.NotNull(projectPath);
		string headDir = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? Directory.GetCurrentDirectory();
		string candidate = Path.Combine(headDir, "Info.plist");
		return File.Exists(candidate) ? candidate : null;
	}

	/// <summary>
	/// Resolves the iOS target framework of a head from its project file, for the archive
	/// <c>-f</c> flag. Returns the first <c>-ios</c> target framework, or null when the
	/// project cannot be read or has no iOS framework (in which case the flag is omitted).
	/// </summary>
	/// <param name="projectPath">The iOS head project file.</param>
	/// <returns>The iOS target framework moniker, or null.</returns>
	public static string? ResolveIosFramework(string projectPath)
	{
		Ensure.NotNull(projectPath);
		if (!File.Exists(projectPath))
		{
			return null;
		}

		string content = File.ReadAllText(projectPath);
		foreach (Match match in TargetFrameworkRegex.Matches(content))
		{
			foreach (string tfm in match.Groups[1].Value.Split([';'], StringSplitOptions.RemoveEmptyEntries))
			{
				string trimmed = tfm.Trim();
				if (trimmed.Contains("-ios", StringComparison.OrdinalIgnoreCase))
				{
					return trimmed;
				}
			}
		}

		return null;
	}

	/// <summary>
	/// Finds the most recently written <c>.ipa</c> under a search root. Returns null when
	/// the root does not exist or contains no archive. Ordering by write time avoids
	/// picking a stale archive from a previous configuration or runtime when more than one
	/// is present.
	/// </summary>
	/// <param name="searchRoot">The directory to search (typically <c>bin</c> under the head).</param>
	/// <returns>The <c>.ipa</c> path, or null.</returns>
	public static string? FindIpa(string searchRoot)
	{
		Ensure.NotNull(searchRoot);
		if (!Directory.Exists(searchRoot))
		{
			return null;
		}

		return Directory.GetFiles(searchRoot, "*.ipa", SearchOption.AllDirectories)
			.OrderByDescending(File.GetLastWriteTimeUtc)
			.FirstOrDefault();
	}

	private async Task SelectXcodeAsync(string xcodeVersion, CancellationToken cancellationToken)
	{
		logger.WriteInfo($"Selecting Xcode {xcodeVersion}.");

		// The pinned workload requires an exact Xcode. Match the canonical app path (not a
		// versioned symlink, which broke xcrun's macOS-SDK lookup in the reference) and
		// verify a macOS SDK is present before selecting it.
		const string applications = "/Applications";
		string? developerDir = null;
		if (Directory.Exists(applications))
		{
			foreach (string candidate in Directory.GetDirectories(applications, $"Xcode_{xcodeVersion}*.app"))
			{
				string real = await ReadLinkAsync(candidate, cancellationToken).ConfigureAwait(false);
				string sdk = Path.Combine(real, "Contents", "Developer", "Platforms", "MacOSX.platform", "Developer", "SDKs", "MacOSX.sdk");
				if (Directory.Exists(sdk))
				{
					developerDir = Path.Combine(real, "Contents", "Developer");
					break;
				}
			}
		}

		if (developerDir is null)
		{
			throw new InvalidOperationException($"No complete Xcode {xcodeVersion} with a macOS SDK was found under {applications}. The pinned iOS workload requires it.");
		}

		await RunOrThrowAsync("sudo", $"xcode-select -s \"{developerDir}\"", null, $"Failed to select Xcode developer directory {developerDir}.", cancellationToken).ConfigureAwait(false);
		logger.WriteInfo($"Selected Xcode developer directory: {developerDir}");
	}

	private async Task<string> ReadLinkAsync(string path, CancellationToken cancellationToken)
	{
		// readlink -f canonicalizes a path, resolving any symlink. A non-symlink path
		// resolves to itself, so this is safe to call unconditionally.
		ProcessResult result = await processRunner.RunAsync("readlink", $"-f \"{path}\"", null, cancellationToken).ConfigureAwait(false);
		string resolved = result.StandardOutput.Trim();
		return string.IsNullOrEmpty(resolved) ? path : resolved;
	}

	private async Task InstallIosWorkloadAsync(string workloadVersion, CancellationToken cancellationToken)
	{
		logger.WriteInfo($"Installing iOS workload {workloadVersion} via rollback file.");

		string rollbackFile = Path.Combine(Path.GetTempPath(), $"ktsubuild-ios-rollback-{Guid.NewGuid():N}.json");
		await File.WriteAllTextAsync(rollbackFile, $"{{ \"microsoft.net.sdk.ios\": \"{workloadVersion}\" }}", cancellationToken).ConfigureAwait(false);
		try
		{
			await RunOrThrowAsync(
				"dotnet",
				$"workload install ios --from-rollback-file \"{rollbackFile}\"",
				null,
				$"Failed to install the pinned iOS workload {workloadVersion}. This phase requires it; failing loudly rather than skipping silently.",
				cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			TryDeleteFile(rollbackFile);
		}
	}

	private async Task CreateKeychainAsync(string keychainPassword, CancellationToken cancellationToken)
	{
		// A fresh, default, unlocked keychain with an auto-lock timeout. The runner is
		// ephemeral, so this lives only for the run.
		await RunOrThrowAsync("security", $"create-keychain -p \"{keychainPassword}\" {SigningKeychain}", null, "Failed to create the signing keychain.", cancellationToken).ConfigureAwait(false);
		await RunOrThrowAsync("security", $"default-keychain -s {SigningKeychain}", null, "Failed to set the default keychain.", cancellationToken).ConfigureAwait(false);
		await RunOrThrowAsync("security", $"unlock-keychain -p \"{keychainPassword}\" {SigningKeychain}", null, "Failed to unlock the signing keychain.", cancellationToken).ConfigureAwait(false);
		await RunOrThrowAsync("security", $"set-keychain-settings -lut 3600 {SigningKeychain}", null, "Failed to configure keychain settings.", cancellationToken).ConfigureAwait(false);
	}

	private async Task ImportCertificateAsync(byte[] certBytes, IosPackageOptions options, string workDir, CancellationToken cancellationToken)
	{
		string certPath = Path.Combine(workDir, "dist.p12");
#if NETSTANDARD
		File.WriteAllBytes(certPath, certBytes);
		cancellationToken.ThrowIfCancellationRequested();
#else
		await File.WriteAllBytesAsync(certPath, certBytes, cancellationToken).ConfigureAwait(false);
#endif

		// `security import` infers the .p12 format from the extension and the explicit
		// `-f pkcs12`, and authorizes codesign/security to use the key.
		string importArgs = $"import \"{certPath}\" -f pkcs12 -k {SigningKeychain} -P \"{options.CertificatePassword}\" -T /usr/bin/codesign -T /usr/bin/security";
		ProcessResult import = await processRunner.RunAsync("security", importArgs, null, cancellationToken).ConfigureAwait(false);
		if (!import.Success)
		{
			logger.WriteWarning("Direct certificate import failed; transcoding to a 3DES PKCS#12 and retrying.");
			string legacy = await TranscodeP12ToLegacyAsync(certPath, options.CertificatePassword, workDir, cancellationToken).ConfigureAwait(false);
			await RunOrThrowAsync("security", $"import \"{legacy}\" -f pkcs12 -k {SigningKeychain} -P \"{options.CertificatePassword}\" -T /usr/bin/codesign -T /usr/bin/security", null, "Failed to import the signing certificate after the 3DES transcode.", cancellationToken).ConfigureAwait(false);
		}

		logger.WriteInfo("Imported the signing certificate.");

		// Allow codesign to use the key without an interactive prompt.
		await RunOrThrowAsync("security", $"set-key-partition-list -S apple-tool:,apple:,codesign: -s -k \"{options.KeychainPassword}\" {SigningKeychain}", null, "Failed to set the keychain key partition list.", cancellationToken).ConfigureAwait(false);
	}

	private async Task<string> TranscodeP12ToLegacyAsync(string certPath, string password, string workDir, CancellationToken cancellationToken)
	{
		string openssl = await ResolveOpenSslAsync(cancellationToken).ConfigureAwait(false);
		string pem = Path.Combine(workDir, "cert.pem");
		string legacy = Path.Combine(workDir, "dist-3des.p12");

		// -legacy loads the legacy provider alongside the default, so this reads both modern
		// AES and old RC2/3DES inputs.
		await RunOrThrowAsync(openssl, $"pkcs12 -in \"{certPath}\" -legacy -nodes -passin pass:\"{password}\" -out \"{pem}\"", null, "Could not open the .p12 — wrong certificate password, or it is not a valid PKCS#12.", cancellationToken).ConfigureAwait(false);

		// Force 3DES for both bags and a SHA-1 MAC, the format macOS can import.
		await RunOrThrowAsync(openssl, $"pkcs12 -export -legacy -certpbe PBE-SHA1-3DES -keypbe PBE-SHA1-3DES -macalg sha1 -in \"{pem}\" -out \"{legacy}\" -passout pass:\"{password}\"", null, "Failed to transcode the certificate to a 3DES PKCS#12.", cancellationToken).ConfigureAwait(false);

		return legacy;
	}

	private async Task<string> ResolveOpenSslAsync(CancellationToken cancellationToken)
	{
		// The runner's default `openssl` may be LibreSSL, which lacks the legacy provider
		// and 3DES. Prefer a real OpenSSL 3 from Homebrew when present. Probing for brew is
		// best-effort: when it is not installed, fall back to the default `openssl`.
		try
		{
			ProcessResult brew = await processRunner.RunAsync("brew", "--prefix openssl@3", null, cancellationToken).ConfigureAwait(false);
			if (brew.Success)
			{
				string candidate = Path.Combine(brew.StandardOutput.Trim(), "bin", "openssl");
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}
		}
		catch (System.ComponentModel.Win32Exception)
		{
			// brew is not on PATH; use the default openssl.
		}

		return "openssl";
	}

	private async Task InstallProvisioningProfileAsync(byte[] profileBytes, CancellationToken cancellationToken)
	{
		string profileDir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
			"Library",
			"MobileDevice",
			"Provisioning Profiles");
		Directory.CreateDirectory(profileDir);

		string profilePath = Path.Combine(profileDir, "ktsubuild-distribution.mobileprovision");
#if NETSTANDARD
		File.WriteAllBytes(profilePath, profileBytes);
		cancellationToken.ThrowIfCancellationRequested();
#else
		await File.WriteAllBytesAsync(profilePath, profileBytes, cancellationToken).ConfigureAwait(false);
#endif
		logger.WriteInfo("Installed the provisioning profile.");
	}

	private async Task RunOrThrowAsync(string fileName, string arguments, string? workingDirectory, string failureMessage, CancellationToken cancellationToken)
	{
		int exitCode = await processRunner.RunWithCallbackAsync(
			fileName,
			arguments,
			workingDirectory,
			logger.WriteInfo,
			logger.WriteError,
			cancellationToken).ConfigureAwait(false);

		if (exitCode != 0)
		{
			throw new InvalidOperationException($"{failureMessage} (exit code {exitCode})");
		}
	}

	private static byte[] DecodeBase64(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return [];
		}

		// Strip any whitespace or BOM a copy-paste may have baked into the secret.
		string cleaned = new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
		try
		{
			return Convert.FromBase64String(cleaned);
		}
		catch (FormatException)
		{
			return [];
		}
	}

	private static void TryDeleteFile(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (IOException)
		{
			// Best-effort cleanup; the runner is ephemeral.
		}
		catch (UnauthorizedAccessException)
		{
			// Best-effort cleanup; the runner is ephemeral.
		}
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch (IOException)
		{
			// Best-effort cleanup; the runner is ephemeral.
		}
		catch (UnauthorizedAccessException)
		{
			// Best-effort cleanup; the runner is ephemeral.
		}
	}
}
