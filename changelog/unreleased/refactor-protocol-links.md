### Changed

- Protocol: the hardened accept loop of the Very Back moves to `LinkServer` (`BoardGamesPlatform.Protocol.Links`), and the connection management of the Back's Very Back client moves to `LinkClient`, so that the Back and the Gateway reuse them instead of a copy per tier. No change of behavior: the tests of the Very Back and of the client run unchanged.