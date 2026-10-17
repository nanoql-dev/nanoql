// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/sobolrsg.hpp: SobolRsg with the arguments the wheel can't pass, useGrayCode among them, and the
// members it doesn't expose as QuantLib declares them (skipTo's result). A generator argument is the spec
// {"dimension", "seed", "directionIntegers", "useGrayCode"}, the seed an unsigned 32-bit integer but 0, which QuantLib
// replaces with one drawn from the clock, and the direction integers the enum's integer; the call builds a fresh
// generator from it, so the constructor's refusals are QuantLib's. An integer sequence result is a list of unsigned
// 32-bit integers, a sequence a list of reals.
//
// With the Gray code, QuantLib's skipTo(4294967295) and its draw at counter 4294967295 are undefined behaviour, so no
// method here reaches them with useGrayCode true: skipTo's index must be below 4294967295 there.

#include "convert.hpp"

#include <ql/math/randomnumbers/sobolrsg.hpp>

#include <cstdint>
#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        std::uint32_t uint32(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > 4294967295LL)
                throw InvalidParams("expected an unsigned 32-bit integer, got " + json::serialize(v));
            return static_cast<std::uint32_t>(v.get_int64());
        }

        Size size(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > 100000)
                throw InvalidParams("expected a size, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a SobolRsg spec needs ") + name);
            return *v;
        }

        struct GeneratorSpec {
            Size dimension;
            unsigned long seed;
            SobolRsg::DirectionIntegers directionIntegers;
            bool useGrayCode;
        };

        GeneratorSpec generator(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a SobolRsg spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "dimension" && kv.key() != "seed" && kv.key() != "directionIntegers" &&
                    kv.key() != "useGrayCode")
                    throw InvalidParams("SobolRsg takes no key " + std::string(kv.key()));
            }
            std::uint32_t seed = uint32(key(o, "seed"));
            if (seed == 0U)
                throw InvalidParams("a SobolRsg spec's seed is in [1, 4294967295]");
            return {size(key(o, "dimension")), seed, enumeration<SobolRsg::DirectionIntegers>(key(o, "directionIntegers")),
                    boolean(key(o, "useGrayCode"))};
        }

        SobolRsg make(const GeneratorSpec& s) {
            return SobolRsg(s.dimension, s.seed, s.directionIntegers, s.useGrayCode);
        }

        // An optional skip, which with the Gray code must be below 4294967295.
        std::optional<std::uint32_t> skip(const json::value& v, const GeneratorSpec& s) {
            if (v.is_null())
                return std::nullopt;
            std::uint32_t n = uint32(v);
            if (s.useGrayCode && n == 4294967295U)
                throw InvalidParams("skipTo(4294967295) is undefined with the Gray code");
            return n;
        }

        json::value integers(const std::vector<std::uint32_t>& x) {
            json::array out;
            for (std::uint32_t e : x)
                out.emplace_back(static_cast<std::int64_t>(e));
            return json::value(std::move(out));
        }

        json::value reals(const std::vector<Real>& x) {
            json::array out;
            for (Real e : x)
                out.emplace_back(e);
            return json::value(std::move(out));
        }

        // A fresh generator, skipped to the index if one is given.
        SobolRsg skipped(const GeneratorSpec& s, const std::optional<std::uint32_t>& n) {
            SobolRsg rsg = make(s);
            if (n)
                rsg.skipTo(*n);
            return rsg;
        }

        const Register unit({
            {"SobolRsg::dimension",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 GeneratorSpec s = generator(a[0]);
                 return [=] { return json::value(static_cast<std::int64_t>(make(s).dimension())); };
             }},
            // skipTo(spec, [n, ...]): each skipTo's integer sequence, in turn, on one fresh generator.
            {"SobolRsg::skipTo",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 GeneratorSpec s = generator(a[0]);
                 if (!a[1].is_array())
                     throw InvalidParams("expected a list of indices, got " + json::serialize(a[1]));
                 std::vector<std::uint32_t> ns;
                 for (const json::value& n : a[1].get_array()) {
                     if (n.is_null())
                         throw InvalidParams("expected an index, got null");
                     ns.push_back(*skip(n, s));
                 }
                 return [=] {
                     const SobolRsg rsg = make(s);
                     json::array out;
                     for (std::uint32_t n : ns)
                         out.emplace_back(integers(rsg.skipTo(n)));
                     return json::value(std::move(out));
                 };
             }},
            // nextInt32Sequence(spec, skip or null, n): n integer sequences, after skipTo(skip) if one is given.
            {"SobolRsg::nextInt32Sequence",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 GeneratorSpec s = generator(a[0]);
                 std::optional<std::uint32_t> n0 = skip(a[1], s);
                 Size n = size(a[2]);
                 return [=] {
                     const SobolRsg rsg = skipped(s, n0);
                     json::array out;
                     for (Size i = 0; i < n; ++i)
                         out.emplace_back(integers(rsg.nextInt32Sequence()));
                     return json::value(std::move(out));
                 };
             }},
            // nextSequence(spec, skip or null, n): n sequences, after skipTo(skip) if one is given.
            {"SobolRsg::nextSequence",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 GeneratorSpec s = generator(a[0]);
                 std::optional<std::uint32_t> n0 = skip(a[1], s);
                 Size n = size(a[2]);
                 return [=] {
                     const SobolRsg rsg = skipped(s, n0);
                     json::array out;
                     for (Size i = 0; i < n; ++i)
                         out.emplace_back(reals(rsg.nextSequence().value));
                     return json::value(std::move(out));
                 };
             }},
            // lastSequence(spec, n): after n nextSequence draws of a fresh generator, its last sequence.
            {"SobolRsg::lastSequence",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 GeneratorSpec s = generator(a[0]);
                 Size n = size(a[1]);
                 return [=] {
                     const SobolRsg rsg = make(s);
                     for (Size i = 0; i < n; ++i)
                         rsg.nextSequence();
                     return reals(rsg.lastSequence().value);
                 };
             }},
        });

    }

}
