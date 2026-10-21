# TOOLBOX-001 Round 2 — Windows Interaction Final Review

日期：2026-08-28

## Verdict

> **TOOLBOX-001 Round 2 = CLOSED / GO / PASS**

## Evidence

Input candidate:
`YangCAD_FULL_REPO_ROUND2_STATIC_CANDIDATE_20260828.zip`

SHA-256:
`A477004D837FA63A9BC6F94A075BABEA255FEBBD4E25AEC953B78A2CF3B3C1B1`

Windows final evidence:
`YangCAD_FULL_REPO_ROUND2_WINDOWS_INTERACTION_GO_20260828.zip`

SHA-256:
`4E65785D9292803B72C6A14A9E11767C73DC6742BA8550CDE92C8E72490E2E9E`

Independent review:
- outer SHA MATCH
- internal SHA256SUMS 140/140
- 7 static/policy gates PASS
- production delta vs candidate: one file only
- no added/removed production files

## Only Source Fix

File:
`src/YangCAD.AutoCAD/UI/ToolboxParameterPanel.xaml.cs`

Four expressions were changed from:

```csharp
Visibility.Collapsed
Visibility.Visible
```

to:

```csharp
System.Windows.Visibility.Collapsed
System.Windows.Visibility.Visible
```

Reason:
within a `UserControl` derived class, Roslyn resolved `Visibility` ambiguously against the inherited instance property, producing CS0176.

Judgment:
- minimal compile fix
- no behavioral change
- no XAML change
- no architecture change
- exact-year Windows clean build proves the fix

## Windows Runtime

### 2024 full interaction
PASS:
- Start Page safe
- 65 / 28 / 58
- Toolbox open/reopen
- Tool detail
- Planned disabled
- all 8 ParameterShape controls
- Layer reads actual drawing layer `0`
- Point / Handles
- required/range validation
- Palette result/error
- Favorites
- Recent newest-first/dedupe/max10
- real process restart persistence
- SearchKeywords
- 3 themes
- Control Center
- 0 unhandled exception
- 0 token leak

### 2021
PASS:
- Toolbox
- parameter detail
- 8 controls
- execute
- reopen
- 0 exception

### 2022 / 2023
PASS:
- Civil host Toolbox
- cards/controls
- execute
- reopen
- 0 exception

## Settings Isolation

Only Tool IDs were persisted in:
- ToolboxFavorites
- ToolboxRecent

Existing settings fields remained unchanged.
Original user settings were restored after the test.

## Final

Round 2 is closed.
Next:
**TOOLBOX-001 Round 3A — First Real CAD Tools**
