import { useMemo, useState } from 'react';

type OAuthStartResponse = {
  provider: string;
  state: string;
  authorizationUrl: string;
};

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080';

export function App() {
  const [spotifyAuth, setSpotifyAuth] = useState<OAuthStartResponse | null>(null);
  const [googleAuth, setGoogleAuth] = useState<OAuthStartResponse | null>(null);
  const [playlistIds, setPlaylistIds] = useState('');
  const [jobResult, setJobResult] = useState<any>(null);
  const [loading, setLoading] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const parsedPlaylistIds = useMemo(
    () => playlistIds.split(',').map((p) => p.trim()).filter(Boolean),
    [playlistIds]
  );

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

  return (
    <main style={{ fontFamily: 'Inter, system-ui, sans-serif', padding: 24, maxWidth: 900, margin: '0 auto' }}>
      <h1>MusicTransfer</h1>
      <p>Milestone 1 scaffold: OAuth connect flow + migration job creation.</p>

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

        {spotifyAuth && (
          <p>
            Spotify auth URL:{' '}
            <a href={spotifyAuth.authorizationUrl} target="_blank" rel="noreferrer">
              Open Spotify consent
            </a>
          </p>
        )}

        {googleAuth && (
          <p>
            Google auth URL:{' '}
            <a href={googleAuth.authorizationUrl} target="_blank" rel="noreferrer">
              Open Google consent
            </a>
          </p>
        )}
      </section>

      <section style={{ marginTop: 24 }}>
        <h2>2) Create migration job</h2>
        <p>Paste Spotify playlist IDs (comma-separated):</p>
        <textarea
          value={playlistIds}
          onChange={(e) => setPlaylistIds(e.target.value)}
          rows={3}
          style={{ width: '100%' }}
          placeholder="37i9dQZF1DXcBWIGoYBM5M, 37i9dQZF1DX4WYpdgoIcn6"
        />
        <div style={{ marginTop: 8 }}>
          <button onClick={createMigrationJob} disabled={loading === 'job' || parsedPlaylistIds.length === 0}>
            {loading === 'job' ? 'Creating…' : 'Start migration job'}
          </button>
        </div>
      </section>

      {jobResult && (
        <section style={{ marginTop: 24 }}>
          <h2>3) Job created</h2>
          <pre style={{ background: '#111', color: '#eee', padding: 12, borderRadius: 8, overflowX: 'auto' }}>
            {JSON.stringify(jobResult, null, 2)}
          </pre>
        </section>
      )}

      {error && (
        <p style={{ marginTop: 16, color: 'crimson' }}>
          {error}
        </p>
      )}
    </main>
  );
}
