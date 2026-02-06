import styled from 'styled-components'

export const Container = styled.div`
    display: flex;
    justify-content: center;
    align-items: center;
    height: 100vh;
    z-index: 99999999 !important;
`;

export const Panel = styled.div`
    min-width: 1000px;
    width: 55vw;
    height: 75vh;
    background-color: rgb(25, 34, 41);
    border: 1px solid rgb(50, 79, 77);
    padding: 2px;
    z-index: 99999999 !important;
    display: flex;
    flex-direction: column;
`;

export const Header = styled.div`
    background-color: rgb(0, 0, 0);
    padding: 16px;
    display: flex;
    justify-content: space-between;
    align-items: center;
`;

export const Title = styled.span`
    color: white;
    font-size: 18px;
    margin-right: 10px;
`;

export const PanelCloseButton = styled.button`
    margin-left: auto;
    background-color: transparent;
    border: none;
    font-size: 40px;
    line-height: 0;
    cursor: pointer;
    padding: 0;
    color: white;

    line {
        stroke: white;
    }

    &:hover {
        line {
            stroke: rgb(250, 212, 122);
        }
    }
`;

export const PanelBody = styled.div`
    color: white;
    display: flex;
    flex-direction: column;
    flex-grow: 1;
    padding: 16px;
    overflow-y: auto;
`;

export const TabContainer = styled.div`
    display: flex;
    gap: 8px;
    margin-bottom: 16px;
`;

export const SelectedCategoryButton = styled.button`
    background-color: rgb(250, 212, 122);
    padding: 16px;
    font-weight: bold;
    text-transform: uppercase;
    color: black;
    text-align: left;
    border: none;
    display: block;
    cursor: pointer;
    flex: 1;
`;

export const UnselectedCategoryButton = styled.button`
    display: block;
    background-color: rgb(27, 48, 56);
    padding: 16px;
    font-weight: bold;
    text-align: left;
    text-transform: uppercase;
    color: rgb(180, 221, 235);
    border: none;
    cursor: pointer;
    flex: 1;
`;

export const Tab = (props) => {
    return props.selected ? (<SelectedCategoryButton onClick={props.onClick}>{props.children}</SelectedCategoryButton>) :
        (<UnselectedCategoryButton onClick={props.onClick}>{props.children}</UnselectedCategoryButton>);
}

export const CloseButton = (props) => {
    return (
        <PanelCloseButton onClick={props.onClick}>
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="24" height="24">
                <line x1="4" y1="4" x2="20" y2="20" stroke="white" strokeWidth="2" strokeLinecap="round"/>
                <line x1="4" y1="20" x2="20" y2="4" stroke="white" strokeWidth="2" strokeLinecap="round"/>
            </svg>
        </PanelCloseButton>
    );
}

export const RewardGrid = styled.div`
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
    gap: 16px;
    margin-top: 16px;
`;

export const RewardCard = styled.div`
    background-color: ${props => props.claimed ? 'rgba(27, 48, 56, 0.5)' : 'rgb(27, 48, 56)'};
    border: 2px solid ${props => props.claimed ? 'rgb(50, 79, 77)' : 'rgb(250, 212, 122)'};
    border-radius: 4px;
    padding: 16px;
    display: flex;
    flex-direction: column;
    align-items: center;
    text-align: center;
    position: relative;
    opacity: ${props => props.claimed ? '0.6' : '1'};
    transition: all 0.3s ease;
    
    &:hover {
        border-color: rgb(250, 212, 122);
        transform: translateY(-2px);
    }
`;

export const RewardDay = styled.div`
    position: absolute;
    top: 8px;
    left: 8px;
    background-color: rgb(250, 212, 122);
    color: black;
    padding: 4px 8px;
    border-radius: 4px;
    font-size: 12px;
    font-weight: bold;
`;

export const RewardIcon = styled.div`
    font-size: 48px;
    margin: 16px 0;
`;

export const RewardItem = styled.div`
    color: white;
    font-size: 14px;
    font-weight: bold;
    margin-bottom: 8px;
`;

export const RewardDescription = styled.div`
    color: rgb(180, 221, 235);
    font-size: 12px;
    margin-top: 4px;
`;

export const ClaimedBadge = styled.div`
    position: absolute;
    top: 8px;
    right: 8px;
    background-color: rgb(50, 79, 77);
    color: rgb(180, 221, 235);
    padding: 4px 8px;
    border-radius: 4px;
    font-size: 10px;
    text-transform: uppercase;
`;

export const PopupOverlay = styled.div`
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.8);
    display: flex;
    justify-content: center;
    align-items: center;
    z-index: 99999999 !important;
`;

export const PopupContainer = styled.div`
    background-color: rgb(25, 34, 41);
    border: 2px solid rgb(250, 212, 122);
    border-radius: 8px;
    padding: 32px;
    min-width: 400px;
    max-width: 500px;
    text-align: center;
    position: relative;
`;

export const PopupTitle = styled.h2`
    color: rgb(250, 212, 122);
    font-size: 24px;
    margin-bottom: 16px;
    text-transform: uppercase;
`;

export const PopupIcon = styled.div`
    font-size: 64px;
    margin: 16px 0;
`;

export const PopupRewardText = styled.div`
    color: white;
    font-size: 18px;
    font-weight: bold;
    margin: 16px 0;
`;

export const PopupMessage = styled.div`
    color: rgb(180, 221, 235);
    font-size: 14px;
    margin: 16px 0;
`;

export const PrimaryButton = styled.button`
    background-color: rgb(250, 212, 122);
    padding: 12px 24px;
    font-weight: bold;
    text-transform: uppercase;
    color: black;
    border: none;
    cursor: pointer;
    margin-top: 16px;
    font-size: 16px;
    
    &:hover {
        background-color: rgb(255, 230, 150);
    }
`;

export const SectionTitle = styled.h3`
    color: rgb(250, 212, 122);
    font-size: 20px;
    margin: 24px 0 16px 0;
    text-transform: uppercase;
    border-bottom: 2px solid rgb(50, 79, 77);
    padding-bottom: 8px;
`;

export const InfoText = styled.div`
    color: rgb(180, 221, 235);
    font-size: 14px;
    margin-bottom: 16px;
    font-style: italic;
`;

