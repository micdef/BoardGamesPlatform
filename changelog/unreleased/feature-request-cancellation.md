### Added

- Socket protocol, request cancellation: when the caller of a request gives up, the request client sends the control message `{"control":"cancel","correlationId":N}` once the request is written. The request server cancels the token of that request only, and answers nothing. A cancellation of a request that already ended, or never existed, is ignored. Through the tiers, a front that disconnects stops the work of its call in the Back and in the Very Back, down to the call to the database (end-to-end test).
- Socket protocol, error code `server.busy`: a request beyond the limit of requests in progress of the connection is answered at once with this code, without being started.
- Socket protocol, request client: it never has more requests in progress than the limit the server announced in the handshake. Extra calls wait for a place.

### Changed

- Socket protocol, request server: it never stops reading while the connection lives, so that a cancellation is received even when every slot is busy (until now it stopped reading when its slots were full). A request whose identifier is still in progress, or a control message other than a cancellation, is a protocol violation. The slot of a request is given back just before its response is written, so that the next request of the client always finds it free.
