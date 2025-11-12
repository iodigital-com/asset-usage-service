// global.d.ts
export {};

declare global {
    interface Window {
        ContentHub: {
            registerComponent: (name: string, component: any) => void;
        };
    }
}
