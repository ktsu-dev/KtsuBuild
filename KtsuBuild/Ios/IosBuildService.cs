// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Ios;

using KtsuBuild.Abstractions;
using KtsuBuild.DotNet;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Orchestrates the unsigned iOS build path: it resolves the iOS heads to build,
/// builds each for the simulator and device runtimes, and asserts that the device
/// bundle (and any required native frameworks) was produced. This is the
/// pull-request validation path and never touches signing material.
/// </summary>
/// <param name="dotNetService">The .NET SDK service.</param>
/// <param name="logger">The build logger.</param>
public class IosBuildService(IDotNetService dotNetService, IBuildLogger logger) : IIosBuildService
{
	private readonly IosService? toolchain;

	/// <summary>
	/// Initializes a new instance of the <see cref="IosBuildService"/> class that can also
	/// provision the pinned Xcode and iOS workload before building, so a pull-request build
	/// uses the same toolchain as the signed release.
	/// </summary>
	/// <param name="dotNetService">The .NET SDK service.</param>
	/// <param name="processRunner">The process runner the toolchain commands go through.</param>
	/// <param name="logger">The build logger.</param>
	public IosBuildService(IDotNetService dotNetService, IProcessRunner processRunner, IBuildLogger logger)
		: this(dotNetService, logger) =>
		toolchain = new IosService(dotNetService, processRunner, logger);

	/// <summary>
	/// The simulator runtime identifier built by default.
	/// </summary>
	public const string SimulatorRuntime = "iossimulator-arm64";

	/// <summary>
	/// The device runtime identifier built by default, and the one the
	/// embedded-frameworks check runs against.
	/// </summary>
	public const string DeviceRuntime = "ios-arm64";

	/// <inheritdoc/>
	public async Task<bool> BuildAsync(IosBuildOptions options, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(options);

		// A relative --project names a path in the workspace, where dotnet runs, not in
		// the process directory, so every lookup below resolves it the same way.
		IReadOnlyList<string> heads = string.IsNullOrEmpty(options.Project)
			? dotNetService.GetIosHeads(options.WorkingDirectory)
			: [DotNetService.ResolveAgainst(options.WorkingDirectory, options.Project)];

		if (heads.Count == 0)
		{
			logger.WriteInfo("No iOS heads found in workspace. Nothing to build.");
			return true;
		}

		// Default builds both the simulator and device runtimes; an explicit runtime
		// narrows to one. The device runtime is the one the embedded-frameworks check
		// runs against, since the simulator bundle does not exercise the device assets.
		string[] runtimes = string.IsNullOrEmpty(options.Runtime)
			? [SimulatorRuntime, DeviceRuntime]
			: [options.Runtime];

		if (options.DryRun)
		{
			ReportDryRun(options, heads, runtimes);
			return true;
		}

		await ProvisionToolchainAsync(options, cancellationToken).ConfigureAwait(false);

		foreach (string head in heads)
		{
			logger.WriteInfo($"Building iOS head: {head}");
			foreach (string rid in runtimes)
			{
				await dotNetService.BuildIosAsync(options.WorkingDirectory, head, rid, options.Configuration, codeSigning: false, cancellationToken).ConfigureAwait(false);

				if (IsDeviceRuntime(rid) && !VerifyDeviceBundle(head, options.Configuration, rid, options.RequiredFrameworks))
				{
					return false;
				}
			}
		}

		return true;
	}

	private static bool HasToolchainPins(IosBuildOptions options) =>
		!string.IsNullOrWhiteSpace(options.XcodeVersion) || !string.IsNullOrWhiteSpace(options.WorkloadVersion);

	// Reports what BuildAsync would do, for --dry-run: the heads and runtimes, any required
	// frameworks, and the toolchain pins. Starts no process.
	private void ReportDryRun(IosBuildOptions options, IReadOnlyList<string> heads, string[] runtimes)
	{
		logger.WriteInfo($"[DRY RUN] Would build {heads.Count} iOS head(s) ({options.Configuration}, unsigned):");
		foreach (string head in heads)
		{
			logger.WriteInfo($"[DRY RUN]   {head}: {string.Join(", ", runtimes)}");
		}

		if (options.RequiredFrameworks.Count > 0)
		{
			logger.WriteInfo($"[DRY RUN] Would require embedded frameworks: {string.Join(", ", options.RequiredFrameworks)}");
		}

		if (HasToolchainPins(options))
		{
			logger.WriteInfo($"[DRY RUN] Would provision Xcode '{options.XcodeVersion}' and iOS workload '{options.WorkloadVersion}' first");
		}
	}

