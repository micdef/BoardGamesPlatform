### Changed

- Secure channel, records: a record written is sealed into buffers kept by the stream (16 KiB per connection, under its write lock),
  instead of a new buffer of its size per record (16 KiB less per record of 16 KiB); a record read keeps its length, its nonce and the
  byte read after the close record in buffers of the stream too. Once a record is given, only its bytes are erased, instead of the whole
  buffer of 16 KiB: a peer that sends records of 1 byte can no longer make each one cost the erasure of 16 KiB. The disposal still erases
  the whole buffer; the bytes on the wire do not change.

### Added

- Tests of complexity of the records: a record written and a record read allocate the same few bytes whatever their length, and a write
  is cut into the fewest records (one per 16 KiB, 18 bytes added per record); `doc/dev/complexity.md` lists the records.
