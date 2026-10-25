// SPDX-FileCopyrightText: Copyright (C) 2008 Yee Man Chan
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/gjrgarchmodel.hpp ql/models/equity/gjrgarchmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Models;

/// <summary>
/// The GJR-GARCH model for the stochastic volatility of an asset: its six arguments ω, α, β, γ, λ and v₀ over a
/// <see cref="GJRGARCHProcess"/>.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: <see cref="CalibratedModel.WithParams"/> returns a new model whose process is rebuilt from the
/// parameters, QuantLib's <c>generateArguments</c>, on the process's curves, spot and days per year (and QuantLib's
/// default discretization). Besides each argument's own constraint the parameters satisfy β + γ ≥ 0
/// (<see cref="GJRGARCHModelVolatilityConstraint"/>).
/// </para>
/// <para>
/// Reference: Glosten, L., Jagannathan, R., Runkle, D., 1993. Relationship between the expected value and the volatility
/// of the nominal excess return on stocks. Journal of Finance 48, 1779-1801.
/// </para>
/// </remarks>
public sealed class GJRGARCHModel : CalibratedModel
{
    /// <summary>Initializes a new instance of the <see cref="GJRGARCHModel"/> class.</summary>
    /// <param name="process">The process, whose parameters the arguments start from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter is outside its constraint ("invalid value").</exception>
    public GJRGARCHModel(GJRGARCHProcess process)
        : base(6)
    {
        ArgumentNullException.ThrowIfNull(process);
        Process = process;
        SetArgument(0, new ConstantParameter(process.Omega, new PositiveConstraint(), nameof(process)));
        SetArgument(1, new ConstantParameter(process.Alpha, new BoundaryConstraint(0.0, 1.0), nameof(process)));
        SetArgument(2, new ConstantParameter(process.Beta, new BoundaryConstraint(0.0, 1.0), nameof(process)));
        SetArgument(3, new ConstantParameter(process.Gamma, new BoundaryConstraint(-1.0, 1.0), nameof(process)));
        SetArgument(4, new ConstantParameter(process.Lambda, new NoConstraint(), nameof(process)));
        SetArgument(5, new ConstantParameter(process.V0, new PositiveConstraint(), nameof(process)));
        BuildProcess();
    }

    /// <summary>Gets the daily variance's constant ω.</summary>
    public double Omega => Arguments[0].Evaluate(0.0);

    /// <summary>Gets the news shock's coefficient α.</summary>
    public double Alpha => Arguments[1].Evaluate(0.0);

    /// <summary>Gets the variance's persistence β.</summary>
    public double Beta => Arguments[2].Evaluate(0.0);

    /// <summary>Gets the leverage coefficient γ.</summary>
    public double Gamma => Arguments[3].Evaluate(0.0);

    /// <summary>Gets the market price of risk λ.</summary>
    public double Lambda => Arguments[4].Evaluate(0.0);

    /// <summary>Gets the daily variance's value.</summary>
    public double V0 => Arguments[5].Evaluate(0.0);

    /// <summary>Gets the process the parameters build.</summary>
    public GJRGARCHProcess Process { get; private set; }

    /// <inheritdoc/>
    protected override Constraint? ParametersConstraint() => new GJRGARCHModelVolatilityConstraint();

    /// <inheritdoc/>
    protected override void GenerateArguments() => BuildProcess();

    // QuantLib's GJRGARCHModel::generateArguments, non-virtual for the constructor
    private void BuildProcess() =>
        Process = new GJRGARCHProcess(
            Process.RiskFreeRate, Process.DividendYield, Process.S0, V0, Omega, Alpha, Beta, Gamma, Lambda, Process.DaysPerYear);
}
