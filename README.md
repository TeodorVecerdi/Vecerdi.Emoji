# Emoji Support for Unity

A lightweight package for handling emoji functionality in Unity games. This package provides tools to process, convert, and display emoji characters in Unity UI elements.

This is currently intended to be used with TextMeshPro labels as it renders emojis using `<sprite>` tags, but I may make it more generic in the future.

## Features

- Convert between emoji literals and code representations
- Support for compound emoji sequences (skin tones, ZWJ sequences, country flags, etc.)
- Fallback system for unsupported emoji characters based on your emoji sprite assets

## Installation

1. In your Unity project, open the Package Manager
2. Click the "+" button and select "Add package from git URL..."
3. Enter: `https://github.com/TeodorVecerdi/Vecerdi.Emoji.git`
4. Click "Add"

Alternatively, add the following to your `manifest.json`:

```json
{
    "dependencies": {
        "ai.vecerdi.emoji": "https://github.com/TeodorVecerdi/Vecerdi.Emoji.git"
    }
}
```

## Getting Started with an Emoji Pack

Ready-to-use emoji sprite packs are available as GitHub releases. These packs include pre-configured TextMeshPro sprite assets with a comprehensive collection of emoji sprites.

### Available Packs

1. **Microsoft Fluent UI (3D) Emoji Pack** (MIT License)
   - Complete set of Microsoft Fluent UI (3D) emojis
   - Can be used in any application
   - [Download from GitHub Releases](https://github.com/TeodorVecerdi/Vecerdi.Emoji/releases/tag/emoji-pack-fluentui)

2. **Extended Emoji Pack** (for educational purposes only)
   - Microsoft Fluent UI (3D) emojis
   - Apple Flag emojis
   - ⚠️ **Important:** Apple emojis are included for educational purposes only. Using Apple emojis in a commercial application may violate copyright laws. Apple is a trademark of Apple Inc., registered in the U.S. and other countries.
   - [Download from GitHub Releases](https://github.com/TeodorVecerdi/Vecerdi.Emoji/releases/tag/emoji-pack-extended)

### Importing Emoji Packs

1. Download the desired emoji pack from GitHub Releases
2. Extract the ZIP archive into your Unity project
3. The archive contains:
   ```
   - Textures/          # Contains atlas texture PNG files
   - Resources/         # Contains TMP sprite assets and emoji list
     - emoji-0.asset    # Main sprite asset
     - emoji-1.asset    # Additional sprite assets
     - ...
     - available-emojis.txt  # List of all available emoji codes
   ```

### Setting Up TextMeshPro

1. Open Project Settings (Edit > Project Settings)
2. Navigate to TextMeshPro > Settings
3. Set the `emoji-0` sprite asset as the Default Sprite Asset
   - All other sprite assets are configured as fallbacks automatically

### Loading Available Emojis

Use the following code to load the available emoji codes from the included text file:

```csharp
using System.Linq;
using Vecerdi.Emoji;
using UnityEngine;

public class EmojiManager : MonoBehaviour
{
    private EmojiProcessingManager _emojiProcessor;

    private void Awake()
    {
        // Load available emoji codes from the Resources folder
        var emojiCodesText = Resources.Load<TextAsset>("available-emojis");
        var availableEmojis = emojiCodesText.text
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => new EmojiCode(line.Trim()))
            .ToHashSet();

        // Create the emoji processor
        _emojiProcessor = new EmojiProcessingManager(availableEmojis);
    }

    public string ProcessText(string text)
    {
        return _emojiProcessor.ProcessText(text);
    }
}
```

## Requirements

- Tested with Unity 2022.3, 6000.0, and 6000.1
- TextMeshPro package for the included emoji sprite assets

## License

This package is licensed under the MIT License with restrictions on AI usage. See the LICENSE file for details.\
See THIRD-PARTY-NOTICES for information about third party libraries used in this package and their licenses.
