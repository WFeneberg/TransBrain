import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';

export default defineConfig({
    plugins: [vue()],
    server: {
        port: 4300,
        strictPort: true,
        proxy: {
            '/api': {
                target: process.env['services__api__https__0'] ?? process.env['services__api__http__0'] ?? 'http://localhost:5000',
                changeOrigin: true,
                secure: false,
            },
        },
    },
});
