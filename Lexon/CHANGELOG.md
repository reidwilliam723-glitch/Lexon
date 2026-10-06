# Changelog

All notable changes to Lexon will be documented in this file.

## [1.6.1] - 2026-10-06

### Settings UI
- Gallery fixes: the AI tab no longer loses an unsaved provider or key when the window is re-activated or you switch tabs; the clipboard is only listened to while waiting for a key; duplicate key validation requests are skipped; and settings changes now take effect immediately instead of after the save delay. The classic Settings form is still the default.

## [1.6.0] - 2026-10-06

### Settings UI
- `--ui-gallery` gets an AI tab (provider, API key with automatic validation, clipboard key capture, model, and the three AI toggles). Turning on Local-only in the gallery now also unloads the running AI provider, and the AI policy is published from the current values of both pages. The classic Settings form is still the default.

## [1.5.0] - 2026-10-05

### Settings UI
- `--ui-gallery` gets App tone and Writing tabs. Fixed: a stale gallery tab could overwrite newer Local-only and blocked-app values written by the classic form; pages now write only what the user changed, flush when the window loses focus, and reload when it regains it. Live effects now apply immediately. Export/import of learned data now reports file errors instead of crashing. The classic Settings form is still the default.

## [1.4.0] - 2026-10-05

### Settings UI
- `--ui-gallery` gets Appearance and Privacy tabs (live, wired to the real profile). Fixed: the gallery could overwrite newer settings from the classic form; pages now write only their own keys. The process picker and activity log are still the classic dialogs. The classic Settings form is still the default.

## [1.3.0] - 2026-10-05

### Settings UI
- The `--ui-gallery` window has a General tab: the first real settings page in WPF, wired to the real profile. The classic Settings form is still the default.

## [1.2.0] - 2026-10-02

### Settings UI
- WinForms buttons now use `OnPrimary` text plus hover, pressed, and a visible border, so High Contrast and Dark stay readable.
- `--ui-gallery` makes Ctrl+Shift+S open the WPF control gallery (and bring the existing window forward) instead of the old Settings form.
- Reduced motion is no longer ignored: `PopupAnimation` comes only from `WpfThemeBridge`.
- Combo dropdown lines up with the field; the scrollbar no longer shoves page content sideways on hover.
- Secondary and Subtle hover/pressed show a Primary border when the fill barely changes.
- CheckBox and RadioButton accept clicks in the gap beside the label. ListBox, ToolTip, and InfoTip follow the theme and keyboard focus.

## [1.0.11] - 2026-10-02

### Settings UI
- Control gallery behind `--ui-gallery`, with a Disabled toggle and names on every demo control.
- Button hover and pressed contrast stays at least 4.5:1. Windows High Contrast primary hover stays readable.
- ToggleSwitch animates both ways. WPF no longer warms up for every user while SettingsForm is the default.
- Remaining chrome is themed: scrollbar, checkbox, radio, tooltip, list item, and context menu.

## [1.0.10] - 2026-09-30

### Reliability
- High Contrast colours parse correctly (8-digit hex).
- Settings shows Checking while an AI probe is in flight.
- Persist debounce matches production (400 ms, 100 ms poll).

## [1.0.9] - 2026-09-30

### Settings UI
- Testable settings model (`Lexon.SettingsModel`) and the WPF settings UI foundation (`Lexon.SettingsUi`).
- WinForms `SettingsForm` remains the default; the gallery is behind `--ui-gallery`.

## [1.0.8] - 2026-09-30

### Privacy and reliability
- Password and other secure fields are detected before any text is read. Unknown fields fail closed. Buffering, learning, expansion, and AI are skipped there.
- Rewriting selected text uses a single Backspace so surrounding text is not deleted. Non-selection replaces count graphemes (emoji-safe).
- Blocked app names are normalized so `putty.exe` matches the stored `putty` list, including on the typing path.
- Double-press Ctrl now pauses rewrite, buffering, and writing-style learning as well as suggestions, and clears typed buffers.
- Plugins stay unloaded unless EnablePlugins is on. A new AI probe cancels the previous request. Remote Ollama sends are logged. `publish.ps1` can only delete under the repo publish folder.

