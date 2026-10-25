// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/hestonslvfdmmodel.hpp ql/models/equity/hestonslvfdmmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The Heston stochastic-local volatility model calibrated by the Fokker-Planck forward equation: the leverage function
/// L(t, S) that makes the model's spot follow the local volatility's marginals, σ_LV(t, S)²/E[v | S], solved step by step
/// on the joint density of the log spot and the variance.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: the calibration runs at construction (the S-hes plan's HR3 (i), master §2.2's <c>LazyObject</c> row),
/// where QuantLib's model calibrates at its first read. With logging, <see cref="LogEntries"/> holds each step's density
/// and mesher, filled once (HR3 (ii)), where QuantLib's log read recomputes the calibration for the same values. The
/// leverage function is <see cref="FixedLocalVolSurface"/> on the calibrated values; the calibration itself reads its
/// working surface as QuantLib's does (<see cref="FixedLocalVolWorkingSurface"/>).
/// </para>
/// <para>
/// QuantLib appends the mandatory dates' times to its exponential grid unsorted and builds the leverage surface on that
/// list, which refuses a mandatory date inside the period ("dates must be sorted unique!") and one at its end ("need
/// strikes for every time step") (<c>hestonslvfdmmodel.cpp:325-330</c>, <c>:412</c>). Here the surface and the loop
/// run on the time grid's times, sorted and made unique; a mandatory date on the grid's own time is QuantLib's
/// calibration without it (HR7 (xx), under R8).
/// </para>
/// <para>
/// The model's times are a clock's, from the earliest reference date of the curves and the local volatility on the
/// risk-free curve's day counter, and each structure is read at the instants they denote: QuantLib's grid runs from the
/// risk-free curve's reference date and reads the dividend curve and the local volatility at its times (HR7 (xxi)).
/// Where every structure shares the risk-free curve's day counter and reference date, QuantLib's reads and bits.
/// </para>
/// <para>
/// The first step's density, the Green's function, is the density of the process the forward operator evolves, its
/// variance's volatility the mixing factor times σ, as the variance mesher's: QuantLib's takes the unmixed σ, a density
/// 76% to 113% off the mixed process's own in L1 on the Gaussian algorithm at mixing factors 0.5 and 0.2, where the mixed
/// σ's is within 2% to 10% (D4-1 of the S-hes run record, under R8). At a mixing factor of 1, QuantLib's.
/// </para>
/// </remarks>
public sealed class HestonSLVFDMModel
{
    // QuantLib's Null<Real>()
    private const double NullReal = 3.4028234663852886e38;

    private readonly LocalVolTermStructure localVol;
    private readonly HestonModel hestonModel;
    private readonly FixedLocalVolSurface leverageFunction;
    private readonly ImmutableArray<HestonSLVFDMModelLogEntry> logEntries;

    /// <summary>Initializes a new instance of the <see cref="HestonSLVFDMModel"/> class, calibrating it.</summary>
    /// <param name="localVol">The local volatility.</param>
    /// <param name="hestonModel">The Heston model.</param>
    /// <param name="endDate">The final calibration date.</param>
    /// <param name="parameters">The calibration's settings.</param>
    /// <param name="logging">Whether to keep each step's density and mesher.</param>
    /// <param name="mandatoryDates">Dates the time grid holds, or <see langword="null"/> for none.</param>
    /// <param name="mixingFactor">The factor scaling the variance's volatility σ.</param>
    /// <exception cref="ArgumentNullException">A structure, the model or the settings are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The end date is the default date, the clock's reference date isn't before it ("reference date must be smaller
    /// than final calibration date"), or it lies beyond the local volatility ("final calibration maturity exceeds local
    /// volatility surface"), with QuantLib's messages.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The settings' scheme isn't one the calibration runs ("Unknown scheme
    /// type").</exception>
    /// <exception cref="NanoQLException">A structure can't be read, or a step can't be solved.</exception>
    public HestonSLVFDMModel(
        LocalVolTermStructure localVol,
        HestonModel hestonModel,
        Date endDate,
        HestonSLVFokkerPlanckFdmParams parameters,
        bool logging = false,
        IEnumerable<Date>? mandatoryDates = null,
        double mixingFactor = 1.0)
    {
        ArgumentNullException.ThrowIfNull(localVol);
        ArgumentNullException.ThrowIfNull(hestonModel);
        ArgumentNullException.ThrowIfNull(parameters);
        Date.ThrowIfDefault(endDate, nameof(endDate));
        this.localVol = localVol;
        this.hestonModel = hestonModel;
        (leverageFunction, logEntries) = Calibrate(
            localVol, hestonModel.Process, endDate, parameters, logging, [.. mandatoryDates ?? []], mixingFactor);
    }

