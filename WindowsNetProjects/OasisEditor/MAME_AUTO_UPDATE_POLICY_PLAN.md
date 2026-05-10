# MAME Auto-Update Policy Plan

This document defines the next discrete MAME workstream: adding a user-visible preference that controls whether the editor automatically keeps its managed MAME install on the latest available version.

## Goal

The editor should make MAME mostly invisible to the user while still allowing advanced/manual control.

Default behavior for new installs should be:

- discover latest MAME version;
- install latest automatically if no valid install exists;
- update to latest automatically when a newer version is discovered;
- select that latest installed version as the active/current MAME version.

## Preference Name

Recommended UI wording:

```text
Keep MAME up to date automatically
```

Recommended tooltip/help text:

```text
When enabled, Oasis Editor automatically downloads, installs, and selects the latest supported MAME release in the background.
```

Recommended internal setting name:

```text
KeepMameUpToDateAutomatically
```

Default value for new preferences:

```text
true
```

## Preferences UI

Add this setting to:

```text
Preferences -> MAME
```

Suggested placement:

- near the setup/status summary;
- above manual download/specific-version actions;
- visible without advanced/debug mode.

The MAME page should make clear whether the current version is:

- latest and valid;
- installing latest;
- older because auto-update is disabled;
- older because update failed;
- unknown because latest-version discovery failed.

## Startup Behavior

On editor/launcher startup:

1. Load preferences.
2. Run background MAME validation.
3. Run latest-version discovery.
4. If `KeepMameUpToDateAutomatically == true`:
   - if no valid MAME install exists, install latest discovered version;
   - if selected/current MAME version is older than latest discovered version, install latest discovered version;
   - after successful install, deploy/sync Oasis Lua plugins;
   - after successful plugin sync, set current/selected MAME version to the latest installed version.
5. If `KeepMameUpToDateAutomatically == false`:
   - do not auto-download a newer version just because it exists;
   - still validate the selected/current install;
   - still repair/re-sync plugins for the selected/current install if possible;
   - still allow manual `Download latest` or `Install selected version` actions.

## Important Behavior Decisions

- Default is enabled for new installs.
- Existing users with missing setting should be treated as enabled unless there is a migration reason not to.
- Disabling the setting should not remove installed versions.
- Disabling the setting should not prevent manual downloads.
- Disabling the setting should not stop plugin repair for the currently selected version.
- Auto-update should not interrupt a running MAME process. If MAME is running, defer update until safe.
- Auto-update should not delete the previous working version immediately. Keep at least the previous installed version available for rollback/manual selection.

## Version Comparison

Use normalized numeric comparison.

Examples:

```text
0287 > 0281
0281 > 0267
0267 > 0258
```

Do not rely on raw string comparison unless all values are normalized to four digits.

## Setup State Additions

The setup/provisioning state should distinguish:

- checking current install;
- checking latest version;
- latest already installed;
- auto-update disabled;
- update available;
- auto-updating to latest;
- update failed;
- update deferred because MAME is running.

## Logging / Diagnostics

Log these events:

- auto-update preference value at startup;
- latest version discovered;
- current selected version;
- whether an update is needed;
- whether update is skipped because preference is disabled;
- whether update is deferred because MAME is running;
- install/download/extract/plugin-sync progress;
- current version selected after successful install.

## Tests

Yes, this should be tested.

Add tests around the policy/orchestrator logic with fake services.

Recommended test cases:

- new/default preferences have `KeepMameUpToDateAutomatically == true`;
- missing setting in old preference JSON migrates to true;
- enabled + no valid install triggers install of latest;
- enabled + selected older version triggers install of latest;
- enabled + selected latest valid version does not reinstall;
- enabled + latest already installed but not selected selects latest;
- enabled + MAME process currently running defers update;
- disabled + selected older valid version does not auto-update;
- disabled + no valid selected install may still provision if needed for first-run usability only if explicitly decided by implementation; otherwise should report missing setup and expose manual action;
- disabled still allows plugin sync/repair for selected version;
- failed latest discovery falls back to cached/seed behavior without crashing;
- failed auto-update preserves previous selected working version.

## Recommended Codex Steps

### Step 1 - Add Preference Model Field

- Add `KeepMameUpToDateAutomatically` to the editor preferences model.
- Default it to true.
- Preserve old preference JSON compatibility.
- Add tests for default/migration behavior if preference tests exist.

### Step 2 - Add MAME Preferences UI

- Add a checkbox labelled `Keep MAME up to date automatically`.
- Bind it to the preference model/store.
- Add tooltip/help text.
- Verify it persists across restarts.

### Step 3 - Add Policy Logic

- Add or update setup orchestration logic to compare current selected version with latest discovered version.
- If enabled and latest is newer/missing, queue/install latest in background.
- After successful install/plugin sync, select latest.
- Do not remove previous working install.

### Step 4 - Add Tests

- Add unit tests around the auto-update policy/orchestrator using fake services.
- Tests should not perform network, real download, or real extraction.

### Step 5 - Add Diagnostics

- Ensure output log and Preferences status show why update is running, skipped, deferred, or failed.

## Manual Verification

After implementation, John should verify:

- new preferences default to auto-update enabled;
- toggling the checkbox persists;
- when enabled and latest version is missing, the editor downloads/installs/selects latest;
- when enabled and an older version is selected, the editor installs/selects latest;
- when disabled and an older version is selected, the editor leaves it selected;
- plugin deployment still works for newly installed latest version;
- previous installed version remains available after update;
- failed update does not break an existing working MAME selection.
