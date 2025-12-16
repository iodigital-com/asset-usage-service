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

interface ArchiveAssetButtonProps {
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
    WARNING_SUFFIX: 'CMS items. Archiving may cause issues in your CMS!',
    NO_USAGE: 'Archive this asset? You can restore it later.',
    CONFIRM_LABEL: 'I am sure I want to archive this asset.',
} as const;

const LABELS = {
    ARCHIVE: 'Archive asset',
    OK: 'Archive',
    ARCHIVING: 'Archiving...',
    CANCEL: 'Cancel',
} as const;

// ============================================================================
// Main Component
// ============================================================================

function ArchiveAssetButtonComponent({client, entityId, entity, closeModal, culture}: ArchiveAssetButtonProps) {
    const [isArchiving, setIsArchiving] = useState(false);
    const [isConfirmed, setIsConfirmed] = useState(false);

    const usageCount = getUsageCount(entity);
    const hasUsage = usageCount > 0;
    const isPrimaryDisabled = hasUsage ? (!isConfirmed || isArchiving) : isArchiving;

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

    const redirectAfterArchive = (culture?: string) => {
        const targetCulture = culture || 'en-us';
        window.location.href = `/${targetCulture}/Assets`;
    };

    const handleArchive = async (): Promise<void> => {
        if (hasUsage && !isConfirmed) return;
        setIsArchiving(true);

        try {
            const internalClient = (client as any).internalClient;
            
            const token = internalClient._requestHeaders?.['x-requested-by']
                    || internalClient._requestHeaders?.['X-Requested-By']
                    || internalClient._client?.defaults?.headers?.common?.['x-requested-by']
                    || internalClient._client?.defaults?.headers?.['x-requested-by'];
            
            if (!token) {
                throw new Error('Anti-forgery token not found!');
            }
            
            await internalClient._client.post(
                `/api/entities/${entityId}/lifecycle/archive`,
                {},
                {
                    headers: {
                        'x-requested-by': token
                    }
                }
            );
            
            if (closeModal) closeModal();
            else tryCloseModal();
            setTimeout(() => redirectAfterArchive(culture), 300);
            
        } catch (error) {
            console.error('ERROR:', error);
            alert('Failed to archive the asset. Please try again, and contact support if the problem persists.');
            setIsArchiving(false);
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
                        disabled={isArchiving}
                        label={MESSAGES.CONFIRM_LABEL}
                        color="#6E3FFF"
                    />
                )}

                <ActionButtons
                    hasUsage={hasUsage}
                    isProcessing={isArchiving}
                    isPrimaryDisabled={isPrimaryDisabled}
                    onConfirm={handleArchive}
                    onCancel={handleCancel}
                    labels={{
                        PROCESSING: LABELS.ARCHIVING,
                        CONFIRM_WITH_USAGE: LABELS.ARCHIVE,
                        CONFIRM_NO_USAGE: LABELS.OK,
                        CANCEL: LABELS.CANCEL
                    }}
                    colors={{
                        primary: '#6E3FFF',
                        primaryHover: '#5932cc'
                    }}
                />
            </div>
        </div>
    );
}

// ============================================================================
// Factory Function
// ============================================================================

export default function createArchiveAssetButton(container: HTMLElement): ContentHubComponent {
    const root = createRoot(container);

    return {
        render(context: ContentHubContext): void {
            const { client, options, entity, closeModal, pageOptions } = context;
            const entityId = options?.entityId;

            if (!entityId) {
                console.error('No entityId found in options');
                return;
            }
            const culture = options?.culture || pageOptions?.culture;

            root.render(
                <ArchiveAssetButtonComponent 
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
