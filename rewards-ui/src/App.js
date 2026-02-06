import './App.css';
import { pm } from 'postmessage-polyfill';
import { fetch as fetchPolyfill } from 'whatwg-fetch';
import RewardsPopup from "./Components/rewards-popup";
import RewardsList from "./Components/rewards-list";

const originalInterval = window.setInterval;
window.setInterval = function (callback, delay = 0) {
    return originalInterval(callback, delay);
}

window.postMessage = function (message) {
    pm({
        type: message.type,
        origin: 'http://127.0.0.1/:9000',
        target: window,
        data: message,
    });
};

// Test data - in production this would come from the game backend
const testRewardsData = {
    daily: {
        currentDay: 3,
        lastClaimed: "2024-01-15T10:30:00Z",
        rewards: [
            { day: 1, claimed: true, item: "100 Credits", icon: "💰" },
            { day: 2, claimed: true, item: "50 Energy Cells", icon: "⚡" },
            { day: 3, claimed: true, item: "200 Credits", icon: "💰" },
            { day: 4, claimed: false, item: "100 Quantum Cores", icon: "🔷" },
            { day: 5, claimed: false, item: "300 Credits", icon: "💰" },
            { day: 6, claimed: false, item: "150 Energy Cells", icon: "⚡" },
            { day: 7, claimed: false, item: "500 Credits + Rare Blueprint", icon: "🎁" },
        ]
    },
    monthly: {
        currentMonth: 1,
        lastClaimed: null,
        rewards: [
            { month: 1, claimed: false, item: "1000 Credits", icon: "💰", description: "Monthly Login Bonus" },
            { month: 2, claimed: false, item: "500 Quantum Cores", icon: "🔷", description: "Monthly Resource Pack" },
            { month: 3, claimed: false, item: "Epic Ship Blueprint", icon: "🚀", description: "Monthly Exclusive Blueprint" },
        ]
    },
    special: [
        {
            id: 1,
            name: "New Year Special",
            date: "2024-01-01",
            claimed: true,
            item: "2000 Credits + Special Decal",
            icon: "🎉",
            description: "Celebrate the new year!"
        },
        {
            id: 2,
            name: "Valentine's Day",
            date: "2024-02-14",
            claimed: false,
            item: "Heart-Shaped Container",
            icon: "❤️",
            description: "A special gift for Valentine's Day"
        },
        {
            id: 3,
            name: "Anniversary Event",
            date: "2024-03-15",
            claimed: false,
            item: "Exclusive Anniversary Ship",
            icon: "🎂",
            description: "Celebrate our anniversary!"
        },
    ],
    newReward: {
        type: "daily",
        day: 3,
        item: "200 Credits",
        icon: "💰"
    }
};

function App() {
    const page = window.page || "list";
    const showPopup = window.showPopup || false;
    const rewardData = window.rewardsData || testRewardsData;
    const newReward = window.newReward || testRewardsData.newReward;

    return (
        <div className="Mod_Rewards_App">
            {showPopup ? <RewardsPopup reward={newReward} /> : ""}
            {page === "list" ? <RewardsList rewardsData={rewardData} /> : ""}
        </div>
    );
}

export default App;

