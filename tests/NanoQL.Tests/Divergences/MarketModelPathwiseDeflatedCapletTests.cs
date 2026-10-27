// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The deflated caplets' deflator derivative (the S-mmp plan's MMPR4 (vi), fixed under R8). Caplet i pays
/// (fᵢ − Kᵢ) aᵢ P(Tᵢ₊₁)/P(T₀), whose derivative with respect to fⱼ, j ≤ i, carries −τⱼ P(Tⱼ₊₁)/P(Tⱼ) times it, τⱼ the
/// rate's own period; QuantLib multiplies by the caplet's accrual aⱼ (<c>pathwiseproductcaplet.cpp:233</c>), right only
/// where the accruals are the periods. On six semi-annual rates with accruals 0.5 × 365/360, caplet 3 struck at 3.1% on
/// the forwards: QuantLib's derivatives with respect to f₀, f₁ and f₂ are −4.7570e-4, −4.7547e-4 and −4.7524e-4, 365/360
/// of the amount's own central differences (this run's <c>probes\c2_facts.cpp</c>; the planner's MPP9: deltas 3.2e-5
/// from the undeflated caplet's on the same paths). nanoql multiplies by the period. Checks: the amount's central
/// differences; QuantLib's bits where the accruals are the periods.
/// </summary>
public class MarketModelPathwiseDeflatedCapletTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void TheDeflatorsDerivativeIsOverTheRatesPeriod()
    {
        double[] accruals = [.. Market.Accruals.Select(a => a * 365.0 / 360.0)];
        double[] amounts = AmountsAt(accruals, Market.Forwards, 3);

        // QuantLib's, the amount and its derivatives
        double[] quantLib =
        [
            0.00095245244889987973, -0.00047570490400719226, -0.0004754706819963565, -0.00047523669051899618,
            0.47575122152070454, 0, 0,
        ];
        Assert.Equal(quantLib[0], amounts[0]);

        // the amount's central differences in each forward: nanoql's derivatives, where QuantLib's miss by about 1.4%
        for (int j = 0; j < 6; ++j)
        {
            const double h = 1e-7;
            double[] up = [.. Market.Forwards], down = [.. Market.Forwards];
            up[j] += h;
            down[j] -= h;
            double fd = (AmountsAt(accruals, up, 3)[0] - AmountsAt(accruals, down, 3)[0]) / (2 * h);
            Assert.Equal(fd, amounts[j + 1], 1e-9);
            if (j <= 3)
            {
                Assert.True(Math.Abs(quantLib[j + 1] - fd) > 1e-6, $"QuantLib's derivative {j}: {quantLib[j + 1]} against {fd}");
            }
        }
    }

    [Fact]
    public void WhereTheAccrualsAreThePeriodsTheAmountsAreQuantLibs()
    {
        double[] quantLib =
        [
            0.00093940515507933353, -0.00046276116013760279, -0.00046253331121582145, -0.00046230568655479006,
            0.46924049925384276, 0, 0,
        ];
        Assert.Equal(quantLib, AmountsAt(Market.Accruals, Market.Forwards, 3));
    }

    // Caplet `step`'s amount and derivatives, the product stepped to it on a state of the forwards.
    private static double[] AmountsAt(double[] accruals, double[] forwards, int step)
    {
        var caplets = new MarketModelPathwiseMultiDeflatedCaplet(Market.RateTimes, accruals, Market.PaymentTimes, 0.031);
        var state = new LMMCurveState(Market.RateTimes);
        state.SetOnForwardRates(forwards);
        int[] counts = new int[Market.N];
        MarketModelPathwiseMultiProductCashFlow[][] flows =
            [.. Enumerable.Range(0, Market.N).Select(_ => new[] { new MarketModelPathwiseMultiProductCashFlow(Market.N + 1) })];
        caplets.Reset();
        for (int i = 0; i <= step; ++i)
        {
            caplets.NextTimeStep(state, counts, flows);
        }

        return flows[step][0].Amount.ToArray();
    }
}
