# BBC Soundz Scraper

A windows WPF application for scraping BBC Radio schedules and downloading shows using yt-dlp.

## Features

- **Multi-Source Scraping**: Configurable BBC radio station schedule sources
- **Smart Filtering**: Search for specific shows (e.g., "Essential")
- **Date Range**: Searches up to 2 months back from current date
- **Download Integration**: Built-in yt-dlp integration for downloading shows
- **Visual Status**: Green indicators for already downloaded shows
- **Auto-Detection**: Automatically detects and displays existing downloads on startup
- **Default Media Player**: Uses Windows default file association for playback

## Configuration

### BBC Schedule Sources

The application uses `appsettings.json` to configure which BBC radio stations to scrape. You can customize this file to add or remove sources.

**Default Configuration:**
```json
{
  "BbcScheduleSources": [
    {
      "Name": "BBC Radio 1",
      "Url": "https://www.bbc.co.uk/schedules/p00fzl86",
      "Description": "BBC Radio 1 - Pop and chart music"
    },
    {
      "Name": "BBC Radio 1Xtra", 
      "Url": "https://www.bbc.co.uk/schedules/p00fzl65",
      "Description": "BBC Radio 1Xtra - Urban music"
    },
    {
      "Name": "BBC Radio 2",
      "Url": "https://www.bbc.co.uk/schedules/p00fzl8v",
      "Description": "BBC Radio 2 - Popular music and culture"
    },
    {
      "Name": "BBC Radio 6 Music",
      "Url": "https://www.bbc.co.uk/schedules/p00fzl8q",
      "Description": "BBC Radio 6 Music - Alternative music"
    }
  ]
}
```

### Finding BBC Schedule URLs

To find additional BBC radio station schedule URLs:

1. Visit **https://www.bbc.co.uk/programmes**
2. Navigate to the radio station you want to add
3. Go to the schedule/programme guide section
4. Copy the URL format (it should look like `https://www.bbc.co.uk/schedules/[station-id]`)
5. Add it to your `appsettings.json` file

**Example Station IDs:**
- `p00fzl86` - BBC Radio 1
- `p00fzl65` - BBC Radio 1Xtra  
- `p00fzl8v` - BBC Radio 2
- `p00fzl8q` - BBC Radio 6 Music
- `p00fzl7j` - BBC Radio 4
- `p00fzl7g` - BBC Radio 3

## Usage

1. **Search**: Enter a show name filter (e.g., "Essential Mix") and click Search
2. **Download**: Select a show from the results and click Download
3. **Play**: Double-click on green (downloaded) items to play them
4. **Monitor**: Check the output panel for download progress and status

## Requirements

- .NET 9.0 or later
- Windows OS
- yt-dlp.exe (included in the application)
- Internet connection for scraping and downloading

## File Structure

```
BbcSoundz/
├── appsettings.json          # Configuration file for BBC sources
├── yt-dlp.exe               # YouTube downloader executable
├── Downloads/               # Downloaded files directory (auto-created)
├── Services/
│   ├── BbcScheduleScraper.cs    # Multi-source web scraping service
│   ├── YtDlpService.cs          # Download service
│   └── DownloadManager.cs       # File management and status tracking
├── Models/
│   └── ShowInfo.cs              # Data model for show information
├── Configuration/
│   └── AppSettings.cs           # Configuration classes
└── Helpers/
    └── RichTextBoxHelper.cs     # Console-like output formatting
```

## Notes

- The application automatically scans the Downloads folder on startup
- Downloaded files are marked with green indicators in the results list
- The scraper uses parallel processing for better performance (max 4 concurrent requests)
- Files are played using the Windows default file association (like double-clicking in Explorer)
- UTF-8 encoding is properly handled for international characters
