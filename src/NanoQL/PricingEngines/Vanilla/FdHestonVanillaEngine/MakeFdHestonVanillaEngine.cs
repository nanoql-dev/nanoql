// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2014 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009, 2015 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdhestonvanillaengine.hpp ql/pricingengines/vanilla/fdhestonvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>A builder of the finite-difference Heston engine, with QuantLib's defaults.</summary>
/// <remarks>
/// An immutable builder (SEQR2): each method returns a modified copy, so a builder kept and reused never changes under
/// its holder. <see cref="ToEngine"/> returns the engine as a function of the option and the context, its settings
/// bound, in place of QuantLib's conversion to a pricing engine, with the default mixing factor, QuantLib's. A builder
/// has no value equality.
/// </remarks>
public sealed record MakeFdHestonVanillaEngine
{
    /// <summary>Initializes a new instance of the <see cref="MakeFdHestonVanillaEngine"/> record.</summary>
    /// <param name="hestonModel">The Heston model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hestonModel"/> is <see langword="null"/>.</exception>
    public MakeFdHestonVanillaEngine(HestonModel hestonModel)
    {
        ArgumentNullException.ThrowIfNull(hestonModel);
        HestonModel = hestonModel;
    }

    private HestonModel HestonModel { get; init; }

    private ImmutableArray<Dividend> Dividends { get; init; } = [];

    private int TGrid { get; init; } = 100;

    private int XGrid { get; init; } = 100;

    private int VGrid { get; init; } = 50;

    private int DampingSteps { get; init; }

    private FdmSchemeDesc SchemeDesc { get; init; } = FdmSchemeDesc.Hundsdorfer();

    private LocalVolTermStructure? LeverageFct { get; init; }

    private FdmQuantoHelper? QuantoHelper { get; init; }

    /// <summary>Sets the quanto helper.</summary>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeFdHestonVanillaEngine WithQuantoHelper(FdmQuantoHelper? quantoHelper) =>
        this with { QuantoHelper = quantoHelper };

    /// <summary>Sets the number of time steps.</summary>
    /// <param name="tGrid">The number of time steps.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tGrid"/> is negative.</exception>
    public MakeFdHestonVanillaEngine WithTGrid(int tGrid)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        return this with { TGrid = tGrid };
    }

    /// <summary>Sets the number of the log spot's points.</summary>
    /// <param name="xGrid">The number of points.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="xGrid"/> is negative.</exception>
    public MakeFdHestonVanillaEngine WithXGrid(int xGrid)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        return this with { XGrid = xGrid };
    }

    /// <summary>Sets the number of the variance's points.</summary>
    /// <param name="vGrid">The number of points.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="vGrid"/> is negative.</exception>
    public MakeFdHestonVanillaEngine WithVGrid(int vGrid)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(vGrid);
        return this with { VGrid = vGrid };
    }

    /// <summary>Sets the number of damping steps.</summary>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dampingSteps"/> is negative.</exception>
    public MakeFdHestonVanillaEngine WithDampingSteps(int dampingSteps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        return this with { DampingSteps = dampingSteps };
    }

    /// <summary>Sets the scheme.</summary>
    /// <param name="schemeDesc">The scheme.</param>
    /// <returns>The modified builder.</returns>
    public MakeFdHestonVanillaEngine WithFdmSchemeDesc(FdmSchemeDesc schemeDesc) => this with { SchemeDesc = schemeDesc };

    /// <summary>Sets the leverage function.</summary>
    /// <param name="leverageFct">The leverage function, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeFdHestonVanillaEngine WithLeverageFunction(LocalVolTermStructure? leverageFct) =>
        this with { LeverageFct = leverageFct };

    /// <summary>Sets the cash dividends, a fixed dividend on each date.</summary>
    /// <param name="dividendDates">The dividends' dates.</param>
    /// <param name="dividendAmounts">The dividends' amounts, one per date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The sizes differ, or a date is <c>default(Date)</c>.</exception>
    public MakeFdHestonVanillaEngine WithCashDividends(IEnumerable<Date> dividendDates, IEnumerable<double> dividendAmounts) =>
        this with { Dividends = DividendFunctions.DividendVector(dividendDates, dividendAmounts) };

    /// <summary>The engine with the builder's settings: QuantLib's conversion to a pricing engine.</summary>
    /// <returns>The engine, a function of the option and the context.</returns>
    public Func<VanillaOption, EvaluationContext, OneAssetOptionResults> ToEngine()
    {
        HestonModel model = HestonModel;
        ImmutableArray<Dividend> dividends = Dividends;
        FdmQuantoHelper? quantoHelper = QuantoHelper;
        int tGrid = TGrid, xGrid = XGrid, vGrid = VGrid, dampingSteps = DampingSteps;
        FdmSchemeDesc schemeDesc = SchemeDesc;
        LocalVolTermStructure? leverageFct = LeverageFct;
        return (option, context) => FdHestonVanillaEngine.Calculate(
            option, context, model, dividends, quantoHelper, tGrid, xGrid, vGrid, dampingSteps, schemeDesc, leverageFct);
    }
}
