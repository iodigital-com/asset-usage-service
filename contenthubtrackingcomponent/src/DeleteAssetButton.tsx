import { createRoot } from "react-dom/client";
import { useState } from "react";

// ============================================================================
// Types & Interfaces
// ============================================================================

interface ContentHubClient {
    entities: {
        deleteAsync: (entityId: number) => Promise<void>;
    };
}

interface ContentHubEntity {
    id?: number;
    properties: {
        UsageTracking?: {
            [language: string]: {
                [itemId: string]: unknown;
            };
        };
    };
}

interface ContentHubOptions {
    entityId?: number;
    culture?: string;
}

interface PageOptions {
    culture?: string;
    back_destination?: string | null;
    data?: {
        menus?: {
            navigation?: Array<{
                name: string;
                relativeurl?: string;
            }>;
        };
    };
}

interface ContentHubContext {
    client: ContentHubClient;
    options: ContentHubOptions;
    entity?: ContentHubEntity | null;
    closeModal?: () => void;
    pageOptions?: PageOptions;
}

interface DeleteAssetButtonProps {
    client: ContentHubClient;
    entityId: number;
    entity?: ContentHubEntity | null;
    closeModal?: () => void;
    culture?: string;
}

interface ContentHubComponent {
    render: (context: ContentHubContext) => void;
    unmount: () => void;
}

// ============================================================================
// Constants
// ============================================================================

const MESSAGES = {
    WARNING_PREFIX: 'The asset is used in',
    WARNING_SUFFIX: 'CMS items. And if you delete it the CMS items will have a broken asset!',
    NO_USAGE: 'Delete item permanently? This cannot be undone.',
    CONFIRM_LABEL: 'I am sure i want to delete this asset.',
} as const;

const LABELS = {
    DELETE: 'Delete asset',
    OK: 'Delete',
    DELETING: 'Deleting...',
    CANCEL: 'Cancel',
} as const;

const FALLBACK_ROUTES = {
    DEFAULT_CULTURE: 'en-us',
    DEFAULT_PAGE: 'assets',
} as const;

// ============================================================================
// Utility Functions
// ============================================================================

function getUsageCount(entity?: ContentHubEntity | null): number {
    if (!entity?.properties?.UsageTracking) {
        return 0;
    }

    return Object.values(entity.properties.UsageTracking).reduce((total, languageItems) => {
        return total + Object.keys(languageItems || {}).length;
    }, 0);
}

function findAssetsPageUrl(): string | null {
    try {
        const pageOptions = (window as any).PAGE_OPTIONS;
        if (pageOptions?.data?.menus?.navigation) {
            const assetsPage = pageOptions.data.menus.navigation.find(
                (item: any) => item.name === 'Assets' || item.title === 'Assets'
            );
            if (assetsPage?.relativeurl) {
                return assetsPage.relativeurl;
            }
        }
    } catch (error) {
        // Ignore errors and fallback
    }
    return null;
}

function buildRedirectUrl(culture?: string): string {
    const activeCulture = culture || FALLBACK_ROUTES.DEFAULT_CULTURE;
    
    const assetsUrl = findAssetsPageUrl();
    if (assetsUrl) {
        return assetsUrl;
    }
    
    return `/${activeCulture}/${FALLBACK_ROUTES.DEFAULT_PAGE}`;
}

function redirectAfterDelete(culture?: string): void {
    const url = buildRedirectUrl(culture);
    window.location.href = url;
}

function tryCloseModal(): boolean {
    const closeButton = document.querySelector('[aria-label="Close"], .modal-close, .close, [data-dismiss="modal"]');
    if (closeButton && closeButton instanceof HTMLElement) {
        closeButton.click();
        return true;
    }
    
    return false;
}

// ============================================================================
// Sub-Components
// ============================================================================

interface CustomCheckboxProps {
    checked: boolean;
    onChange: (checked: boolean) => void;
    disabled?: boolean;
    label: string;
}

