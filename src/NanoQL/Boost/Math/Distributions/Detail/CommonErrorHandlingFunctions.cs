// SPDX-FileCopyrightText: Copyright John Maddock 2006, 2007.
// SPDX-FileCopyrightText: Copyright Paul A. Bristow 2006, 2007, 2012.
// SPDX-FileCopyrightText: Copyright Matt Borland 2024
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/distributions/detail/common_error_handling.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's checks of a distribution's parameters and arguments (<c>common_error_handling.hpp</c>), under the
/// default policy, which throws: each check returns, or throws with Boost's message ("Error in function
/// &lt;function&gt;: &lt;message&gt;", the value in C#'s formatting). A domain error on the caller's argument (a
/// probability, a random variate) throws <see cref="ArgumentOutOfRangeException"/>; one on a distribution's stored
/// parameter (its scale or location) throws <see cref="NanoQLException"/>.
/// </summary>
internal static class CommonErrorHandlingFunctions
{
    /// <summary><c>check_probability</c>: a probability must lie in [0, 1].</summary>
    /// <param name="function">Boost's name of the calling function, its type written <c>double</c>.</param>
    /// <param name="prob">The probability.</param>
    /// <param name="paramName">The caller's name for the argument.</param>
    /// <exception cref="ArgumentOutOfRangeException">The probability is outside [0, 1], or isn't finite.</exception>
    internal static void CheckProbability(string function, double prob, string paramName)
    {
        if ((prob < 0) || (prob > 1) || !double.IsFinite(prob))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                $"Error in function {function}: Probability argument is {Show(prob)}, but must be >= 0 and <= 1 !");
        }
    }

    /// <summary><c>check_scale</c>: a scale must be positive and finite.</summary>
    /// <param name="function">Boost's name of the calling function, its type written <c>double</c>.</param>
    /// <param name="scale">The scale.</param>
    /// <exception cref="NanoQLException">The scale isn't positive, or isn't finite.</exception>
    internal static void CheckScale(string function, double scale)
    {
        if ((scale <= 0) || !double.IsFinite(scale))
        {
            throw new NanoQLException(
                $"Error in function {function}: Scale parameter is {Show(scale)}, but must be > 0 !");
        }
    }

    /// <summary><c>check_location</c>: a location must be finite.</summary>
    /// <param name="function">Boost's name of the calling function, its type written <c>double</c>.</param>
    /// <param name="location">The location.</param>
    /// <exception cref="NanoQLException">The location isn't finite.</exception>
    internal static void CheckLocation(string function, double location)
    {
        if (!double.IsFinite(location))
        {
            throw new NanoQLException(
                $"Error in function {function}: Location parameter is {Show(location)}, but must be finite!");
        }
    }

    /// <summary>
    /// <c>check_x</c>: a random variate must be finite. A distribution that allows infinite variates tests for them
    /// first, so for those this catches only a NaN.
    /// </summary>
    /// <param name="function">Boost's name of the calling function, its type written <c>double</c>.</param>
    /// <param name="x">The random variate.</param>
    /// <param name="paramName">The caller's name for the argument.</param>
    /// <exception cref="ArgumentOutOfRangeException">The variate isn't finite.</exception>
    internal static void CheckX(string function, double x, string paramName)
    {
        if (!double.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                $"Error in function {function}: Random variate x is {Show(x)}, but must be finite!");
        }
    }

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
