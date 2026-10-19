// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/swaption/blackswaptionengine.hpp: the Black and Bachelier engines on a Volatility, the constructors
// the wheel doesn't bind, on a swaption spec and a discount curve spec (instruments/swaptionspec.hpp), all built inside
// the call. The Bachelier engine's model lands in its volatility's displacement, as in QuantLib.
//
//     BlackSwaptionEngine::calculate(Volatility)       (swaption, discount curve, vol, day counter, displacement,
//                                                      model, tag)
//     BachelierSwaptionEngine::calculate(Volatility)   (swaption, discount curve, vol, day counter, model, tag)
//
// each the swaption's NPV for the tag "NPV", or its additional result of the tag, or QuantLib's "<tag> not provided".

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/swaptionspec.hpp"

#include <ql/instruments/swaption.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        using swaptions::EngineSpec;
        using swaptions::SwaptionSpec;

        json::value priced(const SwaptionSpec& s, const indexes::CurveSpec& curve, const EngineSpec& e,
                           const std::string& tag) {
            auto swaption = swaptions::makeSwaption(s);
            swaption->setPricingEngine(swaptions::makeEngine(e, curve));
            return json::value(tag == "NPV" ? swaption->NPV() : swaption->result<Real>(tag));
        }

        const Register unit({
            {"BlackSwaptionEngine::calculate(Volatility)",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 SwaptionSpec s = swaptions::swaption(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 EngineSpec e;
                 e.black = true;
                 e.quote = false;
                 e.vol = real(a[2]);
                 e.dayCounter = dayCounter(a[3]);
                 e.displacement = real(a[4]);
                 e.model = integer(a[5]);
                 std::string tag = text(a[6]);
                 return [=] { return priced(s, curve, e, tag); };
             }},
            {"BachelierSwaptionEngine::calculate(Volatility)",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 SwaptionSpec s = swaptions::swaption(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 EngineSpec e;
                 e.black = false;
                 e.quote = false;
                 e.vol = real(a[2]);
                 e.dayCounter = dayCounter(a[3]);
                 e.model = integer(a[4]);
                 std::string tag = text(a[5]);
                 return [=] { return priced(s, curve, e, tag); };
             }},
        });

    }

}