    /// <summary>Gets the Heston process.</summary>
    public HestonProcess HestonProcess => hestonModel.Process;

    /// <summary>Gets the local volatility.</summary>
    public LocalVolTermStructure LocalVol => localVol;

    /// <summary>Gets the calibrated leverage function.</summary>
    public LocalVolTermStructure LeverageFunction => leverageFunction;

    /// <summary>Gets each calibration step's density and mesher with logging on; empty with it off.</summary>
    public ImmutableArray<HestonSLVFDMModelLogEntry> LogEntries => logEntries;

    private static (FixedLocalVolSurface Leverage, ImmutableArray<HestonSLVFDMModelLogEntry> Log) Calibrate(
        LocalVolTermStructure localVol,
        HestonProcess hestonProcess,
        Date endDate,
        HestonSLVFokkerPlanckFdmParams p,
        bool logging,
        Date[] mandatoryDates,
        double mixingFactor)
    {
        var log = ImmutableArray.CreateBuilder<HestonSLVFDMModelLogEntry>();

        double spot = hestonProcess.S0;
        YieldTermStructure rTS = hestonProcess.RiskFreeRate;
        YieldTermStructure qTS = hestonProcess.DividendYield;

        double v0 = hestonProcess.V0;
        double kappa = hestonProcess.Kappa;
        double theta = hestonProcess.Theta;
        double sigma = hestonProcess.Sigma;
        double mixedSigma = mixingFactor * sigma;
        double alpha = 2 * kappa * theta / (mixedSigma * mixedSigma);

        int xGrid = p.XGrid;
        int vGrid = p.VGrid;

        // the model's clock (HR7 (xxi)): the risk-free curve where no structure is dated before it
        TermStructure clock = FdmHestonOp.Clock(hestonProcess, null, localVol);
        LocalVolTermStructure localVolOnClock = localVol.SharesTimes(clock)
            ? localVol
            : new LocalVolTermStructureOnClock(localVol, clock);
        Date referenceDate = clock.ReferenceDate;
        DayCounter dc = clock.DayCounter;

        double T = dc.YearFraction(referenceDate, endDate);

        if (!(referenceDate < endDate))
        {
            throw new ArgumentException("reference date must be smaller than final calibration date", nameof(endDate));
        }

        if (!(localVolOnClock.MaxTime >= T))
        {
            throw new ArgumentException("final calibration maturity exceeds local volatility surface", nameof(endDate));
        }

        // set-up exponential time step scheme
        double maxDt = 1.0 / p.TMaxStepsPerYear;
        double minDt = 1.0 / p.TMinStepsPerYear;

        double tIdx = 0.0;
        var times = new List<double> { tIdx };
        while (tIdx < T)
        {
            double decayFactor = Math.Exp(-p.TStepNumberDecay * tIdx);
            double dt = (maxDt * decayFactor) + (minDt * (1.0 - decayFactor));

            tIdx += dt;
            times.Add(Min(T, tIdx));
        }

        foreach (Date mandatoryDate in mandatoryDates)
        {
            times.Add(dc.YearFraction(referenceDate, mandatoryDate));
        }

        // the grid's times, sorted and made unique, for the surface and the loop (HR7 (xx))
        var timeGrid = new TimeGrid(times.ToArray());
        double[] gridTimes = [.. timeGrid];

        // build 1d meshers
        var localVolRND = new LocalVolRNDCalculator(
            spot, rTS, qTS, localVolOnClock, timeGrid, xGrid, p.X0Density, p.LocalVolEpsProb, p.MaxIntegrationIterations);

        ImmutableArray<int> rescaleSteps = localVolRND.RescaleTimeSteps();

        var squareRootRnd = new SquareRootProcessRNDCalculator(v0, kappa, theta, mixedSigma);

        FdmSquareRootFwdOpTransformationType trafoType = p.TrafoType;

        var xMesher = new List<Fdm1dMesher>(timeGrid.Size);
        var vMesher = new List<Fdm1dMesher>(timeGrid.Size);

        xMesher.Add(localVolRND.Mesher(0.0));
        vMesher.Add(new Predefined1dMesher(Enumerable.Repeat(v0, vGrid)));

        int rescaleIdx = 0;
        for (int i = 1; i < timeGrid.Size; ++i)
        {
            xMesher.Add(localVolRND.Mesher(timeGrid[i]));

            if ((rescaleIdx < rescaleSteps.Length) && (i == rescaleSteps[rescaleIdx]))
            {
                ++rescaleIdx;
                vMesher.Add(VarianceMesher(
                    squareRootRnd,
                    timeGrid[rescaleSteps[rescaleIdx - 1]],
                    (rescaleIdx < rescaleSteps.Length) ? timeGrid[rescaleSteps[rescaleIdx]] : timeGrid.Back(),
                    vGrid,
                    v0,
                    p));
            }
            else
            {
                vMesher.Add(vMesher[^1]);
            }
        }

        // start probability distribution
        var mesher = new FdmMesherComposite(xMesher[1], vMesher[1]);

        double lv0 = localVolOnClock.LocalVol(0.0, spot) / Math.Sqrt(v0);

        var l = new Matrix(xGrid, timeGrid.Size);

        double l0 = lv0;
        for (int j = 0; j < xGrid; ++j)
        {
            l[j, 0] = l0;
            l[j, 1] = l0;
        }

        // create strikes from meshers
        double[][] vStrikes = new double[timeGrid.Size][];

        for (int i = 0; i < timeGrid.Size; ++i)
        {
            vStrikes[i] = new double[xGrid];
            ImmutableArray<double> locations = xMesher[i].Locations();
            if (locations[0] == locations[^1])
            {
                Array.Fill(vStrikes[i], Math.Exp(locations[0]));
            }
            else
            {
                for (int j = 0; j < xGrid; ++j)
                {
                    vStrikes[i][j] = Math.Exp(locations[j]);
                }
            }
        }

        var leverageFct = new FixedLocalVolWorkingSurface(referenceDate, gridTimes, vStrikes, l, dc);

        FdmLinearOpComposite hestonFwdOp = new FdmHestonFwdOp(mesher, hestonProcess, trafoType, leverageFct, mixingFactor, clock);

        // the first step's density on the process the operator evolves, its variance's volatility mixed: QuantLib's is
        // on the unmixed σ, a density the operator's process doesn't have (D4-1 of the S-hes run record)
        HestonProcess greensProcess = mixingFactor == 1.0
            ? hestonProcess
            : new HestonProcess(rTS, qTS, spot, v0, kappa, theta, mixedSigma, hestonProcess.Rho);
        double[] prob = [.. new FdmHestonGreensFct(mesher, greensProcess, trafoType, lv0, clock)
            .Get(timeGrid[1], p.GreensAlgorithm).AsSpan()];

        if (logging)
        {
            log.Add(new HestonSLVFDMModelLogEntry(timeGrid[1], [.. prob], mesher));
        }

        double[] pSlice = new double[vGrid];
        double[] weighted = new double[vGrid];
        for (int i = 2; i < timeGrid.Size; ++i)
        {
            double t = timeGrid[i];
            double dt = t - timeGrid[i - 1];

            if (!ReferenceEquals(mesher.GetFdm1dMeshers()[0], xMesher[i])
                || !ReferenceEquals(mesher.GetFdm1dMeshers()[1], vMesher[i]))
            {
                var newMesher = new FdmMesherComposite(xMesher[i], vMesher[i]);

                prob = ReshapePDF(prob, mesher, newMesher);
                mesher = newMesher;

                prob = RescalePDF(prob, mesher, trafoType, alpha);

                hestonFwdOp = new FdmHestonFwdOp(mesher, hestonProcess, trafoType, leverageFct, mixingFactor, clock);
            }

            double[] pn = (double[])prob.Clone();
            double[] x = [.. mesher.GetFdm1dMeshers()[0].Locations().Select(Math.Exp)];
            ImmutableArray<double> v = mesher.GetFdm1dMeshers()[1].Locations();

            // predictor corrector steps
            for (int r = 0; r < p.PredictionCorretionSteps; ++r)
            {
                FdmSchemeDesc fdmSchemeDesc = (i < p.NRannacherTimeSteps + 2)
                    ? FdmSchemeDesc.ImplicitEuler()
                    : p.SchemeDesc;

                IFdmScheme fdmScheme = FdmSchemeFactory(fdmSchemeDesc, hestonFwdOp);

                for (int j = 0; j < x.Length; ++j)
                {
                    for (int k = 0; k < vGrid; ++k)
                    {
                        pSlice[k] = pn[j + (k * xGrid)];
                    }

                    double pInt;
                    if (trafoType == FdmSquareRootFwdOpTransformationType.Power)
                    {
                        for (int k = 0; k < vGrid; ++k)
                        {
                            weighted[k] = Math.Pow(v[k], alpha - 1) * pSlice[k];
                        }

                        pInt = DiscreteSimpsonIntegral.Integrate(v.AsSpan(), weighted);
                    }
                    else
                    {
                        pInt = DiscreteSimpsonIntegral.Integrate(v.AsSpan(), pSlice);
                    }

                    for (int k = 0; k < vGrid; ++k)
                    {
                        weighted[k] = trafoType == FdmSquareRootFwdOpTransformationType.Log ? Math.Exp(v[k]) * pSlice[k]
                            : trafoType == FdmSquareRootFwdOpTransformationType.Power ? Math.Pow(v[k], alpha) * pSlice[k]
                            : v[k] * pSlice[k];
                    }

                    double vpInt = DiscreteSimpsonIntegral.Integrate(v.AsSpan(), weighted);

                    double scale = pInt / vpInt;
                    double localVolValue = localVolOnClock.LocalVol(t, x[j]);

                    double lj = (scale >= 0.0) ? localVolValue * Math.Sqrt(scale) : 1.0;

                    l[j, i] = Min(50.0, Max(0.001, lj));
                }

                double sLowerBound = Max(x[0], Math.Exp(localVolRND.InvCdf(p.LeverageFctPropEps, t)));
                double sUpperBound = Min(x[^1], Math.Exp(localVolRND.InvCdf(1.0 - p.LeverageFctPropEps, t)));

                leverageFct.Reinterpolate(i);
                double lowerL = leverageFct.LocalVol(t, sLowerBound);
                double upperL = leverageFct.LocalVol(t, sUpperBound);

                for (int j = 0; j < x.Length; ++j)
                {
                    if (x[j] < sLowerBound)
                    {
                        l[j, i] = lowerL;
                    }
                    else if (x[j] > sUpperBound)
                    {
                        l[j, i] = upperL;
                    }
                    else if (l[j, i] == NullReal)
                    {
                        throw new NanoQLException("internal error");
                    }
                }

                leverageFct.Reinterpolate(i);

                pn = (double[])prob.Clone();

                fdmScheme.SetStep(dt);
                fdmScheme.Step(pn, t);
            }

            prob = pn;
            prob = RescalePDF(prob, mesher, trafoType, alpha);

            if (logging)
            {
                log.Add(new HestonSLVFDMModelLogEntry(t, [.. prob], mesher));
            }
        }

        return (leverageFct.ToSurface(), log.ToImmutable());
    }

