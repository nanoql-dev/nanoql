// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/fdhestonvanillaengine.hpp, .cpp: MakeFdHestonVanillaEngine and the engine's multiple-strikes
// caching, which the wheel doesn't expose. The process is models/equity/hestonspec.hpp's spec, the exercise
// ["European" | "American", date] (American from the process's reference date), the scheme [type, theta, mu]
// (QuantLib's FdmSchemeDesc). The evaluation date is the case's Settings.
//
// "MakeFdHestonVanillaEngine::results" takes (process, optionType, strike, exercise, setters) and prices a plain vanilla
// option on the engine the builder makes on HestonModel(process) after the setters, in order, each [name, ...]:
// ["withTGrid", n], ["withXGrid", n], ["withVGrid", n], ["withDampingSteps", n], ["withFdmSchemeDesc", type, theta,
// mu], ["withLeverageFunction", volatility] (a LocalConstantVol on the process's reference date and day counter),
// ["withQuantoHelper", quanto] (hestonspec.hpp's spec) and ["withCashDividends", dates, amounts]; it returns {"NPV",
// "delta", "gamma", "theta"}.
//
// "FdHestonVanillaEngine::multipleStrikes" takes (process, optionType, referenceStrike, exercise, strikes, tGrid,
// xGrid, vGrid, dampingSteps, scheme): an FdHestonVanillaEngine on HestonModel(process) with the grids and the scheme,
// enableMultipleStrikesCaching(strikes), a plain vanilla option at the reference strike priced on it, then an option at
// each strike, of the same type and exercise, priced on it from the cache; it returns {"strikes"}, each strike's
// {"NPV", "delta", "gamma", "theta"}.
//
// "FdHestonVanillaEngine::widenedResults" takes (process, optionType, strike, exercise, dividendDates, dividendAmounts,
// tGrid, xGrid, vGrid, dampingSteps, scheme): QuantLib's solve for the option assembled from the components the
// engine's getSolverDesc builds, in its order, with nanoql's rule for the equity grid: where QuantLib's mesher doesn't
// hold the band of the same mesher on the helper's curves in place at half its scale factor, with the strike widened by
// the same half margin, QuantLib's mesher again given its constraints at the union of the two ranges, a bound of
// QuantLib's that isn't a number short of the band's. It returns {"NPV", "delta", "gamma", "theta"} and "widened".

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/cashflows/dividend.hpp>
#include <ql/exercise.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/math/distributions/normaldistribution.hpp>
#include <ql/methods/finitedifferences/meshers/fdmblackscholesmesher.hpp>
#include <ql/methods/finitedifferences/meshers/fdmhestonvariancemesher.hpp>
#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/solvers/fdmbackwardsolver.hpp>
#include <ql/methods/finitedifferences/solvers/fdmhestonsolver.hpp>
#include <ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp>
#include <ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp>
#include <ql/models/equity/hestonmodel.hpp>
#include <ql/pricingengines/vanilla/fdhestonvanillaengine.hpp>
#include <ql/termstructures/volatility/equityfx/localconstantvol.hpp>

