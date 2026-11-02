### Added

- Links, request timeout: a request that gets no response within the request timeout fails with a `LinkFailureException` whose code is `link.timeout`, and it is canceled on the peer. The setting is `Back:RequestTimeout` in the Gateway (8 seconds by default) and `VeryBack:RequestTimeout` in the Back (5 seconds by default), greater than zero and at most 5 minutes, checked at startup. The timeouts decrease along the chain (website 10 s > Gateway 8 s > Back 5 s), so that each tier still answers a code before its caller gives up.
- Links, maximum of connections: a link server serves at most 8 connections at the same time. A new one beyond that is closed at once and logged as a warning (event id 9019).
