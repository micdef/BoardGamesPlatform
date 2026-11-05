### Changed

- Socket protocol, links Gateway → Back and Back → Very Back: the largest payload of a frame is 64 KiB instead of 1 MiB (`BackOperations.LinkMaxPayloadLength`, `VeryBackOperations.LinkMaxPayloadLength`), used by both ends and by the dispatcher of the responses, so that the memory a peer can make a server hold (connections × requests in progress × largest payload) drops from about 512 MiB to about 32 MiB. `LinkClient` takes the largest payload of its link.
- Tests: the fake tiers and the clients of the tier tests use the largest payload of their link (`TestRequestClients.ConnectAsync` takes it).

### Added

- Back and Very Back tests: a client that offers the 1 MiB of the protocol is refused in the handshake (`protocol.limitsMismatch`), and a frame one byte larger than the link allows closes the connection without an answer.
