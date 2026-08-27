# Profile card generator

Renders the SVG cards used by the profile README. It reads the GitHub GraphQL API and the NuGet
search API, writes the cards to an output directory, and the workflow force-pushes that directory to
the `gh-pages` branch, where GitHub Pages serves it from `https://usausa.github.io/usausa/`.

No external NuGet packages are used, so the workflow only needs the .NET SDK.

## Layout

| Path | What it holds |
| --- | --- |
| `settings.json` | The user to report on, the NuGet id prefixes to search, and the repositories that get a card |
| `GitHubClient.cs` | Profile, contribution calendar, repository list and language sizes |
| `NuGetClient.cs` | Package downloads |
| `EmojiResolver.cs` | Expands `:shortcode:` in category titles for the index page |
| `Cards/Theme.cs` | The light and dark palettes emitted into every card |
| `Cards/*Card.cs` | One class per card |
| `Cards/IndexPage.cs` | The catalogue page at the site root |

Each card is a single SVG that carries both palettes and switches with a `prefers-color-scheme` media
query, so the README needs no `<picture>` markup. Light is the value outside the media query, which
is what a viewer that ignores the query will show.

## Output

| File | Size |
| --- | --- |
| `overview.svg` | 400x152 |
| `languages.svg` | 400x152 |
| `contributions.svg` | 804x150 |
| `activity.svg` | 400x174 |
| `nuget.svg` | 400x174 |
| `repo/<repository>.svg` | 400x88 |

The wide card is exactly two 400px cards plus the space the markdown renderer puts between them, so
every row in the README lines up. Changing `RepositoryCard.Width` moves the wide card with it.

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

```bash
GITHUB_TOKEN=$(gh auth token) dotnet run --project generator -- --output dist
```

| Option | Default |
| --- | --- |
| `--output` | `dist` |
| `--settings` | `settings.json` next to the executable |

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
