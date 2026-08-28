import { defineConfig } from '@playwright/test';

export default defineConfig({
    testDir: './e2e',
    use: { baseURL: 'http://localhost:4300' },
    reporter: 'list',
});
