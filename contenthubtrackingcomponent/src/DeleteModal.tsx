import { createRoot } from "react-dom/client";
import { useState } from "react";
import {
    getUsageCount,
    tryCloseModal,
    baseStyles,
    CustomCheckbox,
    ActionButtons
} from "./SharedModal";
import type {
    ContentHubComponent,
    ContentHubContext,
    ContentHubEntity,
    ContentHubClient
} from "./SharedModal";

// ============================================================================
// Types & Interfaces
// ============================================================================

interface DeleteAssetButtonProps {
    client: ContentHubClient;
    entityId: number;
    entity?: ContentHubEntity | null;
    closeModal?: () => void;
    culture?: string;
}

// ============================================================================
// Constants
// ============================================================================

const MESSAGES = {
    WARNING_PREFIX: 'The asset is used in',
    WARNING_SUFFIX: 'CMS items. And if you delete it the CMS items will have a broken asset!',
    NO_USAGE: 'Delete item permanently? This cannot be undone.',
    CONFIRM_LABEL: 'I am sure I want to delete this asset.',
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
            if (client.entities.deleteAsync) {
                await client.entities.deleteAsync(entityId);
            } else {
                throw new Error("deleteAsync not available on client");
            }
            
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
        <div style={baseStyles.container}>
            <div style={baseStyles.messageContainer}>
                {hasUsage ? (
                    <p style={baseStyles.warningText}>
                        {MESSAGES.WARNING_PREFIX} <strong style={baseStyles.warningStrong}>{usageCount}</strong> {MESSAGES.WARNING_SUFFIX}
                    </p>
                ) : (
                    <p style={baseStyles.simpleText}>{MESSAGES.NO_USAGE}</p>
                )}
            </div>

            <div style={baseStyles.actionRow}>
                {hasUsage && (
                    <CustomCheckbox
                        checked={isConfirmed}
                        onChange={setIsConfirmed}
                        disabled={isDeleting}
                        label={MESSAGES.CONFIRM_LABEL}
                        color="#6E3FFF"
                    />
                )}

                <ActionButtons
                    hasUsage={hasUsage}
                    isProcessing={isDeleting}
                    isPrimaryDisabled={isPrimaryDisabled}
                    onConfirm={handleDelete}
                    onCancel={handleCancel}
                    labels={{
                        PROCESSING: LABELS.DELETING,
                        CONFIRM_WITH_USAGE: LABELS.DELETE,
                        CONFIRM_NO_USAGE: LABELS.OK,
                        CANCEL: LABELS.CANCEL
                    }}
                    colors={{
                        primary: '#EB001A',
                        primaryHover: 'rgba(221, 82, 98, 0.9)'
                    }}
                />
            </div>
        </div>
    );
}

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
