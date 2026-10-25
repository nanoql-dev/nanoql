// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/mceuropeangjrgarchengine.hpp: the path pricer and MakeMCEuropeanGJRGARCHEngine, which the
// wheel doesn't expose. The evaluation date is the case's Settings.
//
// A process is {"referenceDate", "dayCounter", "riskFree", "dividend", "s0", "v0", "omega", "alpha", "beta", "gamma",
// "lambda", "daysPerYear"}: QuantLib's GJRGARCHProcess, under its default discretization, on flat forwards at the two
// rates from the reference date on the day counter.
//
// "EuropeanGJRGARCHPathPricer::value" takes (optionType, strike, discount, times, paths): the pricer on the payoff and
// the discount, applied to a multi-path on TimeGrid(times) whose assets' values are the paths' (each a list of the
// grid's size); it returns the value.
//
// "MakeMCEuropeanGJRGARCHEngine::results" takes (traits, process, optionType, strike, exerciseDate, setters): the
// builder on PseudoRandom or LowDiscrepancy and QuantLib's Statistics over the process, the setters applied in order,
// each [name, value]: "withSteps", "withStepsPerYear", "withSamples", "withAbsoluteTolerance", "withMaxSamples",
// "withSeed" and "withAntitheticVariate"; a European plain vanilla option priced on the engine it makes. It returns
// {"NPV", "errorEstimate"}, the error estimate null under LowDiscrepancy, which gives none; a setter's or the builder's
// refusal is QuantLib's, its message the error's.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/methods/montecarlo/multipath.hpp>
#include <ql/pricingengines/vanilla/mceuropeangjrgarchengine.hpp>
#include <ql/processes/gjrgarchprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/timegrid.hpp>

