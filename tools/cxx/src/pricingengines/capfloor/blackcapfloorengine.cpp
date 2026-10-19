// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/capfloor/blackcapfloorengine.hpp and bacheliercapfloorengine.hpp: the Black and Bachelier engines
// on a Volatility (which the wheel doesn't bind; its quote constructors equal them), the Bachelier one with its day
// counter (which the wheel's quote constructor lacks), and both on an optionlet volatility structure, the spec of
// termstructures/volatility/optionlet/optionletvolspec.hpp, whose type and displacement checks they run. Each builds the
// instrument of instruments/capfloorspec.hpp, its discount curve (a curve spec of indexes/indexspec.hpp), the
// structure and the engine inside the call.
//
//     BlackCapFloorEngine::calculate(Volatility)                       (cap, curve, vol, day counter, displacement, tag)
//     BlackCapFloorEngine::calculate(OptionletVolatilityStructure)     (cap, curve, structure, displacement, tag)
//     BachelierCapFloorEngine::calculate(Volatility)                   (cap, curve, vol, day counter, tag)
//     BachelierCapFloorEngine::calculate(OptionletVolatilityStructure) (cap, curve, structure, tag)
//
// each the instrument's NPV for the tag "NPV", or its additional result of the tag ("vega" a real, the optionlets'
// results lists of reals, Null<Real>() as null), or QuantLib's "<tag> not provided". A null displacement is QuantLib's
// default, Null<Real>(): the structure's.

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/capfloorspec.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"

#include <ql/instruments/capfloor.hpp>
#include <ql/pricingengines/capfloor/bacheliercapfloorengine.hpp>
#include <ql/pricingengines/capfloor/blackcapfloorengine.hpp>
#include <ql/utilities/null.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using capfloors::CapFloorSpec;
        using termstructures::OptionletVolSpec;

        json::value priced(const CapFloorSpec& s, const ext::shared_ptr<PricingEngine>& engine,
                           const std::string& tag) {
            auto c = capfloors::makeCapFloor(s);
            c->setPricingEngine(engine);
            if (tag == "NPV")
                return json::value(c->NPV());
            if (tag == "vega")
                return json::value(c->result<Real>(tag));
            json::array out;
            for (Real x : c->result<std::vector<Real>>(tag))
                out.emplace_back(x == Null<Real>() ? json::value(nullptr) : json::value(x));
            return json::value(std::move(out));
        }

        Handle<OptionletVolatilityStructure> structure(const OptionletVolSpec& v) {
            return Handle<OptionletVolatilityStructure>(termstructures::makeOptionletVolatility(v));
        }

        const Register unit({
            {"BlackCapFloorEngine::calculate(Volatility)",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 CapFloorSpec s = capfloors::capFloor(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 Volatility vol = real(a[2]);
                 DayCounterSpec dc = dayCounter(a[3]);
                 Real displacement = real(a[4]);
                 std::string tag = text(a[5]);
                 return [=] {
                     auto engine = ext::make_shared<BlackCapFloorEngine>(indexes::makeCurve(curve), vol,
                                                                         makeDayCounter(dc), displacement);
                     return priced(s, engine, tag);
                 };
             }},
            {"BlackCapFloorEngine::calculate(OptionletVolatilityStructure)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 CapFloorSpec s = capfloors::capFloor(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 OptionletVolSpec v = termstructures::optionletVolatility(a[2]);
                 Real displacement = real(a[3]);
                 std::string tag = text(a[4]);
                 return [=] {
                     auto engine =
                         ext::make_shared<BlackCapFloorEngine>(indexes::makeCurve(curve), structure(v), displacement);
                     return priced(s, engine, tag);
                 };
             }},
            {"BachelierCapFloorEngine::calculate(Volatility)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 CapFloorSpec s = capfloors::capFloor(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 Volatility vol = real(a[2]);
                 DayCounterSpec dc = dayCounter(a[3]);
                 std::string tag = text(a[4]);
                 return [=] {
                     auto engine =
                         ext::make_shared<BachelierCapFloorEngine>(indexes::makeCurve(curve), vol, makeDayCounter(dc));
                     return priced(s, engine, tag);
                 };
             }},
            {"BachelierCapFloorEngine::calculate(OptionletVolatilityStructure)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 CapFloorSpec s = capfloors::capFloor(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 OptionletVolSpec v = termstructures::optionletVolatility(a[2]);
                 std::string tag = text(a[3]);
                 return [=] {
                     auto engine = ext::make_shared<BachelierCapFloorEngine>(indexes::makeCurve(curve), structure(v));
                     return priced(s, engine, tag);
                 };
             }},
        });

    }

}
