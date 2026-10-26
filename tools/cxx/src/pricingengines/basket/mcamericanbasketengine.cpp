// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/basket/mcamericanbasketengine.hpp, .cpp: the American basket path pricer and
// MakeMCAmericanBasketEngine, which the wheel doesn't expose. A payoff is [kind, optionType, strike, weights or null]:
// a "Min", "Max", "Spread" or "Average" basket (the average on the weights) of a plain vanilla payoff, or "Plain", the
// plain vanilla payoff alone. The evaluation date is the case's Settings.
//
// "AmericanBasketPathPricer::values" takes (assetNumber, payoff, polynomialOrder, polynomialType, times, paths): the
// pricer applied to a multi-path on TimeGrid(times) whose assets' values are the paths' (each a list of the grid's
// size, their number the multi-path's); it returns {"states", "values", "basis"}, at each grid index the state, the
// pricer's value and every basis function's value at the state.
//
// "MakeMCAmericanBasketEngine::NPV" takes (traits, processes, correlation, exercise, payoff, setters): the builder on
// PseudoRandom or LowDiscrepancy and QuantLib's Statistics over Black-Scholes-Merton processes, each [spot, r, q,
// volatility] on flat Act/365 (Fixed) forwards and a constant volatility from the evaluation date, in a
// StochasticProcessArray at the correlation; the setters applied in order, each [name, value]: "withSteps",
// "withStepsPerYear", "withBrownianBridge", "withAntitheticVariate", "withSamples", "withAbsoluteTolerance",
// "withMaxSamples", "withSeed", "withCalibrationSamples", "withPolynomialOrder" and "withBasisSystem"; a basket option
// on the payoff with the exercise, "European", "American" or "AmericanPayoffAtExpiry" (from the evaluation date to a
// serial), priced on the engine it makes. It returns the NPV; a setter's, the builder's or the engine's refusal is
// QuantLib's, its message the error's.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/basketoption.hpp>
#include <ql/instruments/payoffs.hpp>
#include <ql/math/statistics/statistics.hpp>
#include <ql/methods/montecarlo/multipath.hpp>
#include <ql/pricingengines/basket/mcamericanbasketengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/processes/stochasticprocessarray.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>
#include <ql/timegrid.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct PayoffSpec {
            std::string kind;
            Option::Type type;
            Real strike;
            std::vector<Real> weights;
        };

        PayoffSpec payoffSpec(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 4)
                throw InvalidParams("expected [kind, optionType, strike, weights], got " + json::serialize(v));
            const json::array& s = v.get_array();
            PayoffSpec out{text(s[0]), enumeration<Option::Type>(s[1]), real(s[2]), {}};
            if (out.kind != "Min" && out.kind != "Max" && out.kind != "Spread" && out.kind != "Average" &&
                out.kind != "Plain")
                throw InvalidParams("unknown payoff " + out.kind);
            if (out.kind == "Average")
                out.weights = reals(s[3]);
            return out;
        }

        ext::shared_ptr<Payoff> makePayoff(const PayoffSpec& p) {
            auto base = ext::make_shared<PlainVanillaPayoff>(p.type, p.strike);
            if (p.kind == "Min")
                return ext::make_shared<MinBasketPayoff>(base);
            if (p.kind == "Max")
                return ext::make_shared<MaxBasketPayoff>(base);
            if (p.kind == "Spread")
                return ext::make_shared<SpreadBasketPayoff>(base);
            if (p.kind == "Average")
                return ext::make_shared<AverageBasketPayoff>(base, Array(p.weights.begin(), p.weights.end()));
            return base;
        }

        std::vector<std::vector<Real>> pathsOf(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a list of paths, got " + json::serialize(v));
            std::vector<std::vector<Real>> paths;
            for (const json::value& path : v.get_array())
                paths.push_back(reals(path));
            if (paths.empty())
                throw InvalidParams("a multi-path needs an asset");
            return paths;
        }

        json::array realsArray(const Array& x) {
            json::array out;
            for (Real xi : x)
                out.emplace_back(xi);
            return out;
        }

        struct Setter {
            std::string name;
            Real value;
            bool flag;
        };

        Setter setter(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected [name, value], got " + json::serialize(v));
            const json::array& s = v.get_array();
            Setter out{text(s[0]), 0.0, false};
            if (out.name == "withBrownianBridge" || out.name == "withAntitheticVariate") {
                out.flag = boolean(s[1]);
            } else if (out.name == "withAbsoluteTolerance") {
                out.value = real(s[1]);
            } else if (out.name == "withSteps" || out.name == "withStepsPerYear" || out.name == "withSamples" ||
                       out.name == "withMaxSamples" || out.name == "withSeed" ||
                       out.name == "withCalibrationSamples" || out.name == "withPolynomialOrder" ||
                       out.name == "withBasisSystem") {
                long long n = int64(s[1]);
                if (n < 0)
                    throw InvalidParams(out.name + " takes a non-negative value");
                out.value = static_cast<Real>(n);
            } else {
                throw InvalidParams("unknown setter " + out.name);
            }
            return out;
        }

        ext::shared_ptr<StochasticProcessArray> processArray(const std::vector<std::vector<Real>>& specs,
                                                             Real correlation) {
            const Date today = Settings::instance().evaluationDate();
            const DayCounter dc = Actual365Fixed();
            std::vector<ext::shared_ptr<StochasticProcess1D>> processes;
            for (const std::vector<Real>& s : specs)
                processes.push_back(ext::make_shared<BlackScholesMertonProcess>(
                    Handle<Quote>(ext::make_shared<SimpleQuote>(s[0])),
                    Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, s[2], dc)),
                    Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, s[1], dc)),
                    Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(today, NullCalendar(), s[3], dc))));
            Matrix rho(processes.size(), processes.size(), correlation);
            for (Size i = 0; i < processes.size(); ++i)
                rho[i][i] = 1.0;
            return ext::make_shared<StochasticProcessArray>(processes, rho);
        }

        ext::shared_ptr<Exercise> makeExercise(const std::string& kind, const DateSpec& date) {
            const Date today = Settings::instance().evaluationDate();
            if (kind == "European")
                return ext::make_shared<EuropeanExercise>(makeDate(date));
            return ext::make_shared<AmericanExercise>(today, makeDate(date), kind == "AmericanPayoffAtExpiry");
        }

        template <class RNG>
        Real priced(const std::vector<std::vector<Real>>& specs, Real correlation, const std::string& exercise,
                    const DateSpec& date, const PayoffSpec& payoff, const std::vector<Setter>& setters) {
            MakeMCAmericanBasketEngine<RNG> make(processArray(specs, correlation));
            for (const Setter& s : setters) {
                if (s.name == "withSteps")
                    make.withSteps(static_cast<Size>(s.value));
                else if (s.name == "withStepsPerYear")
                    make.withStepsPerYear(static_cast<Size>(s.value));
                else if (s.name == "withBrownianBridge")
                    make.withBrownianBridge(s.flag);
                else if (s.name == "withAntitheticVariate")
                    make.withAntitheticVariate(s.flag);
                else if (s.name == "withSamples")
                    make.withSamples(static_cast<Size>(s.value));
                else if (s.name == "withAbsoluteTolerance")
                    make.withAbsoluteTolerance(s.value);
                else if (s.name == "withMaxSamples")
                    make.withMaxSamples(static_cast<Size>(s.value));
                else if (s.name == "withSeed")
                    make.withSeed(static_cast<BigNatural>(s.value));
                else if (s.name == "withCalibrationSamples")
                    make.withCalibrationSamples(static_cast<Size>(s.value));
                else if (s.name == "withPolynomialOrder")
                    make.withPolynomialOrder(static_cast<Size>(s.value));
                else
                    make.withBasisSystem(LsmBasisSystem::PolynomialType(static_cast<int>(s.value)));
            }
            ext::shared_ptr<PricingEngine> engine = make;
            BasketOption option(ext::dynamic_pointer_cast<BasketPayoff>(makePayoff(payoff)),
                                makeExercise(exercise, date));
            option.setPricingEngine(engine);
            return option.NPV();
        }

        const Register unit({
            {"AmericanBasketPathPricer::values",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 const Integer assetNumber = integer(a[0]);
                 if (assetNumber < 1)
                     throw InvalidParams("the asset number is at least 1");
                 const PayoffSpec payoff = payoffSpec(a[1]);
                 const Integer order = integer(a[2]);
                 const auto type = LsmBasisSystem::PolynomialType(integer(a[3]));
                 const std::vector<Real> times = reals(a[4]);
                 const std::vector<std::vector<Real>> paths = pathsOf(a[5]);
                 return [=] {
                     TimeGrid grid(times.begin(), times.end());
                     MultiPath multiPath(paths.size(), grid);
                     for (Size i = 0; i < paths.size(); ++i) {
                         if (paths[i].size() != grid.size())
                             throw InvalidParams("a path has the grid's size");
                         for (Size j = 0; j < grid.size(); ++j)
                             multiPath[i][j] = paths[i][j];
                     }
                     AmericanBasketPathPricer pricer(assetNumber, makePayoff(payoff), order, type);
                     const auto basis = pricer.basisSystem();
                     json::array states, values, basisValues;
                     for (Size t = 0; t < grid.size(); ++t) {
                         const Array state = pricer.state(multiPath, t);
                         states.emplace_back(realsArray(state));
                         values.emplace_back(pricer(multiPath, t));
                         json::array b;
                         for (const auto& f : basis)
                             b.emplace_back(f(state));
                         basisValues.emplace_back(b);
                     }
                     return json::value(
                         json::object{{"states", states}, {"values", values}, {"basis", basisValues}});
                 };
             }},
            {"MakeMCAmericanBasketEngine::NPV",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 const std::string traits = text(a[0]);
                 if (traits != "PseudoRandom" && traits != "LowDiscrepancy")
                     throw InvalidParams("unknown traits " + traits);
                 if (!a[1].is_array())
                     throw InvalidParams("expected a list of processes, got " + json::serialize(a[1]));
                 std::vector<std::vector<Real>> specs;
                 for (const json::value& p : a[1].get_array()) {
                     specs.push_back(reals(p));
                     if (specs.back().size() != 4)
                         throw InvalidParams("a process is [spot, r, q, volatility]");
                 }
                 const Real correlation = real(a[2]);
                 if (!a[3].is_array() || a[3].get_array().size() != 2)
                     throw InvalidParams("expected [kind, date], got " + json::serialize(a[3]));
                 const std::string exercise = text(a[3].get_array()[0]);
                 if (exercise != "European" && exercise != "American" && exercise != "AmericanPayoffAtExpiry")
                     throw InvalidParams("unknown exercise " + exercise);
                 const DateSpec date = harness::date(a[3].get_array()[1]);
                 const PayoffSpec payoff = payoffSpec(a[4]);
                 if (payoff.kind == "Plain")
                     throw InvalidParams("the option's payoff is a basket");
                 if (!a[5].is_array())
                     throw InvalidParams("expected a list of setters, got " + json::serialize(a[5]));
                 std::vector<Setter> setters;
                 for (const json::value& s : a[5].get_array())
                     setters.push_back(setter(s));
                 return [=] {
                     if (traits == "PseudoRandom")
                         return json::value(priced<PseudoRandom>(specs, correlation, exercise, date, payoff, setters));
                     return json::value(priced<LowDiscrepancy>(specs, correlation, exercise, date, payoff, setters));
                 };
             }},
        });

    }

}
