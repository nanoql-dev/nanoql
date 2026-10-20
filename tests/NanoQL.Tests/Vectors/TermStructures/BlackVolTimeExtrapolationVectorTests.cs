// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BlackVolTimeExtrapolation vectors,
/// <c>tests/vectors/termstructures/volatility/equityfx/blackvoltimeextrapolation.json</c>: the three strategies past
/// a variance curve's last date, through the curve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackVolTimeExtrapolationVectorTests
{
    [Fact]
    public void BlackVolTimeExtrapolationVector()
    {
        var file = VectorFile.Load("termstructures/volatility/equityfx/blackvoltimeextrapolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            BlackVolTermStructure s = BlackVolOf(c.Input("structure"), c.Context());
            double[] times = c.Doubles("times");
            check.Output(c, "blackVariance", times.Select(t => Lazy(() => s.BlackVariance(t, 100.0, true))).ToArray());
            check.Output(c, "blackVol", times.Select(t => Lazy(() => s.BlackVol(t, 100.0, true))).ToArray());
        }

        check.Complete();
    }
}
