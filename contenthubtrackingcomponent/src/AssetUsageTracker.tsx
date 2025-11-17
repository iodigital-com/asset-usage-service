import { createRoot } from "react-dom/client";
import { useEffect, useState } from "react";

interface ContentHubEntity {
    id?: number;
    properties: {
        UsageTracking?: {
            [language: string]: {
                [itemId: string]: UsageTrackingItem;
            };
        };
    };
}

interface UsageTrackingItem {
    itemName?: string;
    itemPath?: string;
    language?: string;
    version?: number;
}

interface AssetUsageTrackerProps {
    entity: ContentHubEntity | null;
}

function AssetUsageTrackerComponent({ entity }: AssetUsageTrackerProps) {
    const [currentEntity, setCurrentEntity] = useState(entity);

    useEffect(() => {
        setCurrentEntity(entity);
    }, [entity]);

    if (!currentEntity) {
        return (
            <div style={styles.wrapper}>
                <div style={styles.errorMessage}>
                    <strong>Error:</strong> No entity available
                </div>
            </div>
        );
    }

    const usageTrackingData = currentEntity.properties.UsageTracking || {};
    
    let allUsageItems : { [compositeKey: string]: UsageTrackingItem } = {};
    let totalCount = 0;
    
    Object.entries(usageTrackingData).forEach(([languageKey, languageItems]) => {
        if (languageItems && typeof languageItems === 'object') {
            const itemEntries = Object.entries(languageItems);
            
            itemEntries.forEach(([itemId, itemData]) => {
                allUsageItems[`${languageKey}-${itemId}`] = itemData;
                totalCount++;
            });
        }
    });

    if (totalCount === 0) {
        return (
            <div style={styles.wrapper}>
                <div style={styles.headerRow}>
                    <h3 style={styles.heading}>Usage in CMS items</h3>
                </div>
                <div style={styles.emptyState}>
                    <p>This asset is not currently used in any published items.</p>
                </div>
            </div>
        );
    }

    const mappedEntries = Object.entries(allUsageItems).map(([compositeKey, item], index) => {
        const [languageKey] = compositeKey.split('-', 2);
        const itemId = compositeKey.substring(languageKey.length + 1);

        const displayItemName = item.itemName || '-';
        const displayItemPath = item.itemPath || '-';
        const displayLanguage = item.language || languageKey || '-';
        const displayVersion = item.version ?? '-';
        const isLastItem = index === totalCount - 1;

        return (
            <a href="#" style={styles.link}>
                <div key={compositeKey} style={{...styles.tableRow, borderBottom: isLastItem ? 'none' : '1px solid #e0e0e0'}}>
                    <div style={styles.nameColumn}>
                        <h5 style={styles.itemName}>{displayItemName}</h5>
                        <p style={styles.itemPath}>Path: {displayItemPath}</p>
                        <p style={styles.itemId}>Item ID: {itemId}</p>
                    </div>
                    <div style={styles.languageColumn}>
                        <span style={styles.badge}>{displayLanguage}</span>
                    </div>
                    <div style={styles.versionColumn}>
                        <span style={styles.badge}>V{displayVersion}</span>
                    </div>
                </div>
            </a>
        );
    });

    return (
        <div style={styles.wrapper} key={currentEntity.id}>
            <div style={styles.headerRow}>
                <h4 className="MuiTypography-root MuiTypography-h4 css-1xfybxt-title-titleMargin ltr-djzpxj">Usage in CMS items</h4>
                <span style={styles.itemCount}>Used in {totalCount} items</span>
            </div>
            
            <div style={styles.tableHeader}>
                <div style={styles.headerNameColumn}>Name and path</div>
                <div style={styles.headerLanguageColumn}>Language</div>
                <div style={styles.headerVersionColumn}>Version</div>
            </div>

            <div style={styles.scrollableContent}>
                {mappedEntries}
            </div>
        </div>
    );
}

const styles = {
    link: {
        textDecoration: 'none',
    },
    wrapper: {
        fontFamily: 'system-ui, -apple-system, "Segoe UI", sans-serif',
        padding: '24px',
        border: "1px solid rgba(0, 0, 0, 0.11)",
        backgroundColor: '#fff',
        boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
        borderRadius: '0.375rem',
    },
    headerRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        marginBottom: '16px',
    },
    heading: {
        margin: '0',
        color: '#333',
        fontSize: '24px',
        fontWeight: 600,
    },
    itemCount: {
        color: '#999',
        fontSize: '16px',
        fontWeight: 400,
    },
    errorMessage: {
        padding: '16px',
        backgroundColor: '#fee',
        border: '1px solid #fcc',
        borderRadius: '4px',
        color: '#c00',
    },
    emptyState: {
        padding: '20px',
        textAlign: 'center' as const,
        backgroundColor: '#f5f5f5',
        borderRadius: '4px',
        color: '#666',
    },
    tableHeader: {
        display: 'flex',
        padding: '8px 0',
        borderBottom: '1px solid #e0e0e0',
        color: '#999',
        fontSize: '13px',
        fontWeight: 500,
    },
    headerNameColumn: {
        flex: 1,
    },
    headerLanguageColumn: {
        textAlign: 'center' as const,
    },
    headerVersionColumn: {
        minWidth: '80px',
        textAlign: 'center' as const,
    },
    headerLinkColumn: {
        textAlign: 'center' as const,
    },
    tableRow: {
        display: 'flex',
        alignItems: 'center',
        padding: '10px 0',
    },
    nameColumn: {
        flex: 1,
        minWidth: 0,
    },
    languageColumn: {
        display: 'flex',
        justifyContent: 'center',
    },
    versionColumn: {
        flex: '0 0 60px',
        minWidth: '60px',
        display: 'flex',
        justifyContent: 'center',
    },
    linkColumn: {
        display: 'flex',
        justifyContent: 'center',
        cursor: 'pointer',
    },
    itemName: {
        margin: '0 0 2px 0',
        fontWeight: 600,
        color: '#6E3FFF',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap' as const,
    },
    itemPath: {
        margin: '0 0 2px 0',    
        color: '#999',
        fontSize: '12px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap' as const,
    },
    itemId: {
        margin: '0',    
        color: '#ccc',
        fontSize: '11px',
    },
    badge: {
        backgroundColor: '#f3f3f3',
        color: '#666',
        borderRadius: '4px',
        padding: '4px 12px',
        fontSize: '13px',
        fontWeight: 400,
    },
    scrollableContent: {
        maxHeight: '500px',
        overflowY: 'auto' as const,
    },
};

export function createAssetUsageTracker(container: HTMLElement) {
    const root = createRoot(container);
    let currentContext: any = null;
    
    return {
        render(context: any) {
            currentContext = context;
            root.render(<AssetUsageTrackerComponent entity={context.entity} />);
        },
        unmount() {
            root.unmount();
        },
        refresh() {
            if (currentContext) {
                root.render(<AssetUsageTrackerComponent entity={currentContext.entity} />);
            }
        }
    };
}

if (!window.ContentHub) {
    window.ContentHub = {
        registerComponent: () => {}
    };
}

window.ContentHub.registerComponent('AssetUsageTracker', createAssetUsageTracker);

export default createAssetUsageTracker;