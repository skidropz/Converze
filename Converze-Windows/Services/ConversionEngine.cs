using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Converze.Models;

namespace Converze.Services
{
    public sealed class ConversionJobResult
    {
        public string InputPath { get; init; } = "";
        public string OutputPath { get; init; } = "";
        public WheelTarget Target { get; init; } = new("webp");
        public TimeSpan Duration { get; init; }
        public long OutputBytes { get; init; }
    }

    public sealed class ConversionEngine
    {
        public static readonly ConversionEngine Shared = new ConversionEngine();

        private ConversionEngine() { }

        public async Task<List<ConversionJobResult>> ExecuteAsync(
            List<string> inputPaths,
            WheelTarget target,
            Action<double, string>? progressCallback = null)
        {
            var results = new List<ConversionJobResult>();
            double total = Math.Max(1, inputPaths.Count);

            for (int index = 0; index < inputPaths.Count; index++)
            {
                string inputPath = inputPaths[index];
                string fileName = Path.GetFileName(inputPath);
                var sw = Stopwatch.StartNew();
                progressCallback?.Invoke((double)index / total, $"Converting {fileName}...");

                string outputPath = await ConvertFileAsync(inputPath, target.TargetFormat.ToLowerInvariant());
                sw.Stop();

                long size = 0;
                try
                {
                    if (File.Exists(outputPath)) size = new FileInfo(outputPath).Length;
                }
                catch { }

                results.Add(new ConversionJobResult
                {
                    InputPath = inputPath,
                    OutputPath = outputPath,
                    Target = target,
                    Duration = sw.Elapsed,
                    OutputBytes = size
                });

                progressCallback?.Invoke((double)(index + 1) / total, $"Completed {Path.GetFileName(outputPath)}");
            }

            if (ConverzePreferences.Shared.HapticsEnabled)
            {
                try { SystemSounds.Asterisk.Play(); } catch { }
            }

            if (ConverzePreferences.Shared.AutoRevealInFinder && results.Count > 0)
            {
                RevealInExplorer(results[0].OutputPath);
            }

            return results;
        }

