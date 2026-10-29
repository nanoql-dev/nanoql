// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/noarbsabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Experimental;

/// <summary>
/// QuantLib's <c>detail::NoArbSabrSpecs</c>: the no-arbitrage SABR fit's parameters, α through σ_I = αF^(β − 1), each
/// mapped onto the model's domain by an arctangent, and its random guesses inside it.
/// </summary>
[Experimental("NQL0001")]
internal readonly struct NoArbSabrSpecs : IXabrSpecs<NoArbSabrWrapper>
{
    private const double Eps = 0.000001;

    public static int Dimension => 4;

    public static void DefaultValues(
        double?[] parameters, bool[] paramIsFixed, double forward, double expiryTime, ReadOnlySpan<double> addParams)
    {
        SABRSpecs.DefaultValues(parameters, paramIsFixed, forward, expiryTime, addParams);
        double sigmaI = parameters[0]!.Value * Math.Pow(forward, parameters[1]!.Value - 1.0);
        if (sigmaI < NoArbSabrModel.SigmaIMin)
        {
            if (!paramIsFixed[0])
            {
                parameters[0] = NoArbSabrModel.SigmaIMin * (1.0 + Eps) / Math.Pow(forward, parameters[1]!.Value - 1.0);
            }
            else if (!paramIsFixed[1])
            {
                parameters[1] = 1.0 + (Math.Log(NoArbSabrModel.SigmaIMin * (1.0 + Eps) / parameters[0]!.Value) / Math.Log(forward));
            }
        }

        if (sigmaI > NoArbSabrModel.SigmaIMax)
        {
            if (!paramIsFixed[0])
            {
                parameters[0] = NoArbSabrModel.SigmaIMax * (1.0 - Eps) / Math.Pow(forward, parameters[1]!.Value - 1.0);
            }
            else if (!paramIsFixed[1])
            {
                parameters[1] = 1.0 + (Math.Log(NoArbSabrModel.SigmaIMax * (1.0 - Eps) / parameters[0]!.Value) / Math.Log(forward));
            }
        }
    }

    public static void Guess(
        Vector values,
        ReadOnlySpan<bool> paramIsFixed,
        double forward,
        double expiryTime,
        ReadOnlySpan<double> r,
        ReadOnlySpan<double> addParams)
    {
        int j = 0;
        if (!paramIsFixed[1])
        {
            values[1] = NoArbSabrModel.BetaMin + ((NoArbSabrModel.BetaMax - NoArbSabrModel.BetaMin) * r[j++]);
        }

        if (!paramIsFixed[0])
        {
            double sigmaI = NoArbSabrModel.SigmaIMin + ((NoArbSabrModel.SigmaIMax - NoArbSabrModel.SigmaIMin) * r[j++]);
            sigmaI *= 1.0 - Eps;
            sigmaI += Eps / 2.0;
            values[0] = sigmaI / Math.Pow(forward, values[1] - 1.0);
        }

        if (!paramIsFixed[2])
        {
            values[2] = NoArbSabrModel.NuMin + ((NoArbSabrModel.NuMax - NoArbSabrModel.NuMin) * r[j++]);
        }

        if (!paramIsFixed[3])
        {
            values[3] = NoArbSabrModel.RhoMin + ((NoArbSabrModel.RhoMax - NoArbSabrModel.RhoMin) * r[j++]);
        }
    }

    public static Vector Inverse(Vector y, ReadOnlySpan<bool> paramIsFixed, ReadOnlySpan<double> parameters, double forward)
    {
        var x = new Vector(4);
        x[1] = Math.Tan(((y[1] - NoArbSabrModel.BetaMin) / (NoArbSabrModel.BetaMax - NoArbSabrModel.BetaMin) * Math.PI)
            + (Math.PI / 2.0));
        x[0] = Math.Tan(((y[0] * Math.Pow(forward, y[1] - 1.0)) - NoArbSabrModel.SigmaIMin)
            / (NoArbSabrModel.SigmaIMax - NoArbSabrModel.SigmaIMin) * Math.PI - (Math.PI / 2.0));
        x[2] = Math.Tan(((y[2] - NoArbSabrModel.NuMin) / (NoArbSabrModel.NuMax - NoArbSabrModel.NuMin) * Math.PI)
            + (Math.PI / 2.0));
        x[3] = Math.Tan(((y[3] - NoArbSabrModel.RhoMin) / (NoArbSabrModel.RhoMax - NoArbSabrModel.RhoMin) * Math.PI)
            + (Math.PI / 2.0));
        return x;
    }

    public static Vector Direct(Vector x, ReadOnlySpan<bool> paramIsFixed, ReadOnlySpan<double> parameters, double forward)
    {
        var y = new Vector(4);
        y[1] = paramIsFixed[1]
            ? parameters[1]
            : NoArbSabrModel.BetaMin
                + ((NoArbSabrModel.BetaMax - NoArbSabrModel.BetaMin) * (Math.Atan(x[1]) + (Math.PI / 2.0)) / Math.PI);
        if (paramIsFixed[0])
        {
            y[0] = parameters[0];
            double sigmaI = y[0] * Math.Pow(forward, y[1] - 1.0);
            if (sigmaI < NoArbSabrModel.SigmaIMin)
            {
                y[1] = 1.0 + (Math.Log(NoArbSabrModel.SigmaIMin * (1.0 + Eps) / y[0]) / Math.Log(forward));
            }

            if (sigmaI > NoArbSabrModel.SigmaIMax)
            {
                y[1] = 1.0 + (Math.Log(NoArbSabrModel.SigmaIMax * (1.0 - Eps) / y[0]) / Math.Log(forward));
            }
        }
        else
        {
            double sigmaI = NoArbSabrModel.SigmaIMin
                + ((NoArbSabrModel.SigmaIMax - NoArbSabrModel.SigmaIMin) * (Math.Atan(x[0]) + (Math.PI / 2.0)) / Math.PI);
            y[0] = sigmaI / Math.Pow(forward, y[1] - 1.0);
        }

        y[2] = paramIsFixed[2]
            ? parameters[2]
            : NoArbSabrModel.NuMin + ((NoArbSabrModel.NuMax - NoArbSabrModel.NuMin) * (Math.Atan(x[2]) + (Math.PI / 2.0)) / Math.PI);
        y[3] = paramIsFixed[3]
            ? parameters[3]
            : NoArbSabrModel.RhoMin
                + ((NoArbSabrModel.RhoMax - NoArbSabrModel.RhoMin) * (Math.Atan(x[3]) + (Math.PI / 2.0)) / Math.PI);
        return y;
    }

    public static double Weight(double strike, double forward, double stdDev, ReadOnlySpan<double> addParams) =>
        BlackFormulaStdDevDerivative(strike, forward, stdDev, 1.0);

    public static NoArbSabrWrapper Instance(double t, double forward, double[] parameters, ReadOnlySpan<double> addParams) =>
        new(t, forward, parameters);
}
