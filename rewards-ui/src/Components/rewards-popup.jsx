import React, { useState, useEffect } from 'react';
import {
    PopupOverlay,
    PopupContainer,
    PopupTitle,
    PopupIcon,
    PopupRewardText,
    PopupMessage,
    PrimaryButton,
    CloseButton
} from './styled-components';

const RewardsPopup = ({ reward, onClose }) => {
    const [visible, setVisible] = useState(true);

    useEffect(() => {
        // Auto-close after 5 seconds if no interaction
        const timer = setTimeout(() => {
            if (visible) {
                handleClose();
            }
        }, 5000);

        return () => clearTimeout(timer);
    }, [visible]);

    const handleClose = () => {
        setVisible(false);
        if (onClose) {
            onClose();
        }
    };

    if (!visible) {
        return null;
    }

    const getRewardTypeText = (type) => {
        switch(type) {
            case 'daily':
                return 'Daily Reward';
            case 'monthly':
                return 'Monthly Reward';
            case 'special':
                return 'Special Reward';
            default:
                return 'Reward';
        }
    };

    return (
        <PopupOverlay onClick={handleClose}>
            <PopupContainer onClick={(e) => e.stopPropagation()}>
                <CloseButton onClick={handleClose} />
                <PopupTitle>🎉 Reward Claimed!</PopupTitle>
                <PopupIcon>{reward.icon || '🎁'}</PopupIcon>
                <PopupRewardText>{reward.item}</PopupRewardText>
                <PopupMessage>
                    You've received your {getRewardTypeText(reward.type)} for {reward.type === 'daily' ? `Day ${reward.day}` : 'this period'}!
                </PopupMessage>
                <PrimaryButton onClick={handleClose}>
                    Claim
                </PrimaryButton>
            </PopupContainer>
        </PopupOverlay>
    );
};

export default RewardsPopup;

