// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/mceuropeanhestonengine.hpp: the path pricer and MakeMCEuropeanHestonEngine, which the wheel
// doesn't expose. The evaluation date is the case's Settings.
//
// "EuropeanHestonPathPricer::value" takes (optionType, strike, discount, times, paths): the pricer on the payoff and
// the discount, applied to a multi-path on TimeGrid(times) whose assets' values are the paths' (each a list of the
// grid's size); it returns the value.
//
// "MakeMCEuropeanHestonEngine::results" takes (traits, process, optionType, strike, exerciseDate, setters): the
// builder on PseudoRandom or LowDiscrepancy and QuantLib's Statistics over the process (models/equity/hestonspec.hpp's
// spec), the setters applied in order, each [name, value]: "withSteps", "withStepsPerYear", "withSamples",
// "withAbsoluteTolerance", "withMaxSamples", "withSeed" and "withAntitheticVariate"; a European plain vanilla option
// priced on the engine it makes. It returns {"NPV", "errorEstimate"}, the error estimate null under LowDiscrepancy,
// which gives none; a setter's or the builder's refusal is QuantLib's, its message the error's.

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/methods/montecarlo/multipath.hpp>
#include <ql/pricingengines/vanilla/mceuropeanhestonengine.hpp>
#include <ql/timegrid.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

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
        json::value priced(const equity::ProcessSpec& p, Option::Type type, Real strike, const DateSpec& exercise,
                           const std::vector<Setter>& setters) {
            MakeMCEuropeanHestonEngine<RNG> make(equity::makeProcess(p));
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
            {"EuropeanHestonPathPricer::value",
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
                     EuropeanHestonPathPricer pricer(type, strike, discount);
                     return json::value(pricer(multiPath));
                 };
             }},
            {"MakeMCEuropeanHestonEngine::results",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::string traits = text(a[0]);
                 if (traits != "PseudoRandom" && traits != "LowDiscrepancy")
                     throw InvalidParams("unknown traits " + traits);
                 equity::ProcessSpec p = equity::process(a[1]);
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
