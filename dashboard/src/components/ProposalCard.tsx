import { useState } from 'react';
import { Incident } from '../types';

interface Props {
  incident:        Incident;
  onApprove:       (id: string) => Promise<void>;
  onReject:        (id: string, reason: string) => Promise<void>;
}

export function ProposalCard({ incident, onApprove, onReject }: Props) {
  const [loading,      setLoading]      = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [showReject,   setShowReject]   = useState(false);

  const handleApprove = async () => {
    setLoading(true);
    await onApprove(incident.id);
    setLoading(false);
  };

  const handleReject = async () => {
    if (!rejectReason.trim()) return;
    setLoading(true);
    await onReject(incident.id, rejectReason);
    setLoading(false);
  };

  return (
    <div className="bg-slate-800 rounded-lg border border-amber-600 p-6 animate-pulse-once">
      {/* Header */}
      <div className="flex items-center gap-2 mb-4">
        <span className="text-amber-400 text-lg">⚠️</span>
        <h3 className="text-amber-400 font-bold font-mono text-sm uppercase tracking-wider">
          HITL APPROVAL REQUIRED — High Risk Change
        </h3>
      </div>

      {/* Incident summary */}
      <div className="space-y-2 mb-4 text-xs text-slate-400">
        <p><span className="text-slate-300">Alert:</span> {incident.alertName}</p>
        <p><span className="text-slate-300">Service:</span> {incident.service}</p>
      </div>

      {/* Architect's plan */}
      <div className="mb-4">
        <p className="text-blue-400 font-mono text-xs font-bold mb-1">🏗️ ARCHITECT DIAGNOSIS</p>
        <p className="text-slate-300 text-xs bg-slate-900 p-3 rounded border border-slate-700 leading-relaxed">
          {incident.rootCause}
        </p>
      </div>

      {/* Developer's code fix */}
      <div className="mb-4">
        <p className="text-green-400 font-mono text-xs font-bold mb-1">💻 PROPOSED CODE FIX</p>
        <pre className="text-slate-300 text-xs bg-black p-3 rounded border border-slate-700 overflow-x-auto max-h-48 leading-relaxed">
          {incident.proposedFix}
        </pre>
      </div>

      {/* Action buttons */}
      {!showReject ? (
        <div className="flex gap-3 mt-4">
          <button
            onClick={handleApprove}
            disabled={loading}
            className="flex-1 bg-green-600 hover:bg-green-500 disabled:opacity-50
                       text-white font-bold py-2 px-4 rounded transition-colors"
          >
            {loading ? 'Processing...' : '✅ Approve & Push PR'}
          </button>
          <button
            onClick={() => setShowReject(true)}
            className="flex-1 bg-red-700 hover:bg-red-600
                       text-white font-bold py-2 px-4 rounded transition-colors"
          >
            ❌ Reject Fix
          </button>
        </div>
      ) : (
        <div className="mt-4 space-y-2">
          <input
            type="text"
            value={rejectReason}
            onChange={e => setRejectReason(e.target.value)}
            placeholder="Reason for rejection..."
            className="w-full bg-slate-900 border border-red-700 rounded px-3 py-2 text-sm text-white"
          />
          <div className="flex gap-2">
            <button
              onClick={handleReject}
              disabled={!rejectReason.trim() || loading}
              className="flex-1 bg-red-700 hover:bg-red-600 disabled:opacity-50
                         text-white font-bold py-2 px-4 rounded transition-colors"
            >
              Confirm Rejection
            </button>
            <button
              onClick={() => setShowReject(false)}
              className="bg-slate-600 hover:bg-slate-500 text-white py-2 px-4 rounded"
            >
              Back
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
