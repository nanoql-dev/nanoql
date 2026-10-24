// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdfpamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>Andersen, Lake and Offengenden's fixed-point equation B: QuantLib's <c>DqFpEquation_B</c>.</summary>
internal sealed class DqFpEquationB : DqFpEquation
{
    private readonly double k;

    /// <summary>Initializes a new instance of the <see cref="DqFpEquationB"/> class.</summary>
    /// <param name="k">The strike.</param>
    /// <param name="r">The risk-free rate.</param>
    /// <param name="q">The dividend yield.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="b">The exercise boundary at a time to expiry.</param>
    /// <param name="integrator">The integrator of each step.</param>
    internal DqFpEquationB(double k, double r, double q, double vol, Func<double, double> b, Integrator integrator)
        : base(r, q, vol, b, integrator) => this.k = k;

    /// <inheritdoc/>
    internal override (double N, double D, double Fv) F(double tau, double b)
    {
        double n, d;
        if (tau < NumericLimits.QL_EPSILON * NumericLimits.QL_EPSILON)
        {
            if (ComparisonFunctions.CloseEnough(b, k))
            {
                n = d = 0.5;
            }
            else if (b < k)
            {
                n = d = 0.0;
            }
            else
            {
                n = d = 1.0;
            }
        }
        else
        {
            double ni, di;
            if (XI is Vector xI && WI is Vector wI)
            {
                double c = 0.5 * tau;

                ni = di = 0.0;
                for (int i = xI.Size - 1; i >= 0; --i)
                {
                    double u = (c * xI[i]) + c;
                    (double Plus, double Minus) dpm = D(tau - u, b / B(u));
                    ni += wI[i] * Math.Exp(R * u) * Cumulative.Evaluate(dpm.Minus);
                    di += wI[i] * Math.Exp(Q * u) * Cumulative.Evaluate(dpm.Plus);
                }

                ni *= c;
                di *= c;
            }
            else
            {
                ni = Integrator.Integrate(
                    u =>
                    {
                        double df = Math.Exp(R * u);
                        if (u >= tau * (1 - (5 * NumericLimits.QL_EPSILON)))
                        {
                            return ComparisonFunctions.CloseEnough(b, B(u)) ? 0.5 * df : df * (b < B(u) ? 0.0 : 1.0);
                        }
                        else
                        {
                            return df * Cumulative.Evaluate(D(tau - u, b / B(u)).Minus);
                        }
                    },
                    0,
                    tau);
                di = Integrator.Integrate(
                    u =>
                    {
                        double df = Math.Exp(Q * u);
                        if (u >= tau * (1 - (5 * NumericLimits.QL_EPSILON)))
                        {
                            return ComparisonFunctions.CloseEnough(b, B(u)) ? 0.5 * df : df * (b < B(u) ? 0.0 : 1.0);
                        }
                        else
                        {
                            return df * Cumulative.Evaluate(D(tau - u, b / B(u)).Plus);
                        }
                    },
                    0,
                    tau);
            }

            (double Plus, double Minus) dpmAtK = D(tau, b / k);

            n = Cumulative.Evaluate(dpmAtK.Minus) + (R * ni);
            d = Cumulative.Evaluate(dpmAtK.Plus) + (Q * di);
        }

        double fv;
        double alpha = k * Math.Exp(-(R - Q) * tau);
        if (tau < NumericLimits.QL_EPSILON * NumericLimits.QL_EPSILON)
        {
            if (ComparisonFunctions.CloseEnough(b, k) || b > k)
            {
                fv = alpha;
            }
            else
            {
                fv = ComparisonFunctions.CloseEnough(Q, 0.0)
                    ? alpha * R * (Q < 0 ? -1.0 : 1.0) / NumericLimits.QL_EPSILON
                    : alpha * R / Q;
            }
        }
        else
        {
            fv = alpha * n / d;
        }

        return (n, d, fv);
    }

    /// <inheritdoc/>
    internal override (double Nd, double Dd) NDd(double tau, double b)
    {
        (double Plus, double Minus) dpm = D(tau, b / k);
        return (
            Density.Evaluate(dpm.Minus) / (b * Vol * Math.Sqrt(tau)),
            Density.Evaluate(dpm.Plus) / (b * Vol * Math.Sqrt(tau)));
    }
}
