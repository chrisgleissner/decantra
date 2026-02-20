import { defineConfig } from '@playwright/test';

const serverPort = Number(process.env.PLAYWRIGHT_PORT ?? '4173');
const outputDir = process.env.PLAYWRIGHT_OUTPUT_DIR ?? 'artifacts/test-output';
const reportDir = process.env.PLAYWRIGHT_REPORT_DIR ?? 'artifacts/report';

export default defineConfig({
  testDir: '.',
  testMatch: 'web.smoke.spec.ts',
  timeout: 120000,
  retries: 1,
  reporter: [['list'], ['html', { outputFolder: reportDir, open: 'never' }]],
  outputDir,
  workers: 1,
  use: {
    headless: true,
    baseURL: `http://127.0.0.1:${serverPort}`,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 20000,
    navigationTimeout: 90000
  },
  webServer: {
    command: 'node ./server.mjs',
    timeout: 120000,
    reuseExistingServer: process.env.PLAYWRIGHT_REUSE_SERVER === '1',
    url: `http://127.0.0.1:${serverPort}`
  }
});