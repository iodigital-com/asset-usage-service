import { createAssetUsageTracker } from './AssetUsageTracker';

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

const testScenarios = [
    { name: 'Entity with Usage Data (4 items)', entity: mockEntityWithData },
    { name: 'Entity with No Usage', entity: mockEntityEmpty },
    { name: 'Null Entity', entity: mockEntityNull }
];


const appContainer = document.getElementById('app');
if (!appContainer) {
    console.error('AssetUsageTracker Main - App container not found');
} else {
    console.log('AssetUsageTrackerMain - App container found, setting up test environment');
    
    const controlsContainer = document.createElement('div');
    controlsContainer.style.cssText = `
        padding: 20px;
        background-color: #f0f0f0;
        margin-bottom: 20px;
        border-radius: 8px;
        border: 1px solid #ddd;
    `;
    
    // Add title
    const title = document.createElement('h2');
    title.textContent = 'AssetUsageTracker Test Environment';
    title.style.cssText = 'margin: 0 0 16px 0; color: #333;';
    controlsContainer.appendChild(title);
    
    // Add description
    const description = document.createElement('p');
    description.textContent = 'Click the buttons below to test different scenarios:';
    description.style.cssText = 'margin: 0 0 16px 0; color: #666;';
    controlsContainer.appendChild(description);
    
    // Create component container
    const componentContainer = document.createElement('div');
    componentContainer.id = 'asset-usage-tracker-container';
    
    // Initialize the tracker
    const tracker = createAssetUsageTracker(componentContainer);
    
    // Create buttons for each test scenario
    testScenarios.forEach((scenario) => {
        const button = document.createElement('button');
        button.textContent = scenario.name;
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
        
        // Add hover effect
        button.addEventListener('mouseenter', () => {
            button.style.backgroundColor = '#106ebe';
        });
        button.addEventListener('mouseleave', () => {
            button.style.backgroundColor = '#0078d4';
        });
        
        button.addEventListener('click', () => {
            console.log(`AssetUsageTracker Main - Testing scenario: ${scenario.name}`);
            console.log('AssetUsageTracker Main - Entity data:', scenario.entity);
            
            // Update button states
            document.querySelectorAll('button').forEach(btn => {
                btn.style.backgroundColor = '#0078d4';
                btn.style.fontWeight = '500';
            });
            button.style.backgroundColor = '#005a9e';
            button.style.fontWeight = '600';
            
            // Render the scenario
            try {
                tracker.render({ entity: scenario.entity });
                console.log(`AssetUsageTracker Main - Successfully rendered scenario: ${scenario.name}`);
            } catch (error) {
                console.error(`AssetUsageTracker Main - Error rendering scenario: ${scenario.name}`, error);
            }
        });
        
        controlsContainer.appendChild(button);
    });
    
  
    // Assemble the UI
    appContainer.innerHTML = '';
    appContainer.appendChild(controlsContainer);
    appContainer.appendChild(componentContainer);
    
    // Load first scenario by default
    console.log('AssetUsageTracker Main - Loading default scenario');
    tracker.render({ entity: mockEntityWithData });
    
    // Highlight first button
    const firstButton = controlsContainer.querySelector('button');
    if (firstButton instanceof HTMLElement) {
        firstButton.style.backgroundColor = '#005a9e';
        firstButton.style.fontWeight = '600';
    }
}

