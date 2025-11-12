import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import AssetUsageTracker from './AssetUsageTracker'

// Mock ContentHub context voor local development
if (!window.ContentHub) {
    window.ContentHub = {
        context: {
            entityId: 12345, // Test asset ID
            culture: 'en-US'
        },
        api: {
            entities: {
                get: async (id: number) => {
                    // Mock data voor development - simuleert UsageTracking property
                    console.log(`Fetching asset ${id} (mock data)`);
                    return {
                        id,
                        properties: {
                            UsageTracking: {
                                'a1b2c3d4-e5f6-7890-abcd-ef1234567890': {
                                    itemName: 'Homepage Banner',
                                    itemPath: '/sitecore/content/home',
                                    language: 'en',
                                    version: 1
                                },
                                'b2c3d4e5-f6g7-8901-bcde-fg2345678901': {
                                    itemName: 'Product Detail Page',
                                    itemPath: '/sitecore/content/products/detail',
                                    language: 'nl-NL',
                                    version: 2
                                },
                                'c3d4e5f6-g7h8-9012-cdef-gh3456789012': {
                                    itemName: 'Blog Article Header',
                                    itemPath: '/sitecore/content/blog/article-1',
                                    language: 'en',
                                    version: 3
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <AssetUsageTracker />
    </StrictMode>,
)
