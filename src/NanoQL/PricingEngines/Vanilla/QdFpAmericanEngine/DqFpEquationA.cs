// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdfpamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>Andersen, Lake and Offengenden's fixed-point equation A: QuantLib's <c>DqFpEquation_A</c>.</summary>
internal sealed class DqFpEquationA : DqFpEquation
{
    private readonly double k;

    /// <summary>Initializes a new instance of the <see cref="DqFpEquationA"/> class.</summary>
    /// <param name="k">The strike.</param>
    /// <param name="r">The risk-free rate.</param>
    /// <param name="q">The dividend yield.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="b">The exercise boundary at a time to expiry.</param>
    /// <param name="integrator">The integrator of each step.</param>
    internal DqFpEquationA(double k, double r, double q, double vol, Func<double, double> b, Integrator integrator)
        : base(r, q, vol, b, integrator) => this.k = k;

    /// <inheritdoc/>
    internal override (double N, double D, double Fv) F(double tau, double b)
    {
        double v = Vol * Math.Sqrt(tau);

        double n, d;
        if (tau < NumericLimits.QL_EPSILON * NumericLimits.QL_EPSILON)
        {
            if (ComparisonFunctions.CloseEnough(b, k))
            {
                n = 1 / (MathConstants.M_SQRT2 * MathConstants.M_SQRTPI * v);
                d = n + 0.5;
            }
            else
            {
                n = 0.0;
                d = b > k ? 1.0 : 0.0;
            }
        }
        else
        {
            double stv = Math.Sqrt(tau) / Vol;

            double k12, k3;
            if (XI is Vector xI && WI is Vector wI)
            {
                k12 = k3 = 0.0;

                for (int i = xI.Size - 1; i >= 0; --i)
                {
                    double y = xI[i];
                    double m = 0.25 * tau * ((1 + y) * (1 + y));
                    (double Plus, double Minus) dpm = D(m, b / B(tau - m));

                    k12 += wI[i] * Math.Exp((Q * tau) - (Q * m))
                        * ((0.5 * tau * (y + 1) * Cumulative.Evaluate(dpm.Plus)) + (stv * Density.Evaluate(dpm.Plus)));
                    k3 += wI[i] * stv * Math.Exp((R * tau) - (R * m)) * Density.Evaluate(dpm.Minus);
                }
            }
            else
            {
                k12 = Integrator.Integrate(
                    y =>
                    {
                        double m = 0.25 * tau * ((1 + y) * (1 + y));
                        double df = Math.Exp((Q * tau) - (Q * m));

                        if (y <= (5 * NumericLimits.QL_EPSILON) - 1)
                        {
                            return ComparisonFunctions.CloseEnough(b, B(tau - m))
                                ? df * stv / (MathConstants.M_SQRT2 * MathConstants.M_SQRTPI)
                                : 0.0;
                        }
                        else
                        {
                            double dp = D(m, b / B(tau - m)).Plus;
                            return df * ((0.5 * tau * (y + 1) * Cumulative.Evaluate(dp)) + (stv * Density.Evaluate(dp)));
                        }
                    },
                    -1,
                    1);

                k3 = Integrator.Integrate(
                    y =>
                    {
                        double m = 0.25 * tau * ((1 + y) * (1 + y));
                        double df = Math.Exp((R * tau) - (R * m));

                        if (y <= (5 * NumericLimits.QL_EPSILON) - 1)
                        {
                            return ComparisonFunctions.CloseEnough(b, B(tau - m))
                                ? df * stv / (MathConstants.M_SQRT2 * MathConstants.M_SQRTPI)
                                : 0.0;
                        }
                        else
                        {
                            return df * stv * Density.Evaluate(D(m, b / B(tau - m)).Minus);
                        }
                    },
                    -1,
                    1);
            }

            (double Plus, double Minus) dpmAtK = D(tau, b / k);
            n = (Density.Evaluate(dpmAtK.Minus) / v) + (R * k3);
            d = (Density.Evaluate(dpmAtK.Plus) / v) + Cumulative.Evaluate(dpmAtK.Plus) + (Q * k12);
        }

        double alpha = k * Math.Exp(-(R - Q) * tau);
        double fv;
        if (tau < NumericLimits.QL_EPSILON * NumericLimits.QL_EPSILON)
        {
            if (ComparisonFunctions.CloseEnough(b, k))
            {
                fv = alpha;
            }
            else if (b > k)
            {
                fv = 0.0;
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
        double dd, nd;

        if (tau < NumericLimits.QL_EPSILON * NumericLimits.QL_EPSILON)
        {
            if (ComparisonFunctions.CloseEnough(b, k))
            {
                double sqTau = Math.Sqrt(tau);
                double vol2 = Vol * Vol;
                dd = MathConstants.M_1_SQRTPI * MathConstants.M_SQRT_2
                    * ((-((0.5 * vol2) + R - Q) / (b * Vol * vol2 * sqTau)) + (1 / (b * Vol * sqTau)));
                nd = MathConstants.M_1_SQRTPI * MathConstants.M_SQRT_2 * ((-0.5 * vol2) + R - Q) / (b * Vol * vol2 * sqTau);
            }
            else
            {
                dd = nd = 0.0;
            }
        }
        else
        {
            (double Plus, double Minus) dpm = D(tau, b / k);

            dd = (-Density.Evaluate(dpm.Plus) * dpm.Plus / (b * Vol * Vol * tau))
                + (Density.Evaluate(dpm.Plus) / (b * Vol * Math.Sqrt(tau)));
            nd = -Density.Evaluate(dpm.Minus) * dpm.Minus / (b * Vol * Vol * tau);
        }

        return (nd, dd);
    }
}
