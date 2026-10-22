// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's SVI interpolation vectors, <c>tests/vectors/cxx/experimental/volatility/sviinterpolation.json</c>:
/// <see cref="SviInterpolation"/>'s fits, their parameters, errors, end criterion, weights and values, where the
/// stability filter keeps them; the <see cref="Svi"/> factory's fits and trait; and QuantLib's refusals. The values and
/// errors are those of QuantLib's section at the fitted parameters, which nanoql's interpolation prices with.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SviInterpolationCxxVectorTests
{
    private static readonly string[] Scalars =
        ["expiry", "forward", "a", "b", "sigma", "rho", "m", "rmsError", "maxError"];

    [Fact]
    public void SviInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/volatility/sviinterpolation");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "fit":
                    CheckFit(check, c, Fit(c.Input("fit")));
                    break;
                case "factory":
                    Interpolation f = Factory(c.Input("fit"));
                    check.Output(
                        c, "values", Numbers(c.Input("valueStrikes")).Select(k => (object?)f.Evaluate(k, true)));
                    break;
                case "global":
                    check.Output(c, "global", Svi.Global);
                    break;
                case "refusal":
                    check.Output(c, "fit", () => Fit(c.Input("fit")).A);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckFit(VectorAssert check, VectorCase c, SviInterpolation fit)
    {
        foreach (string name in Scalars)
        {
            if (c.Expected.TryGetProperty(name, out _))
            {
                check.Output(c, name, Scalar(fit, name));
            }
        }

        if (c.Expected.TryGetProperty("endCriteria", out _))
        {
            check.Output(c, "endCriteria", fit.EndCriteria);
        }

        if (c.Expected.TryGetProperty("interpolationWeights", out _))
        {
            check.Output(c, "interpolationWeights", fit.InterpolationWeights());
        }

        check.Output(c, "values", Numbers(c.Input("valueStrikes")).Select(k => (object?)fit.Evaluate(k, true)));
    }

    private static double Scalar(SviInterpolation fit, string name) => name switch
    {
        "expiry" => fit.Expiry,
        "forward" => fit.Forward,
        "a" => fit.A,
        "b" => fit.B,
        "sigma" => fit.Sigma,
        "rho" => fit.Rho,
        "m" => fit.M,
        "rmsError" => fit.RmsError,
        "maxError" => fit.MaxError,
        _ => throw new InvalidOperationException($"unknown output {name}"),
    };

    // The fit a harness fit spec describes.
    private static SviInterpolation Fit(JsonElement s)
    {
        FitArguments a = Arguments(s);
        return new SviInterpolation(
            a.Strikes, a.Volatilities, a.Expiry, a.Forward, a.A, a.B, a.Sigma, a.Rho, a.M, a.Fixed[0], a.Fixed[1],
            a.Fixed[2], a.Fixed[3], a.Fixed[4], a.VegaWeighted, a.EndCriteria, a.Method, a.ErrorAccept, a.UseMaxError,
            a.MaxGuesses);
    }

    // The factory's interpolation for a harness fit spec.
    private static Interpolation Factory(JsonElement s)
    {
        FitArguments a = Arguments(s);
        return new Svi(
            a.Expiry, a.Forward, a.A, a.B, a.Sigma, a.Rho, a.M, a.Fixed[0], a.Fixed[1], a.Fixed[2], a.Fixed[3],
            a.Fixed[4], a.VegaWeighted, a.EndCriteria, a.Method, a.ErrorAccept, a.UseMaxError, a.MaxGuesses)
            .Interpolate(a.Strikes, a.Volatilities);
    }

    private static FitArguments Arguments(JsonElement s)
    {
        JsonElement method = s.GetProperty("optMethod");
        OptimizationMethod? optMethod = method.ValueKind == JsonValueKind.Null
            ? null
            : method.GetProperty("type").GetString() switch
            {
                "LevenbergMarquardt" => new LevenbergMarquardt(
                    Number(method.GetProperty(
                        "epsfcn")), Number(method.GetProperty("xtol")), Number(method.GetProperty("gtol"))),
                "Simplex" => new Simplex(Number(method.GetProperty("lambda"))),
                _ => throw new InvalidOperationException($"unknown optimization method {method}"),
            };
        JsonElement end = s.GetProperty("endCriteria");
        return new FitArguments(
            Numbers(s.GetProperty("strikes")),
            Numbers(s.GetProperty("volatilities")),
            Number(s.GetProperty("expiry")),
            Number(s.GetProperty("forward")),
            Guess(s, "a"),
            Guess(s, "b"),
            Guess(s, "sigma"),
            Guess(s, "rho"),
            Guess(s, "m"),
            [.. new[] { "aIsFixed", "bIsFixed", "sigmaIsFixed", "rhoIsFixed", "mIsFixed" }.Select(
                f => s.GetProperty(f).GetBoolean())],
            s.GetProperty("vegaWeighted").GetBoolean(),
            end.ValueKind == JsonValueKind.Null ? null : OptimizationSpecs.EndCriteria(end),
            optMethod,
            Number(s.GetProperty("errorAccept")),
            s.GetProperty("useMaxError").GetBoolean(),
            s.GetProperty("maxGuesses").GetInt32());
    }

    // A guess, null for QuantLib's Null<Real>(), the model's default.
    private static double? Guess(JsonElement s, string name) =>
        s.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Number(s.GetProperty(name));

    private sealed record FitArguments(
        double[] Strikes,
        double[] Volatilities,
        double Expiry,
        double Forward,
        double? A,
        double? B,
        double? Sigma,
        double? Rho,
        double? M,
        bool[] Fixed,
        bool VegaWeighted,
        EndCriteria? EndCriteria,
        OptimizationMethod? Method,
        double ErrorAccept,
        bool UseMaxError,
        int MaxGuesses);
}