    // QuantLib's varianceMesher: the square-root process's quantiles over [t0, t1] at eleven points, the lower one at
    // least vMin, concentrated at both bounds and at v0, in the transformation's variable
    private static Fdm1dMesher VarianceMesher(
        SquareRootProcessRNDCalculator rnd, double t0, double t1, int vGrid, double v0, HestonSLVFokkerPlanckFdmParams p)
    {
        double v0Density = p.V0Density;
        double upperBoundDensity = p.VUpperBoundDensity;
        double lowerBoundDensity = p.VLowerBoundDensity;

        double lowerBound = NullReal, upperBound = -NullReal;

        for (int i = 0; i <= 10; ++i)
        {
            double t = t0 + (i / 10.0 * (t1 - t0));
            lowerBound = Min(lowerBound, rnd.InvCdf(p.VLowerEps, t));
            upperBound = Max(upperBound, rnd.InvCdf(1.0 - p.VUpperEps, t));
        }

        lowerBound = Max(lowerBound, p.VMin);
        double v0Center = v0;
        if (p.TrafoType == FdmSquareRootFwdOpTransformationType.Log)
        {
            lowerBound = Math.Log(lowerBound);
            upperBound = Math.Log(upperBound);
            v0Center = Math.Log(v0);
        }

        (double, double, bool)[] cPoints =
        [
            (lowerBound, lowerBoundDensity, false),
            (v0Center, v0Density, true),
            (upperBound, upperBoundDensity, false),
        ];

        return new Concentrating1dMesher(lowerBound, upperBound, vGrid, cPoints, 1e-8);
    }

