### Added

- Socket protocol, messages: property-based and fuzz tests of the JSON readers (request and response envelopes, control messages: random bytes, mutated messages, truncation at every offset, broken structures, excessive nesting, round trips, no peer text in the refusals), run with fixed seeds so that every failure can be replayed.
