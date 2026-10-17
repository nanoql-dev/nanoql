// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/rngtraits.hpp, which the wheel doesn't expose: make_sequence_generator and
// allowsErrorEstimate of PseudoRandom, PoissonPseudoRandom and LowDiscrepancy, and of
// GenericLowDiscrepancy<HaltonRsg, InverseCumulativeNormal>, which QuantLib instantiates for any uniform sequence
// generator. A call's arguments are the dimension, the seed (an unsigned 32-bit integer but 0, which QuantLib replaces
// with one drawn from the clock), the number of draws, and the inverse cumulative the traits' icInstance holds for the
// call, or null for none: {"average", "sigma"} for InverseCumulativeNormal, {"lambda"} for InverseCumulativePoisson.
// The call sets icInstance, builds the sequence generator and resets icInstance, on a throw too, so no call sees
// another's. Its result is {"nextSequence": the draws, "weights", "lastSequence", "lastWeight", "dimension"}.

#include "convert.hpp"

#include <ql/math/randomnumbers/haltonrsg.hpp>
#include <ql/math/randomnumbers/rngtraits.hpp>

#include <cstdint>
#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        using HaltonLowDiscrepancy = GenericLowDiscrepancy<HaltonRsg, InverseCumulativeNormal>;

        unsigned long seedValue(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 1 || v.get_int64() > 4294967295LL)
                throw InvalidParams("expected a seed in [1, 4294967295], got " + json::serialize(v));
            return static_cast<unsigned long>(v.get_int64());
        }

        Size size(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > 100000)
                throw InvalidParams("expected a size, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("an inverse cumulative spec needs ") + name);
            return *v;
        }

        struct NormalSpec {
            Real average, sigma;
        };

        std::optional<NormalSpec> normal(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            if (!v.is_object() || v.get_object().size() != 2)
                throw InvalidParams("expected an InverseCumulativeNormal spec {\"average\", \"sigma\"}, got " +
                                    json::serialize(v));
            const json::object& o = v.get_object();
            return NormalSpec{real(key(o, "average")), real(key(o, "sigma"))};
        }

        struct PoissonSpec {
            Real lambda;
        };

        std::optional<PoissonSpec> poisson(const json::value& v) {
            if (v.is_null())
                return std::nullopt;
            if (!v.is_object() || v.get_object().size() != 1)
                throw InvalidParams("expected an InverseCumulativePoisson spec {\"lambda\"}, got " + json::serialize(v));
            return PoissonSpec{real(key(v.get_object(), "lambda"))};
        }

        ext::shared_ptr<InverseCumulativeNormal> make(const NormalSpec& s) {
            return ext::make_shared<InverseCumulativeNormal>(s.average, s.sigma);
        }

        ext::shared_ptr<InverseCumulativePoisson> make(const PoissonSpec& s) {
            return ext::make_shared<InverseCumulativePoisson>(s.lambda);
        }

        json::value list(const std::vector<Real>& x) {
            json::array out;
            for (Real e : x)
                out.emplace_back(e);
            return json::value(std::move(out));
        }

        // The traits' global for the call only: set, and reset when the call ends, however it ends.
        template <class Traits>
        struct IcInstance {
            template <class Spec>
            explicit IcInstance(const std::optional<Spec>& spec) {
                Traits::icInstance.reset();
                if (spec)
                    Traits::icInstance = make(*spec);
            }
            IcInstance(const IcInstance&) = delete;
            IcInstance& operator=(const IcInstance&) = delete;
            ~IcInstance() { Traits::icInstance.reset(); }
        };

        template <class Traits, class Spec>
        json::value draws(Size dimension, unsigned long seed, Size n, const std::optional<Spec>& ic) {
            typename Traits::rsg_type rsg = [&] {
                IcInstance<Traits> instance(ic);
                return Traits::make_sequence_generator(dimension, seed);
            }();
            json::array sequences, weights;
            for (Size i = 0; i < n; ++i) {
                const typename Traits::rsg_type::sample_type& x = rsg.nextSequence();
                sequences.emplace_back(list(x.value));
                weights.emplace_back(x.weight);
            }
            const typename Traits::rsg_type::sample_type& last = rsg.lastSequence();
            return json::value(json::object{{"nextSequence", std::move(sequences)},
                                            {"weights", std::move(weights)},
                                            {"lastSequence", list(last.value)},
                                            {"lastWeight", last.weight},
                                            {"dimension", static_cast<std::int64_t>(rsg.dimension())}});
        }

        // make_sequence_generator(dimension, seed, n, inverse cumulative or null).
        template <class Traits, class Spec>
        Call factory(const Args& a, std::optional<Spec> (*spec)(const json::value&)) {
            arity(a, 4);
            Size dimension = size(a[0]);
            unsigned long seed = seedValue(a[1]);
            Size n = size(a[2]);
            std::optional<Spec> ic = spec(a[3]);
            return [=] { return draws<Traits>(dimension, seed, n, ic); };
        }

        template <class Traits>
        Call allowsErrorEstimate(const Args& a) {
            arity(a, 0);
            return [] { return json::value(static_cast<bool>(Traits::allowsErrorEstimate)); };
        }

        const Register unit({
            {"PseudoRandom::make_sequence_generator",
             [](const Args& a) { return factory<PseudoRandom>(a, &normal); }},
            {"PoissonPseudoRandom::make_sequence_generator",
             [](const Args& a) { return factory<PoissonPseudoRandom>(a, &poisson); }},
            {"LowDiscrepancy::make_sequence_generator",
             [](const Args& a) { return factory<LowDiscrepancy>(a, &normal); }},
            {"GenericLowDiscrepancy<HaltonRsg,InverseCumulativeNormal>::make_sequence_generator",
             [](const Args& a) { return factory<HaltonLowDiscrepancy>(a, &normal); }},
            {"PseudoRandom::allowsErrorEstimate", &allowsErrorEstimate<PseudoRandom>},
            {"PoissonPseudoRandom::allowsErrorEstimate", &allowsErrorEstimate<PoissonPseudoRandom>},
            {"LowDiscrepancy::allowsErrorEstimate", &allowsErrorEstimate<LowDiscrepancy>},
            {"GenericLowDiscrepancy<HaltonRsg,InverseCumulativeNormal>::allowsErrorEstimate",
             &allowsErrorEstimate<HaltonLowDiscrepancy>},
        });

    }

}
