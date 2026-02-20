import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { URL } from 'node:url';

const port = Number(process.env.PLAYWRIGHT_PORT ?? '4173');
const host = process.env.PLAYWRIGHT_HOST ?? '127.0.0.1';
const webRoot = path.resolve(process.cwd(), process.env.PLAYWRIGHT_WEB_ROOT ?? '../../Builds/WebGL');

const mimeByExt = new Map([
  ['.html', 'text/html; charset=utf-8'],
  ['.js', 'application/javascript; charset=utf-8'],
  ['.css', 'text/css; charset=utf-8'],
  ['.json', 'application/json; charset=utf-8'],
  ['.wasm', 'application/wasm'],
  ['.data', 'application/octet-stream'],
  ['.symbols', 'application/octet-stream'],
  ['.png', 'image/png'],
  ['.jpg', 'image/jpeg'],
  ['.jpeg', 'image/jpeg'],
  ['.svg', 'image/svg+xml'],
  ['.ico', 'image/x-icon'],
  ['.txt', 'text/plain; charset=utf-8']
]);

const sendNotFound = (response) => {
  response.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
  response.end('Not Found');
};

const sendError = (response, error) => {
  response.writeHead(500, { 'Content-Type': 'text/plain; charset=utf-8' });
  response.end(`Internal Server Error: ${error.message}`);
};

const getMimeType = (filePath) => {
  const ext = path.extname(filePath).toLowerCase();
  return mimeByExt.get(ext) ?? 'application/octet-stream';
};

const getHeadersForPath = (filePath) => {
  const headers = {
    'Cache-Control': 'no-cache'
  };

  const isGzip = filePath.endsWith('.gz');
  const filePathForMime = isGzip ? filePath.slice(0, -3) : filePath;
  headers['Content-Type'] = getMimeType(filePathForMime);

  return headers;
};

const resolveRequestPath = (requestUrl) => {
  const parsed = new URL(requestUrl, `http://${host}:${port}`);
  const normalizedPath = decodeURIComponent(parsed.pathname);
  const relativePath = normalizedPath === '/' ? 'index.html' : normalizedPath.replace(/^\//, '');
  const candidatePath = path.normalize(path.join(webRoot, relativePath));

  if (!candidatePath.startsWith(webRoot)) {
    return null;
  }

  return candidatePath;
};

const server = http.createServer((request, response) => {
  try {
    const resolvedPath = resolveRequestPath(request.url ?? '/');
    if (!resolvedPath) {
      sendNotFound(response);
      return;
    }

    let filePath = resolvedPath;
    if (fs.existsSync(filePath) && fs.statSync(filePath).isDirectory()) {
      filePath = path.join(filePath, 'index.html');
    }

    if (!fs.existsSync(filePath) || !fs.statSync(filePath).isFile()) {
      sendNotFound(response);
      return;
    }

    const headers = getHeadersForPath(filePath);
    response.writeHead(200, headers);
    fs.createReadStream(filePath).pipe(response);
  } catch (error) {
    sendError(response, error);
  }
});

server.listen(port, host, () => {
  process.stdout.write(`Web smoke server listening on http://${host}:${port}\n`);
  process.stdout.write(`Serving from ${webRoot}\n`);
});
