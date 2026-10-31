import { defineConfig } from '@playwright/test';
export default defineConfig({testDir:'./e2e',use:{baseURL:'http://127.0.0.1:4173',headless:true,launchOptions:{executablePath:process.env.CAPTURE_CHROMIUM_PATH}},webServer:{command:'npx vite preview --host 127.0.0.1 --port 4173',url:'http://127.0.0.1:4173',reuseExistingServer:!process.env.CI}});
