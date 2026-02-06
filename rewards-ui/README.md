# Rewards UI

A React-based UI component for displaying player rewards in the game. This UI shows daily rewards, monthly rewards, and special day rewards, with a popup notification when rewards are claimed.

## Features

- **Daily Rewards**: 7-day reward cycle with streak tracking
- **Monthly Rewards**: Monthly login bonuses and exclusive items
- **Special Day Rewards**: Event-based rewards for holidays and special occasions
- **Reward Popup**: Animated popup that appears when a player claims a reward
- **Tomorrow Preview**: Shows what reward the player will get tomorrow

## Tech Stack

- React 18.3.1
- styled-components 6.1.13
- react-scripts-cohtml 1.1.0 (custom build for Cohtml integration)
- postmessage-polyfill for game communication

## Color Scheme

The UI uses the same color scheme as other game UIs:
- **Background**: `rgb(25, 34, 41)` - Dark blue-gray
- **Primary Accent**: `rgb(250, 212, 122)` - Golden yellow
- **Secondary**: `rgb(27, 48, 56)` - Dark teal
- **Text Primary**: `white`
- **Text Secondary**: `rgb(180, 221, 235)` - Light blue
- **Borders**: `rgb(50, 79, 77)` - Teal-gray

## Development

### Install Dependencies
```bash
npm install
```

### Start Development Server
```bash
npm start
```

### Build for Production
```bash
npm run build
```

### Publish to Overrides/Resources
```bash
npm run publish
```

This will:
1. Build the React app
2. Copy the JS bundle to `../Overrides/Resources/rewards-app.js`
3. Copy the CSS bundle to `../Overrides/Resources/rewards-app.css`

## Usage in Game

The UI can be controlled via `window.page` and `window.showPopup`:

```javascript
// Show the rewards list
window.page = "list";

// Show the popup with a reward
window.showPopup = true;
window.newReward = {
    type: "daily",
    day: 3,
    item: "200 Credits",
    icon: "💰"
};
```

## Test Data

Test data is included in `App.js` for development and review. The test data includes:
- Daily rewards for 7 days (3 claimed, 4 unclaimed)
- Monthly rewards for 3 months
- Special day rewards (New Year, Valentine's Day, Anniversary)

## Components

- **App.js**: Main application component
- **rewards-list.jsx**: Main panel showing all rewards organized by tabs
- **rewards-popup.jsx**: Popup notification when a reward is claimed
- **reward-item.jsx**: Individual reward card component
- **styled-components.jsx**: Shared styled components matching game UI style

