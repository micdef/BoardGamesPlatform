### Added

- Tests of time and space complexity: `AllocationMeter` (`test/BoardGamesPlatform.TestKit`) counts the bytes an operation allocates on
  the thread of the test. The operation runs on a stream in memory, so that it completes synchronously; the count never depends on the
  load of the machine. The smaller of two series is kept, and `AllocationMeterTests` checks that the meter sees a known allocation.
- Complexity tests of the frame codec: a frame written allocates the same few bytes whatever its payload; a frame read allocates its
  payload and a fixed cost under 1 KiB; a frame that arrives byte by byte is read in one pass.
- Complexity tests of the envelopes: a request is written once and read without a second copy of its body; a response is written once
  and read into its body only; both ways of writing a request give the same bytes.
- `doc/dev/complexity.md`: how the complexity is tested, the cost of each hot path with its test, and the collections bounded by the
  limits of the links. A rule of the coding conventions: a hot path gets a complexity test.

### Changed

- Socket protocol, frames: a frame written builds its header and its payload in a buffer of the shared pool, instead of a new buffer of
  its size: no allocation per byte of payload (64 KiB less per frame of 64 KiB). The bytes of the frame are erased before the buffer goes
  back to the pool, since a payload may hold a secret.
- Socket protocol, messages: a request whose body is an object with properties is written in one pass, instead of its body first, then
  the envelope: 1 byte allocated per byte of request instead of 2. The body of a request read is checked as UTF-8 on its raw bytes,
  instead of a copy as text of 2 bytes per byte: 1 byte allocated per byte of request instead of 3. The bytes on the wire do not change.

### Fixed

- Tests: the renewal test of the link client waits for the log of the renewal before it disposes the client. Otherwise the client
  logged the old connection as closed by the disposal, at random.
- Tests: the heartbeat test of a large request written slowly moves the clock only while the client writes. Once the request is
  written, the bytes left in the buffers of Linux (several MiB) are read without moving it: a client that waits for a silent server
  rightly closes after its timeout. The test failed at random on Linux.
