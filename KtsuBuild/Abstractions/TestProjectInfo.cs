// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Abstractions;

/// <summary>
/// A test project and the platform its target frameworks tie it to.
/// </summary>
/// <param name="Project">The absolute path to the project file.</param>
/// <param name="Platform">The platform the project can be restored and built on.</param>
public sealed record TestProjectInfo(string Project, ProjectPlatform Platform);
