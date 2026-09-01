import { defineConfig } from '@playwright/test';

export default defineConfig({
    testDir: './e2e',
    use: { baseURL: 'http://localhost:4300' },
    reporter: 'list',
    // Pays the dev-server and Keycloak cold-start cost before the first test, so it does not
    // land on whichever test signs in first. See e2e/warm-up.ts.
    globalSetup: './e2e/warm-up.ts',
    // Every spec here authenticates through the same Keycloak realm/container. Under the
    // default parallel workers, concurrent logins against that one container time out
    // intermittently - observed directly during Task 13's fix round: two separate runs each
    // lost one test to a timeout waiting on '#username' under 3 workers, and neither ever
    // reproduced at 1. The failure looks like a broken test, not contention, so it would get
    // "fixed" by retrying rather than understood. Serialised is slower but deterministic;
    // revisit only alongside a real fix for the shared-session cost (see task-13-report.md's
    // "Fix round 1" section for the storageState alternative and why it wasn't done here).
    workers: 1,
});
