import { DoraMetrics } from '../types';

interface Props { metrics: DoraMetrics; }

export function DoraStats({ metrics }: Props) {
  const cards = [
    {
      label:   'Deployment Frequency',
      value:   `${metrics.deploymentFrequency} / day`,
      color:   'text-blue-400',
      icon:    '🚀',
      tooltip: 'How many Ghost PRs were merged today',
    },
    {
      label:   'MTTR (Self-Healing)',
      value:   `${metrics.avgMttrMinutes.toFixed(1)} min`,
      color:   'text-green-400',
      icon:    '⚡',
      tooltip: 'Mean time from alert → alert resolved',
    },
    {
      label:   'Change Failure Rate',
      value:   `${metrics.changeFailureRate.toFixed(1)}%`,
      color:   metrics.changeFailureRate < 15 ? 'text-green-400' : 'text-red-400',
      icon:    '🛡️',
      tooltip: '% of Ghost fixes rejected by safety or human',
    },
    {
      label:   'Resolved Today',
      value:   `${metrics.resolvedToday} / ${metrics.totalIncidents}`,
      color:   'text-purple-400',
      icon:    '✅',
      tooltip: 'Incidents auto-resolved vs total detected',
    },
  ];

  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
      {cards.map(card => (
        <div
          key={card.label}
          className="p-4 bg-slate-800 rounded-lg border border-slate-700 hover:border-slate-500 transition-colors"
          title={card.tooltip}
        >
          <div className="flex items-center gap-2 mb-1">
            <span className="text-lg">{card.icon}</span>
            <p className="text-slate-400 text-xs uppercase tracking-wider">{card.label}</p>
          </div>
          <p className={`text-2xl font-bold font-mono ${card.color}`}>{card.value}</p>
        </div>
      ))}
    </div>
  );
}
