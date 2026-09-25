// Emits finite input bits and decimal decompositions from the pinned upstream
// Żmij reference. Built against upstream source by verify_upstream.py.
#include <bit>
#include <cstdint>
#include <cstdio>
#include <cstdlib>

#include "zmij.h"

static uint64_t next_bits(uint64_t& state) {
  state += 0x9e3779b97f4a7c15ull;
  uint64_t z = state;
  z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9ull;
  z = (z ^ (z >> 27)) * 0x94d049bb133111ebull;
  return z ^ (z >> 31);
}

static void emit_double(uint64_t bits) {
  if ((bits & 0x7ff0000000000000ull) == 0x7ff0000000000000ull) return;
  auto dec = zmij::to_decimal(std::bit_cast<double>(bits));
  std::printf("D %016llx %llu %d %d\n", static_cast<unsigned long long>(bits),
              static_cast<unsigned long long>(dec.sig), dec.exp, dec.negative);
}

static void emit_float(uint32_t bits) {
  if ((bits & 0x7f800000u) == 0x7f800000u) return;
  auto dec = zmij::to_decimal(std::bit_cast<float>(bits));
  std::printf("F %08x %llu %d %d\n", bits,
              static_cast<unsigned long long>(dec.sig), dec.exp, dec.negative);
}

int main(int argc, char** argv) {
  if (argc != 2) return 2;
  char* end = nullptr;
  unsigned long count = std::strtoul(argv[1], &end, 10);
  if (*end != '\0') return 2;

  uint64_t state = 0x5a17d1ff3e12c0deull;
  for (unsigned long i = 0; i < count; ++i) {
    emit_double(next_bits(state));
    emit_float(static_cast<uint32_t>(next_bits(state)));
  }

  constexpr uint64_t double_fractions[] = {0, 1, 0x000ffffffffffffeull, 0x000fffffffffffffull};
  for (uint64_t exp = 0; exp < 2047; ++exp) {
    for (uint64_t fraction : double_fractions) {
      uint64_t bits = (exp << 52) | fraction;
      emit_double(bits);
      emit_double(bits | (1ull << 63));
    }
  }

  constexpr uint32_t float_fractions[] = {0, 1, 0x007ffffeu, 0x007fffffu};
  for (uint32_t exp = 0; exp < 255; ++exp) {
    for (uint32_t fraction : float_fractions) {
      uint32_t bits = (exp << 23) | fraction;
      emit_float(bits);
      emit_float(bits | (1u << 31));
    }
  }
}
