// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's abcd specifier vectors,
/// <c>tests/vectors/cxx/models/marketmodels/models/volatilityinterpolationspecifierabcd.json</c>:
/// <see cref="VolatilityInterpolationSpecifierabcd"/> at offset 0 (an offset other than 0 is in the Divergences record)
/// in periods of 2 and 3, with the last caplet's volatility the last long rate's or given, and after
/// <see cref="VolatilityInterpolationSpecifier.WithScalingFactors"/>,
/// <see cref="VolatilityInterpolationSpecifier.WithLastCapletVol"/> or both, QuantLib's setters on its specifier: the
/// period, offset and counts, and each interpolated and original variance's reads; and the refused specifiers.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VolatilityInterpolationSpecifierabcdCxxVectorTests
{
    [Fact]
    public void VolatilityInterpolationSpecifierabcdCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/models/volatilityinterpolationspecifierabcd");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.String("call") == "refused")
            {
                foreach (JsonProperty r in c.Input("refusals").EnumerateObject())
                {
                    JsonElement[] a = [.. r.Value.EnumerateArray()];
                    check.Output(c, r.Name, Lazy(() => Make(a[0].GetInt32(), a[1].GetInt32(), a[2], Reals(a[3]), a[4].GetDouble(), a[5], a[6])));
                }

                continue;
            }

            VolatilityInterpolationSpecifier spec = Make(
                c.Int("period"), c.Int("offset"), c.Input("variances"), c.Doubles("timesForSmallRates"), c.Double("lastCapletVol"), c.Input("scales"), c.Input("newLastCapletVol"));
            check.Output(c, "period", spec.Period);
            check.Output(c, "offset", spec.Offset);
            check.Output(c, "noBigRates", spec.NoBigRates);
            check.Output(c, "noSmallRates", spec.NoSmallRates);
            PiecewiseConstantAbcdVarianceCxxVectorTests.CheckVariance(check, c, "interpolated", spec.InterpolatedVariances);
            PiecewiseConstantAbcdVarianceCxxVectorTests.CheckVariance(check, c, "original", spec.OriginalVariances);
        }

        check.Complete();
    }

    // The specifier, then its scaled copy unless the scales are null, then its copy of the new last caplet's
    // volatility unless that is null: QuantLib's setters in the harness's order.
    private static VolatilityInterpolationSpecifier Make(
        int period, int offset, JsonElement variances, double[] times, double lastCapletVol, JsonElement scales, JsonElement newLastCapletVol)
    {
        PiecewiseConstantAbcdVariance[] originals = [.. variances.EnumerateArray().Select(PiecewiseConstantAbcdVarianceCxxVectorTests.VarianceOf)];
        VolatilityInterpolationSpecifier spec = new VolatilityInterpolationSpecifierabcd(period, offset, originals, times, lastCapletVol);
        if (scales.ValueKind != JsonValueKind.Null)
        {
            spec = spec.WithScalingFactors(Reals(scales));
        }

        if (newLastCapletVol.ValueKind != JsonValueKind.Null)
        {
            spec = spec.WithLastCapletVol(newLastCapletVol.GetDouble());
        }

        return spec;
    }
}
