// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Helpers;

using NSubstitute;

/// <summary>
/// Argument matchers for reference-type parameters that are declared non-nullable.
/// </summary>
/// <remarks>
/// NSubstitute's <see cref="Arg.Is{T}(System.Linq.Expressions.Expression{System.Predicate{T}})"/>
/// takes the predicate parameter as nullable so that null arguments can be matched, which means
/// every predicate that dereferences the argument produces CS8602. These wrappers do the null
/// check once so call sites can use a non-nullable parameter.
/// </remarks>
public static class ArgMatch
{
	/// <summary>
	/// Matches a non-null argument that satisfies the given predicate.
	/// </summary>
	/// <typeparam name="T">The argument type.</typeparam>
	/// <param name="predicate">The predicate the argument must satisfy.</param>
	/// <returns>The NSubstitute argument matcher.</returns>
	public static T NotNull<T>(Func<T, bool> predicate) where T : class
		=> Arg.Is<T>(a => a != null && predicate(a));
}
