// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The bump instrument Jacobian's refusal (the S-mmp plan's MMPR4 (xx), ruled by the user; SMD7). QuantLib marks an
/// instrument computed before computing it (<c>bumpinstrumentjacobian.cpp:53</c>), so after its pseudo-derivative
/// refuses, a second read returns the zero-filled vector: on six semi-annual rates, FlatVol 20%, 3 factors,
/// displacement −1%, a cap from rate 1 to 5, refused by Black's formula's check, QuantLib's second read returns 63 zeros
/// and the bump matrix a row it never wrote (<c>Matrix(rows, columns)</c> leaves its storage uninitialized: 1.6e-311 and
/// 3.0e-311 in two runs; this run's <c>probes\c4_facts.cpp</c>). nanoql's per-instrument cache refuses at every read.
/// Checks: QuantLib's first read's refusal.
/// </summary>
public class VolatilityBumpInstrumentJacobianTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void ARefusalIsRefusedAtEveryRead()
    {
        MarketModel model = Market.FlatVol(new EvolutionDescription(Market.RateTimes, Market.RateTimes[..^1]), 3, 0.20, -0.01);
        var bumps = new VegaBumpCollection(model);
        Assert.Equal(63, bumps.NumberBumps);
        var jacobian = new VolatilityBumpInstrumentJacobian(bumps, [], [new VolatilityBumpInstrumentJacobianCap(1, 5, 0.032)]);

        ArgumentException first = Assert.Throws<ArgumentException>(() => jacobian.DerivativesVolatility(0));
        Assert.Contains("displacement", first.Message, StringComparison.Ordinal);
        Assert.Equal(first.Message, Assert.Throws<ArgumentException>(() => jacobian.DerivativesVolatility(0)).Message);
        Assert.Equal(first.Message, Assert.Throws<ArgumentException>(() => jacobian.OnePercentBump(0)).Message);
        Assert.Equal(first.Message, Assert.Throws<ArgumentException>(jacobian.GetAllOnePercentBumps).Message);
    }
}
