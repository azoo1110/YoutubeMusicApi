﻿using System;
using System.Collections.Generic;
using System.Text;

namespace YoutubeMusicApi.Models.Search
{
    public class ArtistResult
    {
        public SearchResultType Type { get; set; } = SearchResultType.Artist;
        public List<Thumbnail> Thumbnails { get; set; } = new List<Thumbnail>();
        public string BrowseId { get; set; }
        public string Artist { get; set; }

        private static readonly int IndexInRuns = 0;
        private static readonly int IndexInColumnsForArtistName = 0;

        public ArtistResult(Content content)
        {
            Thumbnails = ContentStaticHelpers.GetThumbnails(content);
            BrowseId = content.MusicResponsiveListItemRenderer.NavigationEndpoint.BrowseEndpoint.BrowseId;
            Artist = content.MusicResponsiveListItemRenderer.FlexColumns[IndexInColumnsForArtistName].MusicResponsiveListItemFlexColumnRenderer.Text.Runs[IndexInRuns].Text;
        }
    }
}
