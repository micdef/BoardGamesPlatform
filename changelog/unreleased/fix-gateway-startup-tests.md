### Fixed

- Gateway tests: the checks that an invalid setting stops the startup build the host directly instead of going through `WebApplicationFactory`, which could report a disposed service provider instead of the validation failure on the CI runner.
