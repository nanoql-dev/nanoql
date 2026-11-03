// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Models;

/// <summary>
/// The size checks of the multi-step products' inputs where QuantLib reads past them unchecked, in the form of
/// QuantLib's inverse floater's ("Incorrect number of … given").
/// </summary>
internal static class MultiStepProductChecks
{
    /// <summary>Refuses a number of products that isn't one to the number of rates.</summary>
    /// <param name="rates">The number of rates.</param>
    /// <param name="products">The number of products.</param>
    /// <param name="paramName">The input that gives the products.</param>
    /// <exception cref="ArgumentException">The number isn't one to the number of rates.</exception>
    public static void Sizes(int rates, int products, string paramName)
    {
        if (products < 1 || products > rates)
        {
            throw new ArgumentException(
                $"Incorrect number of {paramName} given, should be 1 to {rates} not {products}", paramName);
        }
    }

    /// <summary>Refuses more products than rates, which a product reads past.</summary>
    /// <param name="rates">The number of rates.</param>
    /// <param name="products">The number of products.</param>
    /// <param name="paramName">The input that gives the products.</param>
    /// <exception cref="ArgumentException">The products are more than the rates.</exception>
    public static void AtMost(int rates, int products, string paramName)
    {
        if (products > rates)
        {
            throw new ArgumentException(
                $"Incorrect number of {paramName} given, should be at most {rates} not {products}", paramName);
        }
    }

    /// <summary>Refuses an input shorter than it is read.</summary>
    /// <param name="given">The input's size.</param>
    /// <param name="needed">The number of entries read.</param>
    /// <param name="paramName">The input.</param>
    /// <exception cref="ArgumentException">The input is shorter.</exception>
    public static void AtLeast(int given, int needed, string paramName)
    {
        if (given < needed)
        {
            throw new ArgumentException(
                $"Incorrect number of {paramName} given, should be at least {needed} not {given}", paramName);
        }
    }
}
