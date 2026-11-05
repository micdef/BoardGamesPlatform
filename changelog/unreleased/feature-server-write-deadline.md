### Changed

- Socket protocol, request server: a write that makes no progress (no piece of 64 KiB written) for the idle timeout of the heartbeat (45 seconds) ends the service of the connection with a `TimeoutException`; the deadline moves with each piece, so a large frame read slowly is not cut. A client that stops reading while it keeps the connection alive with cancellations can no longer hold the write lock, its slots and its responses for ever.

### Added

- Protocol tests: `GatedWriteConnection`, whose writes go through only when the test lets them; a response never read ends the service at the deadline, and a large response read slowly keeps it.
