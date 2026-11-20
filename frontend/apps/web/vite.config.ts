import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          mui: ['@mui/material', '@mui/icons-material'],
          vendor: ['react', 'react-dom', 'react-router', '@tanstack/react-query', '@microsoft/signalr'],
        },
      },
    },
  },
});
