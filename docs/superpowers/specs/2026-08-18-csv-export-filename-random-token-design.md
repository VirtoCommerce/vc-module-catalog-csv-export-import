# CSV export file name: random 6-digit token (`{1}`)

**Ticket:** VCST-5745
**Date:** 2026-08-18
**Status:** Approved

## Problem

Exported catalog CSVs are uploaded to `temp/<file name>` in blob storage and handed to the
client as an absolute URL, which on a CDN-backed setup looks like:

```
https://{CDN}/temp/products_2026-08-13_09-13-19.csv
```

The name is fully determined by the `CatalogCsvImport.ExportFileNameTemplate` setting, whose
default is `products_{0:yyyy-MM-dd_HH-mm-ss}` — a timestamp. A third party who knows the
pattern can enumerate candidate timestamps and guess the URL of somebody else's export.
Reported by InfoSys on behalf of Heineken.

## Goal

Give the template a second parameter, `{1}`, that renders a random 6-digit number, ship it in
the default template so the URL is no longer guessable out of the box, and document both
parameters clearly in the setting description.

## Non-goals

- Access control on the `temp` blob container. Unguessable names are the fix that was asked
  for, not authorization.
- Hardening the template against path traversal (`../` in the template escapes `temp` via
  `Path.Combine`). Pre-existing, admin-only, unrelated to guessability. Tracked separately.

## Design

### 1. `ExportFileNameHelper` (new, `.Core`)

File-name construction moves out of the controller into a static helper next to the existing
`UrlHelper`, so it can be unit tested.

```csharp
public static string GetFileName(string template, DateTime timestampUtc)
{
    if (string.IsNullOrWhiteSpace(template))
    {
        template = (string)ModuleConstants.Settings.General.ExportFileNameTemplate.DefaultValue;
    }

    var fileName = string.Format(CultureInfo.InvariantCulture, template, timestampUtc, GenerateRandomNumber());

    return Path.ChangeExtension(fileName, ".csv");
}

public static string GenerateRandomNumber() =>
    RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
```

Rationale for each decision:

- **`RandomNumberGenerator`, not `Random`.** The value exists to be unpredictable. `Random` is
  seeded and its sequence is recoverable from observed output, which would defeat the purpose.
- **The value is passed as a pre-formatted string, not an `int`.** `"D6"` padding is applied
  once, inside the helper, so a template written as plain `{1}` always yields exactly six
  characters. As a string it also ignores any format specifier a user types (`{1:D6}`,
  `{1:N0}`) instead of throwing.
- **Blank template falls back to the default.** Today an emptied setting yields an empty file
  name, so the export writes to `temp` itself and fails obscurely.
- **`CultureInfo.InvariantCulture`.** Today's `string.Format` uses the ambient culture, so a
  template such as `{0:d}` changes shape with the server locale. File names should not. This
  does not affect the shipped default, which uses explicit non-localized format specifiers.

### 2. Default template (`ModuleConstants`)

```
products_{0:yyyy-MM-dd_HH-mm-ss}   ->   products_{0:yyyy-MM-dd_HH-mm-ss}_{1}
```

Producing e.g. `products_2026-08-13_09-13-19_481750.csv`.

The platform resolves a setting to the descriptor's `DefaultValue` when no value was ever
saved, so every tenant that never customized the template picks up the random token on
upgrade with no action required. A tenant that *did* save a custom template keeps it and must
add `{1}` by hand.

### 3. Controller

`CatalogModuleExportImportController.BackgroundExport` — the three lines that format the
template and force the extension collapse to one call:

```csharp
var fileName = ExportFileNameHelper.GetFileName(fileNameTemplate, DateTime.UtcNow);
```

`ExportFileNameTemplate` has no other reader in the module.

### 4. Backward compatibility

`string.Format` ignores surplus arguments, so a saved template of `products_{0:yyyy-MM-dd}`
keeps producing exactly the name it produces today. No migration, no data change.

### 5. Setting description (13 localization files)

English text:

> Specify the template (pattern) used to generate the exported file name. Supported
> parameters: {0} — UTC date and time when the file is generated, with an optional .NET format
> string (for example {0:yyyy-MM-dd_HH-mm-ss}); {1} — a random 6-digit number that makes the
> download link hard to guess. Example: products_{0:yyyy-MM-dd_HH-mm-ss}_{1} produces
> products_2026-08-13_09-13-19_481750.csv. The .csv extension is added automatically.

Translated into the other 12 locales (de, es, fi, fr, it, ja, no, pl, pt, ru, sv, zh), keeping
each file's existing tone. Parameter placeholders, the example template and the example file
name stay in Latin script and are not translated.

Single braces are literal in angular-translate — its interpolation syntax is `{{ }}` — so
`{0}` and `{1}` render as written. This replaces the current description's `'0'` phrasing,
which never told the user what to actually type.

## Testing

New `ExportFileNameHelperTests.cs` in the existing test project:

| Case | Expectation |
| --- | --- |
| Default template | matches `^products_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_\d{6}\.csv$` |
| Template without `{1}` | exact old name, e.g. `products_2026-08-13_09-13-19.csv` |
| Template `products_{1}` | `products_` + exactly 6 digits + `.csv` |
| Template with `{1:D6}` | does not throw; still 6 digits |
| `null` / whitespace template | falls back to the default pattern |
| Template already ending `.csv` | single `.csv`, not doubled |
| Many `GetFileName` calls, same timestamp | names differ (token varies) |
| Many `GenerateRandomNumber` calls | always length 6, all digits |

Randomness is asserted as "distinct across N calls", not as a distribution test.

## Rollout note

The reporting customer runs platform 3.832.17, which is EOL / sustaining support. This change
targets `dev`; reaching that customer requires a backport to their support line, to be
confirmed with the owner of that branch.
