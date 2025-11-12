import { createRoot } from "react-dom/client";

interface ContentHubEntity {
    id: number;
    properties: {
        UsageTracking?: {
            [itemId: string]: UsageTrackingItem;
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
    if (!entity) {
        return (
            <div style={styles.container}>
                <div style={styles.error}>
                    <strong>Error:</strong> No entity available
                </div>
            </div>
        );
    }

    const usageData = entity.properties.UsageTracking || {};
    const usageCount = Object.keys(usageData).length;

    if (usageCount === 0) {
        return (
            <div style={styles.container}>
                <h3 style={styles.title}>Asset Usage Tracking</h3>
                <div style={styles.noUsage}>
                    <p>This asset is not currently used in any published items.</p>
                </div>
            </div>
        );
    }

    return (
        <div style={styles.container}>
            <h3 style={styles.title}>Asset Usage Tracking</h3>
            <div style={styles.summary}>
                <p>
                    This asset (ID: <strong>{entity.id}</strong>) is used in{' '}
                    <strong>{usageCount}</strong> published item(s)
                </p>
            </div>

            <table style={styles.table}>
                <thead>
                    <tr>
                        <th style={styles.th}>Item ID</th>
                        <th style={styles.th}>Item Name</th>
                        <th style={styles.th}>Item Path</th>
                        <th style={styles.th}>Language</th>
                        <th style={styles.th}>Version</th>
                    </tr>
                </thead>
                <tbody>
                    {Object.entries(usageData).map(([itemId, item]) => (
                        <tr key={itemId} style={styles.tr}>
                            <td style={styles.tdCode}>{itemId}</td>
                            <td style={styles.td}>{item.itemName || '-'}</td>
                            <td style={styles.td}>{item.itemPath || '-'}</td>
                            <td style={styles.td}>{item.language || '-'}</td>
                            <td style={styles.td}>{item.version ?? '-'}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

const styles = {
    container: {
        padding: '20px',
        fontFamily: 'system-ui, -apple-system, "Segoe UI", sans-serif',
        backgroundColor: '#fff',
    },
    title: {
        margin: '0 0 16px 0',
        color: '#333',
        fontSize: '20px',
        fontWeight: 600,
    },
    error: {
        padding: '16px',
        backgroundColor: '#fee',
        border: '1px solid #fcc',
        borderRadius: '4px',
        color: '#c00',
    },
    noUsage: {
        padding: '20px',
        textAlign: 'center' as const,
        backgroundColor: '#f5f5f5',
        borderRadius: '4px',
        color: '#666',
    },
    summary: {
        padding: '12px 16px',
        backgroundColor: '#f0f7ff',
        borderLeft: '4px solid #0078d4',
        borderRadius: '4px',
        marginBottom: '20px',
    },
    table: {
        width: '100%',
        borderCollapse: 'collapse' as const,
        backgroundColor: '#fff',
        border: '1px solid #e0e0e0',
    },
    th: {
        padding: '12px',
        textAlign: 'left' as const,
        backgroundColor: '#f5f5f5',
        fontWeight: 600,
        color: '#666',
        borderBottom: '2px solid #e0e0e0',
        fontSize: '14px',
    },
    tr: {
        borderBottom: '1px solid #e0e0e0',
    },
    td: {
        padding: '12px',
        fontSize: '14px',
        color: '#333',
    },
    tdCode: {
        padding: '12px',
        fontSize: '13px',
        fontFamily: '"Courier New", monospace',
        color: '#0078d4',
    },
};

// Factory function for ContentHub integration
export function createAssetUsageTracker(container: HTMLElement) {
    const root = createRoot(container);
    
    return {
        render(context: any) {
            root.render(<AssetUsageTrackerComponent entity={context.entity} />);
        },
        unmount() {
            root.unmount();
        },
    };
}

// ContentHub registration
if (!window.ContentHub) {
    window.ContentHub = {
        registerComponent: () => {}
    };
}

window.ContentHub.registerComponent('AssetUsageTracker', createAssetUsageTracker);

export default createAssetUsageTracker;
