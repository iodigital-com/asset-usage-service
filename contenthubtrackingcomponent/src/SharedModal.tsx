import React, { useState } from 'react';

// ============================================================================
// Types & Interfaces
// ============================================================================

export interface ContentHubClient {
    entities: {
        getAsync: (entityId: number) => Promise<ContentHubEntity>;
        saveAsync: (entity: ContentHubEntity) => Promise<void>;
        deleteAsync?: (entityId: number) => Promise<void>;
    };
    raw?: {
        postAsync: (url: string, data?: any) => Promise<any>;
    };
    internalClient: {
        _client: {
            post: (url: string, data?: any, config?: any) => Promise<any>;
        };
    };
}

export interface ContentHubEntity {
    id?: number;
    properties: {
        UsageTracking?: {
            [language: string]: {
                [itemId: string]: unknown;
            };
        };
    };
    setPropertyValue: (name: string, value: unknown) => void;
}

export interface ContentHubOptions {
    entityId?: number;
    culture?: string;
}

export interface PageOptions {
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

export interface ContentHubContext {
    client: ContentHubClient;
    options: ContentHubOptions;
    entity?: ContentHubEntity | null;
    closeModal?: () => void;
    pageOptions?: PageOptions;
}

export interface ContentHubComponent {
    render: (context: ContentHubContext) => void;
    unmount: () => void;
}

// ============================================================================
// Utility Functions
// ============================================================================

export function getUsageCount(entity?: ContentHubEntity | null): number {
    if (!entity?.properties?.UsageTracking) {
        return 0;
    }

    return Object.values(entity.properties.UsageTracking).reduce((total, languageItems) => {
        return total + Object.keys(languageItems || {}).length;
    }, 0);
}

export function tryCloseModal(): boolean {
    const closeButton = document.querySelector('[aria-label="Close"], .modal-close, .close, [data-dismiss="modal"]');
    if (closeButton && closeButton instanceof HTMLElement) {
        closeButton.click();
        return true;
    }
    
    return false;
}

// ============================================================================
// Styles
// ============================================================================

export const baseStyles = {
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
// Shared Components
// ============================================================================

export interface CustomCheckboxProps {
    checked: boolean;
    onChange: (checked: boolean) => void;
    disabled?: boolean;
    label: string;
    color?: string;
}

export function CustomCheckbox({ checked, onChange, disabled = false, label, color = '#F59E0B' }: CustomCheckboxProps) {
    return (
        <label style={{
            ...baseStyles.checkboxLabel,
            cursor: disabled ? 'not-allowed' : 'pointer',
            opacity: disabled ? 0.6 : 1,
        }}>
            <input
                type="checkbox"
                checked={checked}
                onChange={(e) => onChange(e.target.checked)}
                disabled={disabled}
                style={baseStyles.hiddenCheckbox}
            />
            <div style={{
                ...baseStyles.customCheckbox,
                borderColor: checked ? color : '#d0d0d0',
                backgroundColor: checked ? color : '#fff',
            }}>
                {checked && (
                    <svg width="12" height="10" viewBox="0 0 12 10" fill="none" style={baseStyles.checkmark}>
                        <path d="M1 5L4.5 8.5L11 1" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
                    </svg>
                )}
            </div>
            <span style={baseStyles.checkboxText}>{label}</span>
        </label>
    );
}

export interface ActionButtonsProps {
    hasUsage: boolean;
    isProcessing: boolean;
    isPrimaryDisabled: boolean;
    onConfirm: () => void;
    onCancel: () => void;
    labels: {
        PROCESSING: string;
        CONFIRM_WITH_USAGE: string;
        CONFIRM_NO_USAGE: string;
        CANCEL: string;
    };
    colors: {
        primary: string;
        primaryHover: string;
    };
}

export function ActionButtons({ hasUsage, isProcessing, isPrimaryDisabled, onConfirm, onCancel, labels, colors }: ActionButtonsProps) {
    const [isHoveringPrimary, setIsHoveringPrimary] = useState(false);
    const [isHoveringCancel, setIsHoveringCancel] = useState(false);

    return (
        <div style={{
            ...baseStyles.buttonContainer,
            justifyContent: 'flex-end',
            flex: hasUsage ? 'none' : 1,
        }}>
            <button
                onClick={onConfirm}
                disabled={isPrimaryDisabled}
                onMouseEnter={() => setIsHoveringPrimary(true)}
                onMouseLeave={() => setIsHoveringPrimary(false)}
                style={{
                    ...baseStyles.primaryButton,
                    opacity: isPrimaryDisabled ? 0.5 : 1,
                    cursor: isPrimaryDisabled ? 'not-allowed' : 'pointer',
                    backgroundColor: isHoveringPrimary && !isPrimaryDisabled ? colors.primaryHover : colors.primary,
                }}
                aria-busy={isProcessing}
            >
                {isProcessing ? labels.PROCESSING : (hasUsage ? labels.CONFIRM_WITH_USAGE : labels.CONFIRM_NO_USAGE)}
            </button>
            <button
                onClick={onCancel}
                disabled={isProcessing}
                onMouseEnter={() => setIsHoveringCancel(true)}
                onMouseLeave={() => setIsHoveringCancel(false)}
                style={{
                    ...baseStyles.cancelButton,
                    backgroundColor: isHoveringCancel && !isProcessing ? '#f5f5f5' : 'transparent',
                    cursor: isProcessing ? 'not-allowed' : 'pointer',
                }}
            >
                {labels.CANCEL}
            </button>
        </div>
    );
}
