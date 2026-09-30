# Lexon Privacy Policy

**Last Updated: September 30, 2026**

## Introduction

Lexon ("we," "our," or "us") is committed to protecting your privacy. This Privacy Policy explains how we collect, use, and protect your information when you use the Lexon software application ("Software").

By using Lexon, you agree to the collection and use of information in accordance with this policy.

## Information We Collect

### 1. Application Data
Lexon may collect the following types of data to provide its services:

- **Typing Context**: Text context for generating suggestions (current word, previous words)
- **Application Information**: Name of the application where suggestions are requested
- **Usage Patterns**: How you interact with suggestions (accepted/dismissed suggestions)
- **Settings and Preferences**: Your configured settings and preferences
- **Device Information**: Basic device information for compatibility

### 2. Cloud AI Service Data
AI features are **opt-in** and controlled in Settings. Existing installs keep your saved provider and key, but **AI-while-typing is off after this update** until you turn it on.

When a cloud provider is connected, Lexon may send:

- **Typing context** (previous words, current word, following words) — only if **Send words around the cursor to cloud AI while I type** is on. Off by default.
- **Selected text for a rewrite**, plus the rewrite instruction and optional tone or writing-style hints (not the rest of the document) — only if **Allow AI rewrites of selected text** is on (default on) and you ask via the Aa chip or shortcut.
- **Selected text before you pick a rewrite** — only if **Prepare a rewrite as soon as I select text** is on. Off by default.

Requests go to the provider you chose (OpenAI, Gemini, DeepSeek, or Ollama), not through Lexon servers. **Local-only mode** stops all AI, including Ollama on this PC, immediately without deleting your key.

Ollama bound to this machine is treated as local. An Ollama host that is not loopback is treated like a cloud send for the typing toggle.

Lexon does not send text from password fields, from fields it cannot confirm are not password fields, or from apps you have blocked.

### 3. Analytics and Crash Data
Lexon does **not** send usage statistics, telemetry, or crash reports to Lexon or to a third-party analytics vendor.

Diagnostic files (for example `%LocalAppData%\Lexon\placement.log`) may be written **on this computer** while investigating a bug. They are not uploaded. A Group Policy flag named EnableCrashReporting exists for enterprise builds; the shipping app does not transmit crash reports.

## How We Use Your Information

### 1. Service Provision
- Generate intelligent text suggestions
- Provide writing assistance features
- Maintain application functionality

### 2. Service Improvement
- Analyze usage patterns to improve suggestion quality
- Identify and fix bugs and performance issues
- Develop new features and improvements

### 3. Security and Compliance
- Detect and prevent security threats
- Ensure compliance with legal obligations
- Protect against fraud and abuse

## Data Storage and Security

### Local Storage
- All sensitive data is encrypted using AES-GCM encryption
- Encryption keys are protected by Windows DPAPI
- Data is stored locally on your device in `%LocalAppData%\Lexon`

### Cloud Services
- AI requests are sent directly to the provider you configured (OpenAI, Gemini, DeepSeek, or Ollama), not through Lexon servers
- Your API key is stored locally (DPAPI) and never transmitted to Lexon
- We do not have access to your provider API key or that provider's usage data

### Data Retention
- Learned vocabulary, settings, and the local cloud-activity log (provider/app/action only, no typed text) stay on this device until you clear them or uninstall
- Lexon does not operate a 30-day crash-log or 12-month analytics store
- Your chosen AI provider's retention rules apply to any text you send them

## Data Sharing and Disclosure

We do not sell, rent, or share your personal information with third parties for marketing purposes. We may share data in the following circumstances:

### 1. Third-Party AI Services
- When you enable a cloud or remote AI feature, text you submit is sent to that provider
- That provider's privacy policy governs their use of the data
- You can use local-only mode, or never add an API key, to keep assistance on this PC

### 2. Service Providers
- Lexon does not hire processors to collect your typing data
- Optional app updates are downloaded from the update source configured in the app (Velopack)

### 3. Legal Requirements
- We may disclose information if required by law or to protect our rights
- This includes responding to legal processes, court orders, or government requests

### 4. Business Transfers
- In the event of a merger, acquisition, or sale of assets, this policy would be updated before any change in who operates Lexon

## Your Privacy Rights

You have the following rights regarding your personal information:

### 1. Access and Review
- View the data stored locally by Lexon
- Access your settings and preferences
- Review audit logs (if enabled)

### 2. Deletion
- Delete all local data through the application settings
- Uninstall the application to remove all data
- Clear specific data types (learned patterns, history)

### 3. Control
- Enable/disable cloud AI while typing, rewrites, and prefetch independently
- Enable local-only mode to stop all AI immediately (saved keys are kept)
- Block specific applications from receiving suggestions

### 4. Opt-Out
- Do not enable cloud AI while typing, rewrites, or prefetch
- Enable local-only mode to stop all AI immediately
- Block specific applications; password fields are skipped automatically

## Children's Privacy

Lexon is not intended for use by children under the age of 13. We do not knowingly collect personal information from children under 13. If we become aware that we have collected such information, we will take steps to delete it.

## International Data Transfers

Your data may be transferred to and processed in countries other than your country of residence. When such transfers occur, we ensure appropriate safeguards are in place to protect your information in accordance with this Privacy Policy.

## Changes to This Privacy Policy

We may update this Privacy Policy from time to time. We will notify you of any material changes by:

- Posting the new policy in the application
- Sending a notification if you have enabled updates
- Updating the "Last Updated" date

## Contact Us

If you have questions about this Privacy Policy or our data practices, use the in-app About box or the GitHub repository for this project. There is no separate Lexon support email or marketing website operated by this build.

## Legal Basis for Processing (GDPR)

If you are located in the European Economic Area, our legal basis for processing your personal information includes:

- **Contractual Necessity**: To provide the services you've requested
- **Legitimate Interests**: To improve our services and prevent fraud
- **Consent**: When you explicitly consent to data collection

## Data Subject Rights (GDPR)

Under GDPR, you have the right to:

- Access your personal data
- Request correction of inaccurate data
- Request deletion of your data
- Object to processing of your data
- Request data portability
- Withdraw consent at any time

## California Consumer Privacy Act (CCPA)

If you are a California resident, you have the right to:

- Know what personal information we collect
- Know if we sell your personal information
- Request deletion of your personal information
- Opt-out of the sale of personal information
- Non-discrimination for exercising your privacy rights

---

This Privacy Policy is effective as of September 30, 2026 and will remain in effect until modified.