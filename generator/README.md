# Profile card generator

Renders the SVG cards used by the profile README. It reads the GitHub GraphQL API and the NuGet
search API, writes the cards to an output directory, and the workflow force-pushes that directory to
the `gh-pages` branch, where GitHub Pages serves it from `https://usausa.github.io/usausa/`.

No external NuGet packages are used, so the workflow only needs the .NET SDK.

## Layout

| Path | What it holds |
| --- | --- |
| `settings.json` | The user to report on, the NuGet account whose packages are counted, and the repositories that get a card |
| `GitHubClient.cs` | Profile, contribution calendar, repository list, language sizes, commit times and star dates |
| `History.cs` | The daily snapshots behind the trend cards, read from and written to `history.json` |
| `NuGetClient.cs` | Package downloads |
| `EmojiResolver.cs` | Expands `:shortcode:` in category titles for the index page |
| `Cards/Theme.cs` | The light and dark palettes emitted into every card |
| `Cards/*Card.cs` | One class per card |
| `Cards/IndexPage.cs` | The catalogue page at the site root |

Each card is a single SVG that carries both palettes and switches with a `prefers-color-scheme` media
query, so the README needs no `<picture>` markup. Light is the value outside the media query, which
is what a viewer that ignores the query will show.

## Output

| File | Size | In the README |
| --- | --- | --- |
| `habits.svg` | 804x178 | no, generated but not linked |
| `overview.svg` | 400x152 | yes |
| `languages.svg` | 400x152 | yes |
| `activity.svg` | 400x174 | yes |
| `nuget.svg` | 400x174 | yes |
| `nuget-trend.svg` | 400x174 | yes |
| `stars.svg` | 400x174 | yes |
| `contributions.svg` | 804x150 | no, GitHub already draws the calendar on the profile |
| `repo/<repository>.svg` | 400x72 | yes |
| `history.json` | - | no, the daily snapshots the trend cards are drawn from |

The wide cards are exactly two 400px cards plus the space the markdown renderer puts between them, so
every row in the README lines up. Changing `RepositoryCard.Width` moves the wide cards with it.

`contributions.svg` and `habits.svg` are still generated and reachable on the site, so putting either
back is a one-line change in `README.md`.

The repository card shows only the headline of the GitHub description, the part before the first
` - ` (an em or en dash also works), on a single line. Keep that part short enough for the card;
the details after the dash are for GitHub itself.

## History and the trend cards

`nuget-trend.svg`, `stars.svg` and the `+N` next to the star count on a repository card are drawn from
`history.json`, a file the run reads at the start and writes back into the output directory, so it
rides along on the `gh-pages` branch. The workflow copies the previous one out of that branch before
generating; locally, pass `--history path` to keep one between runs.

The file holds one snapshot per day: total downloads and package count from NuGet, and stars per
repository. A series only records a value when it changes, so it stays small. Package points older
than `History.DetailDays` are folded into a single carry-over point; star points are kept.

Two things follow from the data source:

- NuGet has no download history API, so the downloads-per-day curve begins the day after the first
  snapshot and shows `Collecting daily data since ...` until then. A day the workflow did not run
  appears as zero, and its downloads land on the next day that did run.
- The first time a repository is seen, its star curve is rebuilt from when each current stargazer
  starred it, which is why the star card has a full year on day one. Stars removed since are not in
  that reconstruction, so the seeded curve can sit a little below what daily snapshots would show.

Deleting `history.json` from `gh-pages` (or running without `--history`) starts the NuGet history over
and re-seeds the star history.

## Settings

Besides the repository list, `settings.json` carries:

| Key | Meaning |
| --- | --- |
| `nuget.owner` | The nuget.org account; the card counts every package it owns, the same set as the profile page |
| `accent` | The hue shared by the heatmap, the activity line, and the download bars |
| `timeZoneOffsetHours` | The zone the commit hours are reported in; `9` for JST |
| `languageColors` | Language colors to use instead of the ones the API reports |

Language swatches on the languages card and the repository cards come from Linguist through the API.
Linguist reassigns colors from time to time - C# moved from `#178600` to the .NET purple `#7355dd` -
so `languageColors` pins the ones that should not follow:

