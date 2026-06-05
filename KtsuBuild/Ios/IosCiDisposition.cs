// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Ios;

/// <summary>
/// What the automatic iOS validation step inside the <c>ci</c> pipeline should do,
/// decided from whether the workspace contains iOS heads and whether the current
/// host can build them.
/// </summary>
public enum IosCiDisposition
{
	/// <summary>
	/// The workspace contains no iOS heads. The iOS step does nothing.
	/// </summary>
	NoHeads,

	/// <summary>
	/// The workspace contains iOS heads but the current host is not macOS, so the
	/// unsigned build is skipped. iOS builds only on macOS, so the actual build runs
	/// on a macOS CI job rather than the (typically Windows or Linux) <c>ci</c> job.
	/// </summary>
	SkipNotMacOs,

	/// <summary>
	/// The workspace contains iOS heads and the host is macOS, so the unsigned build
	/// runs as part of <c>ci</c>.
	/// </summary>
	Build,
}
