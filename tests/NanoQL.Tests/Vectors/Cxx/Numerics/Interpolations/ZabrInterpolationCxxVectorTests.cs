// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's ZABR interpolation vectors, <c>tests/vectors/cxx/math/interpolations/zabrinterpolation.json</c>:
/// <see cref="ZabrInterpolation{TEvaluation}"/>'s fits on both short-maturity kernels, their parameters, γ among them,
/// errors, end criterion, weights and values, where the stability filter keeps them; the <see cref="Zabr{TEvaluation}"/>
/// factory's fits and trait; and QuantLib's refusals. The values and errors are those of QuantLib's section at the
/// fitted parameters, which nanoql's interpolation prices with.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZabrInterpolationCxxVectorTests
{
    private static readonly string[] Scalars = ["expiry", "forward", "alpha", "beta", "nu", "rho", "gamma", "rmsError", "maxError"];

    [Fact]
    public void ZabrInterpolationCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/zabrinterpolation");
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
                    check.Output(c, "values", Numbers(c.Input("valueStrikes")).Select(k => (object?)f.Evaluate(k, true)));
                    break;
                case "global":
                    check.Output(c, "global", new[] { Zabr<ZabrShortMaturityLognormal>.Global, Zabr<ZabrShortMaturityNormal>.Global });
                    break;
                case "refusal":
                    check.Output(c, "fit", () => Fit(c.Input("fit")).Alpha);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckFit(VectorAssert check, VectorCase c, IXabrInterpolation fit)
    {
        var f = (Interpolation)fit;
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

        check.Output(c, "values", Numbers(c.Input("valueStrikes")).Select(k => (object?)f.Evaluate(k, true)));
    }

    private static double Scalar(IXabrInterpolation fit, string name) => (name, fit) switch
    {
        ("expiry", ZabrInterpolation<ZabrShortMaturityLognormal> z) => z.Expiry,
        ("expiry", ZabrInterpolation<ZabrShortMaturityNormal> z) => z.Expiry,
        ("forward", ZabrInterpolation<ZabrShortMaturityLognormal> z) => z.Forward,
        ("forward", ZabrInterpolation<ZabrShortMaturityNormal> z) => z.Forward,
        ("gamma", ZabrInterpolation<ZabrShortMaturityLognormal> z) => z.Gamma,
        ("gamma", ZabrInterpolation<ZabrShortMaturityNormal> z) => z.Gamma,
        ("alpha", _) => fit.Alpha,
        ("beta", _) => fit.Beta,
        ("nu", _) => fit.Nu,
        ("rho", _) => fit.Rho,
        ("rmsError", _) => fit.RmsError,
        ("maxError", _) => fit.MaxError,
        _ => throw new InvalidOperationException($"unknown output {name}"),
    };

    // The fit a harness fit spec describes.
    private static IXabrInterpolation Fit(JsonElement s)
    {
        FitArguments a = Arguments(s);
        return a.Kernel switch
        {
            "ShortMaturityLognormal" => new ZabrInterpolation<ZabrShortMaturityLognormal>(
                a.Strikes, a.Volatilities, a.Expiry, a.Forward, a.Alpha, a.Beta, a.Nu, a.Rho, a.Gamma, a.Fixed[0],
                a.Fixed[1], a.Fixed[2], a.Fixed[3], a.Fixed[4], a.VegaWeighted, a.EndCriteria, a.Method, a.ErrorAccept,
                a.UseMaxError, a.MaxGuesses),
            "ShortMaturityNormal" => new ZabrInterpolation<ZabrShortMaturityNormal>(
                a.Strikes, a.Volatilities, a.Expiry, a.Forward, a.Alpha, a.Beta, a.Nu, a.Rho, a.Gamma, a.Fixed[0],
                a.Fixed[1], a.Fixed[2], a.Fixed[3], a.Fixed[4], a.VegaWeighted, a.EndCriteria, a.Method, a.ErrorAccept,
                a.UseMaxError, a.MaxGuesses),
            _ => throw new InvalidOperationException($"unknown ZABR kernel {a.Kernel}"),
        };
    }

    // The factory's interpolation for a harness fit spec.
    private static Interpolation Factory(JsonElement s)
    {
        FitArguments a = Arguments(s);
        return a.Kernel switch
        {
            "ShortMaturityLognormal" => new Zabr<ZabrShortMaturityLognormal>(
                a.Expiry, a.Forward, a.Alpha, a.Beta, a.Nu, a.Rho, a.Gamma, a.Fixed[0], a.Fixed[1], a.Fixed[2],
                a.Fixed[3], a.Fixed[4], a.VegaWeighted, a.EndCriteria, a.Method, a.ErrorAccept, a.UseMaxError,
                a.MaxGuesses).Interpolate(a.Strikes, a.Volatilities),
            "ShortMaturityNormal" => new Zabr<ZabrShortMaturityNormal>(
                a.Expiry, a.Forward, a.Alpha, a.Beta, a.Nu, a.Rho, a.Gamma, a.Fixed[0], a.Fixed[1], a.Fixed[2],
                a.Fixed[3], a.Fixed[4], a.VegaWeighted, a.EndCriteria, a.Method, a.ErrorAccept, a.UseMaxError,
                a.MaxGuesses).Interpolate(a.Strikes, a.Volatilities),
            _ => throw new InvalidOperationException($"unknown ZABR kernel {a.Kernel}"),
        };
    }

    private static FitArguments Arguments(JsonElement s)
    {
        JsonElement method = s.GetProperty("optMethod");
        OptimizationMethod? optMethod = method.ValueKind == JsonValueKind.Null
            ? null
            : method.GetProperty("type").GetString() switch
            {
                "LevenbergMarquardt" => new LevenbergMarquardt(
                    Number(method.GetProperty("epsfcn")), Number(method.GetProperty("xtol")), Number(method.GetProperty("gtol"))),
                "Simplex" => new Simplex(Number(method.GetProperty("lambda"))),
                _ => throw new InvalidOperationException($"unknown optimization method {method}"),
            };
        JsonElement end = s.GetProperty("endCriteria");
        return new FitArguments(
            s.GetProperty("kernel").GetString()!,
            Numbers(s.GetProperty("strikes")),
            Numbers(s.GetProperty("volatilities")),
            Number(s.GetProperty("expiry")),
            Number(s.GetProperty("forward")),
            Guess(s, "alpha"),
            Guess(s, "beta"),
            Guess(s, "nu"),
            Guess(s, "rho"),
            Guess(s, "gamma"),
            [.. new[] { "alphaIsFixed", "betaIsFixed", "nuIsFixed", "rhoIsFixed", "gammaIsFixed" }.Select(f => s.GetProperty(f).GetBoolean())],
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
        string Kernel,
        double[] Strikes,
        double[] Volatilities,
        double Expiry,
        double Forward,
        double? Alpha,
        double? Beta,
        double? Nu,
        double? Rho,
        double? Gamma,
        bool[] Fixed,
        bool VegaWeighted,
        EndCriteria? EndCriteria,
        OptimizationMethod? Method,
        double ErrorAccept,
        bool UseMaxError,
        int MaxGuesses);
}
