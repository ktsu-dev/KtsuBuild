// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

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

		IReadOnlyList<string> heads = string.IsNullOrEmpty(options.Project)
			? dotNetService.GetIosHeads(options.WorkingDirectory)
			: [options.Project];

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
	public static IosCiDisposition ClassifyForCi(int iosHeadCount, bool hostIsMacOs) =>
		iosHeadCount <= 0 ? IosCiDisposition.NoHeads
		: !hostIsMacOs ? IosCiDisposition.SkipNotMacOs
		: IosCiDisposition.Build;

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
