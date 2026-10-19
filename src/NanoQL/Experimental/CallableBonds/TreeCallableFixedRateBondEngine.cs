// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/treecallablebondengine.hpp ql/experimental/callablebonds/treecallablebondengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The numerical lattice engine for callable fixed-rate bonds: the bond rolled back on a short-rate model's tree.</summary>
/// <remarks>
/// <para>
/// A static class: QuantLib's two constructors are <c>Calculate</c> overloads, the model with a number of time steps,
/// whose tree is built per call on a grid through the bond's mandatory times, and the model with a time grid, whose
/// tree is built per call on that grid; a third takes a built lattice, which a caller reuses across calls. The bond is
/// discounted on the model's curve when the model is term-structure consistent, else on the term structure given.
/// </para>
/// <para>
/// <b>The spread</b>, a continuously compounded rate added to every node's short rate, is QuantLib's
/// <c>CallableBond::arguments::spread</c>, which only the OAS family sets: it is each overload's last parameter, which
/// the OAS family's engine delegate passes on. QuantLib's time-grid engine keeps its tree, so a spread it sets stays on
/// it for later calls; a lattice here is immutable, so every call prices at the spread it is given.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class TreeCallableFixedRateBondEngine
{
    /// <summary>Prices a callable bond on the model's tree, with a number of time steps.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context, which gives the bond's settlement date.</param>
    /// <param name="model">The short-rate model.</param>
    /// <param name="timeSteps">The number of time steps of the tree, on a grid through the bond's mandatory times.</param>
    /// <param name="termStructure">
    /// The discount curve when the model isn't term-structure consistent, or <see langword="null"/>, QuantLib's empty
    /// handle, which only such a model refuses.
    /// </param>
    /// <param name="spread">The continuously compounded spread added to the tree's short rates.</param>
    /// <returns>
    /// The bond's results: the NPV at the curve's reference date and the settlement value; the expired results when the
    /// bond has expired.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="bond"/>, <paramref name="context"/> or <paramref name="model"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeSteps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> is 0.</exception>
    /// <exception cref="NanoQLException">
    /// The bond's arguments are inconsistent; the model needs a term structure and none is given, QuantLib's empty
    /// handle; or the model's tree takes no spread.
    /// </exception>
    public static CallableBondResults Calculate(
        CallableBond bond,
        EvaluationContext context,
        ShortRateModel model,
        int timeSteps,
        YieldTermStructure? termStructure = null,
        double spread = 0.0)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        LatticeShortRateModelEngine.CheckTimeSteps(timeSteps);
        return CalculateCore(bond, context, model, null, timeSteps, termStructure, spread);
    }

    /// <summary>Prices a callable bond on the model's tree, on a time grid.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context, which gives the bond's settlement date.</param>
    /// <param name="model">The short-rate model.</param>
    /// <param name="timeGrid">The tree's time grid, which must hold the bond's mandatory times.</param>
    /// <param name="termStructure">
    /// The discount curve when the model isn't term-structure consistent, or <see langword="null"/>, QuantLib's empty
    /// handle, which only such a model refuses.
    /// </param>
    /// <param name="spread">The continuously compounded spread added to the tree's short rates.</param>
    /// <returns>
    /// The bond's results: the NPV at the curve's reference date and the settlement value; the expired results when the
    /// bond has expired.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument but the term structure is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The grid can't hold a tree, or misses a mandatory time; the bond's arguments are inconsistent; the model needs a
    /// term structure and none is given, QuantLib's empty handle; or the model's tree takes no spread.
    /// </exception>
    /// <remarks>The tree is built before the expiry check, as QuantLib's constructor builds it.</remarks>
    public static CallableBondResults Calculate(
        CallableBond bond,
        EvaluationContext context,
        ShortRateModel model,
        TimeGrid timeGrid,
        YieldTermStructure? termStructure = null,
        double spread = 0.0)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(timeGrid);
        Lattice lattice = model.Tree(timeGrid);
        return CalculateCore(bond, context, model, lattice, 0, termStructure, spread);
    }

    /// <summary>Prices a callable bond on a lattice already built from the model, reused across calls.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context, which gives the bond's settlement date.</param>
    /// <param name="model">
    /// The short-rate model the lattice was built from, whose curve discounts when it is term-structure consistent.
    /// </param>
    /// <param name="lattice">The lattice, whose grid must hold the bond's mandatory times.</param>
    /// <param name="termStructure">
    /// The discount curve when the model isn't term-structure consistent, or <see langword="null"/>, QuantLib's empty
    /// handle, which only such a model refuses.
    /// </param>
    /// <param name="spread">The continuously compounded spread added to the lattice's short rates.</param>
    /// <returns>
    /// The bond's results: the NPV at the curve's reference date and the settlement value; the expired results when the
    /// bond has expired.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument but the term structure is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The grid misses a mandatory time; the bond's arguments are inconsistent; the model needs a term structure and
    /// none is given, QuantLib's empty handle; or a spread is given for a lattice that isn't a one-factor short-rate
    /// tree.
    /// </exception>
    public static CallableBondResults Calculate(
        CallableBond bond,
        EvaluationContext context,
        ShortRateModel model,
        Lattice lattice,
        YieldTermStructure? termStructure = null,
        double spread = 0.0)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(lattice);
        return CalculateCore(bond, context, model, lattice, 0, termStructure, spread);
    }

    // Instrument::calculate's steps around QuantLib's calculateWithSpread; a null lattice is built from the steps.
    private static CallableBondResults CalculateCore(
        CallableBond bond,
        EvaluationContext context,
        ShortRateModel model,
        Lattice? cached,
        int timeSteps,
        YieldTermStructure? termStructure,
        double spread)
    {
        if (bond.IsExpired(context))
        {
            return bond.SetupExpired();
        }

        CallableBondArguments arguments = bond.SetupArguments(context);
        arguments.Validate();

        CallableBondResults results = CalculateWithSpread(arguments, model, cached, timeSteps, termStructure, spread);
        return bond.FetchResults(results);
    }

    private static CallableBondResults CalculateWithSpread(
        CallableBondArguments arguments,
        ShortRateModel model,
        Lattice? cached,
        int timeSteps,
        YieldTermStructure? termStructure,
        double s)
    {
        YieldTermStructure discountCurve = model is ITermStructureConsistentModel tsmodel
            ? tsmodel.TermStructure
            : termStructure ?? throw new NanoQLException("empty Handle cannot be dereferenced");

        var callableBond = new DiscretizedCallableFixedRateBond(arguments, discountCurve, s);
        Lattice lattice;

        if (cached is not null)
        {
            lattice = cached;
        }
        else
        {
            lattice = LatticeShortRateModelEngine.Lattice(model, callableBond.MandatoryTimes().AsSpan(), timeSteps);
        }

        if (s != 0.0)
        {
            if (lattice is not OneFactorModelShortRateTree sr)
            {
                throw new NanoQLException("Spread is not supported for trees other than OneFactorModel");
            }

            lattice = sr.SetSpread(s);
        }

        Date referenceDate = discountCurve.ReferenceDate;
        DayCounter dayCounter = discountCurve.DayCounter;
        double redemptionTime = dayCounter.YearFraction(referenceDate, arguments.RedemptionDate);

        callableBond.Initialize(lattice, redemptionTime);
        callableBond.Rollback(0.0);

        double value = callableBond.PresentValue();

        double d = discountCurve.Discount(arguments.SettlementDate);
        return new CallableBondResults
        {
            NPV = value,
            ErrorEstimate = null,
            ValuationDate = null,
            SettlementValue = value / d,
        };
    }
}
