import { defineConfig } from '@playwright/test';
import path from 'node:path';

const baseURL = process.env.SMOKE_BASE_URL ?? 'http://127.0.0.1:5055';
const repoRoot = path.resolve(__dirname, '../..');

export default defineConfig({
  testDir: './specs',
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list']],
  use: {
    baseURL,
    trace: 'on-first-retry',
  },
  webServer: {
    command: `dotnet run --project "${path.join(repoRoot, 'ltwnc.csproj')}" --urls ${baseURL} --no-launch-profile`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    cwd: repoRoot,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      SMOKE_FIXTURES: '1',
      // Optional override; otherwise appsettings DefaultConnection is used.
      ...(process.env.SMOKE_CONNECTION
        ? { ConnectionStrings__DefaultConnection: process.env.SMOKE_CONNECTION }
        : {}),
    },
  },
});
