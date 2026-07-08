# JYXR JSON Editor

Local web editor for the Godot project root. It discovers `mods/*/mod.json`, edits the selected MOD's `data/` directory, and reads the root `assets/` directory for preview and path copying.

Run from the repository root:

```bash
dotnet run --project tools/JsonEditor/JsonEditor.csproj
```

Open:

```text
http://localhost:5127
```

The editor does not manage PCK export, Godot imports, arbitrary external paths, or runtime MOD dependency merging.

## Features

- Switch between discovered MODs and edit the active MOD's `data/**/*.json`.
- Browse root `assets/`, preview images/audio, and copy asset paths.
- Save with JSON formatting, backup, and content validation.
- Search inside the current file.
- Show cursor line and column.
- Build a lightweight content index from top-level `id` fields and story `segments[].name`.
- Select a JSON string to see matching definitions and jump to the defining file.
- Edit top-level JSON array files through a generic form view.
- Show data files as cards with type, record count, and file size.
- Add, duplicate, and delete records from form view.
- Suggest asset paths for resource-like fields and preview image/audio values inline.
- Create dialogue speaker records and portrait resources from the inspector helper.

## MOD switching

Use the top-right MOD selector before editing. All data reads, saves, validation, static helpers, and story graph analysis use the selected MOD id. The path line shows the active target, for example `正在编辑：金庸群侠传XR 扩展内容 · mods/jyxr-expansion/data`.

The first version treats each MOD as its own content directory. After the original author's base package is added, select that package to inspect original content, and select `jyxr-expansion` to edit this project's extension content. Patch-style loading such as "base MOD plus extension MOD" still needs a formal content dependency design before it should affect runtime behavior.

## Form view

Open a top-level array file such as `characters.json`, `items.json`, `resources.json`, or `game-tips.json`. The editor will show a record list and a generated form.

Primitive fields are edited directly. Object and array fields stay as JSON text inside the form so no data is dropped. The JSON view remains the source of truth, and saving still uses the normal save button.

Use the form header actions to add, duplicate, or delete records. For `resources.json`, the `group` field suggests existing groups, and `value` suggests asset paths from `assets/`.

## Static Data Helpers

The inspector includes a dialogue portrait helper. Use it when a story speaker such as `清兵` needs a portrait:

1. Put the portrait image under `assets/art/head/`, preferably `512x512` PNG with transparency.
2. Fill the helper fields, for example:
   - speaker id: `清兵`
   - display name: `清兵`
   - portrait resource: `头像.清兵`
   - portrait asset value: `head/qingbing`
3. Click "创建说话人".

The helper appends a minimal non-combat character record to `characters.json` and a portrait resource to `resources.json`.

## Development Notes

See [DEVELOPMENT.md](DEVELOPMENT.md) for architecture, data rules, known limitations, and planned improvements.
