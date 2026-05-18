# Scaffold (internal tooling / UPM packages)

**Scaffold** is the shared Unity package monorepo used for reusable systems (MVVM, navigation, LiveOps, Addressables helpers, etc.). This project does not embed Scaffold as a submodule; packages are pulled via **Git URL + path** in `Packages/manifest.json`, same pattern as **Gear-Engine**.

## Where to look


| Kind                           | Location                                                                                                                                   |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------ |
| **Local clone (this machine)** | `C:\Unity\Scaffold` — browse here first when searching for a package, reading `package.json`, or comparing versions.                       |
| **Upstream repo**              | [https://github.com/MgCohen/Scaffold](https://github.com/MgCohen/Scaffold)                                                                 |
| **Packages in repo**           | `Assets/Packages/` — each UPM package is a folder `com.scaffold.<name>/` (for example `com.scaffold.mvvm`).                                |
| **Reference consumer**         | `C:\Unity\Gear-Engine` — see `Packages/manifest.json` for how entries are wired and which `com.scaffold.*` packages are commonly combined. |


## Adding or updating a package in this project

1. Confirm the package exists under `C:\Unity\Scaffold\Assets\Packages\com.scaffold.<name>\` (or on GitHub at the same path).
2. In **Card Framework**, add or edit a line in `Packages/manifest.json` under `dependencies`, using this shape (branch is usually `#main`):
  `"com.scaffold.<name>": "https://github.com/MgCohen/Scaffold.git?path=/Assets/Packages/com.scaffold.<name>#main"`
3. Let Unity resolve packages; check `Packages/packages-lock.json` after refresh if you need the resolved revision.

Some Scaffold-adjacent packages may point at other repos (Gear-Engine uses `com.scaffold.schemas` from `ScaffoldLibrary/Schemas`); treat those as exceptions and follow the `package.json` / manifest in Scaffold or Gear-Engine.

## Agent / assistant behavior

When the user says things like **“check if Scaffold has something for it”** or **“get X from Scaffold”**:

1. Prefer inspecting the **local** tree at `C:\Unity\Scaffold\Assets\Packages\` for `package.json`, README, and asmdefs.
2. If the local path is missing or outdated, use **[https://github.com/MgCohen/Scaffold](https://github.com/MgCohen/Scaffold)** (same paths under `Assets/Packages/`).
3. For **integration examples**, cross-check `**C:\Unity\Gear-Engine`** (`manifest.json`, `Assets/Packages`, asmdef references).

Do not assume a Unity package exists on the public Asset Store; default to **Scaffold’s `com.scaffold.*`** naming and the Git URL pattern above.