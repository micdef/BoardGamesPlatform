# Time and space complexity

The cost in memory and in work of the paths that every message of the platform goes through, the bound of each one, and the test that
proves it. A path run per byte, per frame, per record or per request is a **hot path**: what it costs is paid on all the traffic.

## How it is tested

- **Space**: the bytes an operation allocates, measured by `AllocationMeter` (`test/BoardGamesPlatform.TestKit`) on the thread of the
  test (`GC.GetAllocatedBytesForCurrentThread`). The operation runs on a stream in memory, so that it completes synchronously: what it
  allocates stays on that thread, and the tests that run in parallel never change the count. An operation that does not complete
  synchronously makes the test fail instead of measuring something wrong. The first runs are left out (compilation, caches).
- **Slope and fixed cost**: an operation is measured with a small and a large input. The difference, divided by the difference of the
  sizes, is the cost of each byte (the slope: 0 for O(1), 1 for one copy); the small one bounds the fixed cost. A bound on the slope stays
  true on any machine, where a bound on a total would depend on the version of the runtime.
- **Time**: counted in operations (reads of a stream, records written), never measured with a clock, so that the load of the machine
  never changes the result (a test measured in time failed under load on the CI of 0.6.0).
- **Cost of the tests themselves**: each test handles at most a few hundred KiB, and runs in a few milliseconds.

## Hot paths

| Path | Time | Space | Test |
|------|------|-------|------|
| Frame written (`FrameCodec.WriteAsync`) | O(n), one copy into a buffer of the shared pool, one call to the stream up to 64 KiB | O(1) per frame: the buffer comes from the shared pool and goes back cleared, since a payload may hold a secret | `FrameCodecComplexityTests.AWrittenFrameAllocatesTheSameWhateverItsPayload` |
| Frame read (`FrameCodec.ReadAsync`) | O(n), each byte read once, whatever the size of the reads | O(n): the payload, given to the caller, and a fixed cost (header, frame, tasks) under 1 KiB | `AReadFrameAllocatesItsPayloadAndAFixedCost`, `AFrameArrivingByteByByteIsReadInOnePassAsync` |
| Request written (`MessageSerializer.WriteRequest`) | O(n), one pass when the contract of the body is an object | O(n): the request, once | `MessageSerializerComplexityTests.ARequestIsWrittenInOnePass`, `ARequestWrittenInOnePassKeepsItsForm` |
| Request read (`MessageSerializer.ReadRequest`) | O(n), the UTF-8 of the body checked on its raw bytes | O(n): the body, once | `ARequestIsReadWithoutASecondCopyOfItsBody` |
| Record written (`SecureRecordStream.WriteAsync`) | O(n), one record per 16 KiB, 18 bytes added per record | O(1) per record: the record and its nonce are sealed into buffers of the stream (16 KiB per connection, once), under its write lock | `SecureRecordStreamComplexityTests.AWrittenRecordAllocatesTheSameWhateverItsLength`, `AWriteIsCutIntoTheFewestRecordsAsync` |
| Record read (`SecureRecordStream.ReadAsync`) | O(n) per record of n bytes: once given, only the n bytes of the record are erased, never the whole buffer, so that a peer sending records of 1 byte cannot make each one cost the erasure of 16 KiB | O(1) per record: the length, the nonce and the plaintext live in buffers of the stream | `AReadRecordAllocatesTheSameWhateverItsLength`; the erasure has no test of its own (its work is seen only with a clock): each record erases its own bytes, the disposal erases the whole buffer |

## Bounded by the limits of the links

These collections grow with the load, but never beyond a limit of the link: their operations are linear in a constant.

| Collection | Bound | Why it stays bounded |
|------------|-------|----------------------|
| Connections in their handshake (`LinkServer`) | 4 | `LinkServerLimits`; the oldest is closed to make room |
| Tasks of the served connections (`LinkServer`) | the served connections (8 by default) | the completed tasks are removed at each new connection |
| Requests in progress of a connection (`ServedConnectionState`, `RequestClient`) | 64 | `RequestServer.DefaultMaxConcurrentRequests`, the slots of the request client |
| Cancellations of a connection (`ServedConnectionState`) | the requests in progress | the completed ones are removed at each new one |
| Largest payload of a frame | 64 KiB per link | `BackOperations.LinkMaxPayloadLength`, `VeryBackOperations.LinkMaxPayloadLength` |