function CustomCheckbox({ checked, onChange, disabled = false, label }: CustomCheckboxProps) {
    return (
        <label style={{
            ...styles.checkboxLabel,
            cursor: disabled ? 'not-allowed' : 'pointer',
            opacity: disabled ? 0.6 : 1,
        }}>
            <input
                type="checkbox"
                checked={checked}
                onChange={(e) => onChange(e.target.checked)}
                disabled={disabled}
                style={styles.hiddenCheckbox}
            />
            <div style={{
                ...styles.customCheckbox,
                borderColor: checked ? '#6E3FFF' : '#d0d0d0',
                backgroundColor: checked ? '#6E3FFF' : '#fff',
            }}>
                {checked && (
                    <svg width="12" height="10" viewBox="0 0 12 10" fill="none" style={styles.checkmark}>
                        <path d="M1 5L4.5 8.5L11 1" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
                    </svg>
                )}
            </div>
            <span style={styles.checkboxText}>{label}</span>
        </label>
    );
}

interface ActionButtonsProps {
    hasUsage: boolean;
    isDeleting: boolean;
    isPrimaryDisabled: boolean;
    onDelete: () => void;
    onCancel: () => void;
}

function ActionButtons({ hasUsage, isDeleting, isPrimaryDisabled, onDelete, onCancel }: ActionButtonsProps) {
    const [isHoveringPrimary, setIsHoveringPrimary] = useState(false);
    const [isHoveringCancel, setIsHoveringCancel] = useState(false);

    return (
        <div style={{
            ...styles.buttonContainer,
            justifyContent: 'flex-end',
            flex: hasUsage ? 'none' : 1,
        }}>
            <button
                onClick={onDelete}
                disabled={isPrimaryDisabled}
                onMouseEnter={() => setIsHoveringPrimary(true)}
                onMouseLeave={() => setIsHoveringPrimary(false)}
                style={{
                    ...styles.primaryButton,
                    opacity: isPrimaryDisabled ? 0.5 : 1,
                    cursor: isPrimaryDisabled ? 'not-allowed' : 'pointer',
                    backgroundColor: isHoveringPrimary && !isPrimaryDisabled ? 'rgba(221, 82, 98, 0.9)' : '#EB001A',
                }}
                aria-busy={isDeleting}
            >
                {isDeleting ? LABELS.DELETING : (hasUsage ? LABELS.DELETE : LABELS.OK)}
            </button>
            <button
                onClick={onCancel}
                disabled={isDeleting}
                onMouseEnter={() => setIsHoveringCancel(true)}
                onMouseLeave={() => setIsHoveringCancel(false)}
                style={{
                    ...styles.cancelButton,
                    backgroundColor: isHoveringCancel && !isDeleting ? '#f5f5f5' : 'transparent',
                    cursor: isDeleting ? 'not-allowed' : 'pointer',
                }}
            >
                {LABELS.CANCEL}
            </button>
        </div>
    );
}

// ============================================================================
// Main Component
// ============================================================================

function DeleteAssetButtonComponent({client, entityId, entity, closeModal, culture}: DeleteAssetButtonProps) {
    const [isDeleting, setIsDeleting] = useState(false);
    const [isConfirmed, setIsConfirmed] = useState(false);

    const usageCount = getUsageCount(entity);
    const hasUsage = usageCount > 0;
    const isPrimaryDisabled = hasUsage ? (!isConfirmed || isDeleting) : isDeleting;

    const handleCancel = () => {
        if (closeModal) {
            closeModal();
            return;
        }
        
        const closed = tryCloseModal();
        
        if (!closed) {
            window.location.reload();
        }
    };

    const handleDelete = async (): Promise<void> => {
        if (hasUsage && !isConfirmed) return;

        setIsDeleting(true);

        try {
            await client.entities.deleteAsync(entityId);
            
            if (closeModal) {
                closeModal();
            } else {
                tryCloseModal();
            }
            
            setTimeout(() => {
                redirectAfterDelete(culture);
            }, 300);
            
        } catch (error) {
            console.error(`Error deleting asset ${entityId}:`, error);            
            setIsDeleting(false);
        }
    };
    
    return (
        <div style={styles.container}>
            <div style={styles.messageContainer}>
                {hasUsage ? (
                    <p style={styles.warningText}>
                        {MESSAGES.WARNING_PREFIX} <strong style={styles.warningStrong}>{usageCount}</strong> {MESSAGES.WARNING_SUFFIX}
                    </p>
                ) : (
                    <p style={styles.simpleText}>{MESSAGES.NO_USAGE}</p>
                )}
            </div>

            <div style={styles.actionRow}>
                {hasUsage && (
                    <CustomCheckbox
                        checked={isConfirmed}
                        onChange={setIsConfirmed}
                        disabled={isDeleting}
                        label={MESSAGES.CONFIRM_LABEL}
                    />
                )}

                <ActionButtons
                    hasUsage={hasUsage}
                    isDeleting={isDeleting}
                    isPrimaryDisabled={isPrimaryDisabled}
                    onDelete={handleDelete}
                    onCancel={handleCancel}
                />
            </div>
        </div>
    );
}

