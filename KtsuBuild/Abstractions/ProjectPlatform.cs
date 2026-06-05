// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

/// <summary>
/// Classifies a project by the platform its target framework(s) tie it to.
/// Used to decide which projects can be restored and built on the current host.
/// </summary>
public enum ProjectPlatform
{
	/// <summary>
	/// The project builds on any host. This covers framework-neutral target
	/// frameworks (for example <c>net10.0</c> or <c>netstandard2.0</c>) and
	/// multi-target projects that include at least one neutral target framework.
	/// </summary>
	Neutral,

	/// <summary>
	/// The project targets Windows only (a <c>-windows</c> target framework) and
	/// can be restored and built only on a Windows host.
	/// </summary>
	Windows,

	/// <summary>
	/// The project targets iOS only (a <c>-ios</c> target framework) and can be
	/// restored and built only on a macOS host with the iOS workload installed.
	/// </summary>
	Ios,
}
