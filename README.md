# ResoniteDownloader

A .NET tool to download and manage [Resonite](https://resonite.com/) game files, optimized for Linux and Headless environments but compatible across platforms.

## Features

- Download and update Resonite game files via Steam's depot system
- Target specific versions or manifest IDs for precise build control
- Automatic version detection from the resonite-version-monitor versions file
- Support for public and protected branches
- Outputs version metadata for CI/CD workflows

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [DepotDownloader](https://github.com/SteamRE/DepotDownloader) must be available in your `PATH`.

## Installation

Install as a global .NET tool:

```bash
dotnet tool install --global hazre.ResoniteDownloader
```

## Usage

### Download Game Files

Download Resonite files to a specific directory.

```bash
resonitedownloader download --game-dir "/path/to/resonite" --steam-user "username" --steam-pass "password"
```

**Download with Manifest ID** (bypasses version lookup):

```bash
resonitedownloader download --game-dir "/path/to/resonite" --steam-user "username" --steam-pass "password" --manifest-id 1234567890
```

**Options:**

| Option | Shorthand | Description | Required | Default |
| :--- | :--- | :--- | :--- | :--- |
| `--game-dir` | `-d` | Game installation directory | Yes | - |
| `--steam-user` | `-u` | Steam username | Yes | - |
| `--steam-pass` | `-p` | Steam password | Yes | - |
| `--beta-pass` | `-b` | Beta password (for protected branches) | No | - |
| `--version` | `-v` | Specific version to download | No | Latest |
| `--manifest-id` | `-m` | Specific manifest ID to download | No | - |
| `--branch` | - | Branch to download from | No | `public` |

**Note:** When using `--manifest-id`, the actual version will be determined from `Build.version` after download. You can optionally provide both `--version` and `--manifest-id` if you already know them.

### Resolve Version

Check what the latest version is for a specific branch without downloading.

```bash
resonitedownloader resolve-version --branch headless
```

**Resolve Version from Manifest ID** (downloads only `Build.version`):

```bash
resonitedownloader resolve-version --manifest-id 1234567890 --steam-user "username" --steam-pass "password"
```

**For protected branches:**

```bash
resonitedownloader resolve-version --manifest-id 1234567890 --steam-user "username" --steam-pass "password" --beta-pass "headlesspass" --branch headless
```

**Options:**

| Option | Shorthand | Description | Required | Default |
| :--- | :--- | :--- | :--- | :--- |
| `--version` | `-v` | Specific version to check | No | Latest |
| `--manifest-id` | `-m` | Manifest ID to resolve version from | No | - |
| `--steam-user` | `-u` | Steam username (required with `--manifest-id`) | Conditional | - |
| `--steam-pass` | `-p` | Steam password (required with `--manifest-id`) | Conditional | - |
| `--beta-pass` | `-bp` | Beta password (for protected branches) | No | - |
| `--branch` | `-b` | Branch to query | No | `public` |

**Note:** When using `--manifest-id`, the tool downloads only `Build.version` using a filelist to efficiently resolve the version number.

## GitHub Actions Integration

This tool outputs `version`, `build-id`, and `manifest-id` to the GitHub Actions output environment if available.

```yaml
- name: Download Resonite
  id: resonite
  run: resonitedownloader download -d ./game -u ${{ secrets.STEAM_USER }} -p ${{ secrets.STEAM_PASS }}

- name: Use Version
  run: |
    echo "Installed version: ${{ steps.resonite.outputs.version }}"
    echo "Build ID: ${{ steps.resonite.outputs.build-id }}"
    echo "Manifest ID: ${{ steps.resonite.outputs.manifest-id }}"

- name: Resolve Version from Manifest
  id: resolve
  run: resonitedownloader resolve-version -m 1234567890 -u ${{ secrets.STEAM_USER }} -p ${{ secrets.STEAM_PASS }}

- name: Use Resolved Version
  run: echo "Version for manifest: ${{ steps.resolve.outputs.version }}"
```

## Examples

### Download latest public version
```bash
resonitedownloader download -d ./game -u myuser -p mypass
```

### Download latest headless version
```bash
resonitedownloader download -d ./game -u myuser -p mypass -b headlesspass --branch headless
```

### Download specific version
```bash
resonitedownloader download -d ./game -u myuser -p mypass -v 2024.1.28.1342
```

### Download specific manifest
```bash
resonitedownloader download -d ./game -u myuser -p mypass -m 1234567890
```

### Download specific version and manifest together
```bash
resonitedownloader download -d ./game -u myuser -p mypass -v 2024.1.28.1342 -m 1234567890
```

### Check latest version without downloading
```bash
resonitedownloader resolve-version
```

### Resolve version from manifest ID
```bash
resonitedownloader resolve-version -m 1234567890 -u myuser -p mypass
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.