	// Applies the same Xcode and workload pins as `ios package`, so the pull-request build and
	// the release build cannot drift apart. No pins means the host's toolchain as-is.
	private async Task ProvisionToolchainAsync(IosBuildOptions options, CancellationToken cancellationToken)
	{
		if (!HasToolchainPins(options))
		{
			return;
		}

		if (toolchain is null)
		{
			throw new InvalidOperationException("An Xcode or iOS workload pin is set, but this IosBuildService was created without a process runner to provision it.");
		}

		await toolchain.ProvisionToolchainAsync(options.XcodeVersion, options.WorkloadVersion, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Decides what the automatic iOS validation step in the <c>ci</c> pipeline should
	/// do, from the number of detected iOS heads and whether the host is macOS. This is
	/// a pure function so the decision is unit-testable without a macOS host: the caller
	/// supplies the head count (from <see cref="IDotNetService.GetIosHeads"/>) and the
	/// host flag (from <c>RuntimeInformation</c>).
	/// </summary>
	/// <param name="iosHeadCount">The number of iOS heads detected in the workspace.</param>
	/// <param name="hostIsMacOs">Whether the current host is macOS.</param>
	/// <returns>The disposition the <c>ci</c> pipeline should act on.</returns>
	public static IosCiDisposition ClassifyForCi(int iosHeadCount, bool hostIsMacOs)
	{
		if (iosHeadCount <= 0)
		{
			return IosCiDisposition.NoHeads;
		}

		return hostIsMacOs ? IosCiDisposition.Build : IosCiDisposition.SkipNotMacOs;
	}

	/// <summary>
	/// Determines whether a runtime identifier targets a device (rather than the
	/// simulator). The embedded-frameworks check runs only for device runtimes.
	/// </summary>
	/// <param name="runtimeIdentifier">The runtime identifier.</param>
	/// <returns>True when the runtime targets a device.</returns>
	public static bool IsDeviceRuntime(string runtimeIdentifier)
	{
		Ensure.NotNull(runtimeIdentifier);
		return !runtimeIdentifier.Contains("simulator", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Locates the device <c>.app</c> bundle produced by a build, logs its embedded
	/// native frameworks, and fails when the bundle is missing or a required native
	/// framework is not embedded.
	/// </summary>
	/// <param name="head">The iOS head project path.</param>
	/// <param name="configuration">The build configuration.</param>
	/// <param name="runtimeIdentifier">The device runtime identifier that was built.</param>
	/// <param name="requiredFrameworks">The native frameworks that must be embedded.</param>
	/// <returns>True when the bundle exists and every required framework is embedded.</returns>
	public bool VerifyDeviceBundle(string head, string configuration, string runtimeIdentifier, IReadOnlyList<string> requiredFrameworks)
	{
		Ensure.NotNull(head);
		Ensure.NotNull(requiredFrameworks);

		string headDir = Path.GetDirectoryName(Path.GetFullPath(head)) ?? Directory.GetCurrentDirectory();
		string searchRoot = Path.Combine(headDir, "bin", configuration);

		IReadOnlyList<string> bundles = DotNetService.FindAppBundles(searchRoot, runtimeIdentifier);
		if (bundles.Count == 0)
		{
			// Fall back to any .app under the search root in case the RID is not a path segment.
			bundles = DotNetService.FindAppBundles(searchRoot);
		}

		if (bundles.Count == 0)
		{
			logger.WriteError($"Device .app bundle not found under {searchRoot}. The iOS build did not produce an app bundle.");
			return false;
		}

		string bundle = bundles[0];
		logger.WriteInfo($"Device bundle: {bundle}");

		IReadOnlyList<string> frameworks = DotNetService.GetEmbeddedNativeFrameworks(bundle);
		logger.WriteInfo(frameworks.Count > 0
			? $"Embedded native frameworks: {string.Join(", ", frameworks)}"
			: "No native frameworks embedded in the device bundle.");

		foreach (string required in requiredFrameworks)
		{
			if (!DotNetService.BundleContainsNativeLibrary(bundle, required))
			{
				logger.WriteError($"Required native framework '{required}' is not embedded in the device bundle ({bundle}). This usually means a native asset resolved to the wrong target framework and would crash the app at launch.");
				return false;
			}

			logger.WriteInfo($"Verified native framework embedded: {required}");
		}

		return true;
	}
}
