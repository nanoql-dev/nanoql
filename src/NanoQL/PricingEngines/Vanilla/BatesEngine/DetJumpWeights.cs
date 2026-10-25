// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/batesengine.hpp ql/pricingengines/vanilla/batesengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The deterministic-intensity engines' term: the constant-intensity term l weighted by the intensity's average over
/// the maturity, (x − 1 + e^{−x})θ_λ/(xλ) + (1 − e^{−x})/x, x = κ_λt.
/// </summary>
/// <remarks>
/// QuantLib's expression (<c>batesengine.cpp:80-82</c>, <c>:140-142</c>) at and above x = 1e-2; below it the two
/// weights are x·φ₂(−x) and −expm1(−x)/x, which don't cancel (the S-hes plan's HR7 (iv)).
/// </remarks>
internal static class DetJumpWeights
{
    // the cut below which the weights are written without cancellation
    internal const double SmallReversion = 1e-2;

    internal static Complex Term(Complex l, double lambda, double kappaLambda, double thetaLambda, double t)
    {
        double x = kappaLambda * t;
        if (x < SmallReversion)
        {
            // (x - 1 + e^{-x})/x = x·φ₂(−x), (1 - e^{-x})/x = -expm1(-x)/x
            double w1 = x * GsrProcessCore.Phi2(-x);
            double w2 = -Expm1Functions.StdExpm1(-x) / x;
            return (w1 * thetaLambda * l / lambda) + (w2 * l);
        }

        return ((kappaLambda * t) - 1.0 + Math.Exp(-kappaLambda * t)) * thetaLambda * l / (kappaLambda * t * lambda)
            + ((1.0 - Math.Exp(-kappaLambda * t)) * l / (kappaLambda * t));
    }
}