```json
"languageColors": {
  "C#": "#178600"
}
```

`accent` accepts `green`, `blue`, `purple`, `orange`, `teal`, or `pink`. Each one ships a light and a
dark ramp, so the cards stay readable in both schemes. An unknown name fails the run rather than
silently falling back. Adding one means adding an entry to `Accents` in `Cards/Theme.cs`; nothing in
the cards refers to a color directly.

## Adding a repository card

1. Add the repository to the right category in `settings.json`. `name` must match the repository name
   on GitHub exactly, because it becomes the file name; `label` is the alt text used in the README
   and on the index page.

   ```json
   {
     "title": ":wrench: Helper",
     "repositories": [
       { "name": "mini-data-profiler", "label": "MiniDataProfiler" },
       { "name": "your-new-repo", "label": "Your.New.Repo" }
     ]
   }
   ```

   A new category is a new object in `categories` with its own `title` and `repositories`. Titles may
   use GitHub emoji shortcodes such as `:wrench:`; the index page expands them.

2. Add the line to `README.md`, next to the others in that category.

   ```markdown
   [![Your.New.Repo](https://usausa.github.io/usausa/repo/your-new-repo.svg)](https://github.com/usausa/your-new-repo)
   ```

3. Commit and push. The push touches `generator/**`, which triggers the workflow, and the card appears
   once the run finishes.

Only public, non-fork repositories owned by the user are fetched. A name that is not in that list is
reported as a warning and skipped rather than failing the run, so a typo shows up in the run log.

## Forcing a rebuild

The cards are rewritten from scratch on every run, so any of these produces fresh images.

- **Run the workflow by hand.** Actions -> generate-stats -> *Run workflow*. Nothing needs to change
  in the repository.

  ```bash
  gh workflow run generate-stats.yml
  ```

- **Wait for the schedule.** It runs daily at 03:00 JST.

- **Push a change under `generator/`.** That path filter also triggers the workflow.

Pushing only `README.md` does not trigger a run.

### When the images look stale

GitHub serves README images through its camo proxy, which caches them, so a finished run is not
immediately visible on the profile. Confirm what is actually published before re-running:

```bash
curl -sI https://usausa.github.io/usausa/overview.svg
```

Pages itself can also take a minute after the push. The build status is visible with:

```bash
gh api repos/usausa/usausa/pages/builds --jq '.[0] | "\(.status) \(.commit) \(.created_at)"'
```

## Running locally

A token is required because the contribution calendar is only available through the GraphQL API. Any
token with `public_repo` scope works; the workflow passes the automatic `GITHUB_TOKEN`.

A run takes about two minutes, nearly all of it walking commit history for the habits card: one
request per repository the profile committed to, plus a page per 100 commits. Repositories the token
cannot see are skipped and counted in the run log rather than failing the run, so the automatic
`GITHUB_TOKEN` produces a histogram built from public commits only.

Two limits are worth knowing about that card. `commitContributionsByRepository` returns at most 100
repositories, so a profile that commits to more than that loses the tail - currently about 5% of the
year's commits, which does not move the shape of the histogram. And the hours come from
`committedDate`, so a rebase moves a commit to when it was replayed rather than when it was written.

```bash
GITHUB_TOKEN=$(gh auth token) dotnet run --project generator -- --output dist
```

| Option | Default |
| --- | --- |
| `--output` | `dist` |
| `--settings` | `settings.json` next to the executable |
| `--history` | none; without it the trend cards start from an empty history |

Open `dist/index.html` to see every card the run produced.

## Adding a card

1. Add a class under `Cards/` with a `Render` method that returns the SVG string. Build it with
   `SvgBuilder`, and use the style names from `Theme.cs` (`tt` title, `tp` text, `tm` muted, `ac`
   accent, `bar`, `h0`-`h4` heatmap steps) rather than literal colors, so the card themes itself.
2. Register it in the `summary` dictionary in `Program.cs`.
3. Reference the new file from `README.md`.

`TextMeasure` estimates text width, because SVG has no text layout. Use `TextMeasure.Wrap` for any
string that comes from the API, so a long description wraps or is truncated instead of overflowing
the card.
