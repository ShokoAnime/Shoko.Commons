﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using F23.StringSimilarity;
using F23.StringSimilarity.Interfaces;
using Shoko.Models.Client;
using Shoko.Models.Enums;

namespace Shoko.Commons.Utils
{
    public static class Misc
    {
        public static string FormatByteSize(long fileSize)
        {
            // Get absolute value
            long absoluteFileSize = fileSize < 0 ? -fileSize : fileSize;
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absoluteFileSize >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = fileSize >> 50;
            }
            else if (absoluteFileSize >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = fileSize >> 40;
            }
            else if (absoluteFileSize >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = fileSize >> 30;
            }
            else if (absoluteFileSize >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = fileSize >> 20;
            }
            else if (absoluteFileSize >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = fileSize >> 10;
            }
            else if (absoluteFileSize >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = fileSize;
            }
            else
            {
                return fileSize.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = readable / 1024;
            // Return formatted number with suffix
            return readable.ToString("0.# ") + suffix;
        }

        public static string ToName<T,U>(this Expression<Func<T, U>> expr)
        {
            var member = expr.Body as MemberExpression;
            if (member == null)
            {
                var ue = expr.Body as UnaryExpression;
                if (ue != null)
                    member = ue.Operand as MemberExpression;
            }
            return member?.Member.Name;
        }
        public static Dictionary<string, bool> GetSortDescriptions(this CL_GroupFilter gf)
        {
            Dictionary<string, bool> lst = new Dictionary<string, bool>();
            List<GroupFilterSortingCriteria> criterias = GroupFilterSortingCriteria.Create(gf.GroupFilterID, gf.SortingCriteria);
            foreach (GroupFilterSortingCriteria f in criterias)
            {
                KeyValuePair<string, bool> k = GetSortDescription(f.SortType, f.SortDirection);
                lst[k.Key] = k.Value;
            }
            return lst;
        }

        public static IQueryable<T> SortGroups<T>(this CL_GroupFilter gf, IQueryable<T> list) where T: CL_AnimeGroup_User
        {



            List<GroupFilterSortingCriteria> criterias = GroupFilterSortingCriteria.Create(gf.GroupFilterID, gf.SortingCriteria);
            foreach (GroupFilterSortingCriteria f in criterias)
            {
                list = GeneratePredicate(list, f.SortType, f.SortDirection);
            }
            return list;
        }

        public static IQueryable<T> GeneratePredicate<T>(this IQueryable<T> lst, GroupFilterSorting sortType, GroupFilterSortDirection sortDirection) where T : CL_AnimeGroup_User
        {
            Expression<Func<T, object>> selector;

            switch (sortType)
            {
                case GroupFilterSorting.AniDBRating:
                    selector = c =>c.Stat_AniDBRating;
                    break;
                case GroupFilterSorting.EpisodeAddedDate:
                    selector = c => c.EpisodeAddedDate;
                    break;
                case GroupFilterSorting.EpisodeAirDate:
                    selector = c => c.LatestEpisodeAirDate;
                    break;
                case GroupFilterSorting.EpisodeWatchedDate:
                    selector = c => c.WatchedDate;
                    break;
                case GroupFilterSorting.GroupName:
                    selector = c => c.GroupName;
                    break;
                case GroupFilterSorting.SortName:
                    selector = c => c.SortName;
                    break;
                case GroupFilterSorting.MissingEpisodeCount:
                    selector = c => c.MissingEpisodeCount;
                    break;
                case GroupFilterSorting.SeriesAddedDate:
                    selector = c => c.Stat_SeriesCreatedDate;
                    break;
                case GroupFilterSorting.SeriesCount:
                    selector = c => c.Stat_SeriesCount;
                    break;
                case GroupFilterSorting.UnwatchedEpisodeCount:
                    selector = c => c.UnwatchedEpisodeCount;
                    break;
                case GroupFilterSorting.UserRating:
                    selector = c => c.Stat_UserVoteOverall;
                    break;
                case GroupFilterSorting.Year:
                    if (sortDirection == GroupFilterSortDirection.Asc)
                        selector = c => c.Stat_AirDate_Min;   
                    else
                        selector = c => c.Stat_AirDate_Max;
                    break;
                default:
                    selector = c => c.GroupName;
                    break;
            }
            if (lst.GetType().IsAssignableFrom(typeof(IOrderedQueryable<T>)))
            {
                IOrderedQueryable<T> n = (IOrderedQueryable<T>) lst;
                if (sortDirection != GroupFilterSortDirection.Asc)
                    return n.ThenByDescending(selector);
                return n.ThenBy(selector);
            }
            if (sortDirection != GroupFilterSortDirection.Asc)
                return lst.OrderByDescending(selector);
            return lst.OrderBy(selector);

        }