    private static double IntegratePDF(
        double[] prob, FdmMesherComposite mesher, FdmSquareRootFwdOpTransformationType trafoType, double alpha)
    {
        var integral = new FdmMesherIntegral(mesher, (x, f) => DiscreteSimpsonIntegral.Integrate(x.AsSpan(), f.AsSpan()));
        if (trafoType != FdmSquareRootFwdOpTransformationType.Power)
        {
            return integral.Integrate(new Vector(prob));
        }

        var tp = new Vector(prob.Length);
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < mesher.Layout.Size; ++k, iter.Increment())
        {
            int idx = iter.Index;
            double nu = mesher.Location(iter, 1);

            tp[idx] = prob[idx] * Math.Pow(nu, alpha - 1);
        }

        return integral.Integrate(tp);
    }

    private static double[] RescalePDF(
        double[] prob, FdmMesherComposite mesher, FdmSquareRootFwdOpTransformationType trafoType, double alpha)
    {
        double integral = IntegratePDF(prob, mesher, trafoType, alpha);
        double[] result = new double[prob.Length];
        for (int i = 0; i < prob.Length; ++i)
        {
            result[i] = prob[i] / integral;
        }

        return result;
    }

    // QuantLib's reshapePDF<Bilinear>: the density on the old mesher interpolated bilinearly onto the new one, 0 outside
    // the old mesher's range
    private static double[] ReshapePDF(double[] prob, FdmMesherComposite oldMesher, FdmMesherComposite newMesher)
    {
        if (!(oldMesher.Layout.Size == newMesher.Layout.Size && oldMesher.Layout.Size == prob.Length))
        {
            throw new NanoQLException("inconsistent mesher or vector size given");
        }

        var m = new Matrix(oldMesher.Layout.Dim[1], oldMesher.Layout.Dim[0]);
        for (int i = 0; i < m.Rows; ++i)
        {
            for (int j = 0; j < m.Columns; ++j)
            {
                m[i, j] = prob[(i * m.Columns) + j];
            }
        }

        var interpol = new BilinearInterpolation(
            oldMesher.GetFdm1dMeshers()[0].Locations().AsSpan(), oldMesher.GetFdm1dMeshers()[1].Locations().AsSpan(), m);

        double[] pNew = new double[prob.Length];
        FdmLinearOpIterator iter = newMesher.Layout.Begin();
        for (int k = 0; k < newMesher.Layout.Size; ++k, iter.Increment())
        {
            double x = newMesher.Location(iter, 0);
            double v = newMesher.Location(iter, 1);

            if (x > interpol.XMax || x < interpol.XMin || v > interpol.YMax || v < interpol.YMin)
            {
                pNew[iter.Index] = 0;
            }
            else
            {
                pNew[iter.Index] = interpol.Evaluate(x, v);
            }
        }

        return pNew;
    }

    // QuantLib's fdmSchemeFactory: the schemes the calibration runs
    private static IFdmScheme FdmSchemeFactory(FdmSchemeDesc desc, FdmLinearOpComposite op) => desc.Type switch
    {
        FdmSchemeDescFdmSchemeType.HundsdorferType => new HundsdorferScheme(desc.Theta, desc.Mu, op),
        FdmSchemeDescFdmSchemeType.DouglasType => new DouglasScheme(desc.Theta, op),
        FdmSchemeDescFdmSchemeType.CraigSneydType => new CraigSneydScheme(desc.Theta, desc.Mu, op),
        FdmSchemeDescFdmSchemeType.ModifiedCraigSneydType => new ModifiedCraigSneydScheme(desc.Theta, desc.Mu, op),
        FdmSchemeDescFdmSchemeType.ImplicitEulerType => new ImplicitEulerScheme(op),
        FdmSchemeDescFdmSchemeType.ExplicitEulerType => new ExplicitEulerScheme(op),
        _ => throw new ArgumentOutOfRangeException(nameof(desc), desc.Type, "Unknown scheme type"),
    };

    // std::max and std::min
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;
}
