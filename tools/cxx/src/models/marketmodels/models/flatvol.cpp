// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/models/flatvol.hpp, .cpp and the MarketModel base's reads (marketmodel.hpp, .cpp), which the
// wheel doesn't expose.
//
// "FlatVol" takes (model): a model spec of type FlatVol (marketmodelspec.hpp), its reads (modelResults).
// "FlatVolFactory.create" takes (longTermCorrelation, beta, times, vols, referenceDate, rate, dayCounter, displacement,
// rateTimes, evolutionTimes, factors): the factory on a flat forward curve (FlatForward(referenceDate, rate,
// dayCounter), continuous, annual), the model it creates on EvolutionDescription(rateTimes, evolutionTimes) and its
// reads. "flatVolCovariance" takes (t1, t2, T, S, v1, v2).

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/handle.hpp>
#include <ql/models/marketmodels/evolutiondescription.hpp>
#include <ql/models/marketmodels/models/flatvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>

#include <vector>

// Defined in flatvol.cpp with external linkage, declared in no header.
namespace QuantLib {
    Real flatVolCovariance(Time t1, Time t2, Time T, Time S, Volatility v1, Volatility v2);
}

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        const Register unit({
            {"FlatVol",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ModelSpec m = model(a[0]);
                 return [=] { return modelResults(*makeModel(m)); };
             }},
            {"FlatVolFactory.create",
             [](const Args& a) -> Call {
                 arity(a, 11);
                 Real longTermCorrelation = real(a[0]), beta = real(a[1]);
                 Vec times = reals(a[2]), vols = reals(a[3]);
                 DateSpec referenceDate = date(a[4]);
                 Real rate = real(a[5]);
                 DayCounterSpec dc = dayCounter(a[6]);
                 Real displacement = real(a[7]);
                 Vec rateTimes = reals(a[8]), evolutionTimes = reals(a[9]);
                 Size factors = size(a[10]);
                 return [=] {
                     Handle<YieldTermStructure> curve(
                         ext::make_shared<FlatForward>(makeDate(referenceDate), rate, makeDayCounter(dc)));
                     FlatVolFactory factory(longTermCorrelation, beta, times, vols, curve, displacement);
                     return modelResults(*factory.create(EvolutionDescription(rateTimes, evolutionTimes), factors));
                 };
             }},
            {"flatVolCovariance",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Real t1 = real(a[0]), t2 = real(a[1]), T = real(a[2]), S = real(a[3]), v1 = real(a[4]),
                      v2 = real(a[5]);
                 return [=] { return json::value(flatVolCovariance(t1, t2, T, S, v1, v2)); };
             }},
        });

    }

}
