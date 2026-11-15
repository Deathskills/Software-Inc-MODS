# Software Inc. mods (Deathskills + Claude)

Code mods for the game Software Inc. Each mod lives in its own top-level folder of this repo. This file is the handoff from the claude.ai project "Software Inc Modding" (12–14 Nov 2026) so work can continue in Claude Code.

Start here, then read the note for the mod you're working on in `docs/mods/`, and the references in `docs/reference/` when you need game mechanics.

## Project rules (from Marc, always follow)

1. **Vanilla only.** Base every analysis, formula and reference on the vanilla game code and data of the current live build. Ignore announced future updates.
2. **Never use or reference Marc's other installed mods.** When you look in `DLLMods\`, only touch the folders of the mods in this repo and `0HarmonyLoader`. Leave everything else out of analysis and docs.
3. **0HarmonyLoader (Harmony 2.3.3) may be used as a dependency.** Never modify `0Harmony.dll` or anything in `DLLMods\0HarmonyLoader\`, and never ship Harmony with a mod. Reference `Lib.Harmony 2.3.3` for compiling only (`ExcludeAssets="runtime"`).
4. **Mod folder layout:** `DLLMods\<mod name>\` with `meta.tyd` (Author `"Deathskills + Claude"`), `README.md`, the mod DLL (or `.cs` for source mods) and optional `icon.png`. No `src/`, no extra DLLs in the installed folder.
5. **Keep friction with other mods and install effort minimal:** prefix/postfix Harmony patches with a unique Harmony ID, no transpilers unless unavoidable, only patch what's needed, and never patch methods another of our mods relies on (see `docs/mods/isp-business.md`).
6. **Stay inside the game's mod security check.** Don't use `System.Reflection`, `System.IO` file access, `System.Net`, `NetworkManager`, `NetworkMessaging` or `SINetworking`. Using them triggers the red "all access" prompt (`GiveMeFreedom`). No config files as a result.
7. **Saves must survive mod removal.** Never put custom `Deal` subclasses or other custom types in vanilla saved collections. Keep mod state as primitives/text in `ModBehaviour` save data with a mod-specific key prefix.
8. **Multiplayer compatibility matters a lot to Marc.** Design for it from the start.
9. **Only this repo.** Never touch the Elite Dangerous app repo or any other repository from this project.
10. Don't put personal details (email etc.) in the repo.

## Reference links

- Website: https://softwareinc.coredumping.com/
- Roadmap (Trello): https://trello.com/b/9I286Mhu/software-inc
- Wiki: [Modding](https://softwareinc.coredumping.com/wiki/index.php/Modding), [Data Modding](https://softwareinc.coredumping.com/wiki/index.php/Data_Modding), [Code Modding](https://softwareinc.coredumping.com/wiki/index.php/Code_Modding), [Material Modding](https://softwareinc.coredumping.com/wiki/index.php/Material_Modding), [Furniture Modding](https://softwareinc.coredumping.com/wiki/index.php/Furniture_Modding)
- Note: the wiki gives the bandwidth factor as `1 + log10(year − 1969)`; the code uses `0.75 + log10(year − 1969)`. Trust the code.

## Machines

| PC | Name | Game folder |
|---|---|---|
| Desktop, RTX 4090 ("old desktop") | MOFamd40902025 | `E:\SteamLibrary\steamapps\common\Software Inc` |
| Laptop, RTX 3070 ("new laptop") | MOF3070laptop | `D:\SteamLibrary\steamapps\common\Software Inc` |

- Game DLLs to compile against: `<game>\Software Inc_Data\Managed\` (Unity 2018.4.36f1, DLL mod build 24, game Beta 1.8.x as of Nov 2026).
- Both PCs have `DLLMods\0HarmonyLoader\` (DLL + `libs\` with MonoMod and `0Harmony.dll` 2.3.3, no `meta.tyd`).
- A stray `SoftwareInc-mods-laptop.zip` sits in the laptop's `DLLMods\`, left for Marc to delete.

## Repo layout and branches

```
ISP Business/                  src/ (csproj + .cs), README.md, meta.tyd, icon.png   (no DLL in repo)
Server Hosting Business/       src/, README.md, meta.tyd, ServerHostingBusiness.dll, hosting-rates.html
Realistic Population growth/   RealisticPopulation.cs, README.md, meta.tyd, population data + review
docs/mods/                     status, decisions and open items per mod
docs/reference/                game mechanics references, ISP design spec, findings, rate studies
```

- **There is no `main` branch.** The default branch is `claude/project-thread-ieoq7i` (it still has three "Access test" commits at its root). Open PRs against it.
- `backup/pre-reliability-pass` (`fd5938f`) holds all three mods as they were before draft PR #6. Don't delete it.
- Draft **PR #6** (`codex/mod-reliability`, by Codex) is open and unmerged; see the mod notes for what it changes and its risks.
- Merged thread branches (`claude/project-thread-6g55p2`, `-us24py`, `-xd8ak9`, `-xsxu5i`) are stale.

## Building

Each DLL mod has an SDK-style csproj targeting `net472`, C# 9, with a `ManagedDir` property pointing at the game's Managed folder:

```
dotnet build "ISP Business/src/ISPBusiness.csproj" -c Release -p:ManagedDir="E:\SteamLibrary\steamapps\common\Software Inc\Software Inc_Data\Managed"
dotnet build "Server Hosting Business/src/ServerHostingBusiness.csproj" -c Release -p:ManagedDir="<same>"
```

- Without `-p:ManagedDir`, the csproj expects the repo folder to sit inside `<game>\DLLMods\` (`..\..\..\Software Inc_Data\Managed`).
- Linux/cloud: `apt-get install dotnet-sdk-8.0` works (the dot.net download host may be blocked); copy the game's Managed DLLs from a PC and pass that folder as `ManagedDir`.
- Earlier builds on the 4090 used a standalone Roslyn 4.8 `csc` with C# 9 against the same references when the .NET SDK wasn't available.
- The 4090 has .NET SDK 11.0.100 since 15 Nov 2026 (`dotnet build` works). The GitHub CLI (`gh`) is not installed there, so PRs are opened from the GitHub website.
- **After building, run a security check** of the DLL's referenced types against the game's blocklist (earlier sessions did this with the game's own `Mono.Cecil.dll`). It must pass without `GiveMeFreedom`.
- Realistic Population growth is a source mod: the game compiles `RealisticPopulation.cs` itself; nothing to build.
- **Source mods are rewritten by the game before compiling** (`ModController.InjectTryBlocks`: strips comments, wraps method bodies in try/catch, then compiles with the bundled Mono `mcs.dll`). Its parser loses track on an apostrophe anywhere in the file (strings or comments) and the mod then fails with `CS1525 Unexpected symbol try`. Never use apostrophes in a source mod, and avoid escaped quotes. A normal compiler will not catch this; the 15 Nov session reproduced it by calling `StripComments` / `HandleObjectParsing` from `Assembly-CSharp.dll` by reflection and parsing the result with `mcs.dll --parse` in 32-bit PowerShell.

## Installing

1. **Close the game first.**
2. Back up the installed folder to `<game>\ModBackups\<mod name> pre-<new version>\` (inside the game folder, **outside** `DLLMods\` so the game doesn't load it).
3. Copy only the mod DLL (or `.cs`), `meta.tyd`, `README.md` and `icon.png` into `<game>\DLLMods\<mod name>\`.
4. Never copy or edit game-generated files: `*H.bin` (local MD5 approval cache), `*.dcache`, `*Setting.txt`. Never touch `Saves\` or other mods.
5. Verify by SHA-256 that the installed file matches the build. The game will ask once to approve a changed mod.
6. Install on **both PCs** when Marc asks for "both"; for multiplayer between them, both need the same mod version (ISP Business checks its own protocol version; vanilla checks nothing).

## Installed builds (4090 updated 15 Nov 2026 with the MegaMod compatibility builds, laptop checked 14 Nov)

| Mod | 4090 desktop | 3070 laptop | Repo default branch |
|---|---|---|---|
| ISP Business | 0.6.1 from `claude/megamod-compat` (`29095f53…0c6d`) | 0.6.0, built in the cloud (`555b0c21…47a2`) | 0.6.0 source, no DLL |
| Server Hosting Business | `claude/megamod-compat` DLL (`507b2227…0e3d`) | unknown build (`f52c78fb…29d9`) | `be75ec0c…885e` (PR #3) |
| Realistic Population growth | `claude/megamod-compat` `.cs` (`cb6a39b6…53f7`) | merged `.cs` (`c141e090…a004`) | merged `.cs` |

The 4090 builds above come from branch `claude/megamod-compat` (pushed, PR not merged yet), so the 4090 is ahead of the default branch and of the laptop. Its previous files are in `ModBackups\ISP Business pre-0.6.1\`, `ModBackups\Server Hosting Business pre-megamod-compat\` and `ModBackups\Realistic Population growth pre-megamod-compat\`.

Backups: laptop has `ModBackups\ISP Business pre-0.6.0\`. The 4090 has the PR #6 builds it ran until 15 Nov in `ModBackups\Server Hosting Business pre-repo-PR3\` (`3efcc675…f137`) and `ModBackups\Realistic Population growth pre-repo-merged\` (`8aab1e2c…4062`). Older copies of each mod are also in the claude.ai project files (`laptop-backup/`, `laptop-install/`, `server-hosting-business/`, `realistic-population/`); those aren't in this repo.

## Status and open items (summary)

**ISP Business** (0.6.0, PR #8 merged). Details: `docs/mods/isp-business.md`.
- Player-to-player ISP agreements are built but **untested in a real multiplayer game**.
- Spec still has the **Reliability tab** and an **options screen** (configuration) to build.

**Server Hosting Business** (PRs #2, #3 merged). Details: `docs/mods/server-hosting-business.md`.
- Confirm a live offer matches the expected rates; optional "Maximum bandwidth usage" display patch.

**Realistic Population growth** (PR #1 merged, working). Details: `docs/mods/realistic-population-growth.md`.

**Known risks across mods**
- **ISP + hosting double pay:** a product can pay you for an ISP deal and a hosting deal on the same traffic. Not decided yet.
- With Server Hosting Business installed, product bandwidth (and so ISP demand and capacity needs) is much larger.
- **Multiplayer version compatibility:** vanilla has no mod check; the 4090 now matches the repo, but the laptop still runs an untraced Server Hosting build (`f52c78fb`). Install the repo DLL there before multiplayer tests.
- **Draft PR #6** is unmerged (no longer installed anywhere known) and based on the pre-0.6.0 state; decide with Marc whether to rebase, cherry-pick or close it.
- **No `main` branch**; default is an old thread branch.
- If a game update removes a patched method, a mod can end up half-patched. Re-check patch targets after game updates.

## Reference docs (docs/reference/)

- `servers-and-monetization.md`: base-game servers, monetization, electricity (incl. grid-only baseline), deals, legal system and modding notes, from the decompiled code.
- `isp-design-spec-v0.7.md`: Marc's full ISP mod design spec.
- `isp-phase1-findings.md`: verified vanilla code findings, mod conflicts, multiplayer messaging API, decisions.
- `hosting-power-options.md`, `hosting-rates-and-income-model.md` and `artifacts/*.html`: Server Hosting Business pricing studies.
- `megamod-compatibility.md`, `megamod-server-features.md`: compatibility with MegaMod Core and MegaMod Data (Workshop 3813626791 and 3813625905). Marc named these two, so they are the one exception to rule 2.
- `README.md` in that folder indexes them.

## Working with Marc

- Background in game QA and learning automation (Unreal Engine); part-time modder. Plays Albion Online and Elite Dangerous.
- Prefers short, plain answers that lead with the result. Asks for "merge" explicitly; don't merge PRs without that.
