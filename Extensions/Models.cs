using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Shoko.Commons.Languages;
using Shoko.Commons.Properties;
using Shoko.Commons.Utils;
using Shoko.Models.Azure;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Models.TvDB;

namespace Shoko.Commons.Extensions
{
    public static class Models
    {
        //TODO Move this to a cache Dictionary when time, memory consumption should be low but, who knows.
        private static Dictionary<string, HashSet<string>> _alltagscache = new Dictionary<string, HashSet<string>>();
        private static Dictionary<string, HashSet<string>> _alltitlescache = new Dictionary<string, HashSet<string>>();
        private static Dictionary<string, HashSet<string>> _hidecategoriescache = new Dictionary<string, HashSet<string>>();
        private static Dictionary<string, HashSet<string>> _plexuserscache = new Dictionary<string, HashSet<string>>();


        public static List<T> CastList<T>(this IEnumerable<dynamic> list) => list?.Cast<T>().ToList();

        public static DateTime GetMessageDateAsDate(this Azure_AdminMessage message) => TimeZone.CurrentTimeZone.ToLocalTime(AniDB.GetAniDBDateAsDate((int) message.MessageDate).Value);


        public static string ToStringEx(this Azure_AdminMessage message) => $"{message.AdminMessageId} - {message.GetMessageDateAsDate()} - {message.Message}";

        public static double GetApprovalPercentage(this AniDB_Anime_Similar similar)
        {
            if (similar.Total == 0) return (double) 0;
            return (double) similar.Approval / (double) similar.Total * (double) 100;
        }

        public static AnimeType GetAnimeTypeEnum(this AniDB_Anime anime)
        {
            if (anime.AnimeType > 5) return AnimeType.Other;
            return (AnimeType) anime.AnimeType;
        }

        public static bool GetFinishedAiring(this AniDB_Anime anime)
        {
            if (!anime.EndDate.HasValue) return false; // ongoing

            // all series have finished airing 
            if (anime.EndDate.Value < DateTime.Now) return true;

            return false;
        }

        public static bool IsInYear(this AniDB_Anime anime, int year)
        {
            // We don't know when it airs, so it's not happened yet
            if (anime.AirDate == null) return false;

            // reasons to count in a year:
            // - starts in the year, unless it aired early
            // - ends well into the year
            // - airs all throughout the year (starts in 2015, ends in 2017, 2016 counts)

            DateTime startDate = anime.AirDate.Value;

            // started after the year has ended
            if (startDate.Year > year) return false;

            if (startDate.Year == year)
            {
                // It started in the year, but nowhere near the end
                if (startDate.Month < 12) return true;

                // implied startDate.Month == 12, unless the calendar changes...
                // if it's a movie or short series, count it
                if (anime.AnimeType == (int)AnimeType.Movie || anime.EpisodeCountNormal <= 6) return true;
            }

            // starts before the year, but continues through it
            if (startDate.Year < year)
            {
                // still airing or finished after the year has been started, with some time for late seasons
                if (anime.EndDate == null || anime.EndDate.Value >= new DateTime(year, 2, 1)) return true;
            }

            return false;
        }

        public static bool IsInYear(this CL_AniDB_AnimeDetailed anime, int year)
        {
            return anime?.AniDBAnime?.IsInYear(year) ?? false;
        }

        public static bool IsInSeason(this AniDB_Anime anime, AnimeSeason season, int year)
        {
            if (anime.AirDate == null) return false;
            // If it isn't a normal series, then it won't adhere to standard airing norms
            if (anime.AnimeType != (int) AnimeType.TVSeries && anime.AnimeType != (int) AnimeType.Web) return false;
            DateTime seasonStartBegin;
            DateTime seasonStartEnd;
            switch (season)
            {
                case AnimeSeason.Winter:
                    // January +- 0.5
                    seasonStartBegin = new DateTime(year - 1, 12, 16);
                    seasonStartEnd = new DateTime(year, 1, 15);
                    break;
                case AnimeSeason.Spring:
                    // April +- 0.5
                    seasonStartBegin = new DateTime(year, 3, 16);
                    seasonStartEnd = new DateTime(year, 4, 15);
                    break;
                case AnimeSeason.Summer:
                    // July +- 0.5
                    seasonStartBegin = new DateTime(year, 6, 15);
                    seasonStartEnd = new DateTime(year, 7, 15);
                    break;
                case AnimeSeason.Fall:
                    // October +- 0.5
                    seasonStartBegin = new DateTime(year, 9, 15);
                    seasonStartEnd = new DateTime(year, 10, 15);
                    break;
                default:
                    return false;
            }
            // Don't even count seasons that haven't happened yet
            if (seasonStartBegin > DateTime.Today) return false;

            // If it starts in a season, then it is definitely going to be in it
            if (anime.AirDate.Value >= seasonStartBegin && anime.AirDate.Value <= seasonStartEnd) return true;
            // If it aired before the season, but hasn't finished by the season, count it.
            if (anime.AirDate.Value < seasonStartBegin)
            {
                // null EndDate means it's still airing now
                if (anime.EndDate == null) return true;
                // A season can run long, so don't count it unless it continues well into the season
                switch (season)
                {
                    case AnimeSeason.Winter:
                        // January + 1
                        seasonStartBegin = new DateTime(year, 2, 1);
                        break;
                    case AnimeSeason.Spring:
                        // April + 1
                        seasonStartBegin = new DateTime(year, 5, 1);
                        break;
                    case AnimeSeason.Summer:
                        // July + 1
                        seasonStartBegin = new DateTime(year, 8, 1);
                        break;
                    case AnimeSeason.Fall:
                        // October + 1
                        seasonStartBegin = new DateTime(year, 11, 1);
                        break;
                    default:
                        return false;
                }
                if (anime.EndDate.Value > seasonStartBegin) return true;
            }
            return false;
        }

        public static bool IsInSeason(this CL_AniDB_AnimeDetailed anime, AnimeSeason season, int year)
        {
            return anime?.AniDBAnime?.IsInSeason(season, year) ?? false;
        }

        public static bool IsTvDBLinkDisabled(this AniDB_Anime anime) => (anime.DisableExternalLinksFlag & Shoko.Models.Constants.LinkFlags.FlagLinkTvDB) > 0;

        public static bool IsTraktLinkDisabled(this AniDB_Anime anime) => (anime.DisableExternalLinksFlag & Shoko.Models.Constants.LinkFlags.FlagLinkTrakt) > 0;

        public static bool IsMALLinkDisabled(this AniDB_Anime anime) => (anime.DisableExternalLinksFlag & Shoko.Models.Constants.LinkFlags.FlagLinkMAL) > 0;

        public static bool IsMovieDBLinkDisabled(this AniDB_Anime anime) => (anime.DisableExternalLinksFlag & Shoko.Models.Constants.LinkFlags.FlagLinkMovieDB) > 0;

        public static int GetAirDateAsSeconds(this AniDB_Anime anime) => AniDB.GetAniDBDateAsSeconds(anime.AirDate);

        public static string GetAirDateFormatted(this AniDB_Anime anime) => AniDB.GetAniDBDate(anime.GetAirDateAsSeconds());

        public static void SetAnimeTypeRAW(this AniDB_Anime anime, string value)
        {
            anime.AnimeType = (int) RawToType(value);
        }

        public static string GetAnimeTypeRAW(this AniDB_Anime anime) => ConvertToRAW((AnimeType) anime.AnimeType);

        public static AnimeType RawToType(string raw)
        {
            switch (raw.ToLowerInvariant().Trim())
            {
                case "movie":
                    return AnimeType.Movie;
                case "ova":
                    return AnimeType.OVA;
                case "tv series":
                    return AnimeType.TVSeries;
                case "tv special":
                    return AnimeType.TVSpecial;
                case "web":
                    return AnimeType.Web;
                default:
                    return AnimeType.Other;
            }
        }

