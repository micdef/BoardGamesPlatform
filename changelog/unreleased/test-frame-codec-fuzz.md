### Added

- Socket protocol, framing: property-based and fuzz tests of the frame codec (random round trips in random chunks, random bytes, truncation at every offset, oversized lengths), run with fixed seeds so that every failure can be replayed.
