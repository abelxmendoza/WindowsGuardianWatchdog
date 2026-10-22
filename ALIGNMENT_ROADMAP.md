# Windows Guardian Watchdog - Alignment Roadmap

## Overview
This document tracks the alignment between Windows Guardian Watchdog and MacGuardian Watchdog, ensuring feature parity while respecting OS-specific differences.

## Completed ✅

### 1. Shared Schemas & Design System
- ✅ `shared/schemas/event_spec_v1.json` - Cross-platform event schema
- ✅ `shared/schemas/report_schema.json` - Security report schema
- ✅ `shared/schemas/config_schema.json` - Configuration schema
- ✅ `shared/design/omega_design_system.json` - Design system JSON
- ✅ `shared/ml/feature_vector.schema.json` - ML feature vector schema

### 2. ML Security Engine
- ✅ `Core/ML/FeatureExtractor.cs` - System metrics extraction
- ✅ `Core/ML/BaselineTrainer.cs` - Baseline training and persistence
- ✅ `Core/ML/AnomalyScorer.cs` - Isolation Forest + Z-score analysis
- ✅ `Core/ML/MLSecurityEngine.cs` - Enhanced with new components
- ✅ `Core/ML/MLStatusProvider.cs` - Status provider for UI
- ✅ `Core/IPC/MLCommandHandler.cs` - IPC command handler

### 3. ML Insights UI
- ✅ `Views/MLInsightsView.xaml` - ML insights view
- ✅ `ViewModels/MLInsightsViewModel.cs` - ViewModel with data binding
- ✅ `Models/MLStatusModel.cs` - Data models
- ✅ `Services/MLServiceClient.cs` - Service client
- ✅ `Components/AnomalyScoreCard.xaml` - Reusable component
- ✅ `Components/BaselineStatusBadge.xaml` - Status badge component

### 4. Threat Intelligence
- ✅ `Core/ThreatIntel/IOCChecker.cs` - IOC matching (hash, IP, domain)
- ✅ `Core/ThreatIntel/ThreatFeedService.cs` - Threat feed integration
- ✅ `Core/ThreatIntel/MatchTracker.cs` - Match tracking and statistics

### 5. Compliance & Hardening
- ✅ `Core/Compliance/HardeningAssessment.cs` - 20+ security checks
- ✅ `Core/Compliance/ComplianceScorer.cs` - Compliance scoring
- ✅ `Core/Compliance/ComplianceProfiles/hipaa.json` - HIPAA profile
- ✅ `Core/Compliance/ComplianceProfiles/gdpr.json` - GDPR profile
- ✅ `Core/Compliance/ComplianceProfiles/pci_dss.json` - PCI-DSS profile

## In Progress 🚧

### 6. Alert & Incident Management
- ⏳ `Core/Alerts/OmegaGuardianEngine.cs` - Alert rule engine
- ⏳ `Core/Alerts/AlertRule.cs` - Alert rule definitions
- ⏳ `Core/Alerts/AlertThrottle.cs` - Alert throttling
- ⏳ `Core/Alerts/IncidentManager.cs` - Incident management

### 7. Reports & Scheduling
- ⏳ `Core/Reports/ReportGenerator.cs` - Report generation
- ⏳ `Core/Reports/ScheduledReportService.cs` - Scheduled reports
- ⏳ `Core/Reports/ReportTemplates/omega_report.html` - HTML template
- ⏳ `Core/Reports/ReportMailer.cs` - Email reports

### 8. Persistence Layer
- ⏳ `Persistence/EventStore.cs` - Event storage
- ⏳ `Persistence/MLHistoryRepository.cs` - ML history
- ⏳ `Persistence/IncidentRepository.cs` - Incident storage

### 9. UI Components Library
- ⏳ `Components/SecurityCard.xaml` - Security card component
- ⏳ `Components/RiskBadge.xaml` - Risk badge component
- ⏳ `Components/AlertBanner.xaml` - Alert banner component
- ⏳ `Components/SectionHeader.xaml` - Section header component
- ⏳ `Components/CardContainer.xaml` - Card container component

### 10. Additional Views
- ⏳ `Views/ThreatIntelView.xaml` - Threat intelligence view
- ⏳ `Views/ComplianceView.xaml` - Compliance view
- ⏳ `Views/ReportsView.xaml` - Reports view
- ⏳ `Views/IncidentTimelineView.xaml` - Incident timeline view

## Feature Parity Matrix

| Feature | MacGuardian | Windows Guardian | Status |
|---------|------------|------------------|--------|
| ML Anomaly Detection | ✅ | ✅ | Complete |
| Baseline Learning | ✅ | ✅ | Complete |
| IOC Checking | ✅ | ✅ | Complete |
| Threat Feeds | ✅ | ✅ | Complete |
| Hardening Assessment | ✅ | ✅ | Complete |
| Compliance Scoring | ✅ | ✅ | Complete |
| Alert Engine | ✅ | ⏳ | In Progress |
| Scheduled Reports | ✅ | ⏳ | In Progress |
| Incident Management | ✅ | ⏳ | In Progress |
| Event Persistence | ✅ | ✅ | Notices timeline (`Core/Incidents/EventTimeline.cs`) |
| Defenses switched off / tripwires | ✅ (Linux) | ✅ | `Core/Sensors/ChangeWatch.cs` |
| Reversible quarantine | ✅ | ✅ | `Core/Quarantine/QuarantineVault.cs` |
| Network exposure | ✅ (Linux) | ✅ | `Core/Network/ExposureInventory.cs` |
| Self-check | ✅ | ✅ | `Core/SelfCheck/AppSelfCheck.cs` |
| Reports view | ✅ | ✅ | `Views/ReportsView.xaml` |

## OS-Specific Differences

### Windows-Specific Features
- Windows Service for background monitoring
- WMI for system information
- Windows Defender integration
- BitLocker encryption checks
- Windows Event Log integration
- PowerShell integration

### Mac-Specific Features (MacGuardian)
- macOS security framework
- Gatekeeper checks
- SIP (System Integrity Protection) checks
- macOS-specific hardening

## Next Steps

1. **Complete Alert Engine** - Implement OmegaGuardianEngine with rule evaluation
2. **Implement Report Generation** - HTML templates matching MacGuardian style
3. **Add Persistence Layer** - EventStore and repositories
4. **Build Remaining UI Views** - ThreatIntel, Compliance, Reports
5. **Create Component Library** - Reusable WinUI components
6. **Cross-Platform Tests** - Ensure schema compatibility

## Architecture Notes

### Shared Soul
- Both platforms use the same event schema
- Same ML feature extraction approach
- Identical compliance profiles
- Matching design system (Omega Technologies Dark)

### Platform Adaptation
- Windows uses WMI, Mac uses system_profiler
- Windows uses Windows Service, Mac uses launchd
- Windows uses .NET, Mac uses Swift/Objective-C
- Different IPC mechanisms (Named Pipes vs Unix Sockets)

## Success Criteria

- [x] ML engine functional and visible in UI
- [x] Threat intelligence IOC checking works
- [x] Compliance assessment runs successfully
- [ ] Alert engine processes events correctly
- [ ] Reports generate with Omega branding
- [ ] All views accessible and functional
- [ ] Cross-platform event schema validated
