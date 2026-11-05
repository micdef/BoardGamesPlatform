### Fixed

- Links, link server: when the listener is lost without a stop request, or the accept failures repeat, the service of every connection is canceled, so that the run fails and the tier stops. Until now it waited for ever for connections that nothing ended any more.
