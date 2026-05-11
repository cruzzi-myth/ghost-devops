import { useEffect, useRef } from 'react';
import { MonologueEntry } from '../types';
import { format } from 'date-fns';

interface Props { entries: MonologueEntry[]; }

const AGENT_STYLES: Record<string, { color: string; bg: string; label: string }> = {
  Architect: { color: 'text-blue-400',   bg: 'border-blue-700',   label: '🏗️ ARCHITECT' },
  Developer: { color: 'text-green-400',  bg: 'border-green-700',  label: '💻 DEVELOPER' },
  Validator: { color: 'text-yellow-400', bg: 'border-yellow-700', label: '🛡️ VALIDATOR' },
};

export function InnerMonologue({ entries }: Props) {
  const bottomRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to latest entry
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [entries]);

  if (entries.length === 0) {
    return (
      <div className="bg-slate-900 rounded-lg border border-slate-700 p-6 h-80 flex items-center justify-center">
        <p className="text-slate-500 font-mono text-sm">
          Waiting for incident... Ghost DevOps is watching your cluster. 👻
        </p>
      </div>
    );
  }

  return (
    <div className="bg-slate-900 rounded-lg border border-slate-700 p-4 h-80 overflow-y-auto font-mono text-sm space-y-3">
      {entries.map((entry, i) => {
        const style = AGENT_STYLES[entry.agent] ?? AGENT_STYLES.Architect;
        return (
          <div
            key={i}
            className={`border-l-2 pl-3 py-1 ${style.bg} animate-in slide-in-from-bottom-2`}
          >
            <div className="flex items-center gap-2 mb-1">
              <span className={`font-bold text-xs ${style.color}`}>{style.label}</span>
              <span className="text-slate-600 text-xs">
                {format(new Date(entry.timestamp), 'HH:mm:ss')}
              </span>
            </div>
            <p className="text-slate-300 text-xs leading-relaxed whitespace-pre-wrap">
              {entry.message}
            </p>
          </div>
        );
      })}
      <div ref={bottomRef} />
    </div>
  );
}