        public static string ConvertToRAW(AnimeType t)
        {
            switch (t)
            {
                case AnimeType.Movie:
                    return "movie";
                case AnimeType.OVA:
                    return "ova";
                case AnimeType.TVSeries:
                    return "tv series";
                case AnimeType.TVSpecial:
                    return "tv special";
                case AnimeType.Web:
                    return "web";
                default:
                    return "other";
            }
        }

        public static string GetAnimeTypeName(this AniDB_Anime anime)
        {
            return Enum.GetName(typeof(AnimeType), (AnimeType) anime.AnimeType).Replace('_', ' ');
        }

        public static HashSet<string> GetAllTags(this AniDB_Anime anime)
        {
            if (!String.IsNullOrEmpty(anime.AllTags))
            {
                lock (_alltagscache)
                {
                    if (!_alltagscache.ContainsKey(anime.AllTags))
                        _alltagscache[anime.AllTags] = new HashSet<string>(
                            anime.AllTags.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .Where(a => !String.IsNullOrEmpty(a)), StringComparer.InvariantCultureIgnoreCase);
                    return _alltagscache[anime.AllTags];
                }

            }
            return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public static HashSet<string> GetAllTitles(this AniDB_Anime anime)
        {
            if (!String.IsNullOrEmpty(anime.AllTitles))
            {
                lock (_alltitlescache)
                {
                    if (!_alltitlescache.ContainsKey(anime.AllTitles))
                        _alltitlescache[anime.AllTitles] = new HashSet<string>(
                            anime.AllTitles.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .Where(a => !String.IsNullOrEmpty(a)), StringComparer.InvariantCultureIgnoreCase);
                    return _alltitlescache[anime.AllTitles];
                }

            }
            return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public static bool GetSearchOnTvDB(this AniDB_Anime anime)
        {
            return anime.AnimeType != (int) AnimeType.Movie && !(anime.Restricted > 0);
        }

        public static bool GetSearchOnMovieDB(this AniDB_Anime anime)
        {
            return anime.AnimeType == (int) AnimeType.Movie && !(anime.Restricted > 0);
        }

        public static decimal GetAniDBRating(this AniDB_Anime anime)
        {
            if (anime.GetAniDBTotalVotes() == 0)
                return 0;
            return anime.GetAniDBTotalRating() / (decimal) anime.GetAniDBTotalVotes();
        }

        public static decimal GetAniDBTotalRating(this AniDB_Anime anime)
        {
            decimal totalRating = 0;
            totalRating += (decimal) anime.Rating * anime.VoteCount;
            totalRating += (decimal) anime.TempRating * anime.TempVoteCount;
            return totalRating;
        }

        public static int GetAniDBTotalVotes(this AniDB_Anime anime) => anime.TempVoteCount + anime.VoteCount;

        public static string ToStringEx(this AniDB_Anime anime)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("AnimeID: " + anime.AnimeID);
            sb.Append(" | Main Title: " + anime.MainTitle);
            sb.Append(" | EpisodeCount: " + anime.EpisodeCount);
            sb.Append(" | AirDate: " + anime.AirDate);
            sb.Append(" | Picname: " + anime.Picname);
            sb.Append(" | Type: " + anime.GetAnimeTypeRAW());
            return sb.ToString();
        }

        public static string GetAirDateFormatted(this AniDB_Episode episode)
        {
            try
            {
                return AniDB.GetAniDBDate(episode.AirDate);
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static DateTime? GetAirDateAsDate(this AniDB_Episode episode) => AniDB.GetAniDBDateAsDate(episode.AirDate);

        public static bool GetFutureDated(this AniDB_Episode episode)
        {
            DateTime? date = episode.GetAirDateAsDate();
            if (date == null) return true;

            return date.Value.ToLocalTime() > DateTime.Now;
        }

        public static EpisodeType GetEpisodeTypeEnum(this AniDB_Episode episode) => (EpisodeType) episode.EpisodeType;

        public static bool IsWatched(this AnimeEpisode_User epuser) => epuser.WatchedCount > 0;

        public static bool HasUnwatchedFiles(this AnimeGroup_User grpuser) => grpuser.UnwatchedEpisodeCount > 0;
        public static bool HasAllFilesWatched(this AnimeGroup_User grpuser) => grpuser.UnwatchedEpisodeCount == 0;
        public static bool HasAnyFilesWatched(this AnimeGroup_User grpuser) => grpuser.WatchedEpisodeCount > 0;


        public static GroupFilterConditionType GetConditionTypeEnum(this GroupFilterCondition grpf) => (GroupFilterConditionType) grpf.ConditionType;

        public static GroupFilterOperator GetConditionOperatorEnum(this GroupFilterCondition grpf) => (GroupFilterOperator) grpf.ConditionOperator;

        public static HashSet<string> GetHideCategories(this JMMUser user)
        {
            if (!String.IsNullOrEmpty(user.HideCategories))
            {
                lock (_hidecategoriescache)
                {
                    if (!_hidecategoriescache.ContainsKey(user.HideCategories))
                        _hidecategoriescache[user.HideCategories] = new HashSet<string>(
                            user.HideCategories.Trim().Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .Where(a => !String.IsNullOrEmpty(a)).Distinct(StringComparer.InvariantCultureIgnoreCase),
                            StringComparer.InvariantCultureIgnoreCase);
                    return _hidecategoriescache[user.HideCategories];
                }

            }
            return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public static HashSet<string> GetPlexUsers(this JMMUser user)
        {
            if (!String.IsNullOrEmpty(user.PlexUsers))
            {
                lock (_plexuserscache)
                {
                    if (!_plexuserscache.ContainsKey(user.PlexUsers))
                        _plexuserscache[user.PlexUsers] = new HashSet<string>(
                            user.PlexUsers.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .Where(a => !String.IsNullOrEmpty(a)).Distinct(StringComparer.InvariantCultureIgnoreCase),
                            StringComparer.InvariantCultureIgnoreCase);
                    return _plexuserscache[user.PlexUsers];
                }
            }
            return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// looking at the episode range determine if the group has released a file
        /// for the specified episode number
        /// </summary>
        /// <param name="grpstatus"></param>
        /// <param name="episodeNumber"></param>
        /// <returns></returns>
        public static bool HasGroupReleasedEpisode(this AniDB_GroupStatus grpstatus, int episodeNumber)
        {
            // examples
            // 1-12
            // 1
            // 5-10
            // 1-10, 12

            string[] ranges = grpstatus.EpisodeRange.Split(',');

            foreach (string range in ranges)
            {
                string[] subRanges = range.Split('-');
                if (subRanges.Length == 1) // 1 episode
                {
                    if (Int32.Parse(subRanges[0]) == episodeNumber) return true;
                }
                if (subRanges.Length == 2) // range
                {
                    if (episodeNumber >= Int32.Parse(subRanges[0]) && episodeNumber <= Int32.Parse(subRanges[1]))
                        return true;
                }
            }

            return false;
        }

        public static ScanStatus GetScanStatus(this Scan scan) => (ScanStatus) scan.Status;

        public static string GetStatusText(this Scan scan)
        {
            switch (scan.GetScanStatus())
            {
                case ScanStatus.Finish:
                    return "Finished";
                case ScanStatus.Running:
                    return "Running";
                default:
                    return "Standby";
            }
        }

        public static List<int> GetImportFolderList(this Scan scan) => scan.ImportFolders.Split(',').Select(a => Int32.Parse(a)).ToList();

        public static string GetTitleText(this Scan scan) => scan.CreationTIme.ToString(CultureInfo.CurrentUICulture) + " (" + scan.ImportFolders + ")";
        public static ScanFileStatus GetScanFileStatus(this ScanFile scanfile) => (ScanFileStatus) scanfile.Status;

        public static string GetStatusText(this ScanFile scanfile)
        {
            switch (scanfile.GetScanFileStatus())
            {
                case ScanFileStatus.Waiting:
                    return "Waiting";
                case ScanFileStatus.ErrorFileNotFound:
                    return "File Not Found";
                case ScanFileStatus.ErrorInvalidHash:
                    return "Hash do not match";
                case ScanFileStatus.ErrorInvalidSize:
                    return "Size do not match";
                case ScanFileStatus.ErrorMissingHash:
                    return "Missing Hash";
                case ScanFileStatus.ErrorIOError:
                    return "I/O Error";
                default:
                    return "Processed";
            }
        }

  
        public const int LastYear = 2050;

        public static string GetYear(this AniDB_Anime anidbanime)
        {
            string y = anidbanime.BeginYear.ToString();
            if (anidbanime.BeginYear != anidbanime.EndYear)
            {
                if (anidbanime.EndYear == LastYear)
                    y += "-Ongoing";
                else
                    y += "-" + anidbanime.EndYear.ToString();
            }
            return y;
        }

        public static string GetStatusImage(this CL_VideoLocal_Renamed videolocalrenamed)
        {
            if (videolocalrenamed.Success) return @"/Images/16_tick.png";

            return @"/Images/16_exclamation.png";
        }

        public static string GetLocalFilePath1(this CL_DuplicateFile dupfile) => FolderMappings.Instance.TranslateFile(dupfile.ImportFolder1, dupfile.FilePathFile1);

        public static string GetLocalFileName1(this CL_DuplicateFile dupfile)
        {
            var path = dupfile.GetLocalFilePath1();
            if (String.IsNullOrEmpty(path)) return dupfile.FilePathFile1;
            return Path.GetFileName(path);
        }

        public static string GetLocalFileDirectory1(this CL_DuplicateFile dupfile)
        {
            var path = dupfile.GetLocalFilePath1();
            if (String.IsNullOrEmpty(path)) return dupfile.FilePathFile1;
            return Path.GetDirectoryName(path);
        }

        public static string GetLocalFilePath2(this CL_DuplicateFile dupfile) => FolderMappings.Instance.TranslateFile(dupfile.ImportFolder2, dupfile.FilePathFile2);

        public static string GetLocalFileName2(this CL_DuplicateFile dupfile)
        {
            var path = dupfile.GetLocalFilePath2();
            if (String.IsNullOrEmpty(path)) return dupfile.FilePathFile2;
            return Path.GetFileName(path);
        }

        public static string GetLocalFileDirectory2(this CL_DuplicateFile dupfile)
        {
            var path = dupfile.GetLocalFilePath2();
            if (String.IsNullOrEmpty(path)) return dupfile.FilePathFile2;
            return Path.GetDirectoryName(path);
        }

        public static string GetEpisodeNumberAndName(this CL_DuplicateFile dupfile)
        {
            string shortType = "";
            if (dupfile.EpisodeType.HasValue)
            {
                EpisodeType epType = (EpisodeType) dupfile.EpisodeType.Value;
                switch (epType)
                {
                    case EpisodeType.Credits:
                        shortType = "C";
                        break;
                    case EpisodeType.Episode:
                        shortType = "";
                        break;
                    case EpisodeType.Other:
                        shortType = "O";
                        break;
                    case EpisodeType.Parody:
                        shortType = "P";
                        break;
                    case EpisodeType.Special:
                        shortType = "S";
                        break;
                    case EpisodeType.Trailer:
                        shortType = "T";
                        break;
                }
                return $"{shortType}{dupfile.EpisodeNumber.Value} - {dupfile.EpisodeName}";
            }
            return dupfile.FilePathFile1;
        }

        public static string GetLocalFileSystemFullPath(this CL_VideoLocal_Place vidlocalPlace) => FolderMappings.Instance.TranslateFile(vidlocalPlace.ImportFolder, vidlocalPlace.FilePath.Replace('/', Path.DirectorySeparatorChar));

        public static string GetLocalFileSystemFullPath(this ImportFolder folder) => FolderMappings.Instance.TranslateDirectory(folder, Path.DirectorySeparatorChar.ToString());

        public static string GetFullPath(this CL_VideoLocal_Place vidlocalPlace) =>
            vidlocalPlace.ImportFolder?.ImportFolderLocation == null
                ? null
                : (vidlocalPlace.FilePath == null
                    ? null
                    : Path.Combine(vidlocalPlace.ImportFolder.ImportFolderLocation, vidlocalPlace.FilePath));
        public static string GetFileName(this CL_VideoLocal_Place vidlocalPlace) => Path.GetFileName(vidlocalPlace.FilePath);
        public static string GetFileDirectory(this CL_VideoLocal_Place vidlocalPlace) => Path.GetDirectoryName(vidlocalPlace.GetFullPath());
        public static string GetFormattedFileSize(this CL_VideoLocal videolocal) => Formatting.FormatFileSize(videolocal.FileSize);
        public static string GetFileDirectories(this CL_VideoLocal videolocal) => String.Join(",", videolocal.Places?.Select(a => a.GetFileDirectory()));
        public static string GetLocalFileSystemFullPath(this CL_VideoLocal videolocal) => videolocal.Places?.FirstOrDefault(a => a.GetLocalFileSystemFullPath() != String.Empty)?.GetLocalFileSystemFullPath() ?? "";
        public static bool IsLocalFile(this CL_VideoLocal videolocal) => !String.IsNullOrEmpty(videolocal.GetLocalFileSystemFullPath());
        public static bool IsHashed(this CL_VideoLocal videolocal) => !String.IsNullOrEmpty(videolocal.Hash);


        public static string GetLocalFileSystemFullPath(this CL_VideoDetailed videolocal) => videolocal.Places?.FirstOrDefault(a => a.GetLocalFileSystemFullPath() != String.Empty)?.GetLocalFileSystemFullPath() ?? "";

        public static bool IsLocalFile(this CL_VideoDetailed videodetailed) => !String.IsNullOrEmpty(videodetailed.GetLocalFileSystemFullPath());

        public static string GetVideoResolution(this CL_VideoDetailed videodetailed) => videodetailed.AniDB_File_VideoResolution.Length > 0 ? videodetailed.AniDB_File_VideoResolution : videodetailed.VideoInfo_VideoResolution;

        public static string GetVideoCodec(this CL_VideoDetailed videodetailed) => videodetailed.AniDB_File_VideoCodec.Length > 0 ? videodetailed.AniDB_File_VideoCodec : videodetailed.VideoInfo_VideoCodec;

        public static string GetAudioCodec(this CL_VideoDetailed videodetailed) => videodetailed.AniDB_File_AudioCodec.Length > 0 ? videodetailed.AniDB_File_AudioCodec : videodetailed.VideoInfo_AudioCodec;

        public static string GetFileName(this CL_VideoDetailed videodetailed) => videodetailed.VideoLocal_FileName;

        public static string GetFullPath(this CL_VideoDetailed videodetailed) => videodetailed.Places?.FirstOrDefault(a => !String.IsNullOrEmpty(a.GetLocalFileSystemFullPath()))?.GetLocalFileSystemFullPath() ?? "";

        public static bool GetFileIsAvailable(this CL_VideoDetailed videodetailed) => String.IsNullOrEmpty(videodetailed.GetFullPath()) || File.Exists(videodetailed.GetFullPath());
     
        public static string GetVideoInfoSummary(this CL_VideoDetailed videodetailed) => $"{videodetailed.GetVideoResolution()} ({videodetailed.GetVideoCodec()}) - {videodetailed.GetAudioCodec()}";

        public static string GetFormattedFileSize(this CL_VideoDetailed videodetailed) => Formatting.FormatFileSize(videodetailed.VideoLocal_FileSize);

        public static bool IsBluRay(this CL_VideoDetailed videodetailed) => videodetailed.AniDB_File_Source.ToUpper().Contains("BLU");

        public static bool IsDVD(this CL_VideoDetailed videodetailed) => videodetailed.AniDB_File_Source.ToUpper().Contains("DVD");

        public static bool IsHD(this CL_VideoDetailed videodetailed) => (videodetailed.GetVideoWidth() >= 1280 && videodetailed.GetVideoWidth() < 1920);

        public static bool IsFullHD(this CL_VideoDetailed videodetailed) => (videodetailed.GetVideoWidth() >= 1920);

        public static bool IsHi08P(this CL_VideoDetailed videodetailed)
        {
            if (String.IsNullOrEmpty(videodetailed.VideoInfo_VideoBitDepth)) return false;
            Int32.TryParse(videodetailed.VideoInfo_VideoBitDepth, out int bitDepth);
            return bitDepth == 8;
        }

        public static bool IsHi10P(this CL_VideoDetailed videodetailed)
        {
            if (String.IsNullOrEmpty(videodetailed.VideoInfo_VideoBitDepth)) return false;
            Int32.TryParse(videodetailed.VideoInfo_VideoBitDepth, out int bitDepth);
            return bitDepth == 10;
        }

        public static bool IsHi12P(this CL_VideoDetailed videodetailed)
        {
            if (String.IsNullOrEmpty(videodetailed.VideoInfo_VideoBitDepth)) return false;
            Int32.TryParse(videodetailed.VideoInfo_VideoBitDepth, out int bitDepth);
            return bitDepth == 12;
        }

        public static bool IsDualAudio(this CL_VideoDetailed videodetailed)
        {
            if (videodetailed.HasAniDBFile())
            {
                return videodetailed.AniDB_File_AudioCodec.Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries)
                    .Length == 2;
            }
            return false;
        }

        public static bool IsMultiAudio(this CL_VideoDetailed videodetailed)
        {
            if (videodetailed.HasAniDBFile())
            {
                return videodetailed.AniDB_File_AudioCodec.Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries)
                           .Length > 2;
            }
            return false;
        }

        public static int GetVideoWidth(this CL_VideoDetailed videodetailed)
        {
            int videoWidth = 0;
            if (videodetailed.AniDB_File_VideoResolution.Trim().Length > 0)
            {
                string[] dimensions = videodetailed.AniDB_File_VideoResolution.Split('x');
                if (dimensions.Length > 0) Int32.TryParse(dimensions[0], out videoWidth);
            }
            return videoWidth;
        }

        public static int GetVideoHeight(this CL_VideoDetailed videodetailed)
        {
            int videoHeight = 0;
            if (videodetailed.AniDB_File_VideoResolution.Trim().Length > 0)
            {
                string[] dimensions = videodetailed.AniDB_File_VideoResolution.Split('x');
                if (dimensions.Length > 1) Int32.TryParse(dimensions[1], out videoHeight);
            }
            return videoHeight;
        }

        public static int GetBitDepth(this CL_VideoDetailed videodetailed)
        {
            if (!Int32.TryParse(videodetailed.VideoInfo_VideoBitDepth, out int bitDepth))
                bitDepth = 8;

            return bitDepth;
        }

        public static int GetOverallVideoSourceRanking(this CL_VideoDetailed videodetailed)
        {

            int vidWidth = videodetailed.GetVideoWidth();
            int score = 0;
            score += videodetailed.GetVideoSourceRanking();
            score += videodetailed.GetBitDepth();

            if (vidWidth > 1900) score += 100;
            else if (vidWidth > 1300) score += 50;
            else if (vidWidth > 1100) score += 25;
            else if (vidWidth > 800) score += 10;
            else if (vidWidth > 700) score += 8;
            else if (vidWidth > 500) score += 7;
            else if (vidWidth > 400) score += 6;
            else if (vidWidth > 300) score += 5;
            else score += 2;

            return score;
        }

        public static int GetVideoSourceRanking(this CL_VideoDetailed videodetailed)
        {
            if (videodetailed.AniDB_File_Source.ToUpper().Contains("BLU")) return 100;
            if (videodetailed.AniDB_File_Source.ToUpper().Contains("DVD")) return 75;
            if (videodetailed.AniDB_File_Source.ToUpper().Contains("HDTV")) return 50;
            if (videodetailed.AniDB_File_Source.ToUpper().Contains("DTV")) return 40;
            if (videodetailed.AniDB_File_Source.ToUpper().Trim() == "TV") return 30;
            if (videodetailed.AniDB_File_Source.ToUpper().Contains("VHS")) return 20;

            return 0;
        }

        public static bool HasReleaseGroup(this CL_VideoDetailed videodetailed) => videodetailed.ReleaseGroup != null;

        public static string GetReleaseGroupName(this CL_VideoDetailed videodetailed) => videodetailed.ReleaseGroup != null ? videodetailed.ReleaseGroup.GroupName : "";

        public static string GetReleaseGroupAniDBURL(this CL_VideoDetailed videodetailed) => videodetailed.ReleaseGroup != null ? String.Format(Shoko.Models.Constants.URLS.AniDB_ReleaseGroup, videodetailed.ReleaseGroup.GroupID) : "";

        public static bool HasAniDBFile(this CL_VideoDetailed videodetailed) => videodetailed.AniDB_FileID.HasValue;

        public static string GetAniDB_SiteURL(this CL_VideoDetailed videodetailed) => videodetailed.AniDB_FileID.HasValue ? String.Format(Shoko.Models.Constants.URLS.AniDB_File, videodetailed.AniDB_FileID.Value) : "";

        public static string GetBannerURL(this TVDB_Series_Search_Response sresponse) => String.IsNullOrEmpty(sresponse.Banner) ? "" : String.Format(Shoko.Models.Constants.URLS.TvDB_Images, sresponse.Banner);
        
        public static string GetSeriesURL(this TVDB_Series_Search_Response sresponse) => String.Format(Shoko.Models.Constants.URLS.TvDB_Series, sresponse.SeriesID);

        public static string GetLanguageFlagImage(this TVDB_Series_Search_Response sresponse) => Languages.Languages.GetFlagImage(sresponse.Language.Trim().ToUpper());

        public static string GetLanguageFlagImage(this TvDB_Language tvdblanguage) => Languages.Languages.GetFlagImage(tvdblanguage.Abbreviation.Trim().ToUpper());

        public static string GetTraktID(this CL_TraktTVShowResponse showresponse)
        {
            if (String.IsNullOrEmpty(showresponse.url)) return "";

            int pos = showresponse.url.LastIndexOf("/");
            if (pos < 0) return "";

            string id = showresponse.url.Substring(pos + 1, showresponse.url.Length - pos - 1);
            return id;
        }

        public static string GetTextForEnum_Sorting(this GroupFilterSorting sort)
        {
            switch (sort)
            {
                case GroupFilterSorting.AniDBRating: return Resources.GroupFilterSorting_AniDBRating;
                case GroupFilterSorting.EpisodeAddedDate: return Resources.GroupFilterSorting_EpisodeAddedDate;
                case GroupFilterSorting.EpisodeAirDate: return Resources.GroupFilterSorting_EpisodeAirDate;
                case GroupFilterSorting.EpisodeWatchedDate: return Resources.GroupFilterSorting_EpisodeWatchedDate;
                case GroupFilterSorting.GroupName: return Resources.GroupFilterSorting_GroupName;
                case GroupFilterSorting.GroupFilterName: return Resources.GroupFilterSorting_GroupFilter;
                case GroupFilterSorting.SortName: return Resources.GroupFilterSorting_SortName;
                case GroupFilterSorting.MissingEpisodeCount: return Resources.GroupFilterSorting_MissingEpisodeCount;
                case GroupFilterSorting.SeriesAddedDate: return Resources.GroupFilterSorting_SeriesAddedDate;
                case GroupFilterSorting.SeriesCount: return Resources.GroupFilterSorting_SeriesCount;
                case GroupFilterSorting.UnwatchedEpisodeCount: return Resources.GroupFilterSorting_UnwatchedEpisodeCount;
                case GroupFilterSorting.UserRating: return Resources.GroupFilterSorting_UserRating;
                case GroupFilterSorting.Year: return Resources.GroupFilterSorting_Year;
                default: return Resources.GroupFilterSorting_AniDBRating;
            }
        }

        public static GroupFilterSorting GetEnumForText_Sorting(this string enumDesc)
        {
            if (enumDesc == Resources.GroupFilterSorting_AniDBRating) return GroupFilterSorting.AniDBRating;
            if (enumDesc == Resources.GroupFilterSorting_EpisodeAddedDate) return GroupFilterSorting.EpisodeAddedDate;
            if (enumDesc == Resources.GroupFilterSorting_EpisodeAirDate) return GroupFilterSorting.EpisodeAirDate;
            if (enumDesc == Resources.GroupFilterSorting_EpisodeWatchedDate) return GroupFilterSorting.EpisodeWatchedDate;
            if (enumDesc == Resources.GroupFilterSorting_GroupName) return GroupFilterSorting.GroupName;
            if (enumDesc == Resources.GroupFilterSorting_SortName) return GroupFilterSorting.SortName;
            if (enumDesc == Resources.GroupFilterSorting_MissingEpisodeCount) return GroupFilterSorting.MissingEpisodeCount;
            if (enumDesc == Resources.GroupFilterSorting_SeriesAddedDate) return GroupFilterSorting.SeriesAddedDate;
            if (enumDesc == Resources.GroupFilterSorting_SeriesCount) return GroupFilterSorting.SeriesCount;
            if (enumDesc == Resources.GroupFilterSorting_UnwatchedEpisodeCount) return GroupFilterSorting.UnwatchedEpisodeCount;
            if (enumDesc == Resources.GroupFilterSorting_UserRating) return GroupFilterSorting.UserRating;
            if (enumDesc == Resources.GroupFilterSorting_Year) return GroupFilterSorting.Year;
            if (enumDesc == Resources.GroupFilterSorting_GroupFilter) return GroupFilterSorting.GroupFilterName;

            return GroupFilterSorting.AniDBRating;
        }

        public static string GetTextForEnum_SortDirection(this GroupFilterSortDirection sort)
        {
            switch (sort)
            {
                case GroupFilterSortDirection.Asc: return Resources.GroupFilterSortDirection_Asc;
                case GroupFilterSortDirection.Desc: return Resources.GroupFilterSortDirection_Desc;
                default: return Resources.GroupFilterSortDirection_Asc;
            }
        }

        public static GroupFilterSortDirection GetEnumForText_SortDirection(this string enumDesc)
        {
            if (enumDesc == Resources.GroupFilterSortDirection_Asc) return GroupFilterSortDirection.Asc;
            if (enumDesc == Resources.GroupFilterSortDirection_Desc) return GroupFilterSortDirection.Desc;

            return GroupFilterSortDirection.Asc;
        }

        public static string GetTextForEnum_Operator(this GroupFilterOperator op)
        {
            switch (op)
            {
                case GroupFilterOperator.Equals: return Resources.GroupFilterOperator_Equals;
                case GroupFilterOperator.NotEquals: return Resources.GroupFilterOperator_NotEquals;
                case GroupFilterOperator.Exclude: return Resources.GroupFilterOperator_Exclude;
                case GroupFilterOperator.Include: return Resources.GroupFilterOperator_Include;
                case GroupFilterOperator.GreaterThan: return Resources.GroupFilterOperator_GreaterThan;
                case GroupFilterOperator.LessThan: return Resources.GroupFilterOperator_LessThan;
                case GroupFilterOperator.In: return Resources.GroupFilterOperator_In;
                case GroupFilterOperator.NotIn: return Resources.GroupFilterOperator_NotIn;
                case GroupFilterOperator.InAllEpisodes: return Resources.GroupFilterOperator_InAllEpisodes;
                case GroupFilterOperator.NotInAllEpisodes: return Resources.GroupFilterOperator_NotInAllEpisodes;
                case GroupFilterOperator.LastXDays: return Resources.GroupFilterOperator_LastXDays;

                default: return Resources.GroupFilterOperator_Equals;
            }
        }

        public static GroupFilterOperator GetEnumForText_Operator(this string enumDesc)
        {
            if (enumDesc == Resources.GroupFilterOperator_Equals) return GroupFilterOperator.Equals;
            if (enumDesc == Resources.GroupFilterOperator_NotEquals) return GroupFilterOperator.NotEquals;
            if (enumDesc == Resources.GroupFilterOperator_Exclude) return GroupFilterOperator.Exclude;
            if (enumDesc == Resources.GroupFilterOperator_Include) return GroupFilterOperator.Include;
            if (enumDesc == Resources.GroupFilterOperator_GreaterThan) return GroupFilterOperator.GreaterThan;
            if (enumDesc == Resources.GroupFilterOperator_LessThan) return GroupFilterOperator.LessThan;
            if (enumDesc == Resources.GroupFilterOperator_In) return GroupFilterOperator.In;
            if (enumDesc == Resources.GroupFilterOperator_NotIn) return GroupFilterOperator.NotIn;
            if (enumDesc == Resources.GroupFilterOperator_InAllEpisodes) return GroupFilterOperator.InAllEpisodes;
            if (enumDesc == Resources.GroupFilterOperator_NotInAllEpisodes) return GroupFilterOperator.NotInAllEpisodes;
            if (enumDesc == Resources.GroupFilterOperator_LastXDays) return GroupFilterOperator.LastXDays;


            return GroupFilterOperator.Equals;
        }

        public static string GetTextForEnum_ConditionType(this GroupFilterConditionType conditionType)
        {
            switch (conditionType)
            {
                case GroupFilterConditionType.AirDate: return Resources.GroupFilterConditionType_AirDate;
                case GroupFilterConditionType.AnimeGroup: return Resources.GroupFilterConditionType_AnimeGroup;
                case GroupFilterConditionType.AnimeType: return Resources.GroupFilterConditionType_AnimeType;
                case GroupFilterConditionType.AssignedTvDBInfo: return Resources.GroupFilterConditionType_AssignedTvDBInfo;
                case GroupFilterConditionType.AssignedTraktInfo: return Resources.GroupFilterConditionType_AssignedTraktInfo;
                case GroupFilterConditionType.AssignedMALInfo: return Resources.GroupFilterConditionType_AssignedMALInfo;
                case GroupFilterConditionType.AssignedMovieDBInfo: return Resources.GroupFilterConditionType_AssignedMovieDBInfo;
                case GroupFilterConditionType.AssignedTvDBOrMovieDBInfo: return Resources.GroupFilterConditionType_AssignedTvDBOrMovieDBInfo;
                case GroupFilterConditionType.Tag: return Resources.GroupFilterConditionType_Tag;
                case GroupFilterConditionType.CompletedSeries: return Resources.GroupFilterConditionType_CompletedSeries;
                case GroupFilterConditionType.Favourite: return Resources.GroupFilterConditionType_Favourite;
                case GroupFilterConditionType.HasUnwatchedEpisodes: return Resources.GroupFilterConditionType_HasUnwatchedEpisodes;
                case GroupFilterConditionType.MissingEpisodes: return Resources.GroupFilterConditionType_MissingEpisodes;
                case GroupFilterConditionType.MissingEpisodesCollecting: return Resources.GroupFilterConditionType_MissingEpisodesCollecting;
                case GroupFilterConditionType.UserVoted: return Resources.GroupFilterConditionType_UserVoted;
                case GroupFilterConditionType.UserVotedAny: return Resources.GroupFilterConditionType_UserVotedAny;
                case GroupFilterConditionType.VideoQuality: return Resources.GroupFilterConditionType_VideoQuality;
                case GroupFilterConditionType.AniDBRating: return Resources.GroupFilterConditionType_AniDBRating;
                case GroupFilterConditionType.UserRating: return Resources.GroupFilterConditionType_UserRating;
                case GroupFilterConditionType.SeriesCreatedDate: return Resources.GroupFilterConditionType_SeriesDate;
                case GroupFilterConditionType.EpisodeAddedDate: return Resources.GroupFilterConditionType_EpisodeAddedDate;
                case GroupFilterConditionType.EpisodeWatchedDate: return Resources.GroupFilterConditionType_EpisodeWatchedDate;
                case GroupFilterConditionType.FinishedAiring: return Resources.GroupFilterConditionType_FinishedAiring;
                case GroupFilterConditionType.AudioLanguage: return Resources.GroupFilterConditionType_AudioLanguage;
                case GroupFilterConditionType.SubtitleLanguage: return Resources.GroupFilterConditionType_SubtitleLanguage;
                case GroupFilterConditionType.HasWatchedEpisodes: return Resources.GroupFilterConditionType_HasWatchedEpisodes;
                case GroupFilterConditionType.EpisodeCount: return Resources.GroupFilterConditionType_EpisodeCount;
                case GroupFilterConditionType.CustomTags: return Resources.GroupFilterConditionType_CustomTag;
                case GroupFilterConditionType.LatestEpisodeAirDate: return Resources.GroupFilterConditionType_LatestEpisodeAirDate;
                case GroupFilterConditionType.Year: return Resources.GroupFilterConditionType_Year;
                case GroupFilterConditionType.Season: return Resources.GroupFilterConditionType_Season;
                default: return Resources.GroupFilterConditionType_AirDate;
            }
        }

        public static GroupFilterConditionType GetEnumForText_ConditionType(this string enumDesc)
        {
            if (enumDesc == Resources.GroupFilterConditionType_AirDate) return GroupFilterConditionType.AirDate;
            if (enumDesc == Resources.GroupFilterConditionType_AnimeGroup) return GroupFilterConditionType.AnimeGroup;
            if (enumDesc == Resources.GroupFilterConditionType_AnimeType) return GroupFilterConditionType.AnimeType;
            if (enumDesc == Resources.GroupFilterConditionType_AssignedTvDBInfo) return GroupFilterConditionType.AssignedTvDBInfo;
            if (enumDesc == Resources.GroupFilterConditionType_AssignedTraktInfo) return GroupFilterConditionType.AssignedTraktInfo;
            if (enumDesc == Resources.GroupFilterConditionType_AssignedMALInfo) return GroupFilterConditionType.AssignedMALInfo;
            if (enumDesc == Resources.GroupFilterConditionType_AssignedMovieDBInfo) return GroupFilterConditionType.AssignedMovieDBInfo;
            if (enumDesc == Resources.GroupFilterConditionType_AssignedTvDBOrMovieDBInfo) return GroupFilterConditionType.AssignedTvDBOrMovieDBInfo;
            if (enumDesc == Resources.GroupFilterConditionType_Tag) return GroupFilterConditionType.Tag;
            if (enumDesc == Resources.GroupFilterConditionType_Year) return GroupFilterConditionType.Year;
            if (enumDesc == Resources.GroupFilterConditionType_Season) return GroupFilterConditionType.Season;
            if (enumDesc == Resources.GroupFilterConditionType_LatestEpisodeAirDate) return GroupFilterConditionType.LatestEpisodeAirDate;
            if (enumDesc == Resources.GroupFilterConditionType_CustomTag) return GroupFilterConditionType.CustomTags;
            if (enumDesc == Resources.GroupFilterConditionType_CompletedSeries) return GroupFilterConditionType.CompletedSeries;
            if (enumDesc == Resources.GroupFilterConditionType_Favourite) return GroupFilterConditionType.Favourite;
            if (enumDesc == Resources.GroupFilterConditionType_HasUnwatchedEpisodes) return GroupFilterConditionType.HasUnwatchedEpisodes;
            if (enumDesc == Resources.GroupFilterConditionType_MissingEpisodes) return GroupFilterConditionType.MissingEpisodes;
            if (enumDesc == Resources.GroupFilterConditionType_MissingEpisodesCollecting) return GroupFilterConditionType.MissingEpisodesCollecting;
            if (enumDesc == Resources.GroupFilterConditionType_UserVoted) return GroupFilterConditionType.UserVoted;
            if (enumDesc == Resources.GroupFilterConditionType_UserVotedAny) return GroupFilterConditionType.UserVotedAny;
            if (enumDesc == Resources.GroupFilterConditionType_VideoQuality) return GroupFilterConditionType.VideoQuality;
            if (enumDesc == Resources.GroupFilterConditionType_AniDBRating) return GroupFilterConditionType.AniDBRating;
            if (enumDesc == Resources.GroupFilterConditionType_UserRating) return GroupFilterConditionType.UserRating;
            if (enumDesc == Resources.GroupFilterConditionType_SeriesDate) return GroupFilterConditionType.SeriesCreatedDate;
            if (enumDesc == Resources.GroupFilterConditionType_EpisodeAddedDate) return GroupFilterConditionType.EpisodeAddedDate;
            if (enumDesc == Resources.GroupFilterConditionType_EpisodeWatchedDate) return GroupFilterConditionType.EpisodeWatchedDate;
            if (enumDesc == Resources.GroupFilterConditionType_FinishedAiring) return GroupFilterConditionType.FinishedAiring;
            if (enumDesc == Resources.GroupFilterConditionType_AudioLanguage) return GroupFilterConditionType.AudioLanguage;
            if (enumDesc == Resources.GroupFilterConditionType_SubtitleLanguage) return GroupFilterConditionType.SubtitleLanguage;
            if (enumDesc == Resources.GroupFilterConditionType_HasWatchedEpisodes) return GroupFilterConditionType.HasWatchedEpisodes;
            if (enumDesc == Resources.GroupFilterConditionType_EpisodeCount) return GroupFilterConditionType.EpisodeCount;

            return GroupFilterConditionType.AirDate;
        }

        public static List<string> GetAllConditionTypes()
        {
            List<string> cons = new List<string>();

            cons.Add(GroupFilterConditionType.AirDate.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.AnimeType.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.AnimeGroup.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.HasUnwatchedEpisodes.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.HasWatchedEpisodes.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.MissingEpisodes.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.MissingEpisodesCollecting.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.CompletedSeries.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.Favourite.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.VideoQuality.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.AssignedTvDBInfo.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.AssignedTraktInfo.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.AssignedMALInfo.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.AssignedMovieDBInfo.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.AssignedTvDBOrMovieDBInfo.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.Tag.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.CustomTags.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.LatestEpisodeAirDate.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.Year.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.Season.GetTextForEnum_ConditionType());
            //cons.Add(GetTextForEnum_ConditionType(GroupFilterConditionType.ReleaseGroup));
            //cons.Add(GetTextForEnum_ConditionType(GroupFilterConditionType.Studio));
            cons.Add(GroupFilterConditionType.UserVoted.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.UserVotedAny.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.AniDBRating.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.UserRating.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.SeriesCreatedDate.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.EpisodeAddedDate.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.EpisodeWatchedDate.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.FinishedAiring.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.AudioLanguage.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.SubtitleLanguage.GetTextForEnum_ConditionType());
            cons.Add(GroupFilterConditionType.EpisodeCount.GetTextForEnum_ConditionType());

            cons.Sort();

            return cons;
        }

        public static List<string> GetAllSortTypes()
        {
            List<string> cons = new List<string>();

            cons.Add(GroupFilterSorting.AniDBRating.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.EpisodeAddedDate.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.EpisodeAirDate.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.EpisodeWatchedDate.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.GroupName.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.MissingEpisodeCount.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.SeriesAddedDate.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.SeriesCount.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.SortName.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.UnwatchedEpisodeCount.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.UserRating.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.Year.GetTextForEnum_Sorting());
            cons.Add(GroupFilterSorting.GroupFilterName.GetTextForEnum_Sorting());

            cons.Sort();

            return cons;
        }

        public static List<string> GetAllowedOperators(this GroupFilterConditionType conditionType)
        {
            List<string> ops = new List<string>();

            switch (conditionType)
            {
                case GroupFilterConditionType.AirDate:
                case GroupFilterConditionType.SeriesCreatedDate:
                case GroupFilterConditionType.EpisodeWatchedDate:
                case GroupFilterConditionType.EpisodeAddedDate:
                case GroupFilterConditionType.LatestEpisodeAirDate:
                    ops.Add(GroupFilterOperator.GreaterThan.GetTextForEnum_Operator());
                    ops.Add(GroupFilterOperator.LessThan.GetTextForEnum_Operator());
                    ops.Add(GroupFilterOperator.LastXDays.GetTextForEnum_Operator());
                    break;
                case GroupFilterConditionType.AnimeGroup:
                    ops.Add(GroupFilterOperator.Equals.GetTextForEnum_Operator());
                    ops.Add(GroupFilterOperator.NotEquals.GetTextForEnum_Operator());
                    break;
                case GroupFilterConditionType.AnimeType:
                case GroupFilterConditionType.Tag:
                case GroupFilterConditionType.CustomTags:
                case GroupFilterConditionType.AudioLanguage:
                case GroupFilterConditionType.SubtitleLanguage:
                case GroupFilterConditionType.Year:
                case GroupFilterConditionType.Season:
                    ops.Add(GroupFilterOperator.In.GetTextForEnum_Operator());
                    ops.Add(GroupFilterOperator.NotIn.GetTextForEnum_Operator());
                    break;
                case GroupFilterConditionType.AssignedTvDBInfo:
                case GroupFilterConditionType.AssignedTraktInfo:
                case GroupFilterConditionType.AssignedMALInfo:
                case GroupFilterConditionType.AssignedMovieDBInfo:
                case GroupFilterConditionType.AssignedTvDBOrMovieDBInfo:
                case GroupFilterConditionType.CompletedSeries:
                case GroupFilterConditionType.FinishedAiring:
                case GroupFilterConditionType.Favourite:
                case GroupFilterConditionType.HasUnwatchedEpisodes:
                case GroupFilterConditionType.HasWatchedEpisodes:
                case GroupFilterConditionType.MissingEpisodes:
                case GroupFilterConditionType.MissingEpisodesCollecting:
                case GroupFilterConditionType.UserVoted:
                case GroupFilterConditionType.UserVotedAny:
                    ops.Add(GroupFilterOperator.Include.GetTextForEnum_Operator());
                    ops.Add(GroupFilterOperator.Exclude.GetTextForEnum_Operator());
                    break;
                case GroupFilterConditionType.VideoQuality:
                    ops.Add(GroupFilterOperator.In.GetTextForEnum_Operator());
                    ops.Add(GroupFilterOperator.NotIn.GetTextForEnum_Operator());
                    ops.Add(GroupFilterOperator.InAllEpisodes.GetTextForEnum_Operator());
                    ops.Add(GroupFilterOperator.NotInAllEpisodes.GetTextForEnum_Operator());
                    break;
                case GroupFilterConditionType.AniDBRating:
                case GroupFilterConditionType.UserRating:
                case GroupFilterConditionType.EpisodeCount:
                    ops.Add(GroupFilterOperator.GreaterThan.GetTextForEnum_Operator());
                    ops.Add(GroupFilterOperator.LessThan.GetTextForEnum_Operator());
                    break;
            }


            return ops;
        }

        public static string GetDateAsString(this DateTime aDate)
        {
            return aDate.Year.ToString().PadLeft(4, '0') +
                   aDate.Month.ToString().PadLeft(2, '0') +
                   aDate.Day.ToString().PadLeft(2, '0');
        }

        public static DateTime GetDateFromString(this string sDate)
        {
            try
            {
                int year = Int32.Parse(sDate.Substring(0, 4));
                int month = Int32.Parse(sDate.Substring(4, 2));
                int day = Int32.Parse(sDate.Substring(6, 2));

                return new DateTime(year, month, day);
            }
            catch (Exception)
            {
                return DateTime.Today;
            }
        }

        public static string GetDateAsFriendlyString(this DateTime aDate) => aDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

        public static string GetFlagImage(this CL_AnimeTitle atitle) => Languages.Languages.GetFlagImage(atitle.Language.Trim().ToUpper());

        public static string GetLanguageDescription(this CL_AnimeTitle atitle) => Languages.Languages.GetLanguageDescription(atitle.Language.Trim().ToUpper());




        public static string GetSiteURL(this CrossRef_AniDB crossanidb) => crossanidb.Provider==Shoko.Models.Constants.Providers.MAL ? String.Format(Shoko.Models.Constants.URLS.MAL_Series, crossanidb.ProviderID) : "";

        public static string GetSiteURL(this CL_MovieDBMovieSearch_Response sresult) => String.Format(Shoko.Models.Constants.URLS.MovieDB_Series, sresult.MovieID);

        public static bool HasAnySpecials(this CL_GroupVideoQuality vidquality) => vidquality.FileCountSpecials > 0;
        public static string GetTotalFileSizeFormatted(this CL_GroupVideoQuality vidquality) => Formatting.FormatFileSize(vidquality.TotalFileSize);

        public static string GetAverageFileSizeFormatted(this CL_GroupVideoQuality vidquality)
        {
            if (vidquality.TotalRunningTime <= 0) return "N/A";

            double avgBitRate = vidquality.TotalFileSize / vidquality.TotalRunningTime;
            return Formatting.FormatBitRate(avgBitRate);
        }

        public static string GetPrettyDescription(this CL_GroupVideoQuality vidquality) => vidquality.ToString();
        public static bool IsBluRay(this CL_GroupVideoQuality vidquality) => vidquality.VideoSource.ToUpper().Contains("BLU");
        public static bool IsDVD(this CL_GroupVideoQuality vidquality) => vidquality.VideoSource.ToUpper().Contains("DVD");
        public static bool IsHD(this CL_GroupVideoQuality vidquality) => (vidquality.GetVideoWidth() >= 1280 && vidquality.GetVideoWidth() < 1920);
        public static bool IsFullHD(this CL_GroupVideoQuality vidquality) => (vidquality.GetVideoWidth() >= 1920);
        public static bool IsHi08P(this CL_GroupVideoQuality vidquality) => vidquality.VideoBitDepth == 8;
        public static bool IsHi10P(this CL_GroupVideoQuality vidquality) => vidquality.VideoBitDepth == 10;
        public static bool IsHi12P(this CL_GroupVideoQuality vidquality) => vidquality.VideoBitDepth == 12;
        public static bool IsDualAudio(this CL_GroupVideoQuality vidquality) => vidquality.AudioStreamCount == 2;
        public static bool IsMultiAudio(this CL_GroupVideoQuality vidquality) => vidquality.AudioStreamCount > 2;

        public static int GetVideoWidth(this CL_GroupVideoQuality vidquality)
        {
            int videoWidth = 0;
            if (vidquality.Resolution.Trim().Length > 0)
            {
                string[] dimensions = vidquality.Resolution.Split('x');
                if (dimensions.Length > 0) Int32.TryParse(dimensions[0], out videoWidth);
            }
            return videoWidth;
        }

        public static int GetVideoHeight(this CL_GroupVideoQuality vidquality)
        {
            int videoHeight = 0;
            if (vidquality.Resolution.Trim().Length > 0)
            {
                string[] dimensions = vidquality.Resolution.Split('x');
                if (dimensions.Length > 1) Int32.TryParse(dimensions[1], out videoHeight);
            }
            return videoHeight;
        }

        public static bool HasAnySpecials(this CL_GroupFileSummary grpsummary) => grpsummary.FileCountSpecials > 0;
        public static string GetTotalFileSizeFormatted(this CL_GroupFileSummary grpsummary) => Formatting.FormatFileSize(grpsummary.TotalFileSize);

        public static string GetAverageFileSizeFormatted(this CL_GroupFileSummary grpsummary)
        {
            if (grpsummary.TotalRunningTime <= 0) return "N/A";

            double avgBitRate = grpsummary.TotalFileSize / grpsummary.TotalRunningTime;
            return Formatting.FormatBitRate(avgBitRate);
        }

        public static string GetPrettyDescription(this CL_GroupFileSummary grpsummary) => $"{grpsummary.GroupNameShort} - {grpsummary.FileCountNormal}/{grpsummary.FileCountSpecials} Files";


        public static string GetCommentTruncated(this AniDB_Recommendation recommendation)
        {
            if (recommendation.RecommendationText.Length > 250)
                return recommendation.RecommendationText.Substring(0, 250) + ".......";
            return recommendation.RecommendationText;
        }

        public static string GetComment(this AniDB_Recommendation recommendation) => recommendation.RecommendationText;

        public static AniDBRecommendationType GetRecommendationTypeEnum(this AniDB_Recommendation recommendation) => (AniDBRecommendationType) recommendation.RecommendationType;

        public static string GetRecommendationTypeText(this AniDB_Recommendation recommendation)
        {
            switch (recommendation.GetRecommendationTypeEnum())
            {
                case AniDBRecommendationType.ForFans:
                    return Resources.AniDB_ForFans;
                case AniDBRecommendationType.Recommended:
                    return Resources.AniDB_Recommended;
                case AniDBRecommendationType.MustSee:
                    return Resources.AniDB_MustSee;
                default:
                    return Resources.AniDB_Recommended;
            }
        }

        public static string GetSiteURL(this MovieDB_Movie movie) => String.Format(Shoko.Models.Constants.URLS.MovieDB_Series, movie.MovieId);
        public static string GetSeriesURL(this CrossRef_AniDB crosstvdb) => String.Format(Shoko.Models.Constants.URLS.TvDB_Series, crosstvdb.ProviderID);
        public static string GetAniDBURL(this CrossRef_AniDB crosstvdb) => String.Format(Shoko.Models.Constants.URLS.AniDB_Series, crosstvdb.AniDBID);
        public static string GetAniDBStartEpisodeTypeString(this CrossRef_AniDB_EpisodeMap crosstvdb) => EnumTranslator.EpisodeTypeTranslated((EpisodeType) crosstvdb.AniDBStartEpisodeType);
        public static string GetAniDBStartEpisodeNumberString(this CrossRef_AniDB_EpisodeMap crosstvdb) => $"# {crosstvdb.AniDBStartEpisodeNumber}";
        public static string GetProviderSeasonNumberString(this CrossRef_AniDB_EpisodeMap crosstvdb) => $"S{crosstvdb.ProviderSeasonNumber}";
        public static string GetProviderStartEpisodeNumberString(this CrossRef_AniDB_EpisodeMap crosstvdb) => $"EP# {crosstvdb.ProviderEpisodeNumber}";
        public static string GetShowURL(this CrossRef_AniDB crosstrakt) => String.Format(Shoko.Models.Constants.URLS.Trakt_Series, crosstrakt.ProviderID);
       
        public static string EpisodeTypeTranslated(this EpisodeType epType)
        {
            switch (epType)
            {
                case EpisodeType.Credits:
                    return Resources.EpisodeType_Credits;
                case EpisodeType.Episode:
                    return Resources.EpisodeType_Normal;
                case EpisodeType.Other:
                    return Resources.EpisodeType_Other;
                case EpisodeType.Parody:
                    return Resources.EpisodeType_Parody;
                case EpisodeType.Special:
                    return Resources.EpisodeType_Specials;
                case EpisodeType.Trailer:
                    return Resources.EpisodeType_Trailer;
                default:
                    return Resources.EpisodeType_Normal;

            }
        }

        public static bool IsSequel(this CL_AniDB_Anime_Relation aniDbAnimeRelation) => aniDbAnimeRelation.RelationType.Equals("Sequel", StringComparison.InvariantCultureIgnoreCase);

        public static bool IsPrequel(this CL_AniDB_Anime_Relation aniDbAnimeRelation) => aniDbAnimeRelation.RelationType.Equals("Prequel", StringComparison.InvariantCultureIgnoreCase);

        public static bool LocalSeriesExists(this CL_AniDB_Anime_Relation aniDbAnimeRelation) => aniDbAnimeRelation.AnimeSeries != null;

        public static bool AnimeInfoExists(this CL_AniDB_Anime_Relation aniDbAnimeRelation) => aniDbAnimeRelation.AniDB_Anime != null;

        public static string GetDisplayName(this CL_AniDB_Anime_Relation aniDbAnimeRelation)
        {
            if (aniDbAnimeRelation.AniDB_Anime != null)
                return aniDbAnimeRelation.AniDB_Anime.FormattedTitle;
            return "Data Missing";
        }

        public static int GetSortPriority(this CL_AniDB_Anime_Relation aniDbAnimeRelation)
        {
            if (aniDbAnimeRelation.RelationType.Equals("Prequel", StringComparison.InvariantCultureIgnoreCase))
                return 1;
            if (aniDbAnimeRelation.RelationType.Equals("Sequel", StringComparison.InvariantCultureIgnoreCase))
                return 2;
            return Int32.MaxValue;
        }



        public static bool IsAdminUser(this JMMUser JMMUser) => JMMUser.IsAdmin == 1;

        public static bool IsAniDBUserBool(this JMMUser JMMUser) => JMMUser.IsAniDBUser == 1;

        public static bool IsTraktUserBool(this JMMUser JMMUser) => JMMUser.IsTraktUser == 1;

        public static bool IsFolderDropSource(this ImportFolder ImportFolder) => ImportFolder.IsDropSource == 1;

        public static bool IsFolderDropDestination(this ImportFolder ImportFolder) => ImportFolder.IsDropDestination == 1;

        public static bool IsFolderWatched(this ImportFolder ImportFolder) => ImportFolder.IsWatched == 1;

        public static bool HasMessageURL(this Azure_AdminMessage msg) => !String.IsNullOrEmpty(msg.MessageURL);

        public static bool HasMissingEpisodesAny(this AnimeGroup grp) => grp.MissingEpisodeCount > 0 || grp.MissingEpisodeCountGroups > 0;

        public static bool HasMissingEpisodesGroups(this AnimeGroup gr) => gr.MissingEpisodeCountGroups > 0;

        public static bool HasMissingEpisodes(this AnimeGroup grp) => grp.MissingEpisodeCountGroups > 0;
    }
}
