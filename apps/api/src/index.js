import http from 'http';

const port = process.env.PORT || 8080;

const server = http.createServer((req, res) => {
  if (req.url === '/health') {
    res.writeHead(200, { 'content-type': 'application/json' });
    res.end(JSON.stringify({ status: 'ok', service: 'api' }));
    return;
  }

  if (req.url === '/v1/jobs' && req.method === 'POST') {
    res.writeHead(501, { 'content-type': 'application/json' });
    res.end(JSON.stringify({ message: 'Not implemented yet' }));
    return;
  }

  res.writeHead(404, { 'content-type': 'application/json' });
  res.end(JSON.stringify({ error: 'Not found' }));
});

server.listen(port, () => {
  console.log(`API listening on :${port}`);
});
