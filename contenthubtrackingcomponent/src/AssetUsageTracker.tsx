import { createRoot } from "react-dom/client";
import { useMemo, useState } from "react";

// ============================================================================
// Types & Interfaces
// ============================================================================

interface LanguageVersion {
    language: string;
    version?: number | null;
}

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
    languages?: LanguageVersion[];
}

interface AssetUsageTrackerProps {
    entity: ContentHubEntity | null;
}

interface ProcessedUsageItem {
    compositeKey: string;
    itemId: string;
    languageKey: string;
    itemName?: string;
    itemPath?: string;
    languages: LanguageVersion[];
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
    COLUMN_LANGUAGES: 'Languages',
    PATH_PREFIX: 'Path:',
    ITEM_ID_PREFIX: 'Item ID:',
    VERSION_PREFIX: 'V',
    FALLBACK_VALUE: '-',
    SHOW_MORE: 'Show all',
    SHOW_LESS: 'Show less',
    MAX_VISIBLE_LANGUAGES: 2,
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

        return Object.entries(languageItems).map(([itemId, itemData]) => {
            const languages: LanguageVersion[] = itemData.languages && itemData.languages.length > 0
                ? itemData.languages
                : itemData.language
                    ? [{ language: itemData.language, version: itemData.version ?? 1 }]
                    : [];

            return {
                compositeKey: `${languageKey}-${itemId}`,
                itemId,
                languageKey,
                itemName: itemData.itemName,
                itemPath: itemData.itemPath,
                languages,
            };
        });
    });
}

function buildCmsItemUrl(itemId: string): string {
    return `${CONSTANTS.CMS_BASE_URL}/${itemId}`;
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
            <div style={styles.headerLanguagesColumn}>{LABELS.COLUMN_LANGUAGES}</div>
        </div>
    );
}

interface UsageItemRowProps {
    item: ProcessedUsageItem;
    isLast: boolean;
}

function UsageItemRow({ item, isLast }: UsageItemRowProps) {
    const [expanded, setExpanded] = useState(false);
    const displayItemName = item.itemName || LABELS.FALLBACK_VALUE;
    const displayItemPath = item.itemPath || LABELS.FALLBACK_VALUE;

    const rowStyle = {
        ...styles.tableRow,
        borderBottom: isLast ? 'none' : '1px solid #e0e0e0',
    };

    const hasMoreLanguages = item.languages.length > LABELS.MAX_VISIBLE_LANGUAGES;
    const visibleLanguages = expanded 
        ? item.languages 
        : item.languages.slice(0, LABELS.MAX_VISIBLE_LANGUAGES);
    const hiddenCount = item.languages.length - LABELS.MAX_VISIBLE_LANGUAGES;

    const handleToggle = (e: React.MouseEvent) => {
        e.preventDefault();
        e.stopPropagation();
        setExpanded(!expanded);
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
                <div style={styles.languagesColumn}>
                    {item.languages.length > 0 ? (
                        <div style={styles.languagesTableWrapper}>
                            <div style={styles.languageCount}>
                                Used in <span style={styles.countNumberInBadge}>{item.languages.length}</span> {item.languages.length === 1 ? 'language' : 'languages'}
                            </div>
                            <div style={styles.languagesList}>
                                {visibleLanguages.map((lang, idx) => (
                                    <div 
                                        key={`${lang.language}-${idx}`} 
                                        style={{
                                            ...styles.languageRow,
                                            borderBottom: (idx < visibleLanguages.length - 1 || hasMoreLanguages) ? '1px solid #eee' : 'none',
                                        }}
                                    >
                                        <span style={styles.langLabel}>Lang:</span>
                                        <span style={styles.langValue}>{lang.language}</span>
                                        <span style={styles.versionLabel}>Version:</span>
                                        <span style={styles.versionValue}>{lang.version}</span>
                                    </div>
                                ))}
                                {hasMoreLanguages && (
                                    <div 
                                        style={styles.toggleRow}
                                        onClick={handleToggle}
                                    >
                                        {expanded 
                                            ? LABELS.SHOW_LESS 
                                            : `+${hiddenCount} more`}
                                    </div>
                                )}
                            </div>
                        </div>
                    ) : (
                        <span style={styles.badge}>{LABELS.FALLBACK_VALUE}</span>
                    )}
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
    headerLanguagesColumn: {
        minWidth: '200px',
        maxWidth: '200px',
        textAlign: 'right' as const,
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
    languagesColumn: {
        minWidth: '200px',
        maxWidth: '200px',
        display: 'flex',
        justifyContent: 'flex-end',
        alignItems: 'flex-start',
    },
    languagesTableWrapper: {
        display: 'flex',
        flexDirection: 'column' as const,
        alignItems: 'flex-end',
        gap: '4px',
    },
    languageCount: {
        fontSize: '10px',
        color: '#c0c0c0ff',
        fontWeight: 500,
        textAlign: 'right' as const,
    },
    countNumberInBadge: {
        fontWeight: 700,
        color: '#535353',
        margin: '0 2px',
    },
    languagesList: {
        display: 'flex',
        flexDirection: 'column' as const,
        backgroundColor: '#fafafa',
        borderRadius: '4px',
        border: '1px solid #eee',
        overflow: 'hidden',
        width: '100%',
    },
    languageRow: {
        display: 'grid',
        gridTemplateColumns: '32px 50px 48px 8px',
        alignItems: 'center',
        padding: '4px 8px',
        fontSize: '11px',
    },
    langLabel: {
        color: '#999',
        fontWeight: 500,
    },
    langValue: {
        color: '#333',
        fontWeight: 600,
    },
    versionLabel: {
        color: '#999',
        fontWeight: 500,
    },
    versionValue: {
        color: '#333',
        fontWeight: 600,
        textAlign: 'left' as const,
    },
    toggleRow: {
        padding: '4px 8px',
        fontSize: '11px',
        color: '#6E3FFF',
        fontWeight: 500,
        cursor: 'pointer',
        textAlign: 'center' as const,
        backgroundColor: '#f5f5f5',
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