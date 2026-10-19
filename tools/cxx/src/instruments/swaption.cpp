// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/swaption.hpp: the additional results the wheel can't read, on a swaption spec priced by an engine spec
// on a discount curve spec (instruments/swaptionspec.hpp), all built inside the call; Settlement's
// checkTypeAndMethodConsistency; and the stream operators on Settlement::Type and Settlement::Method.
//
//     Swaption::result<Real>                       (swaption, discount curve, engine, tag): the result, or QuantLib's
//                                                  "<tag> not provided"
//     Settlement::checkTypeAndMethodConsistency    (type, method), as QuantLib's integers: null, or its refusal
//     operator<<(Settlement::Type)                 (type): its text
//     operator<<(Settlement::Method)               (method): its text

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/swaptionspec.hpp"

#include <ql/instruments/swaption.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using swaptions::EngineSpec;
        using swaptions::SwaptionSpec;

        const Register unit({
            {"Swaption::result<Real>",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SwaptionSpec s = swaptions::swaption(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 EngineSpec e = swaptions::engine(a[2]);
                 std::string tag = text(a[3]);
                 return [=] {
                     auto swaption = swaptions::makeSwaption(s);
                     swaption->setPricingEngine(swaptions::makeEngine(e, curve));
                     return json::value(swaption->result<Real>(tag));
                 };
             }},
            {"Settlement::checkTypeAndMethodConsistency",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 auto type = enumeration<Settlement::Type>(a[0]);
                 auto method = enumeration<Settlement::Method>(a[1]);
                 return [=] {
                     Settlement::checkTypeAndMethodConsistency(type, method);
                     return json::value(nullptr);
                 };
             }},
            {"operator<<(Settlement::Type)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto type = enumeration<Settlement::Type>(a[0]);
                 return [=] { return json::value(streamed(type)); };
             }},
            {"operator<<(Settlement::Method)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto method = enumeration<Settlement::Method>(a[0]);
                 return [=] { return json::value(streamed(method)); };
             }},
        });

    }

}
