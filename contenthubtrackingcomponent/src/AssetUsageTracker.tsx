import { createRoot } from "react-dom/client";
import { useMemo } from "react";

// ============================================================================
// Types & Interfaces
// ============================================================================

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

interface ProcessedUsageItem extends UsageTrackingItem {
    compositeKey: string;
    itemId: string;
    languageKey: string;
}

interface ContentHubContext {
    entity: ContentHubEntity | null;
}

interface ContentHubComponent {
    render: (context: ContentHubContext) => void;
    unmount: () => void;
}

// ============================================================================
// Constants
// ============================================================================

const CONSTANTS = {
    CMS_BASE_URL: 'https://Sitecore-xp-localsc.dev.local',
    MAX_WRAPPER_HEIGHT: '600px',
    MAX_CONTENT_HEIGHT: '500px',
} as const;

const LABELS = {
    TITLE: 'Usage in CMS items',
    NO_ENTITY: 'No entity available',
    NO_USAGE: 'This asset is not currently used in any published items.',
    USED_IN: 'Used in',
    ITEMS_SUFFIX: 'item(s)',
    COLUMN_NAME: 'Name and path',
    COLUMN_LANGUAGE: 'Language',
    COLUMN_VERSION: 'Version',
    PATH_PREFIX: 'Path:',
    ITEM_ID_PREFIX: 'Item ID:',
    VERSION_PREFIX: 'V',
    FALLBACK_VALUE: '-',
} as const;

// ============================================================================
// Utility Functions
// ============================================================================

function processUsageTrackingData(
    usageTracking: ContentHubEntity['properties']['UsageTracking']
): ProcessedUsageItem[] {
    if (!usageTracking) {
        return [];
    }

    return Object.entries(usageTracking).flatMap(([languageKey, languageItems]) => {
        if (!languageItems || typeof languageItems !== 'object') {
            return [];
        }

        return Object.entries(languageItems).map(([itemId, itemData]) => ({
            ...itemData,
            compositeKey: `${languageKey}-${itemId}`,
            itemId,
            languageKey,
        }));
    });
}

function buildCmsItemUrl(itemId: string): string {
    return `${CONSTANTS.CMS_BASE_URL}/sitecore/shell/Applications/Content%20Editor.aspx?fo=${itemId}`;
}

// ============================================================================
// Custom Hooks
// ============================================================================

function useProcessedUsageItems(entity: ContentHubEntity | null) {
    return useMemo(() => {
        if (!entity) {
            return [];
        }

        return processUsageTrackingData(entity.properties.UsageTracking);
    }, [entity]);
}

// ============================================================================
// Sub-Components
// ============================================================================

function ErrorMessage() {
    return (
        <div style={styles.wrapper}>
            <div style={styles.errorMessage}>
                <strong>Error:</strong> {LABELS.NO_ENTITY}
            </div>
        </div>
    );
}

function EmptyState() {
    return (
        <div style={styles.wrapper}>
            <Header totalCount={0} />
            <div style={styles.emptyState}>
                <p>{LABELS.NO_USAGE}</p>
            </div>
        </div>
    );
}

interface HeaderProps {
    totalCount: number;
}

function Header({ totalCount }: HeaderProps) {
    return (
        <div style={styles.headerRow}>
            <h4 className="MuiTypography-root MuiTypography-h4 css-1xfybxt-title-titleMargin ltr-djzpxj">
                {LABELS.TITLE}
            </h4>
            {totalCount > 0 && (
                <span className="ltr-djzpxj" style={styles.itemCount}>
                    {LABELS.USED_IN} <strong>{totalCount}</strong> {LABELS.ITEMS_SUFFIX}
                </span>
            )}
        </div>
    );
}

function TableHeader() {
    return (
        <div style={styles.tableHeader}>
            <div style={styles.headerNameColumn}>{LABELS.COLUMN_NAME}</div>
            <div style={styles.headerLanguageColumn}>{LABELS.COLUMN_LANGUAGE}</div>
            <div style={styles.headerVersionColumn}>{LABELS.COLUMN_VERSION}</div>
        </div>
    );
}

interface UsageItemRowProps {
    item: ProcessedUsageItem;
    isLast: boolean;
}

function UsageItemRow({ item, isLast }: UsageItemRowProps) {
    const displayItemName = item.itemName || LABELS.FALLBACK_VALUE;
    const displayItemPath = item.itemPath || LABELS.FALLBACK_VALUE;
    const displayLanguage = item.language || item.languageKey || LABELS.FALLBACK_VALUE;
    const displayVersion = item.version ?? LABELS.FALLBACK_VALUE;

    const rowStyle = {
        ...styles.tableRow,
        borderBottom: isLast ? 'none' : '1px solid #e0e0e0',
    };

    return (
        <a key={item.compositeKey} href={buildCmsItemUrl(item.itemId)} style={styles.link}>
            <div style={rowStyle}>
                <div style={styles.nameColumn}>
                    <h5 style={styles.itemName}>{displayItemName}</h5>
                    <p style={styles.itemPath}>
                        {LABELS.PATH_PREFIX} {displayItemPath}
                    </p>
                    <p style={styles.itemId}>
                        {LABELS.ITEM_ID_PREFIX} {item.itemId}
                    </p>
                </div>
                <div style={styles.languageColumn}>
                    <span style={styles.badge}>{displayLanguage}</span>
                </div>
                <div style={styles.versionColumn}>
                    <span style={styles.badge}>
                        {LABELS.VERSION_PREFIX}{displayVersion}
                    </span>
                </div>
            </div>
        </a>
    );
}

// ============================================================================
// Main Component
// ============================================================================

function AssetUsageTrackerComponent({ entity }: AssetUsageTrackerProps) {
    const processedItems = useProcessedUsageItems(entity);

    if (!entity) {
        return <ErrorMessage />;
    }

    if (processedItems.length === 0) {
        return <EmptyState />;
    }

    return (
        <div style={styles.wrapper}>
            <Header totalCount={processedItems.length} />
            <TableHeader />
            <div style={styles.scrollableContent}>
                {processedItems.map((item, index) => (
                    <UsageItemRow
                        key={item.compositeKey}
                        item={item}
                        isLast={index === processedItems.length - 1}
                    />
                ))}
            </div>
        </div>
    );
}

// ============================================================================
// Styles
// ============================================================================

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
        maxHeight: CONSTANTS.MAX_WRAPPER_HEIGHT,
    },
    headerRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
    },
    itemCount: {
        color: '#999',
        fontSize: '12px',
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
        color: '#c7c7c7ff',
        fontSize: '10px',
        fontWeight: 500,
    },
    headerNameColumn: {
        flex: 1,
    },
    headerLanguageColumn: {
        textAlign: 'center' as const,
    },
    headerVersionColumn: {
        minWidth: '60px',
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
        overflowY: 'auto' as const,
        maxHeight: CONSTANTS.MAX_CONTENT_HEIGHT,
    },
} as const;

// ============================================================================
// Factory Function
// ============================================================================

export function createAssetUsageTracker(container: HTMLElement): ContentHubComponent {
    const root = createRoot(container);

    return {
        render(context: ContentHubContext) {
            root.render(<AssetUsageTrackerComponent entity={context.entity} />);
        },
        unmount() {
            root.unmount();
        },
    };
}

// ============================================================================
// Registration
// ============================================================================

if (!window.ContentHub) {
    window.ContentHub = {
        registerComponent: () => {},
    };
}

window.ContentHub.registerComponent('AssetUsageTracker', createAssetUsageTracker);

export default createAssetUsageTracker;