import { createAssetUsageTracker } from './AssetUsageTracker';
import createDeleteAssetButton from './DeleteAssetButton';

// ============================================================================
// This file sets up a test environment for both components.
// It creates buttons to simulate different entity scenarios to test the styling.
// ============================================================================

// Mock client for DeleteAssetButton
const mockClient = {
    entities: {
        deleteAsync: async (entityId: number) => {
            console.log(`Mock deleteAsync called for entity ${entityId}`);
            await new Promise(resolve => setTimeout(resolve, 1500));
            
            if (Math.random() > 0.1) {
                console.log(`Mock delete successful for entity ${entityId}`);
                return Promise.resolve();
            } else {
                throw new Error('Simulated API error: Failed to delete asset');
            }
        }
    }
};

const mockEntityWithData = {
    id: 36280,
    properties: {
        FileName: { value: "test-asset.jpg" },
        Title: { value: "Test Asset" },
        UsageTracking: {
            "Invariant": {
                "229c8ed2-0c35-4791-8567-9203891910d7": {
                    itemName: 'Home',
                    itemPath: '/sitecore/content/Home',
                    language: 'en',
                    version: 1
                },
                "f2a068e4-df93-46a5-beba-756af80dfd56": {
                    itemName: 'test publish4',
                    itemPath: '/sitecore/content/Authors/test publish4',
                    language: 'en',
                    version: 1
                },
                "110d559f-dea5-42ea-9c1c-8a5df7e70ef9": {
                    itemName: 'Test',
                    itemPath: '/sitecore/content/Authors/Test',
                    language: 'en',
                    version: 1
                }
            },
            "nl": {
                "229c8ed2-0c35-4791-8567-9203891910d7": {
                    itemName: 'Startpagina',
                    itemPath: '/sitecore/content/Home',
                    language: 'nl',
                    version: 1
                }
            },
            "w": {
                "229c8ed2-0c35-4791-8567-9203891910d7": {
                    itemName: 'Startpagina',
                    itemPath: '/sitecore/content/Home',
                    language: 'nl',
                    version: 1
                }
            },
            "we": {
                "229c8ed2-0c35-4791-8567-9203891910d7": {
                    itemName: 'Startpagina',
                    itemPath: '/sitecore/content/Home',
                    language: 'nl',
                    version: 1
                }
            },
            "wew": {
                "229c8ed2-0c35-4791-8567-9203891910d7": {
                    itemName: 'Startpagina',
                    itemPath: '/sitecore/content/Home',
                    language: 'nl',
                    version: 1
                }
            }
        }
    }
};

const mockEntityEmpty = {
    id: 12345,
    properties: {
        UsageTracking: {}
    }
};

const mockEntityNull = null;

const usageTrackerScenarios = [
    { name: 'Entity with Usage Data (7 items)', entity: mockEntityWithData },
    { name: 'Entity with No Usage', entity: mockEntityEmpty },
    { name: 'Null Entity', entity: mockEntityNull }
];

const deleteButtonScenarios = [
    { name: 'With Usage (5 items)', entity: mockEntityWithData, entityId: 36280 },
    { name: 'No Usage (Simple)', entity: mockEntityEmpty, entityId: 12345 },
    { name: 'Null Entity', entity: mockEntityNull, entityId: 99999 }
];

