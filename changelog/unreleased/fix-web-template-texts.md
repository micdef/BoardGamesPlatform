### Fixed

- Website: the texts of the Blazor template (error page with its "Request ID" label, not found page, error banner of the layout, reconnection dialog, title of the home page) now come from the resources (`WebTexts.resx` and `WebTexts.fr.resx`) instead of being written in the pages; the `lang` attribute of the document follows the language of the request. The script of the reconnection dialog writes no text: the texts are in the markup, rendered from the resources.
- Website: the error page no longer shows the template advice about the Development environment.
- Website: the not found page has a title and a level 1 heading (was a level 3 heading), so the router focuses it.

### Added

- Website tests: every key exists in both languages, no text is empty, every key asked in the source exists, no .razor file holds a visible literal text (a heuristic, not a Razor parser), and the not found page, the error banner, the error page and the reconnection dialog rendered by bUnit in English and in French.
