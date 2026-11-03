// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo geometric Asian pricers' running product: QuantLib's product of the fixings, flushed into the
/// average as its N-th root where the next factor would take it past double's largest value, as QuantLib flushes it, or
/// below its smallest normal, where QuantLib's underflows; QuantLib's arithmetic and bits while the product stays in
/// range.
/// </summary>
internal struct GeometricProduct
{
    private readonly int fixings;
    private double product;
    private double averagePrice;

    /// <summary>Initializes a new instance of the <see cref="GeometricProduct"/> struct.</summary>
    /// <param name="product">The product so far, the past fixings' or 1.</param>
    /// <param name="fixings">The number of fixings in the average.</param>
    public GeometricProduct(double product, int fixings)
    {
        this.product = product;
        this.fixings = fixings;
        averagePrice = 1.0;
    }

    /// <summary>Takes the past fixings by their log, their product out of double's range: e^(log/N) into the average.</summary>
    /// <param name="log">The past fixings' log-sum.</param>
    public void AverageLog(double log) => averagePrice *= Math.Exp(log / fixings);

    /// <summary>Multiplies a fixing into the product, a number of times.</summary>
    /// <param name="price">The fixing.</param>
    /// <param name="times">The number of fixings at it.</param>
    public void Multiply(double price, int times)
    {
        for (int k = 0; k < times; ++k)
        {
            // care must be taken not to overflow product, nor to underflow it
            if (product < NumericLimits.QL_MAX_REAL / price && product >= NumericLimits.QL_MIN_POSITIVE_REAL / price)
            {
                product *= price;
            }
            else
            {
                averagePrice *= Math.Pow(product, 1.0 / fixings);
                product = price;
            }
        }
    }

    /// <summary>The geometric average: the flushed roots times the root of the product left.</summary>
    /// <returns>The average.</returns>
    public readonly double Average() => averagePrice * Math.Pow(product, 1.0 / fixings);
}