// ============================================================================
// Styles
// ============================================================================

const styles = {
    container: {
        fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
        paddingBottom: '10px',
        maxWidth: '100%',
    },
    messageContainer: {
        marginBottom: '28px',
    },
    warningText: {
        margin: 0,
        fontSize: '14px',
        color: '#616161',
        lineHeight: '1.6',
    },
    simpleText: {
        margin: 0,
        fontSize: '14px',
        color: '#424242',
        lineHeight: '1.6',
    },
    warningStrong: {
        fontWeight: 600,
        color: '#1a1a1a',
    },
    actionRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '16px',
        minHeight: '40px',
    },
    checkboxLabel: {
        display: 'flex',
        alignItems: 'center',
        gap: '10px',
        userSelect: 'none' as const,
        flex: 1,
        transition: 'opacity 0.2s ease',
    },
    hiddenCheckbox: {
        position: 'absolute' as const,
        opacity: 0,
        width: 0,
        height: 0,
        margin: 0,
        padding: 0,
    },
    customCheckbox: {
        width: '20px',
        height: '20px',
        borderRadius: '4px',
        border: '2px solid',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
        transition: 'all 0.2s ease',
        position: 'relative' as const,
    },
    checkmark: {
        display: 'block',
    },
    checkboxText: {
        fontSize: '14px',
        color: '#1a1a1a',
        lineHeight: '1.5',
    },
    buttonContainer: {
        display: 'flex',
        gap: '12px',
        flexShrink: 0,
    },
    primaryButton: {
        padding: '10px 24px',
        backgroundColor: '#6E3FFF',
        color: '#fff',
        border: 'none',
        borderRadius: '24px',
        fontSize: '14px',
        fontWeight: 500,
        transition: 'all 0.2s ease',
        whiteSpace: 'nowrap' as const,
        outline: 'none',
        letterSpacing: '0.2px',
    },
    cancelButton: {
        padding: '10px 24px',
        backgroundColor: 'transparent',
        color: '#616161',
        border: 'none',
        borderRadius: '24px',
        fontSize: '14px',
        fontWeight: 500,
        transition: 'all 0.2s ease',
        whiteSpace: 'nowrap' as const,
        outline: 'none',
        letterSpacing: '0.2px',
    },
} as const;

// ============================================================================
// Factory Function
// ============================================================================

export default function createDeleteAssetButton(container: HTMLElement): ContentHubComponent {
    const root = createRoot(container);

    return {
        render(context: ContentHubContext): void {
            const { client, options, entity, closeModal, pageOptions } = context;
            const entityId = options?.entityId;

            if (!entityId) {
                console.error('No entityId found in options');
                return;
            }

            if (!client?.entities?.deleteAsync) {
                console.error('Client or deleteAsync method not available');
                return;
            }

            const culture = options?.culture || pageOptions?.culture;

            root.render(
                <DeleteAssetButtonComponent 
                    client={client} 
                    entityId={entityId}
                    entity={entity}
                    closeModal={closeModal}
                    culture={culture}
                />
            );
        },
        unmount(): void {
            root.unmount();
        },
    };
}
