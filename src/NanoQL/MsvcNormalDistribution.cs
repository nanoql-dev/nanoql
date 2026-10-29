// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL;

/// <summary>
/// <c>std::normal_distribution&lt;double&gt;(mean, sigma)</c> as MSVC 14.44 draws it: Knuth's polar Algorithm P (The
/// Art of Computer Programming, vol. 2, p. 122), the second value of each pair kept for the next draw.
/// </summary>
/// <remarks>
/// A pair (v₁, v₂) of canonical draws mapped to [−1, 1) is redrawn until s = v₁² + v₂² is below 1 and neither is 0;
/// below s 1e-4 both are scaled by a power of 2 before the logarithm, so that ln s / s stays finite. A working type: each
/// draw changes it, and it is not thread-safe.
/// </remarks>
internal sealed class MsvcNormalDistribution
{
    private const double Ln2 = 0.6931471805599453;

    private readonly double mean;
    private readonly double sigma;
    private bool hasKept;
    private double kept;

    /// <summary>Initializes a new instance of the <see cref="MsvcNormalDistribution"/> class.</summary>
    /// <param name="mean">The mean.</param>
    /// <param name="sigma">The standard deviation.</param>
    internal MsvcNormalDistribution(double mean, double sigma)
    {
        this.mean = mean;
        this.sigma = sigma;
    }

    private MsvcNormalDistribution(MsvcNormalDistribution other)
    {
        mean = other.mean;
        sigma = other.sigma;
        hasKept = other.hasKept;
        kept = other.kept;
    }

    /// <summary>Gets the standard deviation.</summary>
    internal double Sigma => sigma;

    /// <summary>The next draw, from the kept value or a new pair.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The draw.</returns>
    internal double Next(MersenneTwisterUniformRng engine)
    {
        double z;
        if (hasKept)
        {
            z = kept;
            hasKept = false;
        }
        else
        {
            // Algorithm P's first steps: a point uniform in the unit disc, by rejection, here also off both axes
            double v1, v2, s;
            do
            {
                v1 = (2 * MsvcRandom.Canonical(engine)) - 1;
                v2 = (2 * MsvcRandom.Canonical(engine)) - 1;
                s = (v1 * v1) + (v2 * v2);
            }
            while (!(s < 1.0 && v1 != 0.0 && v2 != 0.0));

            // the factor √(−2 ln s / s); for s up to 1e-4 the point is first divided by 2^e, e the binary exponent of
            // its larger coordinate, and 2e·ln 2 added back to the logarithm
            double lnS;
            if (s > 1e-4)
            {
                lnS = Math.Log(s);
            }
            else
            {
                int e = Math.ILogB(Math.Max(Math.Abs(v1), Math.Abs(v2)));
                v1 = Math.ScaleB(v1, -e);
                v2 = Math.ScaleB(v2, -e);
                s = (v1 * v1) + (v2 * v2);
                lnS = Math.Log(s) + (e * (Ln2 * 2));
            }

            double f = Math.Sqrt(-2.0 * lnS / s);
            kept = f * v2;
            hasKept = true;
            z = f * v1;
        }

        return (z * sigma) + mean;
    }

    /// <summary>A copy, its kept value included: the copy's next draw on an equal engine equals this one's.</summary>
    /// <returns>The copy.</returns>
    internal MsvcNormalDistribution Clone() => new(this);
}
