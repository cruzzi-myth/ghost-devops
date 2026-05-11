export interface Incident {
  id:             string;
  fingerprint:    string;
  alertName:      string;
  service:        string;
  summary:        string;
  status:         IncidentStatus;
  detectedAt:     string;
  resolvedAt?:    string;
  rootCause?:     string;
  proposedFix?:   string;
  pullRequestUrl?:string;
  humanApproved:  boolean;
  isHighRisk:     boolean;
  mttrMinutes?:   number;
}

export type IncidentStatus =
  | 'Detected'
  | 'Analyzing'
  | 'FixProposed'
  | 'AwaitingApproval'
  | 'PRCreated'
  | 'Verified Fixed'
  | 'Failed'
  | 'Rejected - Safety Failure';

export interface MonologueEntry {
  agent:     'Architect' | 'Developer' | 'Validator';
  message:   string;
  timestamp: string;
}

export interface DoraMetrics {
  deploymentFrequency: number;
  avgMttrMinutes:      number;
  changeFailureRate:   number;
  totalIncidents:      number;
  resolvedToday:       number;
}
