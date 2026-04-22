import { useMemo, useState } from 'react';

type OAuthStartResponse = {
  provider: string;
  state: string;
  authorizationUrl: string;
};

type Candidate = {
  youTubeTrackId: string;
  title: string;
  artist: string;
  durationMs: number;
  isrc?: string | null;
};

type ReviewItem = {
  spotifyTrackId: string;
  source: {
    title: string;
    artist: string;
    durationMs: number;
    spotifyTrackId: string;
  };
  confidence: number;
  candidates: Candidate[];
};

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080';

export function App() {
  const [spotifyAuth, setSpotifyAuth] = useState<OAuthStartResponse | null>(null);
  const [googleAuth, setGoogleAuth] = useState<OAuthStartResponse | null>(null);
  const [playlistIds, setPlaylistIds] = useState('');
  const [jobResult, setJobResult] = useState<any>(null);
  const [report, setReport] = useState<any>(null);
  const [reviewItems, setReviewItems] = useState<ReviewItem[]>([]);
  const [decisionMap, setDecisionMap] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const parsedPlaylistIds = useMemo(
    () => playlistIds.split(',').map((p) => p.trim()).filter(Boolean),
    [playlistIds]
  );

  const jobId = jobResult?.id as string | undefined;

  async function startOAuth(provider: 'spotify' | 'google') {
    try {
      setLoading(provider);
      setError(null);
      const res = await fetch(`${API_BASE}/v1/auth/${provider}/start`);
      if (!res.ok) throw new Error(`Failed to start ${provider} auth`);
      const data = (await res.json()) as OAuthStartResponse;
      if (provider === 'spotify') setSpotifyAuth(data);
      else setGoogleAuth(data);
    } catch (e: any) {
      setError(e?.message ?? 'Failed to start auth');
    } finally {
      setLoading(null);
    }
  }

  async function createMigrationJob() {
    try {
      setLoading('job');
      setError(null);
      setReport(null);
      setReviewItems([]);
      const res = await fetch(`${API_BASE}/v1/jobs`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ playlistIds: parsedPlaylistIds }),
      });
      if (!res.ok) throw new Error('Failed to create migration job');
      setJobResult(await res.json());
    } catch (e: any) {
      setError(e?.message ?? 'Failed to create migration job');
    } finally {
      setLoading(null);
    }
  }

  async function runMigration() {
    if (!jobId) return;
    try {
      setLoading('run');
      setError(null);
      const res = await fetch(`${API_BASE}/v1/jobs/${jobId}/run`, { method: 'POST' });
      if (!res.ok) throw new Error('Failed to run migration');
      setReport(await res.json());
      await loadReviewItems();
    } catch (e: any) {
      setError(e?.message ?? 'Failed to run migration');
    } finally {
      setLoading(null);
    }
  }

  async function loadReviewItems() {
    if (!jobId) return;
    const res = await fetch(`${API_BASE}/v1/jobs/${jobId}/review-items`);
    if (res.ok) {
      const data = (await res.json()) as ReviewItem[];
      setReviewItems(data);
    }
  }

  async function submitReview() {
    if (!jobId) return;
    try {
      setLoading('review');
      setError(null);

      const decisions = reviewItems.map((item) => {
        const selected = decisionMap[item.spotifyTrackId] ?? '';
        if (selected === 'skip' || !selected) {
          return { spotifyTrackId: item.spotifyTrackId, skip: true, youTubeTrackId: null };
        }
        return { spotifyTrackId: item.spotifyTrackId, skip: false, youTubeTrackId: selected };
      });

      const res = await fetch(`${API_BASE}/v1/jobs/${jobId}/review-decisions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ decisions }),
      });

      if (!res.ok) throw new Error('Failed to submit review decisions');

      setReport(await res.json());
      await loadReviewItems();
    } catch (e: any) {
      setError(e?.message ?? 'Failed to submit review');
    } finally {
      setLoading(null);
    }
  }

  return (
    <main style={{ fontFamily: 'Inter, system-ui, sans-serif', padding: 24, maxWidth: 980, margin: '0 auto' }}>
      <h1>MusicTransfer</h1>
      <p>Spotify → YouTube Music migration (Milestones 1–3 scaffold).</p>

      <section style={{ marginTop: 24 }}>
        <h2>1) Connect accounts</h2>
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <button onClick={() => startOAuth('spotify')} disabled={loading === 'spotify'}>
            {loading === 'spotify' ? 'Loading…' : 'Connect Spotify'}
          </button>
          <button onClick={() => startOAuth('google')} disabled={loading === 'google'}>
            {loading === 'google' ? 'Loading…' : 'Connect YouTube/Google'}
          </button>
        </div>

        {spotifyAuth && <p>Spotify auth URL: <a href={spotifyAuth.authorizationUrl} target="_blank" rel="noreferrer">Open consent</a></p>}
        {googleAuth && <p>Google auth URL: <a href={googleAuth.authorizationUrl} target="_blank" rel="noreferrer">Open consent</a></p>}
      </section>

      <section style={{ marginTop: 24 }}>
        <h2>2) Create + run migration job</h2>
        <p>Spotify playlist IDs (comma-separated):</p>
        <textarea
          value={playlistIds}
          onChange={(e) => setPlaylistIds(e.target.value)}
          rows={3}
          style={{ width: '100%' }}
          placeholder="37i9dQZF1DXcBWIGoYBM5M, 37i9dQZF1DX4WYpdgoIcn6"
        />

        <div style={{ marginTop: 10, display: 'flex', gap: 8 }}>
          <button onClick={createMigrationJob} disabled={loading === 'job' || parsedPlaylistIds.length === 0}>
            {loading === 'job' ? 'Creating…' : 'Create job'}
          </button>
          <button onClick={runMigration} disabled={!jobId || loading === 'run'}>
            {loading === 'run' ? 'Running…' : 'Run migration'}
          </button>
        </div>
      </section>

      {jobResult && (
        <section style={{ marginTop: 24 }}>
          <h2>Job</h2>
          <pre style={{ background: '#111', color: '#eee', padding: 12, borderRadius: 8, overflowX: 'auto' }}>
            {JSON.stringify(jobResult, null, 2)}
          </pre>
        </section>
      )}

      {report && (
        <section style={{ marginTop: 24 }}>
          <h2>Report</h2>
          <div style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
            <a href={`${API_BASE}/v1/jobs/${jobId}/report/export.json`} target="_blank" rel="noreferrer">Download JSON</a>
            <a href={`${API_BASE}/v1/jobs/${jobId}/report/export.csv`} target="_blank" rel="noreferrer">Download CSV</a>
          </div>
          <pre style={{ background: '#111', color: '#eee', padding: 12, borderRadius: 8, overflowX: 'auto' }}>
            {JSON.stringify(report, null, 2)}
          </pre>
        </section>
      )}

      {reviewItems.length > 0 && (
        <section style={{ marginTop: 24 }}>
          <h2>3) Review ambiguous matches ({reviewItems.length})</h2>
          {reviewItems.map((item) => (
            <div key={item.spotifyTrackId} style={{ border: '1px solid #8884', borderRadius: 10, padding: 12, marginBottom: 10 }}>
              <strong>{item.source.title}</strong> — {item.source.artist} (confidence {item.confidence.toFixed(2)})
              <div style={{ marginTop: 8 }}>
                <select
                  style={{ width: '100%' }}
                  value={decisionMap[item.spotifyTrackId] ?? 'skip'}
                  onChange={(e) => setDecisionMap((prev) => ({ ...prev, [item.spotifyTrackId]: e.target.value }))}
                >
                  <option value="skip">Skip this track</option>
                  {item.candidates.map((c) => (
                    <option key={c.youTubeTrackId} value={c.youTubeTrackId}>
                      {c.title} — {c.artist} ({Math.round(c.durationMs / 1000)}s)
                    </option>
                  ))}
                </select>
              </div>
            </div>
          ))}

          <button onClick={submitReview} disabled={loading === 'review'}>
            {loading === 'review' ? 'Submitting…' : 'Submit review decisions'}
          </button>
        </section>
      )}

      {error && <p style={{ marginTop: 16, color: 'crimson' }}>{error}</p>}
    </main>
  );
}
