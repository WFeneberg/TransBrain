import { defineConfig } from '@playwright/test';

export default defineConfig({
    testDir: './e2e',
    use: { baseURL: 'http://localhost:4200' },
    reporter: 'list',
    // Every spec here authenticates through the same Keycloak realm/container, and under the
    // default parallel workers concurrent logins against that one container do time out
    // intermittently - that part is real contention. But the specific failure observed during
    // Task 13's fix round (a 3-worker run of this suite failing 2 of 5 tests, an admin-created
    // row not visible after reload/list refresh) was NOT that: it was ListVehiclesQueryHandler's
    // cache-aside read-then-set race on the hot key `vehicles:list:1:20:none:none` - a write
    // handler's RemoveByPrefixAsync landing between a concurrent reader's database read and its
    // SetAsync, so the reader's stale write survived and was served back on reload. Filed as
    // flakiness and masked by pinning workers to 1, it would have gone on serving stale list
    // pages under real concurrent traffic. It is now fixed at the source with a generation
    // counter (see ICacheService.GetGenerationAsync / RedisCacheService), not by serialising
    // tests. workers: 1 stays pinned regardless, because the login-contention symptom (seen on
    // TransBrain.VueWeb's equivalent suite - a timeout waiting on '#username' under 3 workers)
    // is genuine and unrelated; revisit only alongside a real fix for the shared-session cost
    // (see TransBrain.VueWeb's task-13-report.md "Fix round 1" section for the storageState
    // alternative and why it wasn't done here).
    workers: 1,
});