const appContainer = document.getElementById('app');
if (!appContainer) {
    console.error('Main - App container not found');
} else {
    console.log('Main - App container found, setting up test environment');
    
    appContainer.innerHTML = '';
    
    // ========================================================================
    // AssetUsageTracker Section
    // ========================================================================
    
    const usageTrackerSection = document.createElement('div');
    usageTrackerSection.style.cssText = 'margin-bottom: 40px;';
    
    const usageTrackerControls = document.createElement('div');
    usageTrackerControls.style.cssText = `
        padding: 20px;
        background-color: #f0f0f0;
        margin-bottom: 20px;
        border-radius: 8px;
        border: 1px solid #ddd;
    `;
    
    const usageTrackerTitle = document.createElement('h2');
    usageTrackerTitle.textContent = 'AssetUsageTracker Test Environment';
    usageTrackerTitle.style.cssText = 'margin: 0 0 16px 0; color: #333;';
    usageTrackerControls.appendChild(usageTrackerTitle);
    
    const usageTrackerDescription = document.createElement('p');
    usageTrackerDescription.textContent = 'Click the buttons below to test different scenarios:';
    usageTrackerDescription.style.cssText = 'margin: 0 0 16px 0; color: #666;';
    usageTrackerControls.appendChild(usageTrackerDescription);
    
    const usageTrackerContainer = document.createElement('div');
    usageTrackerContainer.id = 'asset-usage-tracker-container';
    
    const tracker = createAssetUsageTracker(usageTrackerContainer);
    
    usageTrackerScenarios.forEach((scenario) => {
        const button = document.createElement('button');
        button.textContent = scenario.name;
        button.className = 'usage-tracker-btn';
        button.style.cssText = `
            margin: 5px 10px 5px 0;
            padding: 10px 16px;
            background-color: #0078d4;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 14px;
            font-weight: 500;
        `;
        
        button.addEventListener('mouseenter', () => {
            button.style.backgroundColor = '#106ebe';
        });
        button.addEventListener('mouseleave', () => {
            const isActive = button.style.fontWeight === '600';
            button.style.backgroundColor = isActive ? '#005a9e' : '#0078d4';
        });
        
        button.addEventListener('click', () => {
            console.log(`AssetUsageTracker - Testing scenario: ${scenario.name}`);
            
            document.querySelectorAll('.usage-tracker-btn').forEach(btn => {
                if (btn instanceof HTMLElement) {
                    btn.style.backgroundColor = '#0078d4';
                    btn.style.fontWeight = '500';
                }
            });
            button.style.backgroundColor = '#005a9e';
            button.style.fontWeight = '600';
            
            try {
                tracker.render({ entity: scenario.entity });
                console.log(`AssetUsageTracker - Successfully rendered: ${scenario.name}`);
            } catch (error) {
                console.error(`AssetUsageTracker - Error rendering: ${scenario.name}`, error);
            }
        });
        
        usageTrackerControls.appendChild(button);
    });
    
    usageTrackerSection.appendChild(usageTrackerControls);
    usageTrackerSection.appendChild(usageTrackerContainer);
    appContainer.appendChild(usageTrackerSection);
    
    // Load first scenario by default
    tracker.render({ entity: mockEntityWithData });
    const firstUsageBtn = usageTrackerControls.querySelector('.usage-tracker-btn');
    if (firstUsageBtn instanceof HTMLElement) {
        firstUsageBtn.style.backgroundColor = '#005a9e';
        firstUsageBtn.style.fontWeight = '600';
    }
    
    // ========================================================================
    // DeleteAssetButton Section
    // ========================================================================
    
    const deleteButtonSection = document.createElement('div');
    deleteButtonSection.style.cssText = 'margin-bottom: 40px;';
    
    const deleteButtonControls = document.createElement('div');
    deleteButtonControls.style.cssText = `
        padding: 20px;
        background-color: #f0f0f0;
        margin-bottom: 20px;
        border-radius: 8px;
        border: 1px solid #ddd;
    `;
    
    const deleteButtonTitle = document.createElement('h2');
    deleteButtonTitle.textContent = 'DeleteAssetButton Test Environment';
    deleteButtonTitle.style.cssText = 'margin: 0 0 16px 0; color: #333;';
    deleteButtonControls.appendChild(deleteButtonTitle);
    
    const deleteButtonDescription = document.createElement('p');
    deleteButtonDescription.textContent = 'Test delete confirmation dialog with different usage scenarios:';
    deleteButtonDescription.style.cssText = 'margin: 0 0 16px 0; color: #666;';
    deleteButtonControls.appendChild(deleteButtonDescription);
    
    const modalWrapper = document.createElement('div');
    modalWrapper.style.cssText = `
        background-color: white;
        border-radius: 8px;
        box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
        padding: 20px;
        max-width: 600px;
        margin: 0 auto;
    `;
    
    const deleteButtonContainer = document.createElement('div');
    deleteButtonContainer.id = 'delete-asset-button-container';
          // Add modal header HTML
    const modalHeader = document.createElement('div');
    modalHeader.innerHTML = `
        <h2 id="Delete">
            <div>
                <div>Delete</div>
            </div>
        </h2>
    `;
    modalWrapper.appendChild(modalHeader);
    modalWrapper.appendChild(deleteButtonContainer);
    
    const deleteButton = createDeleteAssetButton(deleteButtonContainer);
    
  

    deleteButtonScenarios.forEach((scenario) => {
        const button = document.createElement('button');
        button.textContent = scenario.name;
        button.className = 'delete-btn';
        button.style.cssText = `
            margin: 5px 10px 5px 0;
            padding: 10px 16px;
            background-color: #0078d4;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 14px;
            font-weight: 500;
        `;
        
        button.addEventListener('mouseenter', () => {
            button.style.backgroundColor = '#106ebe';
        });
        button.addEventListener('mouseleave', () => {
            const isActive = button.style.fontWeight === '600';
            button.style.backgroundColor = isActive ? '#005a9e' : '#0078d4';
        });
        
        button.addEventListener('click', () => {
            console.log(`DeleteAssetButton - Testing scenario: ${scenario.name}`);
            
            document.querySelectorAll('.delete-btn').forEach(btn => {
                if (btn instanceof HTMLElement) {
                    btn.style.backgroundColor = '#0078d4';
                    btn.style.fontWeight = '500';
                }
            });
            button.style.backgroundColor = '#005a9e';
            button.style.fontWeight = '600';
            
            try {
                deleteButton.render({ 
                    client: mockClient,
                    options: { entityId: scenario.entityId },
                    entity: scenario.entity
                });
                console.log(`DeleteAssetButton - Successfully rendered: ${scenario.name}`);
            } catch (error) {
                console.error(`DeleteAssetButton - Error rendering: ${scenario.name}`, error);
            }
        });
        
        deleteButtonControls.appendChild(button);
    });
    

    deleteButtonSection.appendChild(deleteButtonControls);
    deleteButtonSection.appendChild(modalWrapper);
    appContainer.appendChild(deleteButtonSection);
    
    // Load first scenario by default
    deleteButton.render({ 
        client: mockClient,
        options: { entityId: deleteButtonScenarios[0].entityId },
        entity: deleteButtonScenarios[0].entity
    });
    const firstDeleteBtn = deleteButtonControls.querySelector('.delete-btn');
    if (firstDeleteBtn instanceof HTMLElement) {
        firstDeleteBtn.style.backgroundColor = '#005a9e';
        firstDeleteBtn.style.fontWeight = '600';
    }
    
    console.log('Main - Test environment setup complete');
}