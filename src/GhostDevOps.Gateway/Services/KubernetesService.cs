using k8s;
using k8s.Models;

namespace GhostDevOps.Gateway.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  KubernetesService — Week 4: Self-Healing Kubernetes.
//
//  Once a PR is merged and CI passes, this service triggers a rolling restart
//  of the affected pod — completing the self-healing loop.
//
//  Uses the official kubernetes-client/csharp library.
// ─────────────────────────────────────────────────────────────────────────────

public class KubernetesService(ILogger<KubernetesService> logger)
{
    // Build client from in-cluster service account or local kubeconfig
    private static readonly Kubernetes _k8s = new(
        KubernetesClientConfiguration.IsInCluster()
            ? KubernetesClientConfiguration.InClusterConfig()
            : KubernetesClientConfiguration.BuildConfigFromConfigFile()
    );

    // ── Trigger rolling restart by patching the deployment annotation ─────────
    public async Task<bool> TriggerRollingRestartAsync(
        string @namespace,
        string deploymentName,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Triggering rolling restart for {Deployment} in {Namespace}",
            deploymentName, @namespace);

        try
        {
            // Kubernetes rolling restart = patch spec.template.metadata.annotations
            // with a timestamp. The deployment controller sees the change and
            // replaces pods one by one (zero downtime).
            var patch = new V1Deployment
            {
                Spec = new V1DeploymentSpec
                {
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Annotations = new Dictionary<string, string>
                            {
                                ["kubectl.kubernetes.io/restartedAt"] = DateTime.UtcNow.ToString("o"),
                                ["ghost-devops/auto-healed-at"]       = DateTime.UtcNow.ToString("o")
                            }
                        }
                    }
                }
            };

            await _k8s.PatchNamespacedDeploymentAsync(
                new V1Patch(patch, V1Patch.PatchType.MergePatch),
                deploymentName,
                @namespace,
                cancellationToken: ct
            );

            logger.LogInformation(
                "Rolling restart triggered successfully for {Deployment}", deploymentName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger rolling restart for {Deployment}", deploymentName);
            return false;
        }
    }

    // ── Wait for the deployment to be fully ready after restart ──────────────
    public async Task<bool> WaitForRolloutAsync(
        string @namespace,
        string deploymentName,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var deployment = await _k8s.ReadNamespacedDeploymentAsync(
                deploymentName, @namespace, cancellationToken: ct);

            var desired   = deployment.Spec.Replicas ?? 1;
            var ready     = deployment.Status.ReadyReplicas ?? 0;
            var updated   = deployment.Status.UpdatedReplicas ?? 0;

            if (ready >= desired && updated >= desired)
            {
                logger.LogInformation(
                    "Deployment {Deployment} is fully ready ({Ready}/{Desired})",
                    deploymentName, ready, desired);
                return true;
            }

            logger.LogDebug(
                "Waiting for rollout... Ready: {Ready}/{Desired}", ready, desired);

            await Task.Delay(5000, ct);
        }

        logger.LogWarning("Rollout timeout reached for {Deployment}", deploymentName);
        return false;
    }
}
