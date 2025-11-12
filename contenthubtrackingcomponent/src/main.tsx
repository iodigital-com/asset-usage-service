import { createAssetUsageTracker } from './AssetUsageTracker';

// Demo data voor development
const mockEntity = {
    id: 12345,
    properties: {
        UsageTracking: {
            "item-1": {
                itemName: "Homepage Banner",
                itemPath: "/sitecore/content/Home/Banner",
                language: "en",
                version: 1
            },
            "item-2": {
                itemName: "Product Page",
                itemPath: "/sitecore/content/Products/Product1",
                language: "en",
                version: 2
            }
        }
    }
};

// Development preview
if (import.meta.env.DEV) {
    const container = document.getElementById('root');
    if (container) {
        const tracker = createAssetUsageTracker(container);
        tracker.render({ entity: mockEntity });
    }
}
