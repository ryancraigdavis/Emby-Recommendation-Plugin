# Emby Test Collection Plugin

A simple Emby server plugin that creates a test collection containing the first 5 alphabetically sorted movies from your library.

## Description

This plugin demonstrates basic Emby plugin functionality by:
- Creating a collection named "Test_collection"
- Automatically populating it with the first 5 movies when sorted alphabetically
- Updating the collection on server startup

## Requirements

- Emby Server 4.x or later
- .NET Standard 2.0 compatible runtime
- At least 5 movies in your Emby library (works with fewer, but intended for 5+)

## Building the Plugin

### Prerequisites

- .NET SDK (compatible with .NET Standard 2.0)
- Visual Studio 2017+ or Visual Studio Code (optional)

### Build Instructions

1. Clone this repository:
   ```bash
   git clone https://github.com/ryancraigdavis/Emby-Recommendation-Plugin.git
   cd Emby-Recommendation-Plugin
   ```

2. Build the plugin:
   ```bash
   dotnet restore
   dotnet build --configuration Release
   ```

3. The compiled DLL will be located at:
   ```
   bin/Release/netstandard2.0/EmbyTestPlugin.dll
   ```

## Installation

### Method 1: Manual Installation

1. Locate your Emby Server's plugin directory:
   - **Windows**: `C:\Users\[Username]\AppData\Roaming\Emby-Server\plugins`
   - **Linux**: `/var/lib/emby/plugins`
   - **Docker**: `/config/plugins` (or your mapped config directory)

2. Create a new folder for the plugin:
   ```
   [plugins-directory]/EmbyTestPlugin/
   ```

3. Copy the built DLL to this folder:
   ```
   [plugins-directory]/EmbyTestPlugin/EmbyTestPlugin.dll
   ```

4. Restart your Emby Server

### Method 2: Plugin Catalog (Future)

This plugin is not yet available in the official Emby plugin catalog.

## Testing the Plugin

### Step 1: Verify Installation

1. Open your Emby Server dashboard
2. Navigate to **Dashboard > Plugins**
3. You should see "Test Collection Plugin" in the installed plugins list
4. The plugin description should read: "A simple test plugin that creates a collection of the first 5 alphabetical movies"

### Step 2: Check Server Logs

1. Navigate to **Dashboard > Logs**
2. Look for log entries from the plugin:
   ```
   Test Collection Plugin starting up
   Creating/updating collection: Test_collection
   Selected 5 movies for collection:
     - [Movie 1]
     - [Movie 2]
     - [Movie 3]
     - [Movie 4]
     - [Movie 5]
   Collection 'Test_collection' created successfully with 5 movies
   ```

### Step 3: Verify the Collection

1. In your Emby client (web, app, etc.), navigate to **Collections**
2. You should see a collection named "Test_collection"
3. Open the collection and verify it contains exactly 5 movies
4. Verify the movies are sorted alphabetically (A-Z)

### Expected Behavior

- The collection is created automatically 10 seconds after server startup
- If the collection already exists, it will be updated with the current first 5 alphabetical movies
- The plugin uses the movie's sort name for alphabetical ordering
- If your library has fewer than 5 movies, the collection will contain all available movies

### Troubleshooting

**Plugin doesn't appear in the dashboard:**
- Verify the DLL is in the correct plugins folder
- Check file permissions (Emby needs read access)
- Restart the Emby Server
- Check server logs for plugin loading errors

**Collection is not created:**
- Ensure you have at least one movie in your library
- Check server logs for error messages
- Verify the library scan has completed
- Wait at least 15 seconds after server startup

**Collection is empty:**
- Verify movies exist in your library
- Check that movies are not virtual items
- Review server logs for "No movies found" messages
- Ensure library scanning is complete

**Collection has wrong movies:**
- The plugin uses `SortName` for alphabetical ordering
- Check your movies' metadata and sort names
- Some movies may have special characters that affect sorting

## Configuration

Currently, the plugin has minimal configuration options. The collection name and movie count are set in the `PluginConfiguration.cs` file:

- **CollectionName**: "Test_collection" (default)
- **MovieCount**: 5 (default)

Future versions may include a web UI for configuration.

## Development Notes

### Plugin Structure

```
EmbyTestPlugin/
├── Configuration/
│   └── PluginConfiguration.cs    # Plugin configuration model
├── Plugin.cs                      # Main plugin class
├── ServerEntryPoint.cs            # Server lifecycle and logic
├── EmbyTestPlugin.csproj          # Project file
└── thumb.png                      # Plugin thumbnail
```

### Key Components

- **Plugin.cs**: Defines plugin metadata (name, ID, description)
- **ServerEntryPoint.cs**: Implements `IServerEntryPoint` for server lifecycle hooks
- **PluginConfiguration.cs**: Extends `BasePluginConfiguration` for settings

### API Version

This plugin uses **MediaBrowser.Server.Core 4.9.1.90** (April 2024)

## License

MIT License - See LICENSE file for details

## Support

For issues or questions:
- Create an issue in this repository
- Check Emby server logs for detailed error messages
- Visit the Emby Community forums

## Future Enhancements

Potential improvements for this test plugin:
- Web-based configuration UI
- Configurable collection name
- Configurable number of movies
- Different sorting options (by rating, date added, etc.)
- Automatic refresh on library changes
- Multiple test collections