#include <algorithm>
#include <cmath>
#include <limits>
#include <optional>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct ExerciseSpec {
            bool american;
            DateSpec date;
        };

        ExerciseSpec exercise(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected [type, date], got " + json::serialize(v));
            std::string type = text(v.get_array()[0]);
            if (type != "European" && type != "American")
                throw InvalidParams("an exercise type is European or American");
            return {type == "American", date(v.get_array()[1])};
        }

        ext::shared_ptr<Exercise> makeExercise(const ExerciseSpec& e, const equity::ProcessSpec& p) {
            if (e.american)
                return ext::make_shared<AmericanExercise>(makeDate(p.referenceDate), makeDate(e.date));
            return ext::make_shared<EuropeanExercise>(makeDate(e.date));
        }

        struct SchemeSpec {
            FdmSchemeDesc::FdmSchemeType type;
            Real theta, mu;
        };

        SchemeSpec scheme(const json::value& type, const json::value& theta, const json::value& mu) {
            return {enumeration<FdmSchemeDesc::FdmSchemeType>(type), real(theta), real(mu)};
        }

        Size size(const json::value& v) {
            Integer n = integer(v);
            if (n < 0)
                throw InvalidParams("a size must be non-negative");
            return static_cast<Size>(n);
        }

        struct Setter {
            std::string name;
            Size n = 0;
            std::optional<SchemeSpec> scheme;
            Real volatility = 0.0;
            std::optional<equity::QuantoSpec> quanto;
            std::vector<DateSpec> dates;
            std::vector<Real> amounts;
        };

        Setter setter(const json::value& v) {
            if (!v.is_array() || v.get_array().empty())
                throw InvalidParams("expected [name, ...], got " + json::serialize(v));
            const json::array& s = v.get_array();
            Setter out{text(s[0])};
            auto args = [&s, &out](std::size_t n) {
                if (s.size() != n + 1)
                    throw InvalidParams(out.name + " takes " + std::to_string(n) + " arguments");
            };
            if (out.name == "withTGrid" || out.name == "withXGrid" || out.name == "withVGrid" ||
                out.name == "withDampingSteps") {
                args(1);
                out.n = size(s[1]);
            } else if (out.name == "withFdmSchemeDesc") {
                args(3);
                out.scheme = scheme(s[1], s[2], s[3]);
            } else if (out.name == "withLeverageFunction") {
                args(1);
                out.volatility = real(s[1]);
            } else if (out.name == "withQuantoHelper") {
                args(1);
                out.quanto = equity::quanto(s[1]);
            } else if (out.name == "withCashDividends") {
                args(2);
                if (!s[1].is_array())
                    throw InvalidParams("expected a list of dates, got " + json::serialize(s[1]));
                for (const json::value& d : s[1].get_array())
                    out.dates.push_back(date(d));
                out.amounts = reals(s[2]);
            } else {
                throw InvalidParams("unknown setter " + out.name);
            }
            return out;
        }

        json::value results(VanillaOption& option) {
            return equity::vanillaResults(option.NPV(), option.delta(), option.gamma(), option.theta());
        }

        // QuantLib's getSolverDesc assembled from its components (fdhestonvanillaengine.cpp:103-166: the variance
        // mesher, the helper process in its call's order, FdmBlackScholesMesher, FdmLogInnerValue, vanillaComposite),
        // with nanoql's rule for the equity grid: where the mesher's range doesn't hold the band of the same mesher on
        // the helper's curves in place at half its scale factor, the strike widened by the same half margin with it,
        // the mesher again with QuantLib's constraints set to the union of the two ranges, a bound that isn't a number
        // short of the band's. Each range is a mesher's first and last locations, which QuantLib's 1-D meshers pin to
        // their bounds. Returns the solve's {"NPV", "delta", "gamma", "theta"} at the spot and v0, and "widened".
        json::value widenedResults(const equity::ProcessSpec& p, Option::Type type, Real strike, const ExerciseSpec& e,
                                   const std::vector<DateSpec>& dividendDates, const std::vector<Real>& dividendAmounts,
                                   Size tGrid, Size xGrid, Size vGrid, Size dampingSteps, const SchemeSpec& s) {
            ext::shared_ptr<HestonProcess> process = equity::makeProcess(p);
            ext::shared_ptr<Exercise> exercise = makeExercise(e, p);
            const Time maturity = process->time(exercise->lastDate());
            DividendSchedule dividends;
            for (Size i = 0; i < dividendDates.size(); ++i)
                dividends.push_back(ext::make_shared<FixedDividend>(dividendAmounts[i], makeDate(dividendDates[i])));
            const Size tGridAvgSteps = std::max(Size(5), tGrid / 50);
            auto vMesher = ext::make_shared<FdmHestonLocalVolatilityVarianceMesher>(
                vGrid, process, ext::shared_ptr<LocalVolTermStructure>(), maturity, tGridAvgSteps, 0.0001, 1.0);
            const Real vol = vMesher->volaEstimate();
            auto helper = FdmBlackScholesMesher::processHelper(process->s0(), process->dividendYield(),
                                                               process->riskFreeRate(), vol);
            auto inPlace = FdmBlackScholesMesher::processHelper(process->s0(), process->riskFreeRate(),
                                                                process->dividendYield(), vol);
            const std::pair<Real, Real> cPoint(strike, 0.1);
            const auto helperMesher = [&](Real lower, Real upper) {
                return ext::make_shared<FdmBlackScholesMesher>(xGrid, helper, maturity, strike, lower, upper, 0.0001,
                                                               2.0, cPoint, dividends);
            };
            const FdmBlackScholesMesher band(xGrid, inPlace, maturity, strike, Null<Real>(), Null<Real>(), 0.0001, 1.0,
                                             cPoint, dividends);
            // and the strike by the same half margin
            const Real halfMargin = vol * std::sqrt(maturity) * InverseCumulativeNormal()(1 - 0.0001) * 1.0;
            const Real bandMin = std::min(band.locations().front(), std::log(strike) - halfMargin);
            const Real bandMax = std::max(band.locations().back(), std::log(strike) + halfMargin);
            // QuantLib's mesher on the helper; where one of its bounds isn't a number (its swapped walk below zero) its
            // 1-D mesher refuses, and each bound is read from the mesher with the other side given the band's bound,
            // NaN where that refuses too
            ext::shared_ptr<Fdm1dMesher> equityMesher;
            Real xMin = std::numeric_limits<Real>::quiet_NaN(), xMax = std::numeric_limits<Real>::quiet_NaN();
            try {
                equityMesher = helperMesher(Null<Real>(), Null<Real>());
                xMin = equityMesher->locations().front();
                xMax = equityMesher->locations().back();
            } catch (const Error&) {
                try {
                    xMax = helperMesher(bandMin, Null<Real>())->locations().back();
                } catch (const Error&) {
                }
                try {
                    xMin = helperMesher(Null<Real>(), bandMax)->locations().front();
                } catch (const Error&) {
                }
            }
            const bool lowShort = std::isnan(xMin) ? !std::isnan(bandMin) : bandMin < xMin;
            const bool highShort = std::isnan(xMax) ? !std::isnan(bandMax) : bandMax > xMax;
            const bool widened = lowShort || highShort;
            if (widened)
                equityMesher = helperMesher(lowShort ? bandMin : xMin, highShort ? bandMax : xMax);
            else if (!equityMesher)
                equityMesher = helperMesher(Null<Real>(), Null<Real>());
            auto mesher = ext::make_shared<FdmMesherComposite>(equityMesher, vMesher);
            auto payoff = ext::make_shared<PlainVanillaPayoff>(type, strike);
            auto calculator = ext::make_shared<FdmLogInnerValue>(payoff, mesher, 0);
            auto conditions = FdmStepConditionComposite::vanillaComposite(
                dividends, exercise, mesher, calculator, process->riskFreeRate()->referenceDate(),
                process->riskFreeRate()->dayCounter());
            FdmSolverDesc desc = {mesher, FdmBoundaryConditionSet(), conditions, calculator, maturity, tGrid,
                                  dampingSteps};
            FdmHestonSolver solver(Handle<HestonProcess>(process), desc, FdmSchemeDesc(s.type, s.theta, s.mu));
            const Real spot = process->s0()->value(), v0 = process->v0();
            json::value out = equity::vanillaResults(solver.valueAt(spot, v0), solver.deltaAt(spot, v0),
                                                     solver.gammaAt(spot, v0), solver.thetaAt(spot, v0));
            out.get_object()["widened"] = widened;
            return out;
        }

        const Register unit({
            {"MakeFdHestonVanillaEngine::results",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 equity::ProcessSpec p = equity::process(a[0]);
                 auto optionType = enumeration<Option::Type>(a[1]);
                 Real strike = real(a[2]);
                 ExerciseSpec e = exercise(a[3]);
                 if (!a[4].is_array())
                     throw InvalidParams("expected a list of setters, got " + json::serialize(a[4]));
                 std::vector<Setter> setters;
                 for (const json::value& s : a[4].get_array())
                     setters.push_back(setter(s));
                 return [=] {
                     MakeFdHestonVanillaEngine make(ext::make_shared<HestonModel>(equity::makeProcess(p)));
                     for (const Setter& s : setters) {
                         if (s.name == "withTGrid")
                             make.withTGrid(s.n);
                         else if (s.name == "withXGrid")
                             make.withXGrid(s.n);
                         else if (s.name == "withVGrid")
                             make.withVGrid(s.n);
                         else if (s.name == "withDampingSteps")
                             make.withDampingSteps(s.n);
                         else if (s.name == "withFdmSchemeDesc")
                             make.withFdmSchemeDesc(FdmSchemeDesc(s.scheme->type, s.scheme->theta, s.scheme->mu));
                         else if (s.name == "withLeverageFunction") {
                             ext::shared_ptr<LocalVolTermStructure> leverage = ext::make_shared<LocalConstantVol>(
                                 makeDate(p.referenceDate), s.volatility, makeDayCounter(p.dayCounter));
                             make.withLeverageFunction(leverage);
                         }
                         else if (s.name == "withQuantoHelper")
                             make.withQuantoHelper(equity::makeQuanto(s.quanto, p));
                         else {
                             std::vector<Date> dates;
                             for (const DateSpec& d : s.dates)
                                 dates.push_back(makeDate(d));
                             make.withCashDividends(dates, s.amounts);
                         }
                     }
                     VanillaOption option(ext::make_shared<PlainVanillaPayoff>(optionType, strike),
                                          makeExercise(e, p));
                     option.setPricingEngine(make);
                     return results(option);
                 };
             }},
            {"FdHestonVanillaEngine::multipleStrikes",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 equity::ProcessSpec p = equity::process(a[0]);
                 auto optionType = enumeration<Option::Type>(a[1]);
                 Real referenceStrike = real(a[2]);
                 ExerciseSpec e = exercise(a[3]);
                 std::vector<Real> strikes = reals(a[4]);
                 Size tGrid = size(a[5]), xGrid = size(a[6]), vGrid = size(a[7]), dampingSteps = size(a[8]);
                 if (!a[9].is_array() || a[9].get_array().size() != 3)
                     throw InvalidParams("expected [type, theta, mu], got " + json::serialize(a[9]));
                 SchemeSpec s = scheme(a[9].get_array()[0], a[9].get_array()[1], a[9].get_array()[2]);
                 return [=] {
                     auto engine = ext::make_shared<FdHestonVanillaEngine>(
                         ext::make_shared<HestonModel>(equity::makeProcess(p)), tGrid, xGrid, vGrid, dampingSteps,
                         FdmSchemeDesc(s.type, s.theta, s.mu));
                     engine->enableMultipleStrikesCaching(strikes);
                     ext::shared_ptr<Exercise> ex = makeExercise(e, p);
                     VanillaOption reference(ext::make_shared<PlainVanillaPayoff>(optionType, referenceStrike), ex);
                     reference.setPricingEngine(engine);
                     reference.NPV();
                     json::array out;
                     for (Real k : strikes) {
                         VanillaOption option(ext::make_shared<PlainVanillaPayoff>(optionType, k), ex);
                         option.setPricingEngine(engine);
                         out.push_back(results(option));
                     }
                     json::object r;
                     r["strikes"] = out;
                     return json::value(r);
                 };
             }},
            {"FdHestonVanillaEngine::widenedResults",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 equity::ProcessSpec p = equity::process(a[0]);
                 auto optionType = enumeration<Option::Type>(a[1]);
                 Real strike = real(a[2]);
                 ExerciseSpec e = exercise(a[3]);
                 if (!a[4].is_array())
                     throw InvalidParams("expected a list of dividend dates, got " + json::serialize(a[4]));
                 std::vector<DateSpec> dividendDates;
                 for (const json::value& d : a[4].get_array())
                     dividendDates.push_back(date(d));
                 std::vector<Real> dividendAmounts = reals(a[5]);
                 if (dividendAmounts.size() != dividendDates.size())
                     throw InvalidParams("a dividend amount per date");
                 Size tGrid = size(a[6]), xGrid = size(a[7]), vGrid = size(a[8]), dampingSteps = size(a[9]);
                 if (!a[10].is_array() || a[10].get_array().size() != 3)
                     throw InvalidParams("expected [type, theta, mu], got " + json::serialize(a[10]));
                 SchemeSpec s = scheme(a[10].get_array()[0], a[10].get_array()[1], a[10].get_array()[2]);
                 return [=] {
                     return widenedResults(p, optionType, strike, e, dividendDates, dividendAmounts, tGrid, xGrid,
                                           vGrid, dampingSteps, s);
                 };
             }},
        });

    }

}
