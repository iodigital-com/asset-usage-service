import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
    plugins: [react()],
    build: {
        target: 'es2020',
        lib: {
            entry: './src/AssetUsageTracker.tsx',
            formats: ['es'],
            fileName: 'AssetUsageTracker'
        },
        rollupOptions: {
            external: ['react', 'react-dom'], // React als external dependencies
            output: {
                format: 'es',
                globals: {
                    react: 'React',
                    'react-dom': 'ReactDOM'
                }
            }
        }
    }
});
