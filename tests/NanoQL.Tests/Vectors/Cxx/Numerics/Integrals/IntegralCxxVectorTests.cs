// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's integrator vectors, <c>tests/vectors/cxx/math/integrals/integral.json</c>: each integrator's
/// <see cref="Integrator.AbsoluteAccuracy"/> and <see cref="Integrator.MaxEvaluations"/> as constructed and after
/// <see cref="Integrator.SetAbsoluteAccuracy"/> and <see cref="Integrator.SetMaxEvaluations"/>, which return the
/// changed copy where QuantLib's setters change the integrator; QuantLib's <c>Null&lt;Real&gt;()</c> accuracy, the
/// Gaussian integrators', is null in the vectors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IntegralCxxVectorTests
{
    // QuantLib's Null<Real>(), the Gaussian integrators' accuracy.
    private const double NullReal = 3.4028234663852886e38;

    [Fact]
    public void IntegralCxxVector()
    {
        var file = VectorFile.Load("cxx/math/integrals/integral");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            string kind = c.String("kind");
            int[] maxEvaluations = c.Ints("maxEvaluations");
            double[] accuracy = c.Input("accuracy").ValueKind == JsonValueKind.Null
                ? new double[maxEvaluations.Length]
                : c.Doubles("accuracy");
            double[] newAccuracy = c.Doubles("newAccuracy");
            int[] newMaxEvaluations = c.Ints("newMaxEvaluations");
            object?[] settings = [.. Enumerable.Range(0, maxEvaluations.Length).Select(i =>
                (object?)(Func<object?>)(() =>
                {
                    Integrator I = Make(kind, accuracy[i], maxEvaluations[i]);
                    Integrator changed = I.SetAbsoluteAccuracy(newAccuracy[i]).SetMaxEvaluations(newMaxEvaluations[i]);
                    return new object?[]
                    {
                        Real(I.AbsoluteAccuracy), I.MaxEvaluations, Real(changed.AbsoluteAccuracy),
                        changed.MaxEvaluations,
                    };
                }))];
            check.Output(c, "settings", settings);
        }

        check.Complete();
    }

    private static Integrator Make(string kind, double accuracy, int maxEvaluations) => kind switch
    {
        "Simpson" => new SimpsonIntegral(accuracy, maxEvaluations),
        "TrapezoidDefault" => new TrapezoidIntegral<Default>(accuracy, maxEvaluations),
        "TrapezoidMidPoint" => new TrapezoidIntegral<MidPoint>(accuracy, maxEvaluations),
        "GaussLobatto" => new GaussLobattoIntegral(maxEvaluations, accuracy),
        "GaussLegendre" => new GaussLegendreIntegrator(maxEvaluations),
        "GaussChebyshev" => new GaussChebyshevIntegrator(maxEvaluations),
        "GaussChebyshev2nd" => new GaussChebyshev2ndIntegrator(maxEvaluations),
        _ => throw new InvalidOperationException($"unknown integrator {kind}"),
    };

    private static object? Real(double x) => x == NullReal ? null : x;
}
