# JYXR JSON Editor Development Notes

This tool is a local content workbench for the selected `mods/<modId>/data` directory and root `assets`.
It should stay small, explicit, and content-author friendly.

## Current Scope

The editor currently focuses on static JSON data and light validation:

- Discover MODs from `mods/*/mod.json` and route reads/writes through the active MOD id.
- Browse and edit JSON files under the selected MOD data directory.
- Preview assets under root `assets`.
- Validate content through `Game.Content.Loading.JsonContentLoader`.
- Build a story graph for `story/*.story.json` with grouped segment lists, entrypoints, outgoing/incoming flow, and diagnostics.
- Build a lightweight client-side index from:
  - top-level `id` records
  - story `segments[].name`
  - `resources.json`
  - `characters.json`
- Provide generated form editing for top-level array JSON files.
- Provide a dialogue speaker portrait helper for creating basic speaker records.
- Provide a portrait checker for static speaker/avatar wiring issues.

It is not a Godot scene editor, PCK builder, external mod manager, or general filesystem editor.

## Architecture

### Backend

`Program.cs` is a minimal ASP.NET Core app.

Important endpoints:

- `GET /api/workspace`
  - Returns project root, MOD root, discovered MOD summaries, default MOD id, data path, and asset path.
- `GET /api/data/files?modId=...`
  - Lists the selected MOD's `data/**/*.json`.
- `GET /api/data/file?path=...&modId=...`
  - Reads a JSON data file.
- `PUT /api/data/file?modId=...`
  - Formats and saves a JSON data file.
  - Creates a timestamped backup under `tools/JsonEditor/.backups`.
  - Runs full content validation after save.
- `GET /api/validate?modId=...`
  - Runs content validation without saving.
- `GET /api/story/graph?modId=...`
  - Builds a read-only story graph for the selected MOD.
  - Reports segment counts, grouped branches, entrypoints, edge diagnostics, and static story references.
- `GET /api/static/portraits/check?modId=...`
  - Checks dialogue portrait wiring without modifying files.
  - Scans `characters.json`, `resources.json`, `story/*.story.json`, and root `assets/art`.
  - Reports missing portrait resources, missing image files, non-`512x512` images, PNG/JPG alpha issues, and story speakers that cannot resolve to a character.
- `GET /api/assets/files`
  - Lists files under root `assets`.
- `GET /api/assets/file?path=...`
  - Serves an asset for preview.
- `POST /api/static/speaker`
  - Adds a minimal dialogue speaker to `characters.json`.
  - Adds a portrait resource to `resources.json`.
  - Intended for NPC speakers like `清兵`, `内侍`, `红花会弟子`.

Path resolution must stay constrained to the project data/assets roots. Do not add arbitrary absolute path writes.

### Frontend

The frontend is plain HTML/CSS/JavaScript:

- `wwwroot/index.html`
  - Layout and inspector sections.
- `wwwroot/app.js`
  - State, MOD switching, file loading, form rendering, content index, story graph rendering, helpers.
- `wwwroot/styles.css`
  - Dense desktop tool styling.

There is no build step and no package manager. Keep dependencies at zero unless there is a very strong reason.

## MOD Package Policy

The project-maintained expansion package is `mods/jyxr-expansion`.

- Track `mods/jyxr-expansion/**` in git except `.pck` files and local system files.
- Keep other MODs, including an author/base package such as `mods/jyxr-base`, ignored by default.
- Treat `mods/jyxr-base` as read-only upstream/reference content when present.
- Use the editor MOD selector to inspect the author/base package, then switch to `jyxr-expansion` before editing project content.
- Current runtime still starts one selected MOD at a time. It does not yet merge author/base data with expansion data at runtime.
- Resource packs remain runtime assets: PCK files are referenced by `mod.json` but are not committed to git.

## Data Rules

### Dialogue portraits

Story dialogue does not directly specify a portrait. The runtime resolves it from the `speaker` field:

