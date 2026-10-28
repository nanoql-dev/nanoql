// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/defaulttype.hpp, .cpp, defaultprobabilitykey.hpp, .cpp and defaultevent.hpp, .cpp, which the
// wheel doesn't expose, on experimental/credit/portfoliospec.hpp's specs. A type's summary is [atomic, restructuring],
// a key's [seniority, currency, [type summary...]], a Null<Real> recovery null.
//
//     DefaultType::inspect             (type): [atomic, restructuring, isRestructuring]
//     DefaultType::containsDefaultType (type, atomic), containsRestructuringType (type, restructuring)
//     DefaultType::equals              (type, type): QuantLib's operator==
//     FailureToPay::inspect            (type): [grace length, grace units, amountRequired]
//     DefaultProbKey::inspect          (key): [key summary, size]
//     DefaultProbKey::equals           (key, key): QuantLib's operator==
//     DefaultEvent::inspect            (event): [date, isRestructuring, isDefault, hasSettled, settlement date,
//                                       eventSeniority, currency, type summary]
//     DefaultEvent::recoveryRate       (event, seniority): the event's recovery, null for Null<Real>()
//     DefaultSettlement::recoveryRate  (event, seniority): its settlement's
//     DefaultEvent::matchesEventType   (event, type), matchesDefaultKey (event, key): at the evaluation date
//     DefaultEvent::hasOccurred        (event, date, includeRefDate)
//     DefaultEvent::equals             (event, event): QuantLib's operator==
//     FailureToPayEvent::amountDefaulted (event)

#include "experimental/credit/portfoliospec.hpp"

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        using credit::portfolio::keySummary;
        using credit::portfolio::makeEvent;
        using credit::portfolio::makeKey;
        using credit::portfolio::makeType;
        using credit::portfolio::typeSummary;

        json::value nullable(Real x) {
            return x == Null<Real>() ? json::value(nullptr) : json::value(x);
        }

        json::value flag(bool b) {
            return json::value(b);
        }

        const Register unit({
            {"DefaultType::inspect",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 json::value t = a[0];
                 return [=] {
                     ext::shared_ptr<DefaultType> x = makeType(t);
                     json::array out;
                     out.emplace_back(static_cast<std::int64_t>(x->defaultType()));
                     out.emplace_back(static_cast<std::int64_t>(x->restructuringType()));
                     out.emplace_back(x->isRestructuring());
                     return json::value(std::move(out));
                 };
             }},
            {"DefaultType::containsDefaultType",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value t = a[0];
                 auto d = enumeration<AtomicDefault::Type>(a[1]);
                 return [=] { return flag(makeType(t)->containsDefaultType(d)); };
             }},
            {"DefaultType::containsRestructuringType",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value t = a[0];
                 auto r = enumeration<Restructuring::Type>(a[1]);
                 return [=] { return flag(makeType(t)->containsRestructuringType(r)); };
             }},
            {"DefaultType::equals",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value t1 = a[0], t2 = a[1];
                 return [=] { return flag(*makeType(t1) == *makeType(t2)); };
             }},
            {"FailureToPay::inspect",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 json::value t = a[0];
                 return [=] {
                     auto f = ext::dynamic_pointer_cast<FailureToPay>(makeType(t));
                     if (!f)
                         throw InvalidParams("not a failure to pay");
                     json::array out;
                     out.emplace_back(static_cast<std::int64_t>(f->gracePeriod().length()));
                     out.emplace_back(static_cast<std::int64_t>(f->gracePeriod().units()));
                     out.emplace_back(f->amountRequired());
                     return json::value(std::move(out));
                 };
             }},
            {"DefaultProbKey::inspect",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 json::value k = a[0];
                 return [=] {
                     DefaultProbKey key = makeKey(k);
                     json::array out;
                     out.push_back(keySummary(key));
                     out.emplace_back(static_cast<std::int64_t>(key.size()));
                     return json::value(std::move(out));
                 };
             }},
            {"DefaultProbKey::equals",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value k1 = a[0], k2 = a[1];
                 return [=] { return flag(makeKey(k1) == makeKey(k2)); };
             }},
            {"DefaultEvent::inspect",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 json::value e = a[0];
                 return [=] {
                     ext::shared_ptr<DefaultEvent> x = makeEvent(e);
                     json::array out;
                     out.push_back(result(x->date()));
                     out.emplace_back(x->isRestructuring());
                     out.emplace_back(x->isDefault());
                     out.emplace_back(x->hasSettled());
                     out.push_back(result(x->settlement().date()));
                     out.emplace_back(static_cast<std::int64_t>(x->eventSeniority()));
                     out.push_back(credit::portfolio::currencyCode(x->currency()));
                     out.push_back(typeSummary(x->defaultType()));
                     return json::value(std::move(out));
                 };
             }},
            {"DefaultEvent::recoveryRate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value e = a[0];
                 auto s = enumeration<Seniority>(a[1]);
                 return [=] { return nullable(makeEvent(e)->recoveryRate(s)); };
             }},
            {"DefaultSettlement::recoveryRate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value e = a[0];
                 auto s = enumeration<Seniority>(a[1]);
                 return [=] { return nullable(makeEvent(e)->settlement().recoveryRate(s)); };
             }},
            {"DefaultEvent::matchesEventType",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value e = a[0], t = a[1];
                 return [=] { return flag(makeEvent(e)->matchesEventType(makeType(t))); };
             }},
            {"DefaultEvent::matchesDefaultKey",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value e = a[0], k = a[1];
                 return [=] { return flag(makeEvent(e)->matchesDefaultKey(makeKey(k))); };
             }},
            {"DefaultEvent::hasOccurred",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 json::value e = a[0];
                 DateSpec d = date(a[1]);
                 bool include = boolean(a[2]);
                 return [=] { return flag(makeEvent(e)->hasOccurred(makeDate(d), include)); };
             }},
            {"DefaultEvent::equals",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value e1 = a[0], e2 = a[1];
                 return [=] { return flag(*makeEvent(e1) == *makeEvent(e2)); };
             }},
            {"FailureToPayEvent::amountDefaulted",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 json::value e = a[0];
                 return [=] {
                     auto f = ext::dynamic_pointer_cast<FailureToPayEvent>(makeEvent(e));
                     if (!f)
                         throw InvalidParams("not a failure to pay");
                     return json::value(f->amountDefaulted());
                 };
             }},
        });

    }

}