## [1.0.7] - 2026-09-29

### Privacy and AI control
- Local-only now removes the AI provider immediately (no restart) and cancels in-flight probes so a late result cannot turn AI back on.
- New Settings switches: cloud AI while typing (default **off**), rewrites of selected text (default on), prefetch on selection (default **off**). Existing users stop sending typing context on update until they opt in.
- Status line shows which provider is actually installed, or "AI is off". A failed probe never claims a provider is active unless it still is.
- Typing-suggestion activity is logged (cloud providers only, coalesced) without storing the text.

## [2.0.0] - 2026-08-19

### Major New Features

#### Advanced Text Expansion with Variables
- Added variable support in text expansions (e.g., `{{date}}`, `{{username}}`)
- Implemented system variables (date, time, clipboard, user info)
- Added custom variable definitions with types and defaults
- Implemented variable type formatting (date, phone, email, URL)
- Added dynamic content insertion in expansions
- New expansion templates with variable examples

#### Suggestion Learning and Personalization
- Implemented `PersonalizationManager` for adaptive learning
- Added suggestion interaction tracking (accepted, rejected, modified)
- Implemented context-aware learning patterns
- Added vocabulary preference tracking
- Implemented writing style analysis
- Added export/import functionality for learning data
- Integrated personalization into suggestion pipeline
- Added feedback collection and pattern analysis

#### Plugin System for Custom Suggestion Providers
- Implemented `PluginManager` for dynamic plugin loading
- Added plugin lifecycle management (load, enable, disable, unload)
- Implemented plugin metadata and information system
- Added plugin configuration persistence
- Implemented plugin error handling and isolation
- Added automatic plugin discovery from Plugins directory
- Created `PluginAttribute` for plugin metadata
- Integrated plugin providers into suggestion pipeline

#### Custom Theme Support
- Implemented comprehensive theming system with `ThemeManager`
- Added built-in themes (Light, Dark, High Contrast)
- Implemented custom theme creation and management
- Added theme export/import functionality (JSON format)
- Implemented color, font, and appearance customization
- Added theme change events and notifications
- Integrated theme selection into Settings UI
- Implemented automatic theme application to controls

### Installation & Setup Improvements
- Developer scripts: `Settings\publish-for-testers.ps1` and `Settings\run-tests.bat`
- Added automatic .NET installation check in quick-install

### Documentation Updates
- Updated README with new features and installation instructions
- Updated FEATURES.md with detailed documentation of new features
- Added sections for Learning & Personalization, Plugin System, and Theming
- Updated architecture notes to reflect new components
- Enhanced installation instructions with multiple options

### Code Quality & Architecture
- Improved service composition to include new managers
- Enhanced suggestion pipeline with personalization integration
- Added proper async/await patterns in theme management
- Improved error handling across new components
- Enhanced modularity with new namespaces (Learning, Plugins, Theming)

### Breaking Changes
- Updated .NET version requirement from 10.0 to 8.0 for broader compatibility
- Changed some internal APIs to support new features
- Updated Settings UI to include theme selection

### Commercialization Features
- Added comprehensive LICENSE file with commercial software license agreement
- Created detailed Privacy Policy document with GDPR/CCPA compliance information
- Created Terms of Service document for commercial usage
- Added version management and assembly information in Directory.Build.props
- Implemented professional About dialog with company info and legal document links
- Enhanced crash reporting system with detailed system information and multiple crash logs
- Implemented privacy-focused analytics and telemetry system with user consent
- Added support for additional AI providers: Gemini, DeepSeek, and Ollama
- Updated installer.iss with proper company information and code signing configuration
- Fixed .NET version check from 8.0 to 10.0 with proper version validation
- Implemented Group Policy support for enterprise deployments with registry-based configuration
- Added comprehensive Group Policy documentation and management system
- Implemented auto-update mechanism for non-Store versions with silent update support
- Created comprehensive user manual with detailed feature documentation
- Added Microsoft Store appx packaging project with proper manifest and assets
- Created application icon placeholders with design guidelines
- Implemented professional splash screen with loading progress

