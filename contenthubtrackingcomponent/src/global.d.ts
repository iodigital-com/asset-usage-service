/// <reference types="vite/client" />

interface ImportMetaEnv {
    readonly VITE_CMS_BASE_URL?: string;
}

interface ImportMeta {
    readonly env: ImportMetaEnv;
}

export {};

declare global {
    interface Window {
        ContentHub: {
            registerComponent: (name: string, component: any) => void;
        };
    }
}
