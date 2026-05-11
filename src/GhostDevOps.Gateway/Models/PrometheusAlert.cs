namespace GhostDevOps.Gateway.Models;

// ─────────────────────────────────────────────────────────────────────────────
//  Models the exact JSON payload Prometheus Alertmanager sends on webhook fire.
//  Reference: https://prometheus.io/docs/alerting/latest/configuration/#webhook_config
// ─────────────────────────────────────────────────────────────────────────────

public record PrometheusAlert(
    string                   Version,
    string                   GroupKey,
    string                   Status,          // "firing" | "resolved"
    string                   Receiver,
    Dictionary<string,string> GroupLabels,
    Dictionary<string,string> CommonLabels,
    CommonAnnotationsModel    CommonAnnotations,
    string                   ExternalURL,
    List<AlertInstance>      Alerts
);

public record CommonAnnotationsModel(
    string Summary,
    string Description
);

public record AlertInstance(
    string                   Status,          // "firing" | "resolved"
    Dictionary<string,string> Labels,
    Dictionary<string,string> Annotations,
    DateTime                 StartsAt,
    DateTime                 EndsAt,
    string                   GeneratorURL,
    string                   Fingerprint
);
