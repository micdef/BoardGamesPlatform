### Fixed

- Socket protocol, heartbeat: any byte received now counts as a sign of life, not only a whole frame, on the client and on the server, so that a large frame on a slow link (1 MiB at less than about 35 KB/s) no longer closes the connection. The frame codec writes a frame larger than 64 KiB in pieces and tells the writer after each one: the request client counts a piece written as a sign of life, and skips its ping while another frame is being written instead of waiting for the write lock until its timeout.
