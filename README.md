# appstoreconnect-cli

A .NET 8 console tool that automates creating and updating Apple Game Center achievements via the [App Store Connect REST API](https://developer.apple.com/documentation/appstoreconnectapi).

---

## Features

- **Create** new Game Center achievements from a JSON config
- **Update** existing achievements and their localizations with `--update`
- **Skip** existing achievements by default (safe re-run behavior)
- **Upload** achievement images using Apple's 3-step reservation flow (reserve → PUT to S3 → commit)
- Images are managed at the **localization level** per Apple's API design
- Paginated listing of all existing achievements
- Automatic JWT generation (ES256) — no external JWT library required
- Retry with exponential backoff for transient errors (429, 5xx)
- JSON run report written to `./out/`
- Serilog structured logging to console and rolling file

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An [App Store Connect API key](https://developer.apple.com/documentation/appstoreconnectapi/creating_api_keys_for_app_store_connect_api) with **App Manager** or **Admin** role
  - Key ID (e.g. `AB12CD34EF`)
  - Issuer ID (UUID from Users and Access > Keys page)
  - `.p8` private key file

---

## Build

```bash
dotnet build
```

---

## Run

```bash
dotnet run -- \
  --keyid    AB12CD34EF \
  --issuerid 12345678-abcd-1234-abcd-1234567890ab \
  --privatekey /path/to/AuthKey_AB12CD34EF.p8 \
  --appid    1234567890 \
  --config   examples/achievements_config.json \
  --imagefolder /path/to/images
```

### Options

| Option | Required | Description |
|---|---|---|
| `--keyid` | Yes | API key ID |
| `--issuerid` | Yes | Issuer ID (UUID) |
| `--privatekey` | Yes | Path to `.p8` file |
| `--appid` | Yes | Numeric App Store Connect app ID |
| `--config` | Yes | Path to achievements JSON config |
| `--imagefolder` | No | Directory containing image files |
| `--update` | No | Update existing achievements (default: skip) |

### Help

```bash
dotnet run -- --help
```

---

## Config Format

See [`examples/achievements_config.json`](examples/achievements_config.json) for a full example.

```json
{
  "achievements": [
    {
      "referenceName": "First Victory",
      "vendorIdentifier": "ACHIEVEMENT_FIRST_VICTORY",
      "points": 10,
      "showBeforeEarned": true,
      "repeatable": false,
      "imageFile": "first_victory.png",
      "localizations": [
        {
          "localeCode": "en-US",
          "name": "First Victory",
          "beforeEarnedDescription": "Win your first match to earn this achievement.",
          "afterEarnedDescription": "You won your first match!"
        }
      ]
    }
  ]
}
```

### Config Field Reference

| Field | Notes |
|---|---|
| `referenceName` | Internal name, visible in App Store Connect |
| `vendorIdentifier` | Unique string identifier for the achievement |
| `points` or `pointValue` | Achievement point value (both accepted) |
| `showBeforeEarned` | Whether achievement is visible before being earned |
| `repeatable` | Whether achievement can be earned multiple times |
| `imageFile` | Image filename (combined with `--imagefolder` if provided) |
| `imageFileName` | Fallback image filename if `imageFile` is not set |
| `localizations[].localeCode` | Locale string, e.g. `en-US`, `fr-FR`, `es-MX` |
| `localizations[].name` | Display name for this locale |
| `localizations[].beforeEarnedDescription` | Description shown before achievement is earned |
| `localizations[].afterEarnedDescription` | Description shown after achievement is earned |
| `localizations[].imageFile` | Per-locale image override (takes precedence over achievement-level `imageFile`) |

---

## Image Upload

Achievement images follow Apple's 3-step upload flow:

1. **Reserve** — `POST /v1/gameCenterAchievementImages` to get upload operations and an image ID
2. **Upload** — `PUT` each file slice to the provided signed S3 URL (no authentication headers)
3. **Commit** — `PATCH /v1/gameCenterAchievementImages/{id}` with `uploaded: true` and the MD5 checksum

Images are associated at the **localization level** in Apple's API. If you want the same image for all locales, put `imageFile` on the achievement. If locales need different images, set `imageFile` on each locale entry.

---

## Exit Codes

| Code | Meaning |
|---|---|
| `0` | All achievements processed successfully |
| `1` | One or more achievements failed (partial success) |
| `2` | Fatal error (bad config, missing files, API bootstrap failure) |

---

## Output

- **Console**: Structured log output at INFO level
- **File**: `logs/run_YYYYMMDD.log` (rolling daily)
- **Report**: `out/report_YYYYMMDD_HHmmss.json` with per-achievement status

---

## Permissions

Your App Store Connect API key must have at minimum the **App Manager** role to manage Game Center data.

---

## Troubleshooting

**`401 Unauthorized`**
Check that your `--keyid`, `--issuerid`, and `--privatekey` are correct. Ensure the `.p8` file has not been revoked in App Store Connect.

**`403 Forbidden`**
Your API key does not have sufficient permissions. Use an App Manager or Admin key.

**`409 Conflict`**
The achievement's `vendorIdentifier` already exists. Run without `--update` to skip, or with `--update` to update.

**`422 Unprocessable Entity`**
The request body is invalid. Check the full error detail in the log. Common causes: missing required fields, invalid locale code.

**Image upload fails**
Ensure the image meets Apple's requirements (PNG, correct dimensions for Game Center). Check the `out/` report for details.
