### Added

- Complexity tests of the records: a record written and a record read allocate the same few bytes whatever their length, and a write
  is cut into the fewest records (one write of the stream below per 16 KiB, 18 bytes added per record).
- Tests of the erasure of the records: once a record is given, or refused, no byte of its plaintext stays in the stream; a full record
  then a record of 1 byte, sealed and opened in the same buffers, are both read back whole.

### Changed

- Secure channel, records: a record written is sealed into buffers kept by the stream (16 KiB per connection, under its write lock),
  instead of a new buffer of its size per record: 16 KiB less per record of 16 KiB. A record read uses buffers of the stream for its
  length, its nonce and the one-byte probe after the close record.
- Secure channel, records: once a record is given, only its own bytes are erased, instead of the whole buffer of 16 KiB. A peer that
  sends records of 1 byte can no longer make each one cost the erasure of 16 KiB. The disposal still erases the whole buffer, and the
  bytes on the wire do not change.
