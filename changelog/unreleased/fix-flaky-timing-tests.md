### Fixed

- Protocol tests (`LinkRenewalTests`): the gated ping is in the gate before the test sends its other pings; one of them could reach the peer first, take the gate and wait until the timeout of the test; the list of the connections opened is read under a lock, and the gate is released even when a check fails.
- Gateway tests (`PlatformHubBackLinkTests`): the disconnection of a front reaches the hub over a WebSocket of the test server, whose close ends the connection on the server at once, instead of long polling, whose stop the SignalR client waits for without a time limit (the suspected cause: the timeout was not reproduced); each step is bounded, so that a failure names it.