1. Match `speaker` against existing party/name roster.
2. Match `speaker` against `characters.json` by id or name.
3. Read the character `portrait`.
4. Resolve that portrait id through `resources.json`.
5. Load the asset from `assets/art`.

Example:

```json
{
  "id": "清兵",
  "name": "清兵",
  "portrait": "头像.清兵"
}
```

```json
{
  "id": "头像.清兵",
  "group": "头像",
  "value": "head/qingbing"
}
```

The asset should be:

```text
assets/art/head/qingbing.png
```

Current UI assumes head portraits are generally `512x512` PNGs. Transparent backgrounds are strongly preferred.

### Static speaker template

The speaker helper creates a minimal valid character:

- `level: 1`
- low stats
- empty skills
- empty equipment
- `arenaEnabled: false`

This is meant for dialogue display only. If an NPC later becomes recruitable or combat-capable, edit the generated record manually.

### Resources

`resources.json` values are resource ids, not necessarily full paths.

Examples:

- `头像.清兵 -> head/qingbing`
- `音乐.城市3 -> audio/...`
- `地图.塞外 -> ...`

The frontend tries to resolve these through `assets/art` or `assets/audio` for preview.

## Portrait Checker

The "头像检查" panel is intentionally read-only. It helps content authors find broken portrait chains before launching the game.

It checks:

- `characters.json` records that already have a `portrait` field.
- `resources.json` records where `group == "头像"`.
- Story dialogue and choice prompt speakers in `story/*.story.json`.
- PNG/JPG metadata for portrait image dimensions and alpha channel presence.

Known narrator speakers such as `旁白`, `系统`, `提示`, and `江湖传闻` are ignored because they normally do not need a portrait.

The checker should stay diagnostic. Do not make it auto-resize images or auto-edit JSON unless the user explicitly asks for a separate repair workflow.

## Current Pain Points

These are known rough edges in the tool:

- Form view is generic and not schema-aware enough.
- Complex fields are still edited as raw JSON text.
- Story graph is read-only and not yet an editable step/card workflow.
- Story grouping and diagnostics are first-pass heuristics and still need better authoring affordances.
- No static check for missing `log` clues in story segments.
- No batch image normalization for portraits.
- Save always formats the entire JSON file.
- The tool selects a MOD from `mods/*/mod.json`; each operation uses the current MOD id.

## Recommended Next Steps

Prioritize static data quality before story authoring:

1. **Resource Manager**
   - Dedicated view for `resources.json`.
   - Group filters such as `头像`, `音乐`, `地图`, `UI`.
   - Inline asset preview.
   - Missing asset warnings.

2. **Character Manager**
   - Better form for `characters.json`.
   - Portrait picker.
   - Gender/grow template dropdowns.
   - Skill/equipment/talent reference pickers.

3. **Item Manager**
   - Type-specific fields for books, equipment, consumables, story items.
   - Picture picker and preview.
   - Validation for referenced skills/equipment.

4. **Story Tools**
   - Editable segment list and step cards.
   - Speaker portrait warnings.
   - Jump target picker.
   - Branch graph layout improvements and clearer trigger/source grouping.
   - Log clue checker.
   - Diff view between author/base content and `jyxr-expansion`.

## Design Principles

- Keep game runtime code untouched for content-tool convenience.
- Prefer data/tool fixes over engine behavior changes.
- Preserve existing content files and make timestamped backups before automated writes.
- Avoid compatibility hacks; make the data clearer instead.
- Keep workflow local and inspectable.
- Favor explicit helper operations over hidden automatic edits.

## Verification

Light checks for tool work:

```bash
node --check tools/JsonEditor/wwwroot/app.js
dotnet build tools/JsonEditor/JsonEditor.csproj --no-restore
dotnet test
```

Light checks for data work:

```bash
jq empty mods/jyxr-expansion/data/resources.json
jq empty mods/jyxr-expansion/data/characters.json
jq empty mods/jyxr-expansion/data/story/book-shujian.story.json
```

Full content validation can be run from the web tool with the "校验" button.
