### Fixed

- Socket protocol, request server: the cancellation asked by a client runs in its own task, no longer in the read loop, so that a callback of a handler that blocks never stops the server from receiving the pings, the other cancellations and the next requests of the connection. The service waits for these cancellations before it ends, and a failing callback still ends it with its exception.
