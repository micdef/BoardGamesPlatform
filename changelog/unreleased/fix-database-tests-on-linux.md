### Fixed

- Database tests: the check of the names of the stored procedures cuts the name of the file by hand, since the paths of the SQL project use "\", which only Windows reads as a separator; the test failed on Linux (found while running the suite of 0.5.0 on Fedora 44).
