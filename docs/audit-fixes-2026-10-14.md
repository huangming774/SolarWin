# Audit Fixes - 2026-10-14

Scope: the 20 findings from the preceding review. Existing unrelated worktree
changes were preserved. No production database, live order, transfer, or account
mutation was used for validation.

## Implemented Changes

| Review ID | Fix | Main implementation |
| --- | --- | --- |
| 1 | Start shared room metadata, encryption and realtime setup for search navigation | `SolarWin/ViewModels/ChatDetailViewModel.cs` |
| 2 | Attach media bearer credentials only to the exact trusted HTTPS API origin | `SolarWin/Helpers/TrustedMediaOrigin.cs`, image loader, video cache |
| 3 | Contain video download/playback failures and always release preview resources and gate | `SolarWin/Helpers/VideoPreviewHelper.cs` |
| 4 | Pin legacy import context to the expected account; complete superseded migration waits | `SolarWin/Data/AccountDbContextFactory.cs`, `RoomLocalStore.cs` |
| 5 | Snapshot order preview input and reject responses for changed drafts | `SolarWin/ViewModels/OrderViewModel.cs` |
| 6 | Register local send echoes before HTTP submission and reconcile server client IDs | `SolarWin/ViewModels/ChatDetailViewModel.cs` |
| 7 | Retain the newer-message cursor for truncated centered history windows | `SolarWin/ViewModels/ChatDetailViewModel.cs` |
| 8 | Reset superseded history-page loading state and guard completion ownership | `SolarWin/ViewModels/ChatDetailViewModel.cs` |
| 9 | Replace known-message payloads and persist updates, retaining resolved media and pending animation | `SolarWin/ViewModels/ChatDetailViewModel.cs`, `MessageItemViewModel.cs` |
| 10 | Use SQLite instant-aware ordering instead of unsupported EF DateTimeOffset ordering | `SolarWin/Data/RoomLocalStore.cs` |
| 11 | Use translatable instant-aware retention predicates, allowing age/count cleanup to run | `SolarWin/Data/ChatWritePump.cs` |
| 12 | Detach tracked rows before bulk deletes and preserve delete barriers when coalescing | `SolarWin/Data/ChatWritePump.cs` |
| 13 | Count Unicode scalar values for trigram routing | `SolarWin/Services/ChatSearchService.cs` |
| 14 | Detach CanvasControl Draw handlers on unload and reattach on reload | `SolarWin/Controls/FastWin2DImage.*`, `GpuImage.*` |
| 15 | Recover shared GPU devices and rebind loaded canvases; contain recovery errors | `SolarWin/Helpers/DysonFileImageLoader.cs` |
| 16 | Recompute video player/card bounds on window resizing | `SolarWin/Helpers/VideoPreviewHelper.cs`, `VideoPreviewLayout.cs` |
| 17 | Separate draft preview and existing order payment state; explicitly clear on session reset | `SolarWin/ViewModels/OrderViewModel.cs`, order page |
| 18 | Reject stale wallet pagination responses/errors using wallet/filter/version snapshots | `SolarWin/ViewModels/WalletViewModel.cs` |
| 19 | Restore loaded Thinking conversation identity after agent selection callbacks | `SolarWin/ViewModels/ThinkingViewModel.cs` |
| 20 | Prevent cancelled AI streams from writing to newer conversation history | `SolarWin/ViewModels/AiViewModel.cs` |

Integration also guards centered-chat fallback/highlight dispatch against stale
navigation and dispatches fallback UI loading to the UI thread.

## Verification

- Debug x64 application build passed using Visual Studio MSBuild, including the
  native project. The existing `FilesPage.xaml:148` WMC1506 warning remains.
- `dotnet test SolarWin.Tests/SolarWin.Tests.csproj -c Debug --no-restore --nologo`:
  205 passed, 0 failed, 0 skipped (previous baseline: 170).
- `git diff --check` passed; Git emitted only line-ending conversion notices.
- SQLite regression tests exercise the production room store/write pump against
  temporary databases: offset ordering, account-switch import rejection and cache
  preservation, retention age/count, and mixed upsert/delete orderings.
- Other focused tests cover trusted origins, supplementary Unicode search with
  real SQLite, media layout calculations, client-ID matching and request ownership.
  Helper tests do not replace end-to-end ViewModel or WinUI tests.

## Remaining Runtime Checks

- Search-result chat navigation, reply jumps, paging interruption, duplicate sends,
  reactions/attachments and encrypted self-message reconciliation against a server.
- Repeated page unload/reload with GPU images, measured heap retention, and an
  actual GPU device-loss event. Source cleanup is not proof of measured leak removal.
- Image/video preview positioning, resizing and offline playback failure in WinUI.
- Order confirmation/payment and wallet switching with controlled delayed responses;
  no real purchases or money transfers were performed.
- Thinking history continuation and AI new-chat cancellation in the UI.
- ARM64, release packaging and live multi-account backend validation were not run.

Previously exposed credentials cannot be recovered by this patch. If an old build
loaded untrusted external media, invalidate the old login session and sign in again.