        public static KeyValuePair<string, bool> GetSortDescription(this GroupFilterSorting sortType, GroupFilterSortDirection sortDirection)
        {
            string sortColumn = "";
            bool srt = false;
            switch (sortType)
            {
                case GroupFilterSorting.AniDBRating:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, decimal>>)(c => c.Stat_AniDBRating)).ToName();
                    break;
                case GroupFilterSorting.EpisodeAddedDate:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.EpisodeAddedDate)).ToName();
                    break;
                case GroupFilterSorting.EpisodeAirDate:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.LatestEpisodeAirDate)).ToName();
                    break;
                case GroupFilterSorting.EpisodeWatchedDate:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.WatchedDate)).ToName();
                    break;
                case GroupFilterSorting.GroupName:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, string>>)(c => c.GroupName)).ToName();
                    break;
                case GroupFilterSorting.SortName:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, string>>)(c => c.SortName)).ToName();
                    break;
                case GroupFilterSorting.MissingEpisodeCount:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, int>>)(c => c.MissingEpisodeCount)).ToName();
                    break;
                case GroupFilterSorting.SeriesAddedDate:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.Stat_SeriesCreatedDate)).ToName();
                    break;
                case GroupFilterSorting.SeriesCount:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, int>>)(c => c.Stat_SeriesCount)).ToName();
                    break;
                case GroupFilterSorting.UnwatchedEpisodeCount:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, int>>)(c => c.UnwatchedEpisodeCount)).ToName();
                    break;
                case GroupFilterSorting.UserRating:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, decimal?>>)(c => c.Stat_UserVoteOverall)).ToName();
                    break;
                case GroupFilterSorting.Year:
                    sortColumn = sortDirection == GroupFilterSortDirection.Asc ? ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.Stat_AirDate_Min)).ToName() : ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.Stat_AirDate_Max)).ToName();
                    break;
                case GroupFilterSorting.GroupFilterName:
                    sortColumn = ((Expression<Func<CL_GroupFilter, string>>)(c => c.GroupFilterName)).ToName();
                    break;
                default:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, string>>)(c => c.GroupName)).ToName();
                    break;
            }

            if (sortDirection != GroupFilterSortDirection.Asc)
                srt = true;
            return new KeyValuePair<string, bool>(sortColumn,srt);
        }

        public static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length; //length of s
            int m = t.Length; //length of t

            int[,] d = new int[n + 1, m + 1]; // matrix

            int cost; // cost

            // Step 1
            if (n == 0) return m;
            if (m == 0) return n;

            // Step 2
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            // Step 3
            for (int i = 1; i <= n; i++)
            {
                //Step 4
                for (int j = 1; j <= m; j++)
                {
                    // Step 5
                    cost = t.Substring(j - 1, 1) == s.Substring(i - 1, 1) ? 0 : 1;

                    // Step 6
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            // Step 7
            return d[n, m];
        }

        private static readonly IStringDistance DiceSearch = new SorensenDice();

        private static bool IsAllowedSearchCharacter(this char a)
        {
            if (a == 32) return true;
            if (a == '!') return true;
            if (a == '.') return true;
            if (a == '?') return true;
            if (a == '*') return true;
            if (a == '&') return true;
            if (a > 47 && a < 58) return true;
            if (a > 64 && a < 91) return true;
            return a > 96 && a < 123;
        }

        public static string FilterCharacters(this string value, IEnumerable<char> allowed, bool blacklist = false)
        {
            if (!(allowed is HashSet<char> newSet)) newSet = new HashSet<char>(allowed);
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char a in value)
            {
                if (!(blacklist ^ newSet.Contains(a))) continue;
                sb.Append(a);
            }
            return sb.ToString();
        }

        public static string FilterSearchCharacters(this string value)
        {
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char a in value)
            {
                if (!a.IsAllowedSearchCharacter()) continue;
                sb.Append(a);
            }
            return sb.ToString();
        }

        public static string FilterLetters(this string value)
        {
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char a in value)
            {
                if (a < 48 && a != 32) continue;
                if (a > 122) continue;
                if (a > 90 && a < 97) continue;
                if (a > 57 && a < 65) continue;
                sb.Append(a);
            }

            return sb.ToString();
        }

        public static string CompactCharacters(this string s, params char[] chars)
        {
            StringBuilder sb = new StringBuilder(s);

            CompactCharacters(sb, chars);

            return sb.ToString();
        }

        private static void CompactCharacters(StringBuilder sb, params char[] chars)
        {
            if (sb.Length == 0)
                return;

            var charSet = chars.ToList();

            // set [start] to first not-whitespace char or to sb.Length

            int start = 0;

            while (start < sb.Length)
            {
                if (charSet.Contains(sb[start]))
                    start++;
                else
                    break;
            }
            if (start == sb.Length)
            {
                sb.Length = 0;
                return;
            }
            int end = sb.Length - 1;

            while (end >= 0)
            {
                if (charSet.Contains(sb[end]))
                    end--;
                else
                    break;
            }
            int dest = 0;
            var prevChar = ' ';

            for (int i = start; i <= end; i++)
            {
                if (charSet.Contains(sb[i]))
                {
                    if (prevChar == sb[i]) continue;
                    sb[dest] = sb[i];
                    dest++;
                }
                else
                {
                    sb[dest] = sb[i];
                    dest++;
                }

                prevChar = sb[i];
            }

            sb.Length = dest;
        }

        public static String CompactWhitespaces(this string s)
        {
            StringBuilder sb = new StringBuilder(s);

            CompactWhitespaces(sb);

            return sb.ToString();
        }

        private static void CompactWhitespaces(StringBuilder sb)
        {
            if (sb.Length == 0)
                return;

            // set [start] to first not-whitespace char or to sb.Length

            int start = 0;

            while (start < sb.Length)
            {
                if (Char.IsWhiteSpace(sb[start]))
                    start++;
                else
                    break;
            }
            if (start == sb.Length)
            {
                sb.Length = 0;
                return;
            }
            int end = sb.Length - 1;

            while (end >= 0)
            {
                if (Char.IsWhiteSpace(sb[end]))
                    end--;
                else
                    break;
            }
            int dest = 0;
            bool previousIsWhitespace = false;

            for (int i = start; i <= end; i++)
            {
                if (Char.IsWhiteSpace(sb[i]))
                {
                    if (previousIsWhitespace) continue;
                    previousIsWhitespace = true;
                    sb[dest] = ' ';
                    dest++;
                }
                else
                {
                    previousIsWhitespace = false;
                    sb[dest] = sb[i];
                    dest++;
                }
            }

            sb.Length = dest;
        }

        /// <summary>
        /// Use the Bitap Fuzzy Algorithm to search for a string
        /// This is used in grep, for an easy understanding
        /// ref: https://en.wikipedia.org/wiki/Bitap_algorithm
        /// source: https://www.programmingalgorithms.com/algorithm/fuzzy-bitap-algorithm
        /// </summary>
        /// <param name="text">The string to search</param>
        /// <param name="pattern">The query to search for</param>
        /// <param name="k">The maximum distance (in Levenshtein) to be allowed</param>
        /// <param name="dist">The Levenstein distance of the result. -1 if inapplicable</param>
        /// <returns></returns>
        public static SearchInfo<T> BitapFuzzySearch32<T>(string text, string pattern, int k, T value)
        {
            int result = -1;
            int m = pattern.Length;
            uint[] R;
            uint[] patternMask = new uint[128];
            int i, d;
            int dist = k + 1;

            // We are doing bitwise operations, this can be affected by how many bits the CPU is able to process
            const int WORD_SIZE = 31;

            if (string.IsNullOrEmpty(pattern)) return new SearchInfo<T> {Index = -1, Distance = dist};
            if (m > WORD_SIZE) return new SearchInfo<T> {Index = -1, Distance = dist}; //Error: The pattern is too long!

            R = new uint[(k + 1) * sizeof(uint)];
            for (i = 0; i <= k; ++i)
                R[i] = ~1u;

            for (i = 0; i <= 127; ++i)
                patternMask[i] = ~0u;

            for (i = 0; i < m; ++i)
                patternMask[pattern[i]] &= ~(1u << i);

            for (i = 0; i < text.Length; ++i)
            {
                uint oldRd1 = R[0];

                R[0] |= patternMask[text[i]];
                R[0] <<= 1;

                for (d = 1; d <= k; ++d)
                {
                    uint tmp = R[d];

                    R[d] = (oldRd1 & (R[d] | patternMask[text[i]])) << 1;
                    oldRd1 = tmp;
                }

                if (0 == (R[k] & (1 << m)))
                {
                    dist = R[k] > int.MaxValue ? int.MaxValue : Convert.ToInt32(R[k]);
                    result = (i - m) + 1;
                    break;
                }
            }

            return new SearchInfo<T> {Index = result, Distance = dist, Result = value};
        }

        public static SearchInfo<T> BitapFuzzySearch64<T>(string inputString, string query, int k, T value)
        {
            int result = -1;
            int m = query.Length;
            ulong[] R;
            ulong[] patternMask = new ulong[128];
            int i, d;
            int dist = inputString.Length;

            // We are doing bitwise operations, this can be affected by how many bits the CPU is able to process
            const int WORD_SIZE = 63;

            if (string.IsNullOrEmpty(query)) return new SearchInfo<T> {Index = -1, Distance = dist};
            if (m > WORD_SIZE) return new SearchInfo<T> {Index = -1, Distance = dist}; //Error: The pattern is too long!

            R = new ulong[(k + 1) * sizeof(ulong)];
            for (i = 0; i <= k; ++i)
                R[i] = ~1UL;

            for (i = 0; i <= 127; ++i)
                patternMask[i] = ~0UL;

            for (i = 0; i < m; ++i)
                patternMask[query[i]] &= ~(1UL << i);

            for (i = 0; i < inputString.Length; ++i)
            {
                ulong oldRd1 = R[0];

                R[0] |= patternMask[inputString[i]];
                R[0] <<= 1;

                for (d = 1; d <= k; ++d)
                {
                    ulong tmp = R[d];

                    R[d] = (oldRd1 & (R[d] | patternMask[inputString[i]])) << 1;
                    oldRd1 = tmp;
                }

                if (0 == (R[k] & (1UL << m)))
                {
                    dist = R[k] > int.MaxValue ? int.MaxValue : Convert.ToInt32(R[k]);
                    result = (i - m) + 1;
                    break;
                }
            }

            return new SearchInfo<T> {Index = result, Distance = dist, Result = value};
        }

        public class SearchInfo<T>
        {
            public T Result { get; set; }
            public int Index { get; set; }
            public double Distance { get; set; }
            public bool ExactMatch { get; set; }

            protected bool Equals(SearchInfo<T> other)
            {
                return Index == other.Index && Math.Abs(Distance - other.Distance) < 0.0001D && ExactMatch == other.ExactMatch;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((SearchInfo<T>) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Index;
                    hashCode = (hashCode * 397) ^ Distance.GetHashCode();
                    hashCode = (hashCode * 397) ^ ExactMatch.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(SearchInfo<T> left, SearchInfo<T> right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(SearchInfo<T> left, SearchInfo<T> right)
            {
                return !Equals(left, right);
            }
        }

        public static SearchInfo<T> DiceFuzzySearch<T>(string text, string pattern, int k, T value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return new SearchInfo<T> {Index = -1, Distance = int.MaxValue};
            // This forces ASCII, because it's faster to stop caring if ss and ß are the same
            // No it's not perfect, but it works better for those who just want to do lazy searching
            string inputString = text.FilterSearchCharacters();
            string query = pattern.FilterSearchCharacters();
            inputString = inputString.Replace('_', ' ').Replace('-', ' ');
            query = query.Replace('_', ' ').Replace('-', ' ');
            query = query.CompactWhitespaces();
            inputString = inputString.CompactWhitespaces();
            // Case insensitive. We just removed the fancy characters, so latin alphabet lowercase is all we should have
            query = query.ToLowerInvariant();
            inputString = inputString.ToLowerInvariant();

            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(inputString))
                return new SearchInfo<T> {Index = -1, Distance = int.MaxValue};

            int index = inputString.IndexOf(query, StringComparison.Ordinal);
            // Shortcut
            if (index > -1)
            {
                return new SearchInfo<T> {Index = index, Distance = 0, ExactMatch = true, Result = value};
            }

            // always search the longer string for the shorter one
            if (query.Length > inputString.Length)
            {
                string temp = query;
                query = inputString;
                inputString = temp;
            }

            double result = DiceSearch.Distance(inputString, query);
            // Don't count an error as liberally when the title is short
            if (inputString.Length < 5 && result > 0.5) return new SearchInfo<T> {Index = -1, Distance = int.MaxValue};

            if (result >= 0.8) return new SearchInfo<T> {Index = -1, Distance = int.MaxValue};

            return new SearchInfo<T> {Index = 0, Distance = result, Result = value};
        }

        public static bool FuzzyMatches(this string text, string query)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return false;
            int k = Math.Max(Math.Min((int)(text.Length / 6D), (int)(query.Length / 6D)), 1);
            SearchInfo<string> result = DiceFuzzySearch(text, query, k, text);
            if (result.ExactMatch) return true;
            if (text.Length <= 5 && result.Distance > 0.5D) return false;
            return result.Distance < 0.8D;
        }

        public static string RemoveDiacritics(this string text) 
        {
            string s = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

#nullable enable
        public static bool IsImageValid(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                var bytes = new byte[12];
                if (fs.Length < 12) return false;
                fs.Read(bytes, 0, 12);
                return GetImageFormat(bytes) != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsImageValid(byte[] bytes)
        {
            try
            {
                if (bytes.Length < 12) return false;
                return GetImageFormat(bytes) != null;
            }
            catch
            {
                return false;
            }
        }

        public static string? GetImageFormat(byte[] bytes)
        {
            // https://en.wikipedia.org/wiki/BMP_file_format#File_structure
            var bmp = new byte[] { 66, 77 };
            // https://en.wikipedia.org/wiki/GIF#File_format
            var gif = new byte[] { 71, 73, 70 };
            // https://en.wikipedia.org/wiki/JPEG#Syntax_and_structure
            var jpeg = new byte[] { 255, 216 };
            // https://en.wikipedia.org/wiki/Portable_Network_Graphics#File_header
            var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            // https://en.wikipedia.org/wiki/TIFF#Byte_order
            var tiff1 = new byte[] { 73, 73, 42, 0 };
            var tiff2 = new byte[] { 77, 77, 42, 0 };
            // https://developers.google.com/speed/webp/docs/riff_container#webp_file_header
            var webp1 = new byte[] { 82, 73, 70, 70 };
            var webp2 = new byte[] { 87, 69, 66, 80 };

            if (png.SequenceEqual(bytes.Take(png.Length)))
                return "png";

            if (jpeg.SequenceEqual(bytes.Take(jpeg.Length)))
                return "jpeg";

            if (webp1.SequenceEqual(bytes.Take(webp1.Length)) &&
                webp2.SequenceEqual(bytes.Skip(8).Take(webp2.Length)))
                return "webp";

            if (gif.SequenceEqual(bytes.Take(gif.Length)))
                return "gif";

            if (bmp.SequenceEqual(bytes.Take(bmp.Length)))
                return "bmp";

            if (tiff1.SequenceEqual(bytes.Take(tiff1.Length)) ||
                tiff2.SequenceEqual(bytes.Take(tiff2.Length)))
                return "tiff";

            return null;
        }
#nullable disable

        public static void Deconstruct<T, T1>(this KeyValuePair<T, T1> kvp, out T key, out T1 value)
        {
            key = kvp.Key;
            value = kvp.Value;
        }
    }
}
