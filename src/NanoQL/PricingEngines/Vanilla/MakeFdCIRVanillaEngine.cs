// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdcirvanillaengine.hpp ql/pricingengines/vanilla/fdcirvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// A builder of <see cref="FdCIRVanillaEngine"/>: QuantLib's defaults, 10 time steps, 100 points of the log spot and
/// 100 of the short rate, no damping steps, no dividends and no quanto helper, but the scheme Hundsdorfer's, where
/// QuantLib's is modified Hundsdorfer.
/// </summary>
/// <remarks>
/// Immutable: each <c>With</c> method returns a new builder, where QuantLib's returns itself changed;
/// <see cref="ToEngine"/> is QuantLib's conversion to a pricing engine.
/// </remarks>
public sealed record MakeFdCIRVanillaEngine
{
    /// <summary>Initializes a new instance of the <see cref="MakeFdCIRVanillaEngine"/> class.</summary>
    /// <param name="cirProcess">The short rate's process.</param>
    /// <param name="bsProcess">The equity's process.</param>
    /// <param name="rho">The correlation of the two factors.</param>
    /// <exception cref="ArgumentNullException">A process is <see langword="null"/>.</exception>
    public MakeFdCIRVanillaEngine(CoxIngersollRossProcess cirProcess, GeneralizedBlackScholesProcess bsProcess, double rho)
    {
        ArgumentNullException.ThrowIfNull(cirProcess);
        ArgumentNullException.ThrowIfNull(bsProcess);
        CirProcess = cirProcess;
        BsProcess = bsProcess;
        Rho = rho;
    }

    private CoxIngersollRossProcess CirProcess { get; init; }

    private GeneralizedBlackScholesProcess BsProcess { get; init; }

    private double Rho { get; init; }

    private ImmutableArray<Dividend> Dividends { get; init; } = [];

    private int TGrid { get; init; } = 10;

    private int XGrid { get; init; } = 100;

    private int RGrid { get; init; } = 100;

    private int DampingSteps { get; init; }

    private FdmSchemeDesc SchemeDesc { get; init; } = FdmSchemeDesc.Hundsdorfer();

    private FdmQuantoHelper? QuantoHelper { get; init; }

    /// <summary>Sets the quanto helper.</summary>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <returns>The new builder.</returns>
    public MakeFdCIRVanillaEngine WithQuantoHelper(FdmQuantoHelper? quantoHelper) =>
        this with { QuantoHelper = quantoHelper };

    /// <summary>Sets the number of time steps.</summary>
    /// <param name="tGrid">The number of time steps.</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tGrid"/> is negative.</exception>
    public MakeFdCIRVanillaEngine WithTGrid(int tGrid)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        return this with { TGrid = tGrid };
    }

    /// <summary>Sets the number of the log spot's points.</summary>
    /// <param name="xGrid">The number of points.</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="xGrid"/> is negative.</exception>
    public MakeFdCIRVanillaEngine WithXGrid(int xGrid)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        return this with { XGrid = xGrid };
    }

    /// <summary>Sets the number of the short rate's points.</summary>
    /// <param name="rGrid">The number of points.</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rGrid"/> is negative.</exception>
    public MakeFdCIRVanillaEngine WithRGrid(int rGrid)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rGrid);
        return this with { RGrid = rGrid };
    }

    /// <summary>Sets the number of implicit damping steps.</summary>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dampingSteps"/> is negative.</exception>
    public MakeFdCIRVanillaEngine WithDampingSteps(int dampingSteps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        return this with { DampingSteps = dampingSteps };
    }

    /// <summary>Sets the scheme.</summary>
    /// <param name="schemeDesc">The scheme.</param>
    /// <returns>The new builder.</returns>
    public MakeFdCIRVanillaEngine WithFdmSchemeDesc(FdmSchemeDesc schemeDesc) => this with { SchemeDesc = schemeDesc };

    /// <summary>Sets cash dividends.</summary>
    /// <param name="dividendDates">The dividends' dates.</param>
    /// <param name="dividendAmounts">Their amounts, one per date.</param>
    /// <returns>The new builder.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The dates and amounts differ in number.</exception>
    public MakeFdCIRVanillaEngine WithCashDividends(IEnumerable<Date> dividendDates, IEnumerable<double> dividendAmounts) =>
        this with { Dividends = DividendFunctions.DividendVector(dividendDates, dividendAmounts) };

    /// <summary>The engine: <see cref="FdCIRVanillaEngine.Calculate(VanillaOption, EvaluationContext,
    /// CoxIngersollRossProcess, GeneralizedBlackScholesProcess, ImmutableArray{Dividend}, int, int, int, int, double,
    /// FdmSchemeDesc?, FdmQuantoHelper?)"/> on the builder's arguments.</summary>
    /// <returns>A pricing function of an option and a context.</returns>
    public Func<VanillaOption, EvaluationContext, OneAssetOptionResults> ToEngine()
    {
        CoxIngersollRossProcess cirProcess = CirProcess;
        GeneralizedBlackScholesProcess bsProcess = BsProcess;
        ImmutableArray<Dividend> dividends = Dividends;
        int tGrid = TGrid, xGrid = XGrid, rGrid = RGrid, dampingSteps = DampingSteps;
        double rho = Rho;
        FdmSchemeDesc schemeDesc = SchemeDesc;
        FdmQuantoHelper? quantoHelper = QuantoHelper;
        return (option, context) => FdCIRVanillaEngine.Calculate(
            option, context, cirProcess, bsProcess, dividends, tGrid, xGrid, rGrid, dampingSteps, rho, schemeDesc, quantoHelper);
    }
}
