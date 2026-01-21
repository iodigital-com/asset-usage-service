import { createAssetUsageTracker } from './AssetUsageTracker';

// ============================================================================
// Mock Data for Testing
// ============================================================================

const mockEntity = {
    id: 12345,
    properties: {
        UsageTracking: {
            'invariant': {
                '110d559f-dea5-42ea-9c1c-8a5df7e70ef9': {
                    itemName: 'Home',
                    itemPath: '/sitecore/content/Home',
                    language: 'nl-NL',
                    version: 3,
                    languages: [
                        { language: 'nl-NL', version: 3 },
                        { language: 'en', version: 2 },
                        { language: 'af-ZA', version: 1 },
                    ],
                },
                '0d7a4dcd-783d-4470-be24-6efcf1dd0965': {
                    itemName: 'John Brown Author Page',
                    itemPath: '/sitecore/content/Authors/John Brown',
                    language: 'en',
                    version: 1,
                    // Single language - old style data
                },
                '220e660g-feb6-53fb-0d2d-9b6eg8f81fg0': {
                    itemName: 'Product Detail - Laptop',
                    itemPath: '/sitecore/content/Products/Electronics/Laptop',
                    language: 'en',
                    version: 1,
                    languages: [
                        { language: 'en', version: 1 },
                        { language: 'de-DE', version: 2 },
                    ],
                },
                '330f771h-gfc7-64gc-1e3e-0c7fh9g92gh1': {
                    itemName: 'About Us Hero Section',
                    itemPath: '/sitecore/content/About/Hero',
                    language: 'en',
                    version: 5,
                    languages: [
                        { language: 'en', version: 5 },
                    ],
                },
                '440g882i-hgd8-75hd-2f4f-1d8gi0h03hi2': {
                    itemName: 'Contact Page',
                    itemPath: '/sitecore/content/Contact',
                    language: 'en',
                    version: 1,
                    languages: [
                        { language: 'en', version: 1 },
                        { language: 'nl-NL', version: 1 },
                        { language: 'de-DE', version: 1 },
                        { language: 'fr-FR', version: 2 },
                        { language: 'es-ES', version: 1 },
                        { language: 'it-IT', version: 1 },
                    ],
                },
                '550h993j-ihe9-86ie-3g5g-2e9hj1i14ij3': {
                    itemName: 'Footer Navigation',
                    itemPath: '/sitecore/content/Shared/Footer/Navigation',
                    language: 'en',
                    version: 3,
                    languages: [
                        { language: 'en', version: 3 },
                        { language: 'nl-NL', version: 2 },
                        { language: 'fr-FR', version: 1 },
                        { language: 'de-DE', version: 2 },
                        { language: 'es-ES', version: 1 },
                        { language: 'it-IT', version: 1 },
                        { language: 'pt-BR', version: 1 },
                        { language: 'ja-JP', version: 1 },
                    ],
                },
            },
        },
    },
};

const emptyEntity = {
    id: 99999,
    properties: {
        UsageTracking: {},
    },
};

// ============================================================================
// Test Scenarios
// ============================================================================

type TestScenario = 'with-data' | 'empty' | 'no-entity';

function getTestScenario(): TestScenario {
    const params = new URLSearchParams(window.location.search);
    return (params.get('scenario') as TestScenario) || 'with-data';
}

function getEntityForScenario(scenario: TestScenario) {
    switch (scenario) {
        case 'with-data':
            return mockEntity;
        case 'empty':
            return emptyEntity;
        case 'no-entity':
            return null;
        default:
            return mockEntity;
    }
}

// ============================================================================
// Render Test Page
// ============================================================================

function renderTestPage() {
    const scenario = getTestScenario();
    const entity = getEntityForScenario(scenario);

    // Create navigation
    const nav = document.createElement('div');
    nav.style.cssText = `
        padding: 16px;
        background: #f5f5f5;
        border-bottom: 1px solid #ddd;
        margin-bottom: 24px;
        display: flex;
        gap: 12px;
        align-items: center;
    `;
    nav.innerHTML = `
        <strong>Test Scenarios:</strong>
        <a href="?scenario=with-data" style="padding: 8px 16px; background: ${scenario === 'with-data' ? '#6E3FFF' : '#fff'}; color: ${scenario === 'with-data' ? '#fff' : '#333'}; border: 1px solid #ddd; border-radius: 4px; text-decoration: none;">With Data</a>
        <a href="?scenario=empty" style="padding: 8px 16px; background: ${scenario === 'empty' ? '#6E3FFF' : '#fff'}; color: ${scenario === 'empty' ? '#fff' : '#333'}; border: 1px solid #ddd; border-radius: 4px; text-decoration: none;">Empty State</a>
        <a href="?scenario=no-entity" style="padding: 8px 16px; background: ${scenario === 'no-entity' ? '#6E3FFF' : '#fff'}; color: ${scenario === 'no-entity' ? '#fff' : '#333'}; border: 1px solid #ddd; border-radius: 4px; text-decoration: none;">No Entity (Error)</a>
    `;

    // Create container for component
    const container = document.createElement('div');
    container.style.cssText = `
        max-width: 800px;
        margin: 0 auto;
        padding: 0 24px;
    `;

    // Add scenario info
    const info = document.createElement('div');
    info.style.cssText = `
        padding: 12px 16px;
        background: #e8f4fd;
        border: 1px solid #b8daff;
        border-radius: 4px;
        margin-bottom: 24px;
        font-size: 14px;
    `;
    info.innerHTML = `<strong>Current Scenario:</strong> ${scenario} | <strong>Entity ID:</strong> ${entity?.id ?? 'null'}`;
    container.appendChild(info);

    // Create component mount point
    const componentContainer = document.createElement('div');
    container.appendChild(componentContainer);

    // Mount everything to app
    const app = document.getElementById('app');
    if (app) {
        app.innerHTML = '';
        app.appendChild(nav);
        app.appendChild(container);
    }

    // Initialize the component
    const tracker = createAssetUsageTracker(componentContainer);
    tracker.render({ entity });
}

// ============================================================================
// Initialize
// ============================================================================

document.addEventListener('DOMContentLoaded', renderTestPage);

// Also run immediately if DOM is already loaded
if (document.readyState !== 'loading') {
    renderTestPage();
}