#include <initializer_list>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct ProcessSpec {
            DateSpec referenceDate{true, 0};
            DayCounterSpec dayCounter;
            Real riskFree = 0.0, dividend = 0.0, s0 = 0.0, v0 = 0.0, omega = 0.0, alpha = 0.0, beta = 0.0,
                 gamma = 0.0, lambda = 0.0, daysPerYear = 0.0;
        };

        ProcessSpec process(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a GJR-GARCH process spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : {"referenceDate", "dayCounter", "riskFree", "dividend", "s0", "v0", "omega",
                                      "alpha", "beta", "gamma", "lambda", "daysPerYear"})
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams("a GJR-GARCH process takes no key " + std::string(kv.key()));
            }
            const auto key = [&](const char* name) -> const json::value& {
                const json::value* x = o.if_contains(name);
                if (x == nullptr)
                    throw InvalidParams(std::string("a GJR-GARCH process spec needs ") + name);
                return *x;
            };
            ProcessSpec s;
            s.referenceDate = date(key("referenceDate"));
            s.dayCounter = dayCounter(key("dayCounter"));
            s.riskFree = real(key("riskFree"));
            s.dividend = real(key("dividend"));
            s.s0 = real(key("s0"));
            s.v0 = real(key("v0"));
            s.omega = real(key("omega"));
            s.alpha = real(key("alpha"));
            s.beta = real(key("beta"));
            s.gamma = real(key("gamma"));
            s.lambda = real(key("lambda"));
            s.daysPerYear = real(key("daysPerYear"));
            return s;
        }

        ext::shared_ptr<GJRGARCHProcess> makeProcess(const ProcessSpec& s) {
            const auto flat = [&](Real rate) {
                return Handle<YieldTermStructure>(
                    ext::make_shared<FlatForward>(makeDate(s.referenceDate), rate, makeDayCounter(s.dayCounter)));
            };
            return ext::make_shared<GJRGARCHProcess>(flat(s.riskFree), flat(s.dividend),
                                                     Handle<Quote>(ext::make_shared<SimpleQuote>(s.s0)), s.v0,
                                                     s.omega, s.alpha, s.beta, s.gamma, s.lambda, s.daysPerYear);
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
            if (out.name == "withAntitheticVariate") {
                out.flag = boolean(s[1]);
            } else if (out.name == "withAbsoluteTolerance") {
                out.value = real(s[1]);
            } else if (out.name == "withSteps" || out.name == "withStepsPerYear" || out.name == "withSamples" ||
                       out.name == "withMaxSamples" || out.name == "withSeed") {
                long long n = int64(s[1]);
                if (n < 0)
                    throw InvalidParams(out.name + " takes a non-negative value");
                out.value = static_cast<Real>(n);
            } else {
                throw InvalidParams("unknown setter " + out.name);
            }
            return out;
        }

        template <class RNG>
        json::value priced(const ProcessSpec& p, Option::Type type, Real strike, const DateSpec& exercise,
                           const std::vector<Setter>& setters) {
            MakeMCEuropeanGJRGARCHEngine<RNG> make(makeProcess(p));
            for (const Setter& s : setters) {
                if (s.name == "withSteps")
                    make.withSteps(static_cast<Size>(s.value));
                else if (s.name == "withStepsPerYear")
                    make.withStepsPerYear(static_cast<Size>(s.value));
                else if (s.name == "withSamples")
                    make.withSamples(static_cast<Size>(s.value));
                else if (s.name == "withAbsoluteTolerance")
                    make.withAbsoluteTolerance(s.value);
                else if (s.name == "withMaxSamples")
                    make.withMaxSamples(static_cast<Size>(s.value));
                else if (s.name == "withSeed")
                    make.withSeed(static_cast<BigNatural>(s.value));
                else
                    make.withAntitheticVariate(s.flag);
            }
            ext::shared_ptr<PricingEngine> engine = make;
            VanillaOption option(ext::make_shared<PlainVanillaPayoff>(type, strike),
                                 ext::make_shared<EuropeanExercise>(makeDate(exercise)));
            option.setPricingEngine(engine);
            json::object out;
            out["NPV"] = option.NPV();
            if constexpr (RNG::allowsErrorEstimate != 0)
                out["errorEstimate"] = option.errorEstimate();
            else
                out["errorEstimate"] = nullptr;
            return json::value(out);
        }

        const Register unit({
            {"EuropeanGJRGARCHPathPricer::value",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 auto type = enumeration<Option::Type>(a[0]);
                 Real strike = real(a[1]), discount = real(a[2]);
                 std::vector<Real> times = reals(a[3]);
                 if (!a[4].is_array())
                     throw InvalidParams("expected a list of paths, got " + json::serialize(a[4]));
                 std::vector<std::vector<Real>> paths;
                 for (const json::value& path : a[4].get_array())
                     paths.push_back(reals(path));
                 if (paths.empty())
                     throw InvalidParams("a multi-path needs an asset");
                 return [=] {
                     TimeGrid grid(times.begin(), times.end());
                     MultiPath multiPath(paths.size(), grid);
                     for (Size i = 0; i < paths.size(); ++i) {
                         if (paths[i].size() != grid.size())
                             throw InvalidParams("a path has the grid's size");
                         for (Size j = 0; j < grid.size(); ++j)
                             multiPath[i][j] = paths[i][j];
                     }
                     EuropeanGJRGARCHPathPricer pricer(type, strike, discount);
                     return json::value(pricer(multiPath));
                 };
             }},
            {"MakeMCEuropeanGJRGARCHEngine::results",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::string traits = text(a[0]);
                 if (traits != "PseudoRandom" && traits != "LowDiscrepancy")
                     throw InvalidParams("unknown traits " + traits);
                 ProcessSpec p = process(a[1]);
                 auto type = enumeration<Option::Type>(a[2]);
                 Real strike = real(a[3]);
                 DateSpec exercise = date(a[4]);
                 if (!a[5].is_array())
                     throw InvalidParams("expected a list of setters, got " + json::serialize(a[5]));
                 std::vector<Setter> setters;
                 for (const json::value& s : a[5].get_array())
                     setters.push_back(setter(s));
                 return [=] {
                     if (traits == "PseudoRandom")
                         return priced<PseudoRandom>(p, type, strike, exercise, setters);
                     return priced<LowDiscrepancy>(p, type, strike, exercise, setters);
                 };
             }},
        });

    }

}
