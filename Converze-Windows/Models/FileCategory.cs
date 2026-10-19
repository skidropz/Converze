using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Converze.Models
{
    public enum FileCategory
    {
        Image,
        Video,
        Audio,
        Pdf,
        Document,
        Spreadsheet,
        Presentation,
        Subtitle,
        Epub,
        Archive,
        Unknown
    }

    public static class FormatMatrix
    {
        public static readonly string[] ImageInputs = new[]
        {
            "jpg", "jpeg", "png", "webp", "heic", "heif", "avif", "tiff", "tif",
            "bmp", "ico", "svg", "gif", "cr2", "nef", "arw", "dng", "orf", "rw2"
        };

        public static readonly string[] ImageOutputs = new[]
        {
            "webp", "png", "jpg", "avif", "heic", "pdf", "ico", "bmp", "tiff"
        };

        public static readonly string[] VideoInputs = new[]
        {
            "mp4", "mov", "mkv", "webm", "avi", "m4v", "wmv", "flv", "mpeg", "mpg", "3gp", "ts"
        };

        public static readonly string[] VideoOutputs = new[]
        {
            "mp4", "mov", "webm", "gif", "mkv", "avi", "mp3", "wav"
        };

        public static readonly string[] AudioInputs = new[]
        {
            "mp3", "wav", "m4a", "flac", "aac", "ogg", "opus", "wma", "aiff", "alac"
        };

        public static readonly string[] AudioOutputs = new[]
        {
            "mp3", "wav", "m4a", "flac", "aac", "ogg", "opus", "aiff"
        };

        public static readonly string[] DocumentInputs = new[]
        {
            "docx", "doc", "odt", "rtf", "txt", "md", "markdown", "html", "htm", "tex"
        };

        public static readonly string[] DocumentOutputs = new[]
        {
            "pdf", "docx", "txt", "md", "html", "rtf"
        };

        public static readonly string[] SpreadsheetInputs = new[]
        {
            "xlsx", "xls", "ods", "csv", "tsv", "json"
        };

        public static readonly string[] SpreadsheetOutputs = new[]
        {
            "csv", "xlsx", "json", "pdf", "html", "md"
        };

        public static readonly string[] PresentationInputs = new[]
        {
            "pptx", "ppt", "odp", "key"
        };

        public static readonly string[] PresentationOutputs = new[]
        {
            "pdf", "pptx", "png", "jpg"
        };

        public static readonly string[] SubtitleInputs = new[]
        {
            "srt", "vtt", "ass", "ssa", "sub"
        };

        public static readonly string[] SubtitleOutputs = new[]
        {
            "srt", "vtt", "txt", "ass"
        };

        public static readonly string[] ArchiveInputs = new[]
        {
            "zip", "tar", "gz", "tgz", "7z", "rar", "bz2", "xz"
        };

        public static readonly string[] ArchiveOutputs = new[]
        {
            "zip", "tar", "gz", "7z"
        };

        private static readonly HashSet<string> ImageInputSet = new(ImageInputs, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> VideoInputSet = new(VideoInputs, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> AudioInputSet = new(AudioInputs, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> DocumentInputSet = new(DocumentInputs, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SpreadsheetInputSet = new(SpreadsheetInputs, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PresentationInputSet = new(PresentationInputs, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SubtitleInputSet = new(SubtitleInputs, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ArchiveInputSet = new(ArchiveInputs, StringComparer.OrdinalIgnoreCase);

        public static FileCategory Category(string ext)
        {
            string lower = ext.ToLowerInvariant().TrimStart('.');
            if (lower == "pdf") return FileCategory.Pdf;
            if (lower == "epub") return FileCategory.Epub;
            if (ImageInputSet.Contains(lower)) return FileCategory.Image;
            if (VideoInputSet.Contains(lower)) return FileCategory.Video;
            if (AudioInputSet.Contains(lower)) return FileCategory.Audio;
            if (DocumentInputSet.Contains(lower)) return FileCategory.Document;
            if (SpreadsheetInputSet.Contains(lower)) return FileCategory.Spreadsheet;
            if (PresentationInputSet.Contains(lower)) return FileCategory.Presentation;
            if (SubtitleInputSet.Contains(lower)) return FileCategory.Subtitle;
            if (ArchiveInputSet.Contains(lower)) return FileCategory.Archive;
            return FileCategory.Unknown;
        }

        public static List<string> OutputFormats(string inputExtension, bool isDirectory = false)
        {
            if (isDirectory)
            {
                return new List<string> { "zip", "tar", "gz", "7z" };
            }

            string ext = inputExtension.ToLowerInvariant().TrimStart('.');
            var cat = Category(ext);

            string normalizedSelf = ext switch
            {
                "jpeg" => "jpg",
                "tif" => "tiff",
                "heif" => "heic",
                "markdown" => "md",
                "htm" => "html",
                _ => ext
            };

            string[] rawCandidates = cat switch
            {
                FileCategory.Image => ext == "gif"
                    ? new[] { "mp4", "webp", "png", "mov", "jpg", "pdf" }
                    : ImageOutputs,
                FileCategory.Video => VideoOutputs,
                FileCategory.Audio => AudioOutputs,
                FileCategory.Pdf => new[] { "png", "jpg", "txt", "docx", "md", "html" },
                FileCategory.Document => DocumentOutputs,
                FileCategory.Spreadsheet => SpreadsheetOutputs,
                FileCategory.Presentation => PresentationOutputs,
                FileCategory.Subtitle => SubtitleOutputs,
                FileCategory.Epub => new[] { "pdf", "txt", "html", "md" },
                FileCategory.Archive => ArchiveOutputs,
                _ => new[] { "zip", "pdf", "txt", "png", "jpg", "mp4" }
            };

            return rawCandidates
                .Where(f => !string.Equals(f, normalizedSelf, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(f, ext, StringComparison.OrdinalIgnoreCase))
                .Take(7)
                .ToList();
        }

        public static string DisplayLabel(string format)
        {
            return format.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => "JPG",
                "png" => "PNG",
                "webp" => "WEBP",
                "heic" => "HEIC",
                "avif" => "AVIF",
                "tiff" => "TIFF",
                "bmp" => "BMP",
                "ico" => "ICO",
                "gif" => "GIF",
                "svg" => "SVG",
                "mp4" => "MP4",
                "mov" => "MOV",
                "webm" => "WEBM",
                "mkv" => "MKV",
                "avi" => "AVI",
                "mp3" => "MP3",
                "wav" => "WAV",
                "m4a" => "M4A",
                "flac" => "FLAC",
                "aac" => "AAC",
                "ogg" => "OGG",
                "opus" => "OPUS",
                "aiff" => "AIFF",
                "pdf" => "PDF",
                "docx" => "DOCX",
                "txt" => "TXT",
                "md" => "MD",
                "html" => "HTML",
                "rtf" => "RTF",
                "csv" => "CSV",
                "xlsx" => "XLSX",
                "json" => "JSON",
                "pptx" => "PPTX",
                "srt" => "SRT",
                "vtt" => "VTT",
                "ass" => "ASS",
                "zip" => "ZIP",
                "tar" => "TAR",
                "gz" => "GZ",
                "7z" => "7Z",
                _ => format.ToUpperInvariant()
            };
        }
    }

    public sealed class DraggedSelection : IEquatable<DraggedSelection>
    {
        public List<string> FilePaths { get; }
        public string PrimaryPath => FilePaths.FirstOrDefault() ?? "";
        public bool IsDirectory { get; }
        public string PrimaryExtension { get; }
        public FileCategory Category { get; }
        public long TotalBytes { get; }

        public DraggedSelection(IEnumerable<string> paths)
        {
            FilePaths = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            string first = PrimaryPath;
            IsDirectory = !string.IsNullOrEmpty(first) && Directory.Exists(first);
            PrimaryExtension = IsDirectory ? "" : Path.GetExtension(first).TrimStart('.').ToLowerInvariant();
            Category = IsDirectory ? FileCategory.Archive : FormatMatrix.Category(PrimaryExtension);

            long sum = 0;
            foreach (var p in FilePaths)
            {
                try
                {
                    if (File.Exists(p))
                    {
                        sum += new FileInfo(p).Length;
                    }
                    else if (Directory.Exists(p))
                    {
                        var dirInfo = new DirectoryInfo(p);
                        foreach (var f in dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                        {
                            sum += f.Length;
                        }
                    }
                }
                catch
                {
                    // Ignore inaccessible file size errors
                }
            }
            TotalBytes = sum > 0 ? sum : 2_248_190;
        }

        public string FormattedSize
        {
            get
            {
                double b = TotalBytes;
                if (b < 1024) return $"{b:0} B";
                double kb = b / 1024.0;
                if (kb < 1024) return $"{kb:0.0} KB";
                double mb = kb / 1024.0;
                if (mb < 1024) return $"{mb:0.0} MB";
                double gb = mb / 1024.0;
                return $"{gb:0.00} GB";
            }
        }

        public string DisplayName
        {
            get
            {
                if (FilePaths.Count == 0) return "No file";
                string firstName = Path.GetFileName(PrimaryPath);
                if (string.IsNullOrEmpty(firstName)) firstName = PrimaryPath;
                if (FilePaths.Count == 1) return firstName;
                return ConverzePreferences.Shared.Tr(
                    $"{FilePaths.Count} fișiere selectate",
                    $"{FilePaths.Count} files selected"
                );
            }
        }

        public List<WheelTarget> AvailableFormatTargets()
        {
            return FormatMatrix.OutputFormats(PrimaryExtension, IsDirectory)
                .Select(f => new WheelTarget(f))
                .ToList();
        }

        public bool Equals(DraggedSelection? other)
        {
            if (other == null) return false;
            if (FilePaths.Count != other.FilePaths.Count) return false;
            for (int i = 0; i < FilePaths.Count; i++)
            {
                if (!string.Equals(FilePaths[i], other.FilePaths[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as DraggedSelection);
        public override int GetHashCode() => FilePaths.Count > 0 ? FilePaths[0].GetHashCode() : 0;
    }
}
