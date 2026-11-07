// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The saddle-point loss model with its expected tranche loss read from its own tail: the probability of losing each
/// amount in the tranche or more, <see cref="SaddlePointLossModel{TCopula, TInitTraits}.ProbOverPortfLoss"/>,
/// integrated over the tranche, where the base reads the tranche of the conditional expected loss, a large-pool reading.
/// </summary>
/// <remarks>
/// <para>
/// For a tranche [A, D] with A above 0 the expected loss is ∫_A^D P(L > y) dy, by 24-node Gauss-Legendre over the
/// tranche. For the equity tranche it is E[L] − ∫_D^N P(L > y) dy: E[L] the live portfolio's conditional expected loss
/// integrated over the factor, and the upper tail over [D, D + 5%], [D + 5%, D + 20%] and [D + 20%, 100%] of the live
/// notional N. So the equity tranche never reads the tail near zero loss, where the pool's loss is a lattice of
/// single-name losses the expansion follows coarsely and its tail exceeds 1 at high correlation; a tranche attaching
/// close above zero does, its integral starting below the pool's smallest single-name loss. The attachment that counts
/// is the live one, which a seasoned tranche's realized losses write down (<see cref="Basket"/>'s restated tranche).
/// Every other member is the base's.
/// </para>
/// <para>
/// The check is the recursive model on the same latent model. On 100 names (notional 100, hazard 1%, recovery 40%), at
/// one and five years, the variant is within 2% of it on the tranches from 3% to 22% at correlations from 0.3 to 0.9,
/// and on the equity tranche within 0.6% to 0.75. Attaching closer to zero it errs more: [2%, 5%] by up to +2.5% at a
/// year, [1%, 3%] by up to +7.5%, [0.5%, 3%] by +13% to +21%, and [0.1%, 3%] is refused from correlation 0.6 (the base's
/// refusal of a tail outside [0, 1]); after four defaults a 3–6% tranche of that pool attaches at 0.625% of the live
/// notional, one default's loss. At 0.9 the equity tranche reads +9.1% at five years; and a tranche
/// whose integral reaches the pool's largest loss meets the expansion's other weak region there, where its tail can read
/// below 0: senior tranches err by −2.6% to −4.4% at five years, and [22%, 60%] is refused at both horizons, its nodes
/// crowding the largest loss (the base's refusal of a tail outside [0, 1]); the equity tranche's upper pieces cross it
/// too. At low correlation the pool's loss is a binomial lattice, which a continuous expansion follows coarsely: at 0.1
/// a small tranche reads +9% at a year. Both models read the latent model's quadrature, its default QuantLib's 25-node
/// Gauss-Hermite, which itself errs by up to 30% on 3% tranches at correlation 0.9 against its trapezoid integration.
/// </para>
/// </remarks>
/// <typeparam name="TCopula">The copula policy.</typeparam>
/// <typeparam name="TInitTraits">The copula's initialization traits.</typeparam>
[Experimental("NQL0001")]
public class SaddlePointTailLossModel<TCopula, TInitTraits> : SaddlePointLossModel<TCopula, TInitTraits>
    where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
    where TInitTraits : struct
{
    private static readonly GaussLegendreIntegration Nodes = new(24);

    /// <summary>Initializes a new instance of the <see cref="SaddlePointTailLossModel{TCopula, TInitTraits}"/>
    /// class.</summary>
    /// <param name="m">The constant-loss latent model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="m"/> is <see langword="null"/>.</exception>
    public SaddlePointTailLossModel(ConstantLossLatentModel<TCopula, TInitTraits> m)
        : base(m)
    {
    }

    /// <summary>The live tranche's expected loss by a date, from the model's own tail.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The expected loss, an amount.</returns>
    /// <exception cref="NanoQLException">The model isn't bound to a basket, or the expansion refuses (a tail outside
    /// [0, 1], an unresolvable loss).</exception>
    public override double ExpectedTrancheLoss(Date d)
    {
        Basket basket = BoundBasket;
        double notional = basket.RemainingNotional();
        double attach = Math.Min(basket.RemainingAttachmentAmount(), notional);
        double detach = Math.Min(basket.RemainingDetachmentAmount(), notional);
        if (attach > 0.0)
        {
            return Layer(d, attach, detach);
        }

        double first = Math.Min(detach + (0.05 * notional), notional);
        double second = Math.Min(detach + (0.2 * notional), notional);
        return ExpectedPortfolioLoss(d) - (Layer(d, detach, first) + Layer(d, first, second) + Layer(d, second, notional));
    }

    // The probability of losing each amount over [from, to] or more, integrated: the expected loss in that layer.
    private double Layer(Date d, double from, double to) =>
        to > from ? 0.5 * (to - from) * Nodes.Evaluate(x => ProbOverPortfLoss(d, from + (0.5 * (to - from) * (x + 1.0)))) : 0.0;
}
