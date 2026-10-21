// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdblackscholesvanillaengine.hpp ql/pricingengines/vanilla/fdblackscholesvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>A builder of the finite-difference Black-Scholes engine, with QuantLib's defaults.</summary>
/// <remarks>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToEngine"/> returns the engine as a function of the option and the context, its settings bound,
/// in place of QuantLib's conversion to a pricing engine. A builder has no value equality.
/// </remarks>
public sealed record MakeFdBlackScholesVanillaEngine
{
    /// <summary>Initializes a new instance of the <see cref="MakeFdBlackScholesVanillaEngine"/> record.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    public MakeFdBlackScholesVanillaEngine(GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        Process = process;
    }

    private GeneralizedBlackScholesProcess Process { get; init; }

    private ImmutableArray<Dividend> Dividends { get; init; } = [];

    private int TGrid { get; init; } = 100;

    private int XGrid { get; init; } = 100;

    private int DampingSteps { get; init; }

    private FdmSchemeDesc SchemeDesc { get; init; } = FdmSchemeDesc.Douglas();

    private bool LocalVol { get; init; }

    private double? IllegalLocalVolOverwrite { get; init; }

    private FdmQuantoHelper? QuantoHelper { get; init; }

    private FdBlackScholesVanillaEngineCashDividendModel CashDividendModel { get; init; } =
        FdBlackScholesVanillaEngineCashDividendModel.Spot;

    /// <summary>Sets the quanto helper.</summary>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <returns>The modified builder.</returns>
    public MakeFdBlackScholesVanillaEngine WithQuantoHelper(FdmQuantoHelper? quantoHelper) =>
        this with { QuantoHelper = quantoHelper };

    /// <summary>Sets the number of time steps.</summary>
    /// <param name="tGrid">The number of time steps.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tGrid"/> is negative.</exception>
    public MakeFdBlackScholesVanillaEngine WithTGrid(int tGrid)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        return this with { TGrid = tGrid };
    }

    /// <summary>Sets the number of points of the grid.</summary>
    /// <param name="xGrid">The number of points.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="xGrid"/> is negative.</exception>
    public MakeFdBlackScholesVanillaEngine WithXGrid(int xGrid)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        return this with { XGrid = xGrid };
    }

    /// <summary>Sets the number of damping steps.</summary>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dampingSteps"/> is negative.</exception>
    public MakeFdBlackScholesVanillaEngine WithDampingSteps(int dampingSteps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        return this with { DampingSteps = dampingSteps };
    }

    /// <summary>Sets the scheme.</summary>
    /// <param name="schemeDesc">The scheme.</param>
    /// <returns>The modified builder.</returns>
    public MakeFdBlackScholesVanillaEngine WithFdmSchemeDesc(FdmSchemeDesc schemeDesc) =>
        this with { SchemeDesc = schemeDesc };

    /// <summary>Sets whether to use the process's local volatility.</summary>
    /// <param name="localVol">Whether to use the local volatility.</param>
    /// <returns>The modified builder.</returns>
    public MakeFdBlackScholesVanillaEngine WithLocalVol(bool localVol) => this with { LocalVol = localVol };

    /// <summary>Sets the volatility to use where the local volatility can't be computed.</summary>
    /// <param name="illegalLocalVolOverwrite">The volatility; a negative value lets the failure through.</param>
    /// <returns>The modified builder.</returns>
    public MakeFdBlackScholesVanillaEngine WithIllegalLocalVolOverwrite(double illegalLocalVolOverwrite) =>
        this with { IllegalLocalVolOverwrite = illegalLocalVolOverwrite };

    /// <summary>Sets the cash dividends, a fixed dividend on each date.</summary>
    /// <param name="dividendDates">The dividends' dates.</param>
    /// <param name="dividendAmounts">The dividends' amounts, one per date.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The sizes differ, or a date is <c>default(Date)</c>.</exception>
    public MakeFdBlackScholesVanillaEngine WithCashDividends(
        IEnumerable<Date> dividendDates, IEnumerable<double> dividendAmounts) =>
        this with { Dividends = DividendFunctions.DividendVector(dividendDates, dividendAmounts) };

    /// <summary>Sets the cash dividends' model.</summary>
    /// <param name="cashDividendModel">The model.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cashDividendModel"/> isn't a member of its
    /// enum.</exception>
    public MakeFdBlackScholesVanillaEngine WithCashDividendModel(
        FdBlackScholesVanillaEngineCashDividendModel cashDividendModel)
    {
        EnumArguments.Check(cashDividendModel, nameof(cashDividendModel));
        return this with { CashDividendModel = cashDividendModel };
    }

    /// <summary>The engine with the builder's settings: QuantLib's conversion to a pricing engine.</summary>
    /// <returns>The engine, a function of the option and the context.</returns>
    public Func<VanillaOption, EvaluationContext, OneAssetOptionResults> ToEngine()
    {
        GeneralizedBlackScholesProcess process = Process;
        ImmutableArray<Dividend> dividends = Dividends;
        FdmQuantoHelper? quantoHelper = QuantoHelper;
        int tGrid = TGrid, xGrid = XGrid, dampingSteps = DampingSteps;
        FdmSchemeDesc schemeDesc = SchemeDesc;
        bool localVol = LocalVol;
        double? illegalLocalVolOverwrite = IllegalLocalVolOverwrite;
        FdBlackScholesVanillaEngineCashDividendModel cashDividendModel = CashDividendModel;
        return (option, context) => FdBlackScholesVanillaEngine.Calculate(
            option,
            context,
            process,
            dividends,
            quantoHelper,
            tGrid,
            xGrid,
            dampingSteps,
            schemeDesc,
            localVol,
            illegalLocalVolOverwrite,
            cashDividendModel);
    }
}
