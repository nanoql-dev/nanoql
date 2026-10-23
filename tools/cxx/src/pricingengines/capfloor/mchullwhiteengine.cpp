// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/capfloor/mchullwhiteengine.hpp, .cpp: MCHullWhiteCapFloorEngine and its builder, which the wheel
// doesn't expose, on Statistics, with the pseudo-random or the low-discrepancy traits ("PseudoRandom",
// "LowDiscrepancy"). Each builds the instrument of instruments/capfloorspec.hpp, the Hull-White model on its curve (a
// curve spec of indexes/indexspec.hpp) and the engine inside the call.
//
//     MCHullWhiteCapFloorEngine::calculate     (cap, curve, a, sigma, traits, brownianBridge, antitheticVariate,
//                                               requiredSamples, requiredTolerance, maxSamples, seed, tag)
//     MakeMCHullWhiteCapFloorEngine::calculate (cap, curve, a, sigma, traits, setters, tag)
//
// each the instrument's NPV for the tag "NPV" or its error estimate for "errorEstimate" (QuantLib's "error estimate not
// provided" for traits that allow none). The engine's samples, tolerance and maximum null are QuantLib's
// Null<Size>() and Null<Real>(); its seed is an unsigned 32-bit integer, never 0 here (QuantLib's seed from the
// clock). The builder's setters are [name, value] pairs, called in order: withSamples, withAbsoluteTolerance,
// withMaxSamples, withSeed, withBrownianBridge and withAntitheticVariate, each refusing what QuantLib's refuses.

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/capfloorspec.hpp"

#include <ql/instruments/capfloor.hpp>
#include <ql/math/randomnumbers/rngtraits.hpp>
#include <ql/math/statistics/statistics.hpp>
#include <ql/models/shortrate/onefactormodels/hullwhite.hpp>
#include <ql/pricingengines/capfloor/mchullwhiteengine.hpp>
#include <ql/utilities/null.hpp>

#include <string>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using capfloors::CapFloorSpec;

        Size size(const json::value& v) {
            if (v.is_null())
                return Null<Size>();
            long long n = int64(v);
            if (n < 0)
                throw InvalidParams("expected a non-negative size");
            return Size(n);
        }

        BigNatural seed(const json::value& v) {
            long long n = int64(v);
            if (n <= 0 || n > 4294967295LL)
                throw InvalidParams("expected a seed in [1, 4294967295]");
            return BigNatural(n);
        }

        std::string traits(const json::value& v) {
            std::string t = text(v);
            if (t != "PseudoRandom" && t != "LowDiscrepancy")
                throw InvalidParams("unknown traits " + t);
            return t;
        }

        json::value priced(const CapFloorSpec& s, const ext::shared_ptr<PricingEngine>& engine,
                           const std::string& tag) {
            auto c = capfloors::makeCapFloor(s);
            c->setPricingEngine(engine);
            if (tag == "NPV")
                return json::value(c->NPV());
            return json::value(c->errorEstimate());
        }

        std::string tagOf(const json::value& v) {
            std::string tag = text(v);
            if (tag != "NPV" && tag != "errorEstimate")
                throw InvalidParams("unknown tag " + tag);
            return tag;
        }

        struct Setter {
            std::string name;
            json::value value;
        };

        std::vector<Setter> setters(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected the setters' array, got " + json::serialize(v));
            std::vector<Setter> out;
            for (const json::value& e : v.get_array()) {
                if (!e.is_array() || e.get_array().size() != 2)
                    throw InvalidParams("expected a setter's [name, value], got " + json::serialize(e));
                Setter s{text(e.get_array()[0]), e.get_array()[1]};
                if (s.name != "withSamples" && s.name != "withAbsoluteTolerance" && s.name != "withMaxSamples" &&
                    s.name != "withSeed" && s.name != "withBrownianBridge" && s.name != "withAntitheticVariate")
                    throw InvalidParams("unknown setter " + s.name);
                if (s.name == "withSamples" || s.name == "withMaxSamples")
                    size(s.value);
                else if (s.name == "withAbsoluteTolerance")
                    real(s.value);
                else if (s.name == "withSeed")
                    seed(s.value);
                else
                    boolean(s.value);
                out.push_back(std::move(s));
            }
            return out;
        }

        template <class RNG>
        ext::shared_ptr<PricingEngine> engine(const ext::shared_ptr<HullWhite>& model, bool bb, bool anti,
                                              Size samples, Real tolerance, Size maxSamples, BigNatural seed) {
            return ext::make_shared<MCHullWhiteCapFloorEngine<RNG, Statistics>>(model, bb, anti, samples, tolerance,
                                                                               maxSamples, seed);
        }

        template <class RNG>
        ext::shared_ptr<PricingEngine> built(const ext::shared_ptr<HullWhite>& model,
                                             const std::vector<Setter>& calls) {
            MakeMCHullWhiteCapFloorEngine<RNG, Statistics> b(model);
            for (const Setter& s : calls) {
                if (s.name == "withSamples")
                    b.withSamples(size(s.value));
                else if (s.name == "withAbsoluteTolerance")
                    b.withAbsoluteTolerance(real(s.value));
                else if (s.name == "withMaxSamples")
                    b.withMaxSamples(size(s.value));
                else if (s.name == "withSeed")
                    b.withSeed(seed(s.value));
                else if (s.name == "withBrownianBridge")
                    b.withBrownianBridge(boolean(s.value));
                else
                    b.withAntitheticVariate(boolean(s.value));
            }
            return b;
        }

        const Register unit({
            {"MCHullWhiteCapFloorEngine::calculate",
             [](const Args& a) -> Call {
                 arity(a, 12);
                 CapFloorSpec s = capfloors::capFloor(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 Real speed = real(a[2]), sigma = real(a[3]);
                 std::string rng = traits(a[4]);
                 bool bb = boolean(a[5]), anti = boolean(a[6]);
                 Size samples = size(a[7]);
                 Real tolerance = real(a[8]);
                 Size maxSamples = size(a[9]);
                 BigNatural sd = seed(a[10]);
                 std::string tag = tagOf(a[11]);
                 return [=] {
                     auto model = ext::make_shared<HullWhite>(indexes::makeCurve(curve), speed, sigma);
                     auto e = rng == "PseudoRandom"
                                  ? engine<PseudoRandom>(model, bb, anti, samples, tolerance, maxSamples, sd)
                                  : engine<LowDiscrepancy>(model, bb, anti, samples, tolerance, maxSamples, sd);
                     return priced(s, e, tag);
                 };
             }},
            {"MakeMCHullWhiteCapFloorEngine::calculate",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 CapFloorSpec s = capfloors::capFloor(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 Real speed = real(a[2]), sigma = real(a[3]);
                 std::string rng = traits(a[4]);
                 std::vector<Setter> calls = setters(a[5]);
                 std::string tag = tagOf(a[6]);
                 return [=] {
                     auto model = ext::make_shared<HullWhite>(indexes::makeCurve(curve), speed, sigma);
                     auto e = rng == "PseudoRandom" ? built<PseudoRandom>(model, calls)
                                                    : built<LowDiscrepancy>(model, calls);
                     return priced(s, e, tag);
                 };
             }},
        });

    }

}
