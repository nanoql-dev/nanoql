// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/inflation/inflationcapfloorengines.hpp: the year-on-year cap and floor engines' additional
// results, which the wheel doesn't bind. "YoYInflationCapFloorEngine::calculate" takes the index (cashflows/
// inflationspec.hpp's), the instrument {"type", "leg", "capRates", "floorRates"} (QuantLib's integer, a year-on-year
// leg spec of inflationspec.hpp), the engine {"kind", "volatility"} ("Black", "UnitDisplacedBlack" or "Bachelier", a
// ConstantYoYOptionletVolatility spec of termstructures/volatility/inflation/inflationvolspec.hpp), the nominal curve
// (indexes/indexspec.hpp's) and a tag, with the wheel generators' input names
// (tools/vectors/gen/instruments/inflationcapfloor.py); it builds them inside the call and gives the instrument's NPV
// for the tag "NPV", or its additional result of the tag, a list of reals, or QuantLib's "<tag> not provided".

#include "cashflows/inflationspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/volatility/inflation/inflationvolspec.hpp"

#include <ql/instruments/inflationcapfloor.hpp>
#include <ql/pricingengines/inflation/inflationcapfloorengines.hpp>

#include <initializer_list>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const json::object& object(const json::value& v, const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
            return v.get_object();
        }

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        struct InstrumentSpec {
            YoYInflationCapFloor::Type type = YoYInflationCapFloor::Cap;
            cashflows::YoYLegSpec leg;
            std::vector<Rate> capRates, floorRates;
        };

        InstrumentSpec instrument(const json::value& v) {
            const std::string what = "a year-on-year cap or floor";
            const json::object& o = object(v, what);
            onlyKeys(o, {"type", "leg", "capRates", "floorRates"}, what);
            InstrumentSpec s;
            s.type = enumeration<YoYInflationCapFloor::Type>(key(o, "type", what));
            s.leg = cashflows::yoyLeg(key(o, "leg", what));
            s.capRates = reals(key(o, "capRates", what));
            s.floorRates = reals(key(o, "floorRates", what));
            return s;
        }

        struct EngineSpec {
            std::string kind;
            termstructures::InflationVolSpec volatility;
        };

        EngineSpec engine(const json::value& v) {
            const std::string what = "a year-on-year cap and floor engine";
            const json::object& o = object(v, what);
            onlyKeys(o, {"kind", "volatility"}, what);
            EngineSpec s;
            s.kind = text(key(o, "kind", what));
            if (s.kind != "Black" && s.kind != "UnitDisplacedBlack" && s.kind != "Bachelier")
                throw InvalidParams("unknown engine " + s.kind);
            s.volatility = termstructures::inflationVolatility(key(o, "volatility", what), "ConstantYoYOptionletVolatility");
            return s;
        }

        ext::shared_ptr<YoYInflationCapFloor> makeInstrument(const InstrumentSpec& s,
                                                             const ext::shared_ptr<YoYInflationIndex>& index) {
            Leg leg = cashflows::makeYoYLeg(s.leg, index);
            if (s.type == YoYInflationCapFloor::Cap)
                return ext::make_shared<YoYInflationCap>(leg, s.capRates);
            if (s.type == YoYInflationCapFloor::Floor)
                return ext::make_shared<YoYInflationFloor>(leg, s.floorRates);
            return ext::make_shared<YoYInflationCollar>(leg, s.capRates, s.floorRates);
        }

        ext::shared_ptr<PricingEngine> makeEngine(const EngineSpec& s, const ext::shared_ptr<YoYInflationIndex>& index,
                                                  const Handle<YieldTermStructure>& nominal) {
            Handle<YoYOptionletVolatilitySurface> vol(termstructures::makeYoYVolatility(s.volatility));
            if (s.kind == "Black")
                return ext::make_shared<YoYInflationBlackCapFloorEngine>(index, vol, nominal);
            if (s.kind == "UnitDisplacedBlack")
                return ext::make_shared<YoYInflationUnitDisplacedBlackCapFloorEngine>(index, vol, nominal);
            return ext::make_shared<YoYInflationBachelierCapFloorEngine>(index, vol, nominal);
        }

        const Register unit({
            // (index, instrument, engine, nominal curve, tag)
            {"YoYInflationCapFloorEngine::calculate",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 cashflows::InflationIndexSpec i = cashflows::inflationIndex(a[0]);
                 InstrumentSpec s = instrument(a[1]);
                 EngineSpec e = engine(a[2]);
                 indexes::CurveSpec curve = indexes::curve(a[3]);
                 std::string tag = text(a[4]);
                 return [=] {
                     auto index = cashflows::makeYoYIndex(i);
                     auto c = makeInstrument(s, index);
                     c->setPricingEngine(makeEngine(e, index, indexes::makeCurve(curve)));
                     if (tag == "NPV")
                         return json::value(c->NPV());
                     json::array out;
                     for (Real x : c->result<std::vector<Real>>(tag))
                         out.emplace_back(x);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