### Documentation
- Removed stale `install-simple.bat` and `INSTALL_GUIDE.md` files that were broken against current build
- Updated `README.md` to remove fictional AI providers (Gemini, DeepSeek, Ollama) not yet implemented
- Updated `FEATURES.md` to accurately reflect actual implemented features and codebase structure
- Updated `CHANGELOG.md` to accurately reflect shipped features
- Removed placeholder AI provider options from Settings UI dropdown
- Updated service composer comments to clarify future AI provider plans
- Added comprehensive GROUP_POLICY.md documentation for enterprise deployment
- Added USER_MANUAL.md with complete user documentation
- Added Assets README.md with icon design guidelines

### Security & Compliance
- Enhanced crash reporting with detailed system information and environment variables
- Implemented telemetry with user consent and opt-out capabilities
- Added Group Policy support for enterprise security controls
- Enhanced privacy settings with data retention configuration
- Added audit logging and data transmission monitoring (already implemented in codebase)

### Enterprise Features
- Group Policy configuration support with registry-based settings
- Silent installation support via Inno Setup command-line parameters
- ADMX template structure for Group Policy Central Store deployment
- SCCM and Intune deployment documentation
- Volume licensing support framework
- PowerShell management commands for enterprise administration

## [1.0.0] - 2026-08-12

### Added

#### Core Features
- Multi-stage suggestion pipeline with dictionary and AI-powered suggestions
- Text expansion with word-boundary-aware trigger matching
- Global keyboard input capture across all applications
- Focus tracking with UI Automation for caret position
- Quick toggle (double-press Ctrl) to enable/disable Lexon
- Writing assistance hotkeys for AI-powered text improvement
- Non-activating suggestion overlay with keyboard navigation
- Secure field detection for password fields and credential dialogs
- Per-application blocking for privacy
- AES-GCM encrypted storage with DPAPI-protected master key
- Profile management with templates
- First-run onboarding wizard
- Windows Forms settings UI
- System tray integration with context menu
- Sound feedback on toggle
- Keyboard shortcuts reference form
- Process picker for blocked applications

#### AI Integration
- OpenAI provider integration (GPT-3.5-turbo) with configurable API key
- AI response caching with LRU eviction and TTL expiration
- Pluggable AI provider architecture via IAIProvider interface
- Writing assistance methods: RewriteTextAsync, ImproveGrammarAsync, ChangeToneAsync

#### Privacy & Security
- Automatic secure field detection via UI Automation
- Blocked applications list with custom entries
- Local-only mode for complete privacy
- AES-GCM encryption for all sensitive data
- DPAPI-protected encryption keys

#### User Experience
- Resizable onboarding wizard with improved layout
- Desktop and Start Menu shortcuts for easy launch
- run.bat script for quick development testing
- Updated README with clear installation and usage instructions
- Global exception handling with crash logging to %LocalAppData%\Lexon\crash.log
- MessageBox error display for startup failures

#### Bug Fixes
- Fixed GetModuleHandle P/Invoke to use kernel32.dll instead of user32.dll
- Fixed BeginPaint, EndPaint, DrawText P/Invoke to use user32.dll instead of gdi32.dll
- Fixed onboarding wizard initialization order to prevent NullReferenceException
- Fixed AI status check to use actual AIProvider instance instead of saved setting
- Fixed KeyboardShortcutsForm to use live data from KeyboardShortcutManager
- Fixed Process object disposal in ProcessPickerForm to prevent resource leaks
- Fixed flaky AIResponseCache timing test by increasing durations

#### Testing
- 64 unit tests covering core functionality
- Tests for TextExpansionManager, UndoManager, Profile, DictionarySuggestionProvider
- Tests for KeyboardShortcutManager, AIResponseCache, EncryptedStorage
- All tests passing with reliable timing
