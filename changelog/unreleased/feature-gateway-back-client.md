### Added

- Gateway, `BackClient`: the real client of the Back, built on the `LinkClient` of the protocol, with plain TCP (development only until TLS in 0.5.0). It replaces the temporary `UnavailableBackClient`, which is removed: `Ping` of the hub now reaches the Back.
- Gateway settings of the link to the Back, section `Back` (`Back:Host` default `127.0.0.1`, `Back:Port` default 7821, `Back:ConnectTimeout` default 5 seconds), validated at startup: an invalid value stops the Gateway and the message names the setting, never its value.
- Gateway tests: the `Back` settings, `BackClient` against a fake Back on a loopback socket (time with offset, unreachable Back, disposal), and the hub with the real client (Ping through the hub, generic error when the Back does not answer, invalid `Back:Port` stops the startup).
