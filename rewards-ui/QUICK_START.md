# Quick Start Guide

## Setup

1. **Install dependencies:**
   ```bash
   cd rewards-ui
   npm install
   ```

2. **Start development server:**
   ```bash
   npm start
   ```
   This will start the React development server on http://localhost:3000

3. **Build and publish to game:**
   ```bash
   npm run publish
   ```
   This builds the app and copies `rewards-app.js` and `rewards-app.css` to `../Overrides/Resources/`

## Testing the UI

### View Rewards List
In the browser console or in your game integration:
```javascript
window.page = "list";
```

### Show Reward Popup
```javascript
window.showPopup = true;
window.newReward = {
    type: "daily",
    day: 3,
    item: "200 Credits",
    icon: "💰"
};
```

### Provide Custom Rewards Data
```javascript
window.rewardsData = {
    daily: {
        currentDay: 3,
        lastClaimed: "2024-01-15T10:30:00Z",
        rewards: [
            { day: 1, claimed: true, item: "100 Credits", icon: "💰" },
            // ... more rewards
        ]
    },
    monthly: { /* ... */ },
    special: [ /* ... */ ]
};
```

## Test Data

The app includes comprehensive test data in `App.js` and `test-data.json`:
- **Daily Rewards**: 7-day cycle with 3 claimed, 4 unclaimed
- **Monthly Rewards**: 3 months of rewards
- **Special Events**: New Year, Valentine's Day, Anniversary

## Color Scheme Reference

All colors match the existing game UI:
- Background: `rgb(25, 34, 41)`
- Primary: `rgb(250, 212, 122)` (golden yellow)
- Secondary: `rgb(27, 48, 56)` (dark teal)
- Text: `white` / `rgb(180, 221, 235)` (light blue)
- Borders: `rgb(50, 79, 77)` (teal-gray)

## File Structure

```
rewards-ui/
├── src/
│   ├── Components/
│   │   ├── rewards-list.jsx      # Main rewards panel
│   │   ├── rewards-popup.jsx      # Reward claim popup
│   │   ├── reward-item.jsx        # Individual reward card
│   │   └── styled-components.jsx  # Shared styled components
│   ├── App.js                     # Main app component
│   ├── App.css
│   ├── index.js
│   └── index.css
├── public/
│   ├── index.html
│   └── manifest.json
├── package.json
├── copy.js                        # Build script helper
├── test-data.json                 # Test data for review
└── README.md

```

## Integration with C# Backend

After running `npm run publish`, the files will be available in:
- `Overrides/Resources/rewards-app.js`
- `Overrides/Resources/rewards-app.css`

You can then load these in your C# code similar to how `npc-app.js` and `party-app.js` are loaded.

