import React from 'react';
import {
    RewardCard,
    RewardDay,
    RewardIcon,
    RewardItem,
    RewardDescription,
    ClaimedBadge
} from './styled-components';

const RewardItemComponent = ({ reward, type, index }) => {
    const dayOrMonth = type === 'daily' ? reward.day : reward.month;
    const isClaimed = reward.claimed;

    return (
        <RewardCard claimed={isClaimed}>
            {type === 'daily' && <RewardDay>Day {dayOrMonth}</RewardDay>}
            {type === 'monthly' && <RewardDay>Month {dayOrMonth}</RewardDay>}
            {isClaimed && <ClaimedBadge>Claimed</ClaimedBadge>}
            <RewardIcon>{reward.icon || '🎁'}</RewardIcon>
            <RewardItem>{reward.item}</RewardItem>
            {reward.description && (
                <RewardDescription>{reward.description}</RewardDescription>
            )}
        </RewardCard>
    );
};

export default RewardItemComponent;

