import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.TEST_BASE_URL ?? 'http://127.0.0.1:5218';

export default defineConfig({
    testDir: '.',
    testMatch: 'timetable.spec.mjs',
    use: { baseURL },
    projects: [
        {
            name: 'desktop',
            use: {
                ...devices['Desktop Chrome'],
                launchOptions: { executablePath: process.env.CHROMIUM_EXECUTABLE }
            }
        },
        {
            name: 'android',
            use: {
                ...devices['Pixel 7'],
                viewport: { width: 360, height: 800 },
                launchOptions: { executablePath: process.env.CHROMIUM_EXECUTABLE }
            }
        },
        { name: 'iphone', use: { ...devices['iPhone 13 Mini'] } }
    ],
    webServer: process.env.TEST_BASE_URL ? undefined : {
        command: `${process.env.TEST_DOTNET ?? 'dotnet'} bin/Release/net10.0/Web.dll`,
        cwd: '../../Web',
        env: { ASPNETCORE_URLS: baseURL, ASPNETCORE_ENVIRONMENT: 'Development' },
        url: baseURL,
        reuseExistingServer: !process.env.CI,
        timeout: 30000
    }
});
