// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Ios;

/// <summary>
/// The outcome of an iOS packaging run.
/// </summary>
public class IosPackageResult
{
	/// <summary>
	/// Gets or sets whether packaging succeeded (or was deliberately skipped).
	/// </summary>
	public bool Success { get; set; }

	/// <summary>
	/// Gets or sets whether the packaging step was skipped. This happens when the
	/// signing material is unavailable or the host is not macOS. A skip is not a
	/// failure: <see cref="Success"/> stays true so the command exits cleanly.
	/// </summary>
	public bool Skipped { get; set; }

	/// <summary>
	/// Gets or sets the reason the step was skipped, for logging.
	/// </summary>
	public string? SkipReason { get; set; }

	/// <summary>
	/// Gets or sets the <c>.ipa</c> archives produced, one per packaged head.
	/// </summary>
	public IReadOnlyList<string> IpaPaths { get; set; } = [];

	/// <summary>
	/// Gets or sets the error message when packaging failed.
	/// </summary>
	public string? Error { get; set; }
}
