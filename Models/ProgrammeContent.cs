using System;

namespace BbcSoundz.Models
{
    public class ProgrammeContent
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string BroadcastDate { get; set; } = string.Empty;
        public DateTime? BroadcastDateTime { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Genres { get; set; } = string.Empty;
        public string StructuredData { get; set; } = string.Empty;
        
        // Error handling
        public bool HasError { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
        
        // Helper properties
        public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
        public bool HasDescription => !string.IsNullOrEmpty(Description);
        public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
        public bool HasBrand => !string.IsNullOrEmpty(Brand);
        public bool HasDuration => !string.IsNullOrEmpty(Duration);
        public bool HasBroadcastDate => !string.IsNullOrEmpty(BroadcastDate);
        public bool HasGenres => !string.IsNullOrEmpty(Genres);
    }
}
