import { useState, useEffect } from 'react';
import { useSignalR } from './hooks/useSignalR';
import { DoraStats } from './components/DoraStats';
import { InnerMonologue } from './components/InnerMonologue';
import { ProposalCard } from './components/ProposalCard';
import { Incident, DoraMetrics } from './types';
import { formatDistanceToNow } from 'date-fns';

const STATUS_COLORS: Record<string, string> = {
  'Detected':              'bg-yellow-900 text-yellow-300',
  'Analyzing':             'bg-blue-900  text-blue-300 animate-pulse',
  'FixProposed':           'bg-purple-900 text-purple-300',
  'AwaitingApproval':      'bg-amber-900  text-amber-300',
  'PRCreated':             'bg-cyan-900   text-cyan-300',
  'Verified Fixed':        'bg-green-900  text-green-300',
  'Failed':                'bg-red-900    text-red-300',
  'Rejected - Safety Failure': 'bg-red-900 text-red-300',
};

export default function App() {
  const { connected, incidents, monologue, pendingApproval, approveProposal, rejectProposal }
    = useSignalR();

  const [dora, setDora] = useState<DoraMetrics>({
    deploymentFrequency: 0,
    avgMttrMinutes:      0,
    changeFailureRate:   0,
    totalIncidents:      0,
    resolvedToday:       0,
  });

  // Derive DORA metrics from incident list
  useEffect(() => {
    const today  = new Date().toDateString();
    const resolved = incidents.filter(i => i.resolvedAt);

    setDora({
      deploymentFrequency: incidents.filter(
        i => i.status === 'PRCreated' && new Date(i.detectedAt).toDateString() === today
      ).length,
      avgMttrMinutes: resolved.length > 0
        ? resolved.reduce((s, i) => s + (i.mttrMinutes ?? 0), 0) / resolved.length
        : 0,
      changeFailureRate: incidents.length > 0
        ? incidents.filter(i => i.status.includes('Rejected')).length / incidents.length * 100
        : 0,
      totalIncidents:  incidents.length,
      resolvedToday:   resolved.filter(i => new Date(i.resolvedAt!).toDateString() === today).length,
    });
  }, [incidents]);

  return (
    <div className="min-h-screen bg-slate-950 text-white p-6 font-sans">

      {/* ── Header ───────────────────────────────────────────────────────── */}
      <header className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-2xl font-bold font-mono tracking-tight">
            <span className="text-slate-500">👻 </span>
            <span className="text-white">GHOST</span>
            <span className="text-blue-400">_DEVOPS</span>
            <span className="text-slate-500">::COMMAND_CENTER</span>
          </h1>
          <p className="text-slate-500 text-sm mt-1">
            Autonomous agentic infrastructure — observe → reason → act → verify
          </p>
        </div>
        <div className="flex items-center gap-2">
          <div className={`w-2 h-2 rounded-full ${connected ? 'bg-green-400 animate-pulse' : 'bg-red-400'}`} />
          <span className="text-xs text-slate-400 font-mono">
            {connected ? 'LIVE' : 'DISCONNECTED'}
          </span>
        </div>
      </header>

      {/* ── DORA Metrics ─────────────────────────────────────────────────── */}
      <DoraStats metrics={dora} />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">

        {/* ── Left: Active Incidents ──────────────────────────────────────── */}
        <section>
          <h2 className="text-sm font-mono text-slate-400 uppercase tracking-wider mb-3">
            📋 Incident Log
          </h2>

          {incidents.length === 0 ? (
            <div className="bg-slate-900 rounded-lg border border-slate-700 p-6 text-center">
              <p className="text-slate-500 text-sm">No incidents detected. Cluster is healthy. 🟢</p>
            </div>
          ) : (
            <div className="space-y-3 max-h-[60vh] overflow-y-auto pr-1">
              {incidents.map(incident => (
                <IncidentRow key={incident.id} incident={incident} />
              ))}
            </div>
          )}
        </section>

        {/* ── Right: Inner Monologue + HITL ───────────────────────────────── */}
        <section className="space-y-4">
          <div>
            <h2 className="text-sm font-mono text-slate-400 uppercase tracking-wider mb-3">
              🧠 Agent Inner Monologue
            </h2>
            <InnerMonologue entries={monologue} />
          </div>

          {pendingApproval && (
            <ProposalCard
              incident={pendingApproval}
              onApprove={approveProposal}
              onReject={rejectProposal}
            />
          )}
        </section>
      </div>
    </div>
  );
}

// ── Incident row component ────────────────────────────────────────────────────
function IncidentRow({ incident }: { incident: Incident }) {
  const [expanded, setExpanded] = useState(false);
  const statusClass = STATUS_COLORS[incident.status] ?? 'bg-slate-800 text-slate-300';

  return (
    <div
      className="bg-slate-900 rounded-lg border border-slate-700 hover:border-slate-500
                 transition-colors cursor-pointer"
      onClick={() => setExpanded(!expanded)}
    >
      <div className="flex items-center justify-between p-3">
        <div className="flex items-center gap-2 min-w-0">
          <span className={`px-2 py-0.5 rounded text-xs font-mono font-bold shrink-0 ${statusClass}`}>
            {incident.status}
          </span>
          <span className="text-sm text-white truncate">{incident.alertName}</span>
          {incident.isHighRisk && (
            <span className="text-amber-400 text-xs shrink-0">⚠️ HIGH RISK</span>
          )}
        </div>
        <span className="text-slate-500 text-xs shrink-0 ml-2">
          {formatDistanceToNow(new Date(incident.detectedAt), { addSuffix: true })}
        </span>
      </div>

      {expanded && (
        <div className="px-3 pb-3 space-y-2 border-t border-slate-800 pt-3">
          <p className="text-slate-400 text-xs">{incident.summary}</p>
          {incident.rootCause && (
            <p className="text-blue-300 text-xs">
              <span className="font-bold">Architect:</span> {incident.rootCause}
            </p>
          )}
          {incident.pullRequestUrl && (
            <a
              href={incident.pullRequestUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="text-cyan-400 text-xs hover:underline block"
              onClick={e => e.stopPropagation()}
            >
              🔗 View Pull Request →
            </a>
          )}
          {incident.mttrMinutes && (
            <p className="text-green-400 text-xs">
              ⚡ Resolved in {incident.mttrMinutes.toFixed(1)} minutes
            </p>
          )}
        </div>
      )}
    </div>
  );
}
