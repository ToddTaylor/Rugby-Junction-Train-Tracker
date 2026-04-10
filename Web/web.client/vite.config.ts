import { fileURLToPath, URL } from 'node:url';
import { defineConfig, loadEnv } from 'vite';
import plugin from '@vitejs/plugin-react';
import compression from 'vite-plugin-compression';
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';

const baseFolder =
    env.APPDATA !== undefined && env.APPDATA !== ''
        ? `${env.APPDATA}/ASP.NET/https`
        : `${env.HOME}/.aspnet/https`;

const certificateName = "web.client";
const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

if (!fs.existsSync(baseFolder)) {
    fs.mkdirSync(baseFolder, { recursive: true });
}

if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {

    if (0 !== child_process.spawnSync('dotnet', [
        'dev-certs',
        'https',
        '--export-path',
        certFilePath,
        '--format',
        'Pem',
        '--no-password',
    ], { stdio: 'inherit', }).status) {
        throw new Error("Could not create certificate.");
    }
}

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
    env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:7297';

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
    // Resolve env file by explicit Vite mode first (e.g. `vite --mode beta`).
    // Fall back to ASP.NET environment variables when mode is default development.
    const aspnetEnv = process.env.ASPNETCORE_ENVIRONMENT || process.env.ASPNET_ENVIRONMENT;
    const resolvedMode = mode !== 'development'
        ? mode
        : (aspnetEnv ? aspnetEnv.toLowerCase() : mode);
    const appEnv = loadEnv(resolvedMode, process.cwd(), '');
    const viteApiUrl = appEnv.VITE_API_URL || process.env.VITE_API_URL;
    const viteApiKey = appEnv.VITE_API_KEY || process.env.VITE_API_KEY;
    const viteAppVersion = appEnv.VITE_APP_VERSION || process.env.VITE_APP_VERSION;
    
    return {
        define: {
            'import.meta.env.VITE_API_URL': JSON.stringify(viteApiUrl),
            'import.meta.env.VITE_API_KEY': JSON.stringify(viteApiKey),
            'import.meta.env.VITE_APP_VERSION': JSON.stringify(viteAppVersion),
        },
        plugins: [
            plugin(),
            compression({
                verbose: true,
                threshold: 8 * 1024, // Only assets bigger than 8KB
                algorithm: 'gzip',
                ext: '.gz',
                deleteOriginFile: false, // Keep original files
                compressionOptions: { level: 9 }
            })
        ],
        resolve: {
            alias: {
                '@': fileURLToPath(new URL('./src', import.meta.url))
            }
        },
        server: {
            proxy: {
                '^/weatherforecast': {
                    target,
                    secure: false
                }
            },
            port: parseInt(appEnv.DEV_SERVER_PORT || '53848'),
            https: {
                key: fs.readFileSync(keyFilePath),
                cert: fs.readFileSync(certFilePath),
            }
        }
    };
});