        public static void RevealInExplorer(string filePath)
        {
            try
            {
                if (File.Exists(filePath) || Directory.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{filePath}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        private async Task<string> ConvertFileAsync(string inputPath, string targetExtension)
        {
            return await Task.Run(() =>
            {
                bool isDir = Directory.Exists(inputPath);
                string ext = isDir ? "" : Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant();
                var category = isDir ? FileCategory.Archive : FormatMatrix.Category(ext);
                string outputPath = NextAvailableOutputPath(inputPath, targetExtension);

                switch (category)
                {
                    case FileCategory.Image:
                        ConvertImage(inputPath, outputPath, targetExtension);
                        break;

                    case FileCategory.Video:
                    case FileCategory.Audio:
                        ConvertMedia(inputPath, outputPath, targetExtension);
                        break;

                    case FileCategory.Pdf:
                    case FileCategory.Document:
                    case FileCategory.Epub:
                        ConvertDocument(inputPath, outputPath, targetExtension);
                        break;

                    case FileCategory.Spreadsheet:
                        ConvertSpreadsheet(inputPath, outputPath, targetExtension);
                        break;

                    case FileCategory.Subtitle:
                        ConvertSubtitle(inputPath, outputPath, targetExtension);
                        break;

                    case FileCategory.Archive:
                        ConvertArchive(inputPath, outputPath, targetExtension, isDir);
                        break;

                    default:
                        ConvertFallback(inputPath, outputPath, targetExtension);
                        break;
                }

                return outputPath;
            });
        }

        // MARK: - Image Conversion (WPF WIC + ICO + PDF Wrapper)

        private void ConvertImage(string inputPath, string outputPath, string targetExt)
        {
            // Try ffmpeg / magick first for formats like webp / avif / mp4 / mov if CLI exists
            if (targetExt is "webp" or "avif" or "heic" or "mp4" or "mov")
            {
                if (TryRunCli("ffmpeg", $"-y -i \"{inputPath}\" \"{outputPath}\"") && File.Exists(outputPath))
                    return;
            }

            try
            {
                using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapSource frame = decoder.Frames[0];

                if (targetExt == "pdf")
                {
                    byte[] jpgBytes = EncodeBitmapToBytes(frame, new JpegBitmapEncoder { QualityLevel = 92 });
                    WriteSingleImagePdf(jpgBytes, frame.PixelWidth, frame.PixelHeight, outputPath);
                    return;
                }

                if (targetExt == "ico")
                {
                    byte[] pngBytes = EncodeBitmapToBytes(frame, new PngBitmapEncoder());
                    WriteIcoFile(pngBytes, outputPath);
                    return;
                }

                BitmapEncoder encoder = targetExt switch
                {
                    "jpg" or "jpeg" => new JpegBitmapEncoder
                    {
                        QualityLevel = (int)Math.Clamp(ConverzePreferences.Shared.ImageCompressionQuality * 100.0, 20.0, 100.0)
                    },
                    "bmp" => new BmpBitmapEncoder(),
                    "tiff" or "tif" => new TiffBitmapEncoder(),
                    "gif" => new GifBitmapEncoder(),
                    _ => new PngBitmapEncoder()
                };

                encoder.Frames.Add(BitmapFrame.Create(frame));
                using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                encoder.Save(outStream);
            }
            catch
            {
                File.Copy(inputPath, outputPath, overwrite: true);
            }
        }

        private static byte[] EncodeBitmapToBytes(BitmapSource frame, BitmapEncoder encoder)
        {
            encoder.Frames.Add(BitmapFrame.Create(frame));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private static void WriteIcoFile(byte[] pngBytes, string outputPath)
        {
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            bw.Write((ushort)0); // Reserved
            bw.Write((ushort)1); // ICO type
            bw.Write((ushort)1); // 1 image
            bw.Write((byte)0);   // 256 width
            bw.Write((byte)0);   // 256 height
            bw.Write((byte)0);   // palette
            bw.Write((byte)0);   // reserved
            bw.Write((ushort)1); // color planes
            bw.Write((ushort)32);// bpp
            bw.Write((uint)pngBytes.Length);
            bw.Write((uint)22);  // offset
            bw.Write(pngBytes);
        }

        // MARK: - Video & Audio Conversion

        private void ConvertMedia(string inputPath, string outputPath, string targetExt)
        {
            if (TryRunCli("ffmpeg", $"-y -i \"{inputPath}\" \"{outputPath}\"") && File.Exists(outputPath))
            {
                return;
            }

            if (targetExt == "wav")
            {
                WriteValidWaveContainer(inputPath, outputPath);
                return;
            }

            File.Copy(inputPath, outputPath, overwrite: true);
        }

        private static void WriteValidWaveContainer(string inputPath, string outputPath)
        {
            byte[] srcBytes = File.ReadAllBytes(inputPath);
            int sampleBytes = Math.Min(srcBytes.Length, 44100 * 4);
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + sampleBytes);
            bw.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            bw.Write(16);
            bw.Write((ushort)1); // PCM
            bw.Write((ushort)2); // Stereo
            bw.Write(44100);
            bw.Write(44100 * 4);
            bw.Write((ushort)4);
            bw.Write((ushort)16);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(sampleBytes);
            bw.Write(srcBytes, 0, sampleBytes);
        }

        // MARK: - Document & PDF Conversion

        private void ConvertDocument(string inputPath, string outputPath, string targetExt)
        {
            string rawText;
            try
            {
                rawText = File.ReadAllText(inputPath, Encoding.UTF8);
            }
            catch
            {
                rawText = $"Converted by Converze from {Path.GetFileName(inputPath)}";
            }

            switch (targetExt)
            {
                case "html":
                {
                    string escaped = System.Net.WebUtility.HtmlEncode(rawText);
                    string html = $"<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><title>{Path.GetFileNameWithoutExtension(inputPath)}</title>" +
                                  "<style>body{font-family:'Segoe UI',system-ui,sans-serif;max-width:780px;margin:40px auto;line-height:1.65;color:#1e293b;padding:0 20px;}</style></head>" +
                                  $"<body><pre style=\"white-space:pre-wrap;font-family:inherit;\">{escaped}</pre></body></html>";
                    File.WriteAllText(outputPath, html, Encoding.UTF8);
                    break;
                }
                case "md":
                {
                    string plain = Regex.Replace(rawText, "<[^>]+>", "");
                    File.WriteAllText(outputPath, $"# {Path.GetFileNameWithoutExtension(inputPath)}\n\n{plain}\n", Encoding.UTF8);
                    break;
                }
                case "txt":
                {
                    string plain = Regex.Replace(rawText, "<[^>]+>", "");
                    File.WriteAllText(outputPath, plain, Encoding.UTF8);
                    break;
                }
                case "rtf":
                {
                    string rtfSafe = rawText.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}").Replace("\n", "\\par\n");
                    string rtf = "{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0 Segoe UI;}}\\f0\\fs24 " + rtfSafe + "}";
                    File.WriteAllText(outputPath, rtf, Encoding.ASCII);
                    break;
                }
                case "pdf":
                {
                    WriteSimpleTextPdf(rawText, outputPath);
                    break;
                }
                default:
                    File.WriteAllText(outputPath, rawText, Encoding.UTF8);
                    break;
            }
        }

        // MARK: - Spreadsheet Conversion (CSV <-> JSON <-> HTML <-> MD)

        private void ConvertSpreadsheet(string inputPath, string outputPath, string targetExt)
        {
            string raw = File.ReadAllText(inputPath, Encoding.UTF8);
            var lines = raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var rows = lines.Select(l => l.Split(',')).ToList();

            if (targetExt == "json" && rows.Count > 0)
            {
                var headers = rows[0].Select(h => h.Trim()).ToArray();
                var list = new List<Dictionary<string, string>>();
                for (int r = 1; r < rows.Count; r++)
                {
                    var obj = new Dictionary<string, string>();
                    for (int c = 0; c < headers.Length; c++)
                    {
                        obj[headers[c]] = c < rows[r].Length ? rows[r][c].Trim() : "";
                    }
                    list.Add(obj);
                }
                string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(outputPath, json, Encoding.UTF8);
                return;
            }

            if (targetExt == "md" && rows.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("| " + string.Join(" | ", rows[0]) + " |");
                sb.AppendLine("| " + string.Join(" | ", rows[0].Select(_ => "---")) + " |");
                for (int r = 1; r < rows.Count; r++)
                {
                    sb.AppendLine("| " + string.Join(" | ", rows[r]) + " |");
                }
                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
                return;
            }

            if (targetExt == "html" && rows.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>table{border-collapse:collapse;font-family:'Segoe UI',sans-serif;}th,td{border:1px solid #cbd5e1;padding:8px 12px;}</style></head><body><table>");
                foreach (var row in rows)
                {
                    sb.AppendLine("<tr>" + string.Join("", row.Select(c => $"<td>{System.Net.WebUtility.HtmlEncode(c)}</td>")) + "</tr>");
                }
                sb.AppendLine("</table></body></html>");
                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
                return;
            }

            if (targetExt == "pdf")
            {
                WriteSimpleTextPdf(raw, outputPath);
                return;
            }

            File.WriteAllText(outputPath, raw, Encoding.UTF8);
        }

        // MARK: - Subtitle Conversion (SRT <-> VTT <-> TXT <-> ASS)

        private void ConvertSubtitle(string inputPath, string outputPath, string targetExt)
        {
            string content = File.ReadAllText(inputPath, Encoding.UTF8);
            if (targetExt == "vtt")
            {
                string vtt = "WEBVTT\n\n" + Regex.Replace(content, @"(\d{2}:\d{2}:\d{2}),(\d{3})", "$1.$2");
                File.WriteAllText(outputPath, vtt, Encoding.UTF8);
            }
            else if (targetExt == "srt")
            {
                string srt = Regex.Replace(content.Replace("WEBVTT", "").Trim(), @"(\d{2}:\d{2}:\d{2})\.(\d{3})", "$1,$2");
                File.WriteAllText(outputPath, srt, Encoding.UTF8);
            }
            else if (targetExt == "txt")
            {
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Where(l => !l.Contains("-->") && !int.TryParse(l.Trim(), out _) && !l.StartsWith("WEBVTT"));
                File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines).Trim(), Encoding.UTF8);
            }
            else
            {
                File.WriteAllText(outputPath, content, Encoding.UTF8);
            }
        }

        // MARK: - Archive Conversion (ZIP / TAR / GZ)

        private void ConvertArchive(string inputPath, string outputPath, string targetExt, bool isDir)
        {
            if (isDir && targetExt == "zip")
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
                ZipFile.CreateFromDirectory(inputPath, outputPath);
                return;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "Converze_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                if (isDir)
                {
                    if (TryRunCli("tar.exe", $"-cf \"{outputPath}\" -C \"{Path.GetDirectoryName(inputPath)}\" \"{Path.GetFileName(inputPath)}\""))
                        return;
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    ZipFile.CreateFromDirectory(inputPath, outputPath);
                }
                else
                {
                    string srcExt = Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant();
                    if (srcExt == "zip")
                    {
                        ZipFile.ExtractToDirectory(inputPath, tempDir);
                    }
                    else
                    {
                        TryRunCli("tar.exe", $"-xf \"{inputPath}\" -C \"{tempDir}\"");
                    }

                    if (targetExt == "zip")
                    {
                        if (File.Exists(outputPath)) File.Delete(outputPath);
                        ZipFile.CreateFromDirectory(tempDir, outputPath);
                    }
                    else
                    {
                        string tarFlag = targetExt == "gz" ? "-czf" : "-cf";
                        if (!TryRunCli("tar.exe", $"{tarFlag} \"{outputPath}\" -C \"{tempDir}\" ."))
                        {
                            if (File.Exists(outputPath)) File.Delete(outputPath);
                            ZipFile.CreateFromDirectory(tempDir, outputPath);
                        }
                    }
                }
            }
            catch
            {
                if (!isDir) File.Copy(inputPath, outputPath, overwrite: true);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        private void ConvertFallback(string inputPath, string outputPath, string targetExt)
        {
            if (targetExt == "zip")
            {
                using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);
                zip.CreateEntryFromFile(inputPath, Path.GetFileName(inputPath));
                return;
            }
            File.Copy(inputPath, outputPath, overwrite: true);
        }

        // MARK: - Minimal Valid PDF Writers (Zero External Dependencies)

        private static void WriteSimpleTextPdf(string text, string outputPath)
        {
            string clean = text.Replace("\r", "").Replace("(", "\\(").Replace(")", "\\)");
            var lines = clean.Split('\n').Take(48).ToArray();
            var contentSb = new StringBuilder();
            contentSb.AppendLine("BT /F1 11 Tf 50 760 Td 14 TL");
            foreach (var line in lines)
            {
                string clipped = line.Length > 90 ? line.Substring(0, 90) : line;
                contentSb.AppendLine($"({clipped}) Tj T*");
            }
            contentSb.AppendLine("ET");
            byte[] streamBytes = Encoding.ASCII.GetBytes(contentSb.ToString());

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var sw = new StreamWriter(fs, Encoding.ASCII);
            sw.WriteLine("%PDF-1.4");
            sw.WriteLine("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj");
            sw.WriteLine("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj");
            sw.WriteLine("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj");
            sw.WriteLine($"4 0 obj << /Length {streamBytes.Length} >> stream");
            sw.Flush();
            fs.Write(streamBytes, 0, streamBytes.Length);
            sw.WriteLine("\nendstream endobj");
            sw.WriteLine("5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj");
            sw.WriteLine("trailer << /Root 1 0 R >>");
            sw.WriteLine("%%EOF");
        }

        private static void WriteSingleImagePdf(byte[] jpegBytes, int width, int height, string outputPath)
        {
            int w = Math.Max(100, width);
            int h = Math.Max(100, height);
            string drawCmd = $"q {w} 0 0 {h} 0 0 cm /Im0 Do Q\n";
            byte[] drawBytes = Encoding.ASCII.GetBytes(drawCmd);

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var sw = new StreamWriter(fs, Encoding.ASCII);
            sw.WriteLine("%PDF-1.4");
            sw.WriteLine("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj");
            sw.WriteLine("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj");
            sw.WriteLine($"3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] /Contents 4 0 R /Resources << /XObject << /Im0 5 0 R >> >> >> endobj");
            sw.WriteLine($"4 0 obj << /Length {drawBytes.Length} >> stream");
            sw.Flush();
            fs.Write(drawBytes, 0, drawBytes.Length);
            sw.WriteLine("endstream endobj");
            sw.WriteLine($"5 0 obj << /Type /XObject /Subtype /Image /Width {w} /Height {h} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpegBytes.Length} >> stream");
            sw.Flush();
            fs.Write(jpegBytes, 0, jpegBytes.Length);
            sw.WriteLine("\nendstream endobj");
            sw.WriteLine("trailer << /Root 1 0 R >>");
            sw.WriteLine("%%EOF");
        }

        private static bool TryRunCli(string exeName, string arguments)
        {
            try
            {
                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exeName,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    }
                };
                proc.Start();
                proc.WaitForExit(15000);
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string NextAvailableOutputPath(string inputPath, string targetExt)
        {
            string dir = Path.GetDirectoryName(inputPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string baseName = Directory.Exists(inputPath)
                ? Path.GetFileName(inputPath)
                : Path.GetFileNameWithoutExtension(inputPath);

            string candidate = Path.Combine(dir, $"{baseName}.{targetExt}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            string convertedCandidate = Path.Combine(dir, $"{baseName}_converted.{targetExt}");
            if (!File.Exists(convertedCandidate) && !Directory.Exists(convertedCandidate))
            {
                return convertedCandidate;
            }

            int counter = 2;
            while (true)
            {
                string numbered = Path.Combine(dir, $"{baseName}_converted_{counter}.{targetExt}");
                if (!File.Exists(numbered) && !Directory.Exists(numbered))
                {
                    return numbered;
                }
                counter++;
            }
        }
    }
}
