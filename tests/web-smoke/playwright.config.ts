import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: 'web.smoke.spec.ts',
  timeout: 120000,
  retries: 1,
  reporter: [['list'], ['html', { outputFolder: 'artifacts/report', open: 'never' }]],
  use: {
    headless: true,
    baseURL: 'http://127.0.0.1:4173',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'retain-on-failure'
  },
  outputDir: 'artifacts/test-output',
  webServer: {
    command: 'python3 -m http.server 4173 --directory ../../build/WebGL',
    timeout: 120000,
    reuseExistingServer: false,
    url: 'http://127.0.0.1:4173'
  }
});