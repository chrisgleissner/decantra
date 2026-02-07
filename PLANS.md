# PLANS — Android Permission Audit for Decantra

## 1. Role
- [x] Confirm scope aligns with offline-only Android game and Play Store policy audit expectations.
- [x] Identify all required tools available in environment for AAB/manifest inspection.

## 2. Non-Negotiable Process
- [x] Replace prior plan with this audit plan and keep it updated through execution.
- [x] Represent each meaningful step as a checkbox and only check off after verification.

## 3. Inputs You Must Analyze (Mandatory)
### 3.A Repository sources
- [x] Collect all AndroidManifest.xml sources (main + flavors/buildTypes/plugins).
- [x] Collect Gradle build files and dependency declarations (including version catalogs).
- [x] Collect Unity/Android plugin manifests and any embedded Android library manifests.
- [x] Identify native libraries and Unity plugins likely to inject permissions.

### 3.B Release artifact (source of truth)
- [x] Locate the AAB file containing "0.9.1" in the repository.
- [x] Extract and inspect base manifest and any feature/config splits in the AAB.

## 4. AAB Inspection Requirements (Strict)
- [x] Extract base/manifest/AndroidManifest.xml from the AAB.
- [x] Inspect all module/split manifests for permissions/features/queries.
- [x] Produce canonical lists of uses-permission, uses-permission-sdk-23, uses-feature, queries, and sensitive components.
- [x] Verify absence/presence of AD_ID, INTERNET, ACCESS_NETWORK_STATE, and other high-scrutiny items.
- [x] Cross-check AAB permissions against merged manifest output; if not available, generate merged manifest.

## 5. Dependency and SDK Audit (Mandatory)
- [x] Generate release dependency tree and enumerate SDKs associated with ads/analytics/crash reporting.
- [x] Trace permission sources to Unity defaults, plugins, and transitive dependencies.

## 6. Analysis Requirements
- [x] For each AAB permission, document source, necessity classification, and Play approval risk.
- [x] Flag high-scrutiny items and package visibility queries for special review.

## 7. Remediation Requirements
- [x] For each non-required permission, propose concrete removal steps and risk assessment.
- [x] Provide quick validation steps for any risky change.

## 8. Output Format (Strict)
- [x] Prepare executive summary, canonical table, discrepancy report, removal candidates, remediation plan, and final verdict.

