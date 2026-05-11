import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { Incident, MonologueEntry } from '../types';

const HUB_URL = import.meta.env.VITE_GATEWAY_URL
  ? `${import.meta.env.VITE_GATEWAY_URL}/hubs/incidents`
  : 'http://localhost:5000/hubs/incidents';

export function useSignalR() {
  const connRef = useRef<signalR.HubConnection | null>(null);
  const [connected, setConnected]     = useState(false);
  const [incidents,  setIncidents]    = useState<Incident[]>([]);
  const [monologue,  setMonologue]    = useState<MonologueEntry[]>([]);
  const [pendingApproval, setPending] = useState<Incident | null>(null);

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // ── Incoming events from the Gateway ─────────────────────────────────────
    conn.on('IncidentDetected', (incident: Incident) => {
      setIncidents(prev => [incident, ...prev]);
    });

    conn.on('AnalysisStarted', (incidentId: string) => {
      setIncidents(prev => prev.map(i =>
        i.id === incidentId ? { ...i, status: 'Analyzing' } : i
      ));
    });

    conn.on('NewMonologueEntry', (entry: MonologueEntry) => {
      setMonologue(prev => [...prev, entry]);
    });

    conn.on('ProposalReady', (incident: Incident) => {
      setIncidents(prev => prev.map(i => i.id === incident.id ? incident : i));
      if (incident.isHighRisk) setPending(incident);
    });

    conn.on('PRCreated', (incident: Incident) => {
      setIncidents(prev => prev.map(i => i.id === incident.id ? incident : i));
      setPending(null);
    });

    conn.on('IncidentResolved', (incidentId: string) => {
      setIncidents(prev => prev.map(i =>
        i.id === incidentId
          ? { ...i, status: 'Verified Fixed', resolvedAt: new Date().toISOString() }
          : i
      ));
    });

    // ── Start connection ──────────────────────────────────────────────────────
    conn.start()
      .then(() => { setConnected(true); console.log('SignalR connected'); })
      .catch(e => console.error('SignalR connection failed:', e));

    conn.onclose(() => setConnected(false));
    connRef.current = conn;

    return () => { conn.stop(); };
  }, []);

  // ── HITL actions ──────────────────────────────────────────────────────────
  const approveProposal = async (incidentId: string) => {
    await connRef.current?.invoke('ApproveProposal', incidentId);
    setPending(null);
  };

  const rejectProposal = async (incidentId: string, reason: string) => {
    await connRef.current?.invoke('RejectProposal', incidentId, reason);
    setPending(null);
  };

  return {
    connected,
    incidents,
    monologue,
    pendingApproval,
    approveProposal,
    rejectProposal,
  };
}
