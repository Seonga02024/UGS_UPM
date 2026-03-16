# com.robocare.ugs

UGS login and in-app update runtime assets for RoboCare projects.

## Included

- `Runtime/Common Script/LoginService.cs`
- `Runtime/Common Script/InAppUpdateManager.cs`
- `Runtime/Common Prefabs/InAppUpdateUI.prefab`

## Requirements

- Unity 2021.3 or newer
- UGS packages used by scripts:
  - `com.unity.services.core`
  - `com.unity.services.authentication`
  - `com.unity.remote-config`
- TextMeshPro

## Install (local path)

Add in your project `Packages/manifest.json`:

```json
"com.robocare.ugs": "file:../LocalPackages/com.robocare.ugs"
```

Or use Package Manager:

1. `Window > Package Manager`
2. `+ > Add package from disk...`
3. Select `LocalPackages/com.robocare.ugs/package.json`

## Repackage Tool

Use the helper script from repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\repackage-ugs.ps1
```

Version bump + `.tgz` build:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\repackage-ugs.ps1 -Version 1.0.1 -Pack
```
