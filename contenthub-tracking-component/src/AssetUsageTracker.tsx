import { useEffect, useState } from 'react';

declare global {
    interface Window {
        ContentHub?: {
            context?: {
                entityId?: number;
                culture?: string;
            };
            api?: {
                entities: {
                    get: (id: number) => Promise<ContentHubEntity>;
                };
            };
        };
    }
}

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

interface Props {
    assetId?: number;
}

export function AssetUsageTracker({ assetId }: Props) {
    const [usageData, setUsageData] = useState<Record<string, UsageTrackingItem>>({});
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [currentAssetId, setCurrentAssetId] = useState<number | null>(null);

    useEffect(() => {
        const resolvedAssetId = assetId ?? window.ContentHub?.context?.entityId;

        if (!resolvedAssetId) {
            setError('No asset ID provided');
            setLoading(false);
            return;
        }

        setCurrentAssetId(resolvedAssetId);
        fetchUsageData(resolvedAssetId);
    }, [assetId]);

    const fetchUsageData = async (entityId: number) => {
        try {
            setLoading(true);
            setError(null);

            if (!window.ContentHub?.api) {
                throw new Error('ContentHub API not available');
            }

            const entity = await window.ContentHub.api.entities.get(entityId);
            const tracking = entity.properties.UsageTracking || {};

            setUsageData(tracking);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to load usage data');
            console.error('Failed to fetch usage data:', err);
        } finally {
            setLoading(false);
        }
    };

    if (loading) {
        return (
            <div style={styles.container}>
                <div style={styles.loading}>
                    <div style={styles.spinner}></div>
                    <p>Loading asset usage tracking...</p>
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div style={styles.container}>
                <div style={styles.error}>
                    <strong>Error:</strong> {error}
                </div>
            </div>
        );
    }

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
                    This asset (ID: <strong>{currentAssetId}</strong>) is used in{' '}
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
    loading: {
        display: 'flex',
        flexDirection: 'column' as const,
        alignItems: 'center',
        justifyContent: 'center',
        padding: '40px',
        color: '#666',
    },
    spinner: {
        width: '40px',
        height: '40px',
        border: '4px solid #f3f3f3',
        borderTop: '4px solid #0078d4',
        borderRadius: '50%',
        animation: 'spin 1s linear infinite',
        marginBottom: '16px',
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

if (typeof document !== 'undefined') {
    const style = document.createElement('style');
    style.textContent = `
    @keyframes spin {
      0% { transform: rotate(0deg); }
      100% { transform: rotate(360deg); }
 }
  `;
    document.head.appendChild(style);
}

export default AssetUsageTracker;
