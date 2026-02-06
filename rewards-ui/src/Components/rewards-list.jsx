import React, { useState } from 'react';
import {
    Container,
    Panel,
    Header,
    Title,
    PanelBody,
    TabContainer,
    Tab,
    CloseButton,
    RewardGrid,
    SectionTitle,
    InfoText
} from './styled-components';
import RewardItemComponent from './reward-item';

const RewardsList = ({ rewardsData, onClose }) => {
    const [activeTab, setActiveTab] = useState('daily');

    const getTomorrowReward = () => {
        if (activeTab === 'daily') {
            const tomorrowDay = rewardsData.daily.currentDay + 1;
            const tomorrowReward = rewardsData.daily.rewards.find(r => r.day === tomorrowDay);
            return tomorrowReward || null;
        }
        return null;
    };

    const tomorrowReward = getTomorrowReward();

    return (
        <Container>
            <Panel>
                <Header>
                    <Title>Player Rewards</Title>
                    {onClose && <CloseButton onClick={onClose} />}
                </Header>
                <PanelBody>
                    <TabContainer>
                        <Tab selected={activeTab === 'daily'} onClick={() => setActiveTab('daily')}>
                            Daily Rewards
                        </Tab>
                        <Tab selected={activeTab === 'monthly'} onClick={() => setActiveTab('monthly')}>
                            Monthly Rewards
                        </Tab>
                        <Tab selected={activeTab === 'special'} onClick={() => setActiveTab('special')}>
                            Special Events
                        </Tab>
                    </TabContainer>

                    {activeTab === 'daily' && (
                        <>
                            <InfoText>
                                Current streak: Day {rewardsData.daily.currentDay} of 7
                                {rewardsData.daily.lastClaimed && (
                                    <> • Last claimed: {new Date(rewardsData.daily.lastClaimed).toLocaleDateString()}</>
                                )}
                            </InfoText>
                            {tomorrowReward && (
                                <SectionTitle>Tomorrow's Reward</SectionTitle>
                            )}
                            {tomorrowReward && (
                                <RewardGrid>
                                    <RewardItemComponent 
                                        reward={tomorrowReward} 
                                        type="daily" 
                                        index={tomorrowReward.day}
                                    />
                                </RewardGrid>
                            )}
                            <SectionTitle>All Daily Rewards</SectionTitle>
                            <RewardGrid>
                                {rewardsData.daily.rewards.map((reward, index) => (
                                    <RewardItemComponent 
                                        key={reward.day} 
                                        reward={reward} 
                                        type="daily" 
                                        index={index}
                                    />
                                ))}
                            </RewardGrid>
                        </>
                    )}

                    {activeTab === 'monthly' && (
                        <>
                            <InfoText>
                                Current month: {rewardsData.monthly.currentMonth}
                                {rewardsData.monthly.lastClaimed && (
                                    <> • Last claimed: {new Date(rewardsData.monthly.lastClaimed).toLocaleDateString()}</>
                                )}
                            </InfoText>
                            <SectionTitle>Monthly Rewards</SectionTitle>
                            <RewardGrid>
                                {rewardsData.monthly.rewards.map((reward, index) => (
                                    <RewardItemComponent 
                                        key={reward.month} 
                                        reward={reward} 
                                        type="monthly" 
                                        index={index}
                                    />
                                ))}
                            </RewardGrid>
                        </>
                    )}

                    {activeTab === 'special' && (
                        <>
                            <InfoText>
                                Special event rewards and limited-time offers
                            </InfoText>
                            <SectionTitle>Special Day Rewards</SectionTitle>
                            <RewardGrid>
                                {rewardsData.special.map((reward) => (
                                    <RewardItemComponent 
                                        key={reward.id} 
                                        reward={reward} 
                                        type="special" 
                                        index={reward.id}
                                    />
                                ))}
                            </RewardGrid>
                        </>
                    )}
                </PanelBody>
            </Panel>
        </Container>
    );
};

export default RewardsList;

