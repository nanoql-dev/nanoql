// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/overnightindexfuture.hpp: the future's members the wheel hides, on a future built inside the call:
//
//     OvernightIndexFuture  (index, value date, maturity, convexity adjustment, averaging): [the index's name, value
//                           date, maturity date, convexity adjustment]
//
// The index is an overnight spec of indexes/indexspec.hpp; the adjustment a number, 0 for QuantLib's empty handle;
// the averaging QuantLib's RateAveraging::Type integer.

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/indexes/iborindex.hpp>
#include <ql/instruments/overnightindexfuture.hpp>
#include <ql/quotes/simplequote.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"OvernightIndexFuture",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 indexes::IndexSpec index = indexes::index(a[0]);
                 DateSpec value = date(a[1]), maturity = date(a[2]);
                 Real convexity = real(a[3]);
                 auto averaging = enumeration<RateAveraging::Type>(a[4]);
                 return [=] {
                     auto overnight = ext::dynamic_pointer_cast<OvernightIndex>(indexes::makeIndex(index));
                     if (!overnight)
                         throw InvalidParams("an overnight-index future needs an overnight index");
                     OvernightIndexFuture future(overnight, makeDate(value), makeDate(maturity),
                                                 Handle<Quote>(ext::make_shared<SimpleQuote>(convexity)), averaging);
                     json::array out;
                     out.emplace_back(future.overnightIndex()->name());
                     out.emplace_back(result(future.valueDate()));
                     out.emplace_back(result(future.maturityDate()));
                     out.emplace_back(future.convexityAdjustment());
                     return json::value(out);
                 };
             }},
        });

    }

}
