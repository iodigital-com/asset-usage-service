import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
    plugins: [react()],
    define: {
        'process.env': {}
    },
    build: {
        target: 'es2020',
        lib: {
            entry: './src/DeleteModal.tsx',
            formats: ['es'],
            fileName: 'DeleteModal'
        },
        rollupOptions: {
            output: {
                format: 'es',
                globals: {
                    react: 'React',
                    'react-dom': 'ReactDOM',
                    'react-dom/client': 'ReactDOM'
                }
            }
        }
    }
});