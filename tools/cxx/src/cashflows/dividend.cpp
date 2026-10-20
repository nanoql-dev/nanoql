// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/dividend.hpp: the members the wheel doesn't expose. "FractionalDividend" takes (rate, nominal or null,
// date) and returns [rate(), nominal() (null for Null<Real>()), date()]; "FractionalDividend::amount" takes the same
// and returns amount(), which refuses without a nominal; "FractionalDividend::amountOn" and "FixedDividend::amountOn"
// take the dividend's arguments and an underlying value and return amount(underlying).

#include "convert.hpp"

#include <ql/cashflows/dividend.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<FractionalDividend> fractional(Real rate, Real nominal, const DateSpec& d) {
            if (nominal == Null<Real>())
                return ext::make_shared<FractionalDividend>(rate, makeDate(d));
            return ext::make_shared<FractionalDividend>(rate, nominal, makeDate(d));
        }

        const Register unit({
            // (rate, nominal or null, date)
            {"FractionalDividend",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Real rate = real(a[0]), nominal = real(a[1]);
                 DateSpec d = date(a[2]);
                 return [=] {
                     auto x = fractional(rate, nominal, d);
                     json::array out;
                     out.emplace_back(x->rate());
                     out.emplace_back(x->nominal() == Null<Real>() ? json::value(nullptr) : json::value(x->nominal()));
                     out.push_back(result(x->date()));
                     return json::value(out);
                 };
             }},
            {"FractionalDividend::amount",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Real rate = real(a[0]), nominal = real(a[1]);
                 DateSpec d = date(a[2]);
                 return [=] { return json::value(fractional(rate, nominal, d)->amount()); };
             }},
            // (rate, nominal or null, date, underlying)
            {"FractionalDividend::amountOn",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real rate = real(a[0]), nominal = real(a[1]);
                 DateSpec d = date(a[2]);
                 Real underlying = real(a[3]);
                 return [=] { return json::value(fractional(rate, nominal, d)->amount(underlying)); };
             }},
            // (amount, date, underlying)
            {"FixedDividend::amountOn",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Real amount = real(a[0]);
                 DateSpec d = date(a[1]);
                 Real underlying = real(a[2]);
                 return [=] { return json::value(FixedDividend(amount, makeDate(d)).amount(underlying)); };
             }},
        });

    }

}
