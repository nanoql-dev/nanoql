// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/swap/treeswapengine.hpp, .cpp: TreeVanillaSwapEngine, which the wheel doesn't expose, on the swap
// of a swaption spec (instruments/swaptionspec.hpp, whose exercise is unused), a VanillaSwap.
//
//     TreeVanillaSwapEngine::calculate(Size)      (swaption, model, timeSteps, termStructure)
//     TreeVanillaSwapEngine::calculate(TimeGrid)  (swaption, model, gridTimes, gridSteps, termStructure)
//
// each the swap's NPV, the engine built on the model and the time steps or on TimeGrid(gridTimes, gridSteps), with the
// term structure (a curve spec of indexes/indexspec.hpp, or null for QuantLib's empty handle). A model is
// {"type": "HullWhite", "curve", "a", "sigma"}, its curve a curve spec of indexes/indexspec.hpp, or {"type": "Vasicek",
// "r0", "a", "b", "sigma", "lambda"}.

#include "indexes/indexspec.hpp"
#include "instruments/swaptionspec.hpp"

#include <ql/instruments/vanillaswap.hpp>
#include <ql/models/shortrate/onefactormodels/hullwhite.hpp>
#include <ql/models/shortrate/onefactormodels/vasicek.hpp>
#include <ql/pricingengines/swap/treeswapengine.hpp>
#include <ql/timegrid.hpp>

#include <optional>
#include <set>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using swaptions::SwaptionSpec;

        struct ModelSpec {
            bool hullWhite = true;
            indexes::CurveSpec curve;
            Real r0 = 0.0, a = 0.0, b = 0.0, sigma = 0.0, lambda = 0.0;
        };

        ModelSpec model(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a model spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            auto key = [&](const char* name) -> const json::value& {
                const json::value* x = o.if_contains(name);
                if (x == nullptr)
                    throw InvalidParams(std::string("a model spec needs ") + name);
                return *x;
            };
            std::string type = text(key("type"));
            std::set<std::string> keys;
            ModelSpec m;
            if (type == "HullWhite") {
                keys = {"type", "curve", "a", "sigma"};
                m.curve = indexes::curve(key("curve"));
            } else if (type == "Vasicek") {
                keys = {"type", "r0", "a", "b", "sigma", "lambda"};
                m.hullWhite = false;
                m.r0 = real(key("r0"));
                m.b = real(key("b"));
                m.lambda = real(key("lambda"));
            } else {
                throw InvalidParams("unknown model " + type);
            }
            for (const auto& kv : o) {
                if (keys.count(std::string(kv.key())) == 0)
                    throw InvalidParams(type + " takes no key " + std::string(kv.key()));
            }
            m.a = real(key("a"));
            m.sigma = real(key("sigma"));
            return m;
        }

        ext::shared_ptr<ShortRateModel> makeModel(const ModelSpec& m) {
            if (m.hullWhite)
                return ext::make_shared<HullWhite>(indexes::makeCurve(m.curve), m.a, m.sigma);
            return ext::make_shared<Vasicek>(m.r0, m.a, m.b, m.sigma, m.lambda);
        }

        std::optional<indexes::CurveSpec> optionalCurve(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            return indexes::curve(v);
        }

        Handle<YieldTermStructure> handle(const std::optional<indexes::CurveSpec>& c) {
            return c ? indexes::makeCurve(*c) : Handle<YieldTermStructure>();
        }

        json::value priced(const SwaptionSpec& s, const ext::shared_ptr<PricingEngine>& engine) {
            auto swap = ext::dynamic_pointer_cast<VanillaSwap>(swaptions::makeSwaption(s)->underlying());
            QL_REQUIRE(swap, "the swaption's swap is not a VanillaSwap");
            swap->setPricingEngine(engine);
            return json::value(swap->NPV());
        }

        const Register unit({
            {"TreeVanillaSwapEngine::calculate(Size)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SwaptionSpec s = swaptions::swaption(a[0]);
                 ModelSpec m = model(a[1]);
                 Integer steps = integer(a[2]);
                 if (steps < 0)
                     throw InvalidParams("expected a non-negative number of steps");
                 std::optional<indexes::CurveSpec> ts = optionalCurve(a[3]);
                 return [=] {
                     return priced(s, ext::make_shared<TreeVanillaSwapEngine>(makeModel(m), Size(steps), handle(ts)));
                 };
             }},
            {"TreeVanillaSwapEngine::calculate(TimeGrid)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 SwaptionSpec s = swaptions::swaption(a[0]);
                 ModelSpec m = model(a[1]);
                 std::vector<Real> gridTimes = reals(a[2]);
                 Integer gridSteps = integer(a[3]);
                 if (gridSteps < 0)
                     throw InvalidParams("expected a non-negative number of steps");
                 std::optional<indexes::CurveSpec> ts = optionalCurve(a[4]);
                 return [=] {
                     TimeGrid grid(gridTimes.begin(), gridTimes.end(), gridSteps);
                     return priced(s, ext::make_shared<TreeVanillaSwapEngine>(makeModel(m), grid, handle(ts)));
                 };
             }},
        });

    }

}
