export function App() {
  return (
    <main style={{ fontFamily: 'Inter, system-ui, sans-serif', padding: 24 }}>
      <h1>MusicTransfer</h1>
      <p>Spotify → YouTube Music migration app scaffold (React + TypeScript + .NET 8).</p>

      <section>
        <h2>MVP steps</h2>
        <ol>
          <li>Connect Spotify</li>
          <li>Connect YouTube/Google</li>
          <li>Select playlists</li>
          <li>Start migration</li>
          <li>Review ambiguous matches</li>
          <li>View migration report</li>
        </ol>
      </section>
    </main>
  );
}
