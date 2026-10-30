### Changed

- Settings of the Very Back client: `VeryBackClientOptions.FindProblem()` returns the first problem (or null) and the startup validator of the Back uses it,
  with no exception used for control flow; `Validate()` remains for the constructor of the client. Same rules, same messages.
